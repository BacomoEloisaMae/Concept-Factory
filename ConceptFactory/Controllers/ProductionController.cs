using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;
using ConceptFactory.Filters;

namespace ConceptFactory.Controllers
{
    // Production monitoring — lives inside the admin area, gated to
    // Admin + Production staff only (see AdminAuthFilter). This is
    // the ONLY place ProductionStage gets changed; because it writes to
    // the same Order row Order Management reads, changes here show up
    // there immediately with no extra syncing.
    [AdminAuthFilter(Roles = "Admin,Production")]
    public class ProductionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Only orders that have actually been confirmed enter production
        // tracking — "Pending ..." orders haven't paid yet and Cancelled
        // orders never will.
        private static readonly string[] TrackedStatuses =
        {
            "Confirmed",
            "In Production",
            "Ready for Pickup",
            "Completed"
        };
        private static readonly Dictionary<string, string> TabToStatus = new()
        {
            ["Confirmed"] = "Confirmed",
            ["InProduction"] = "In Production",
            ["ReadyForPickup"] = "Ready for Pickup",
            ["Completed"] = "Completed"
        };

        // GET: /Production/pIndex
        //
        // The three stat cards (Total Orders / In Production / Completed)
        // are the primary way in now — nothing is queried or shown until
        // one of them (or the Confirmed/Ready for Pickup tabs below them,
        // or a search) is actually clicked, rather than dumping the full
        // tracked-order list the moment the page opens.
        public async Task<IActionResult> pIndex(string? tab, string? search, int? stage, int page = 1)
        {
            // Production staff don't get an "all stations" Overview — they
            // only ever see their own assigned station, labeled "My
            // Station" in the sidebar (_AdminLayout.cshtml).
            if (MyStationIndex.HasValue)
                return RedirectToAction(nameof(pStation), new { stage = MyStationIndex.Value });

            int pageSize = 10;

            var baseQuery = _context.Orders
                .Include(o => o.OrderDetails)
                .Where(o => TrackedStatuses.Contains(o.Status))
                .AsQueryable();

            ViewBag.ConfirmedCount = await _context.Orders.CountAsync(o => o.Status == "Confirmed");
            ViewBag.InProductionCount = await _context.Orders.CountAsync(o => o.Status == "In Production");
            ViewBag.ReadyForPickupCount = await _context.Orders.CountAsync(o => o.Status == "Ready for Pickup");
            ViewBag.CompletedCount = await _context.Orders.CountAsync(o => o.Status == "Completed");
            ViewBag.TotalOrders = await _context.Orders.CountAsync(o => TrackedStatuses.Contains(o.Status));
            // Orders still waiting on a down payment / cash / GCash
            // verification — not tracked in production yet, but useful for
            // admins to see what's about to enter the pipeline.
            ViewBag.PendingCount = await _context.Orders.CountAsync(o =>
                o.Status == "Pending Down Payment" ||
                o.Status == "Pending Cash Payment" ||
                o.Status == "Pending Payment Verification");

            bool hasSelection = !string.IsNullOrWhiteSpace(tab) || stage.HasValue || !string.IsNullOrWhiteSpace(search);

            var orders = new List<Order>();
            int totalCount = 0;
            var startedDates = new Dictionary<int, DateTime>();

            if (hasSelection)
            {
                var query = baseQuery;

                // "tab=All" (Total Orders stat) is a deliberate request to see
                // everything tracked — it just isn't in TabToStatus because it
                // adds no extra Where clause, same as leaving tab out entirely
                // used to. The difference now is that leaving it out entirely
                // means "nothing selected yet", handled by hasSelection above.
                if (!string.IsNullOrWhiteSpace(tab) && TabToStatus.TryGetValue(tab, out var statusValue))
                    query = query.Where(o => o.Status == statusValue);

                // Stage filter — the dropdown has 6 options (index 0-4 are the
                // "In Production" stations; index 5 is "Ready for Pickup",
                // which isn't an In-Production stage at all, it's its own
                // order status). Picking a specific stage implicitly narrows
                // to whichever status that stage actually belongs to,
                // regardless of which tab is selected.
                if (stage.HasValue)
                {
                    query = stage.Value == ProductionWorkflow.ReadyForPickupIndex
                        ? query.Where(o => o.Status == "Ready for Pickup")
                        : query.Where(o => o.Status == "In Production" && o.ProductionStage == stage.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    bool hasOrderIdMatch = ConceptFactory.Utils.OrderSearchHelper.TryParseOrderId(search, out int parsedOrderId);
                    query = query.Where(o =>
                        o.CustomerName.Contains(search) ||
                        (hasOrderIdMatch && o.OrderID == parsedOrderId) ||
                        o.OrderDetails.Any(d => (d.ProductNameSnapshot ?? "").Contains(search)));
                }

                totalCount = await query.CountAsync();
                orders = await query
                    .OrderBy(o => o.Status == "Completed" ? 1 : 0)
                    .ThenByDescending(o => o.OrderDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // "Started Date" (table column) — the moment each order's
                // first bit of progress at Cutting flipped it into "In
                // Production" (see pSaveStationProgress), read back from the
                // Notification that same moment writes rather than a
                // dedicated column. GroupBy+Min (not a straight
                // ToDictionary) since an order can end up with more than one
                // "ProductionStarted" notification (e.g. re-entering
                // production) — a duplicate OrderID key would throw.
                var orderIds = orders.Select(o => o.OrderID).ToList();
                startedDates = await _context.Notifications
                    .Where(n => n.Type == "ProductionStarted" && orderIds.Contains(n.OrderID))
                    .GroupBy(n => n.OrderID)
                    .Select(g => new { OrderID = g.Key, CreatedAt = g.Min(n => n.CreatedAt) })
                    .ToDictionaryAsync(x => x.OrderID, x => x.CreatedAt);
            }

            ViewBag.HasSelection = hasSelection;
            ViewBag.Tab = tab ?? "";
            ViewBag.Search = search;
            ViewBag.Stage = stage;
            ViewBag.Page = page;
            ViewBag.TotalPages = hasSelection ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
            ViewBag.TotalCount = totalCount;
            ViewBag.StartedDates = startedDates;

            ViewData["Title"] = "Production";
            ViewData["ActivePage"] = "Production";

            return View(orders);
        }

        // POST: /Production/pSetTargetDate/5 — inline-edited from the "Target
        // Date" column on the Overview table. date is a plain yyyy-MM-dd
        // string from an <input type="date">; empty/null clears it back to
        // "Not set".
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pSetTargetDate(int id, string? date)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (string.IsNullOrWhiteSpace(date))
            {
                order.ProductionTargetDate = null;
            }
            else if (DateTime.TryParse(date, out var parsed))
            {
                order.ProductionTargetDate = parsed.Date;
            }
            else
            {
                return BadRequest(new { success = false, message = "Invalid date." });
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                targetDate = order.ProductionTargetDate?.ToString("MMM d, yyyy")
            });
        }

        // Station names/keys indexed by ProductionStage (0-5) — pulled from
        // Utils/ProductionStations.cs, the single shared source also used
        // by UsersController (Station picker), Notification.StationIndex
        // scoping, and the sidebar (_AdminLayout.cshtml). Kept as local
        // aliases here since every method below already refers to
        // StationNames/StationKeys.
        private static readonly string[] StationNames = ProductionStations.Names;
        private static readonly string[] StationKeys = ProductionStations.Keys;

        // Production staff are restricted to their own assigned station
        // (User.StationIndex, set from User Management → Add/Edit Staff) —
        // stored in session at login as "StaffStationIndex" (see
        // AuthController.TryLoginStaffAsync). Admin has no restriction
        // (returns null == "no restriction" everywhere this is checked).
        private int? MyStationIndex =>
            HttpContext.Session.GetString("StaffRole") == "Production"
                ? HttpContext.Session.GetInt32("StaffStationIndex")
                : null;

        // Icon per station for the "Total in [Station]" stat card — same
        // ordering as StationNames/StationKeys (0-5).
        private static readonly string[] StationImages =
        {
            "~/images/logo/cutt.png",
            "~/images/logo/print blue 2.png",
            "~/images/logo/sewing 1.png",
            "~/images/logo/blue trim 3.png",
            "~/images/logo/quality check 1.png",
            "~/images/logo/ready 1.png"
        };
        // Verb forms shown on each station's "Mark as Done" popup — only
        // meaningful for stages 0-4; Ready for Pickup uses the separate
        // pMarkCompleted flow. Base/Past/Gerund, e.g. "Cut" is irregular
        // (same base/past) so this is spelled out per station rather than
        // guessed with a suffix rule.
        private static readonly (string Base, string Past, string Gerund)[] StationVerbForms =
        {
            ("Cut", "Cut", "Cutting"),
            ("Print", "Printed", "Printing"),
            ("Sew", "Sewn", "Sewing"),
            ("Trim", "Trimmed", "Trimming"),
            ("Check", "Checked", "Checking")
        };

        // GET: /Production/pStation?stage=0 — admin-only "per station" view
        // (Cutting/Printing/Sewing/Trimming/Quality Check/Ready for Pickup
        // in the sidebar dropdown), showing everything currently sitting at
        // that one stage. Orders only ever reach a station once Billing has
        // approved the down payment — PaymentWorkflow.ApplyPaymentStatus is
        // what first flips Status to "Confirmed" — so a payment still
        // "Waiting for Verification" never appears at any station here;
        // that's what keeps production gated on billing approval. The
        // Cutting station additionally surfaces "Confirmed" orders that
        // haven't started yet (ProductionStage still -1), since they're
        // queued to start there next.
        [HttpGet]
        public async Task<IActionResult> pStation(int stage, string? tab, string? search, int page = 1)
        {
            stage = Math.Clamp(stage, 0, ProductionWorkflow.ReadyForPickupIndex);
            // A Production account can only ever view/work their own
            // assigned station — redirect anything else back to it rather
            // than a bare "access denied".
            if (MyStationIndex.HasValue && stage != MyStationIndex.Value)
                return RedirectToAction(nameof(pStation), new { stage = MyStationIndex.Value });
            int pageSize = 10;
            bool isReadyForPickup = stage == ProductionWorkflow.ReadyForPickupIndex;
            bool isCutting = stage == 0;
            DateTime todayStart = DateTime.Today;

            var baseQuery = _context.Orders.Include(o => o.OrderDetails).AsQueryable();
            baseQuery = isReadyForPickup
                ? baseQuery.Where(o => o.Status == "Ready for Pickup")
                : isCutting
                    ? baseQuery.Where(o => o.Status == "Confirmed" || (o.Status == "In Production" && o.ProductionStage == 0))
                    : baseQuery.Where(o => o.Status == "In Production" && o.ProductionStage == stage);
            // Stat cards always reflect the FULL station set, regardless of
            // whichever tab is currently selected below — same pattern as oIndex.
            int totalCount = await baseQuery.CountAsync();

            // "Pending from Previous" / "In Progress" / "Completed Today" — each
            // tab below maps to exactly one of these three queries, so the table
            // always matches whatever number the matching stat card shows.
            IQueryable<Order> pendingFromPreviousQuery;
            IQueryable<Order> inProgressQuery;
            IQueryable<Order> completedTodayQuery;

            if (isCutting)
            {
                // Same quantity-driven definition as the middle stations
                // below — "pending" means nothing's been cut yet, whether
                // the order was confirmed today or last week. Previously
                // this was gated on OrderDate < today too, which meant an
                // order confirmed THIS morning with 0 pcs cut was already
                // counted as "In Progress".
                pendingFromPreviousQuery = baseQuery.Where(o => o.ProductionStageQuantityDone == 0);
                inProgressQuery = baseQuery.Where(o => o.ProductionStageQuantityDone > 0);
                // "Completed" at this station means the order has moved
                // PAST it (ProductionStage > this station's index, or the
                // whole order is Completed) — counted overall, not just
                // whatever finished today, so the number reflects every
                // order that has ever cleared this station.
                completedTodayQuery = _context.Orders.Include(o => o.OrderDetails)
                    .Where(o => o.ProductionStage >= stage + 1);
            }
            else if (isReadyForPickup)
            {
                pendingFromPreviousQuery = baseQuery.Where(o =>
                    o.ProductionStageUpdatedAt == null || o.ProductionStageUpdatedAt < todayStart);
                inProgressQuery = baseQuery.Where(o =>
                    o.ProductionStageUpdatedAt != null && o.ProductionStageUpdatedAt >= todayStart);
                // Every order ever picked up, overall — not just today's.
                completedTodayQuery = _context.Orders.Include(o => o.OrderDetails)
                    .Where(o => o.Status == "Completed");
            }
            else
            {
                // Unlike Cutting (where "pending" means the order hasn't
                // been touched at all) or Ready for Pickup (a binary
                // pickup wait), these middle stations need to distinguish
                // "just arrived from the previous station, nothing done
                // here yet" from "work has actually started here" — that's
                // ProductionStageQuantityDone, not when the order arrived.
                // SetStage() resets it to 0 on handoff, so a fresh arrival
                // reads 0 until someone types a quantity in this station's
                // modal, regardless of what day it arrived.
                pendingFromPreviousQuery = baseQuery.Where(o => o.ProductionStageQuantityDone == 0);
                inProgressQuery = baseQuery.Where(o => o.ProductionStageQuantityDone > 0);
                // Same overall-not-just-today fix as Cutting above.
                completedTodayQuery = _context.Orders.Include(o => o.OrderDetails)
                    .Where(o => o.ProductionStage >= stage + 1);
            }

            int pendingFromPreviousCount = await pendingFromPreviousQuery.CountAsync();
            int completedTodayCount = await completedTodayQuery.CountAsync();
            int inProgressCount = Math.Max(0, totalCount - pendingFromPreviousCount);

            // Which query backs the actual table depends on the selected tab.
            // Ready for Pickup only has "All" and "Completed Today" — no
            // partial state exists between waiting and picked up, so a
            // stray ?tab=InProgress/PendingFromPrevious there just falls
            // back to the full list. Nothing is actually run/shown, though,
            // until a stat card (or the matching tab) or a search has been
            // clicked — same "don't dump everything on open" behavior as
            // pIndex above.
            bool hasSelection = !string.IsNullOrWhiteSpace(tab) || !string.IsNullOrWhiteSpace(search);

            IQueryable<Order> query = tab switch
            {
                "InProgress" when !isReadyForPickup => inProgressQuery,
                "PendingFromPrevious" when !isReadyForPickup => pendingFromPreviousQuery,
                "CompletedToday" => completedTodayQuery,
                _ => baseQuery
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool hasOrderIdMatch = ConceptFactory.Utils.OrderSearchHelper.TryParseOrderId(search, out int parsedOrderId);
                query = query.Where(o =>
                    o.CustomerName.Contains(search) ||
                    (hasOrderIdMatch && o.OrderID == parsedOrderId) ||
                    o.OrderDetails.Any(d => (d.ProductNameSnapshot ?? "").Contains(search)));
            }

            int tabCount = 0;
            var orders = new List<Order>();
            if (hasSelection)
            {
                tabCount = await query.CountAsync();
                orders = await query
                    .OrderByDescending(o => o.OrderDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }

            ViewBag.StationIndex = stage;
            ViewBag.StationName = StationNames[stage];
            ViewBag.StationImage = StationImages[stage];
            ViewBag.HasSelection = hasSelection;
            ViewBag.Tab = tab ?? "";
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = hasSelection ? (int)Math.Ceiling(tabCount / (double)pageSize) : 0;
            // Both counts are exposed: TotalCount drives the pagination summary
            // for whatever's currently showing, StationTotalCount is the
            // always-full "Total in {station}" stat card number.
            ViewBag.TotalCount = tabCount;
            ViewBag.StationTotalCount = totalCount;

            ViewBag.PendingFromPreviousCount = pendingFromPreviousCount;
            ViewBag.CompletedTodayCount = completedTodayCount;
            ViewBag.InProgressCount = inProgressCount;

            ViewData["Title"] = $"Production — {StationNames[stage]}";
            ViewData["ActivePage"] = "Production";
            ViewData["ActiveStation"] = StationKeys[stage];

            return View(orders);
        }

        // GET: /Production/pDetailsPanel/5 — the popup modal content.
        // readOnly=true (used by the Overview page's "View Progress"
        // button) renders the stepper as a plain read-only display with no
        // way to advance stages — Overview is purely a monitoring view now.
        // Station pages (pStation.cshtml) call this without readOnly, so
        // production staff can still actually advance stages from there.
        // Both read the same Order row, so there's nothing to keep "in
        // sync" beyond that — Overview just always shows whatever the
        // stations last set.
        [HttpGet]
        public async Task<IActionResult> pDetailsPanel(int id, bool readOnly = false)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            ViewBag.ReadOnly = readOnly;
            // Order Timeline card (below the stepper) — reconstructed from
            // ActivityLogs rather than a dedicated history table, since
            // Billing approval + every pSetStage/pCompleteStation call
            // already writes one here with this exact OrderID. Ordered
            // oldest-first so it reads top-to-bottom like the stepper does.
            ViewBag.StationName = (order.ProductionStage >= 0 && order.ProductionStage < StationNames.Length)
            ? StationNames[order.ProductionStage]
            : "";

            ViewBag.TimelineLogs = await _context.ActivityLogs
                .Where(l => l.OrderID == id && l.Category == "Production")
                .OrderBy(l => l.Timestamp)
                .ToListAsync();

            return PartialView("_ProductionPanel", order);
        }


        // GET: /Production/pStationModal?id=5&stage=0 — the "Mark as Done"
        // popup a station page (pStation.cshtml) opens for one order.
        // Distinct from pDetailsPanel/_ProductionPanel (the full 6-stage
        // stepper the Overview page uses) — this is the focused,
        // single-station quantity + remarks form.
        [HttpGet]
        public async Task<IActionResult> pStationModal(int id, int stage)
        {
            stage = Math.Clamp(stage, 0, ProductionWorkflow.ReadyForPickupIndex);
            if (MyStationIndex.HasValue && stage != MyStationIndex.Value)
                return StatusCode(403, "You can only manage your assigned station.");
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            (string Base, string Past, string Gerund) verbForms = stage < StationVerbForms.Length ? StationVerbForms[stage] : ("Complete", "Completed", "Completing");
            ViewBag.Stage = stage;
            ViewBag.StationName = StationNames[stage];
            ViewBag.StationVerbBase = verbForms.Base;
            ViewBag.StationVerbPast = verbForms.Past;
            ViewBag.StationVerbGerund = verbForms.Gerund;
            ViewBag.NextStationName = stage < ProductionWorkflow.ReadyForPickupIndex ? StationNames[stage + 1] : "Ready for Pickup";
            // An order opened from the Ready for Pickup station's "Picked
            // Up" tab is already Completed — the modal must show it as
            // read-only instead of offering "Mark as Picked Up" again.
            ViewBag.AlreadyPickedUp = stage == ProductionWorkflow.ReadyForPickupIndex && order.Status == "Completed";
            // Same idea for every other station's "Completed" tab: once an
            // order has moved past this station (or finished entirely),
            // there's nothing left to mark here. Note OrderDetail.
            // ProductionQuantityDone gets reset to 0 by SetStage() on
            // handoff to the next station (see pCompleteStation below), so
            // it no longer reflects what happened HERE — the view falls
            // back to showing each item at its full quantity instead of
            // that now-stale number.
            ViewBag.StationCleared = stage != ProductionWorkflow.ReadyForPickupIndex && order.ProductionStage > stage;
            return PartialView("_StationUpdateModal", order);
        }

        // POST: /Production/pSaveStationProgress — autosaves the quantities
        // typed into the station modal as the staff member goes, WITHOUT
        // advancing the stage. Called from station-update.js on every edit
        // (debounced) so progress survives a closed modal or page refresh.
        // itemQuantitiesJson is {orderDetailId: quantity}, one entry per
        // row in the popup — a color/size line's progress is tracked on
        // that OrderDetail directly (OrderDetail.ProductionQuantityDone),
        // NOT lumped into one order-wide number, so a 5x Red-Small +
        // 3x Blue-Large order can't have "8 done" without knowing which
        // is which. Order.ProductionStageQuantityDone is kept in sync as
        // a cached sum purely so the station list's cheap stat-card
        // queries (pStation) don't need to join/sum OrderDetails.
        // For Cutting specifically, any progress here is also what first
        // flips the order from "Confirmed" to "In Production" — otherwise
        // it would sit at "Confirmed" even while work is visibly
        // underway. Reaching full quantity here does NOT move the order
        // to the next station on its own; that still only happens
        // through pCompleteStation ("Mark as Done").
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pSaveStationProgress(int id, int stage, string? itemQuantitiesJson)
        {
            stage = Math.Clamp(stage, 0, ProductionWorkflow.ReadyForPickupIndex);
            if (MyStationIndex.HasValue && stage != MyStationIndex.Value)
                return StatusCode(403, "You can only manage your assigned station.");
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            bool isCutting = stage == 0;
            bool atThisStation = isCutting
                ? (order.Status == "Confirmed" || (order.Status == "In Production" && order.ProductionStage == 0))
                : (order.Status == "In Production" && order.ProductionStage == stage);

            if (!atThisStation)
                return BadRequest("This order is no longer at this station.");

            Dictionary<int, int>? itemQuantities = null;
            if (!string.IsNullOrWhiteSpace(itemQuantitiesJson))
            {
                try { itemQuantities = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(itemQuantitiesJson); }
                catch (System.Text.Json.JsonException) { return BadRequest("Could not read the per-item quantities submitted."); }
            }
            if (itemQuantities == null) return BadRequest("Missing per-item quantities.");

            var productionItems = order.OrderDetails.ToList();
            foreach (var d in productionItems)
            {
                int entered = itemQuantities.TryGetValue(d.OrderDetailID, out int q) ? q : 0;
                d.ProductionQuantityDone = Math.Clamp(entered, 0, d.Quantity);
            }
            order.ProductionStageQuantityDone = productionItems.Sum(d => d.ProductionQuantityDone);

            // First bit of progress at Cutting — the order is genuinely in
            // production now, so reflect that on Status instead of leaving
            // it at "Confirmed" until someone clicks "Mark as Done".
            bool justStartedProduction = isCutting && order.Status == "Confirmed" && order.ProductionStageQuantityDone > 0;
            if (justStartedProduction)
            {
                order.Status = "In Production";
                order.ProductionStage = 0;
            }

            await _context.SaveChangesAsync();

            if (justStartedProduction)
            {
                await NotificationService.NotifyAdminAsync(_context, order.OrderID, "ProductionStarted",
                    "Production Started",
                    $"Order #ORD-{order.OrderID:D3} — {order.CustomerName}'s order has entered production at Cutting.",
                    stationIndex: 0);
                await NotificationService.NotifyCustomerAsync(_context, order.OrderID, "ProductionStarted",
                    "Production has started",
                    $"Order #ORD-{order.OrderID:D5} is now in production — first stop: Cutting.");
            }

            return Ok(new
            {
                success = true,
                quantity = order.ProductionStageQuantityDone,
                status = order.Status,
                items = productionItems.ToDictionary(d => d.OrderDetailID, d => d.ProductionQuantityDone)
            });
        }

        // POST: /Production/pCompleteStation — the popup's "Mark as Done"
        // submit. Advances the order past the given station (stage ->
        // stage+1), stamping ProductionStageUpdatedAt/ProductionRemarks.
        // Only succeeds if the order is genuinely still sitting at that
        // station, so a stale/duplicate submit can't double-advance it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pCompleteStation(int id, int stage, string? itemQuantitiesJson, string? remarks)
        {
            stage = Math.Clamp(stage, 0, ProductionWorkflow.ReadyForPickupIndex);
            if (MyStationIndex.HasValue && stage != MyStationIndex.Value)
                return StatusCode(403, "You can only manage your assigned station.");
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            bool isCutting = stage == 0;
            bool atThisStation = isCutting
                ? (order.Status == "Confirmed" || (order.Status == "In Production" && order.ProductionStage == 0))
                : (order.Status == "In Production" && order.ProductionStage == stage);

            if (!atThisStation)
                return BadRequest("This order is no longer at this station.");

            var productionItems = order.OrderDetails.ToList();

            // Each color/size line has to individually reach its own
            // quantity — an order combining, say, 5x Red-Small and 3x
            // Blue-Large can't be marked done just because 8 pcs total
            // were entered somewhere; both lines need to actually be
            // finished.
            Dictionary<int, int>? itemQuantities = null;
            if (!string.IsNullOrWhiteSpace(itemQuantitiesJson))
            {
                try { itemQuantities = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(itemQuantitiesJson); }
                catch (System.Text.Json.JsonException) { return BadRequest("Could not read the per-item quantities submitted."); }
            }
            if (itemQuantities == null) return BadRequest("Missing per-item quantities.");

            foreach (var d in productionItems)
            {
                if (!itemQuantities.TryGetValue(d.OrderDetailID, out int enteredQty) || enteredQty < d.Quantity)
                {
                    string label = string.IsNullOrEmpty(d.ProductNameSnapshot) ? "an item" : d.ProductNameSnapshot;
                    string variant = string.Join(" / ", new[] { d.SelectedColor, d.SelectedSize }.Where(v => !string.IsNullOrEmpty(v)));
                    string itemDesc = string.IsNullOrEmpty(variant) ? label : $"{label} ({variant})";
                    return BadRequest($"{itemDesc} still needs {d.Quantity} pcs before this station can be marked done.");
                }
            }

            int target = productionItems.Sum(d => d.Quantity);

            order.ProductionRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            int oldStage = order.ProductionStage;
            ProductionWorkflow.SetStage(order, stage + 1); // also resets every item's ProductionQuantityDone back to 0
            await _context.SaveChangesAsync();

            string stationName = (stage >= 0 && stage < StationNames.Length) ? StationNames[stage] : $"stage {stage}";

            // Real stage-history row — see Models/ProductionTracking.cs.
            // Order.ProductionStage/ProductionStageUpdatedAt still drive
            // every existing Production screen; this just adds the timeline.
            _context.ProductionTracking.Add(new ProductionTracking
            {
                OrderID = order.OrderID,
                ProductionStage = order.ProductionStage,
                StageName = (order.ProductionStage >= 0 && order.ProductionStage < StationNames.Length)
                    ? StationNames[order.ProductionStage] : $"stage {order.ProductionStage}",
                StageStatus = "Reached",
                UpdatedByName = HttpContext.Session.GetString("AdminName"),
                UpdatedAt = DateTime.Now,
                Remarks = order.ProductionRemarks
            });
            await _context.SaveChangesAsync();

            await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Completed Station",
                $"{order.CustomerName} — finished {stationName} ({target} pcs across {productionItems.Count} item{(productionItems.Count == 1 ? "" : "s")}).", order.OrderID);
            await NotifyProductionStageChangeAsync(order, oldStage, order.ProductionStage);

            return Ok(new { success = true, message = $"Order #ORD-{order.OrderID:D3} marked done at {stationName}." });
        }

        // POST: /Production/pSetStage — clicking a stage in the stepper
        // marks it (and everything before it) done.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pSetStage(int id, int stage)
        {
            if (MyStationIndex.HasValue)
                return RedirectToAction(nameof(pStation), new { stage = MyStationIndex.Value });
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                int oldStage = order.ProductionStage;
                ProductionWorkflow.SetStage(order, stage);
                await _context.SaveChangesAsync();
                string stageName = (stage >= 0 && stage < StationNames.Length) ? StationNames[stage] : $"stage {stage}";

                _context.ProductionTracking.Add(new ProductionTracking
                {
                    OrderID = order.OrderID,
                    ProductionStage = order.ProductionStage,
                    StageName = stageName,
                    StageStatus = "Reached",
                    UpdatedByName = HttpContext.Session.GetString("AdminName"),
                    UpdatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Advanced Production Stage",
                    $"{order.CustomerName} — reached {stageName}.", order.OrderID);
                await NotifyProductionStageChangeAsync(order, oldStage, order.ProductionStage);
                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} production updated.";
            }
            return RedirectToAction(nameof(pIndex));
        }

        // POST: /Production/pMarkCompleted
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pMarkCompleted(int id)
        {
            if (MyStationIndex.HasValue && MyStationIndex.Value != ProductionStations.ReadyForPickupIndex)
                return StatusCode(403, "You can only manage your assigned station.");
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                // Guard against double-processing: an order that's already
                // "Completed" (picked up) has nothing left to mark — without
                // this, re-opening it from the Picked Up tab and submitting
                // again would re-log the pickup and re-notify the admin.
                if (order.Status != "Ready for Pickup")
                {
                    return BadRequest(new { success = false, message = $"Order #ORD-{order.OrderID:D3} has already been picked up." });
                }

                var productionItems = order.OrderDetails.ToList();
                int target = productionItems.Sum(d => d.Quantity);

                ProductionWorkflow.MarkCompleted(order);

                // Advance the stage pointer so the stepper + timeline both
                // treat "Completed" (last stage, index 6) as done.
                order.ProductionStage = 6; // stages.Length - 1

                await _context.SaveChangesAsync();

                _context.ProductionTracking.Add(new ProductionTracking
                {
                    OrderID = order.OrderID,
                    ProductionStage = order.ProductionStage,
                    StageName = "Completed",
                    StageStatus = "Completed",
                    UpdatedByName = HttpContext.Session.GetString("AdminName"),
                    UpdatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Completed Station",
                    $"{order.CustomerName} — finished Ready for Pickup ({target} pcs across {productionItems.Count} item{(productionItems.Count == 1 ? "" : "s")}).", order.OrderID);
                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Marked Order Completed",
                    $"{order.CustomerName} — items picked up.", order.OrderID);

                await NotificationService.NotifyAdminAsync(_context, order.OrderID, "Completed",
                    "Order Completed",
                    $"Order #ORD-{order.OrderID:D3} — {order.CustomerName} picked up their order.",
                    stationIndex: ProductionStations.ReadyForPickupIndex);

                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} marked Completed.";
            }
            return RedirectToAction(nameof(pIndex));
        }

        // Sends the admin "production advanced" notification for any stage
        // change, plus whichever single customer notification fits the
        // transition — "ready for pickup" if the new stage is the last
        // one, or a generic "moved to X" otherwise. Called from both
        // pCompleteStation and pSetStage, since either one can produce
        // any of these transitions. The "production started" customer
        // notification is NOT sent from here — that one fires the moment
        // the order actually leaves "Confirmed" for the first time, in
        // pSaveStationProgress above, since that's when production
        // genuinely begins (a station can sit "at Cutting" for a while
        // before anyone clicks "Mark as Done").
        private async Task NotifyProductionStageChangeAsync(Order order, int oldStage, int newStage)
        {
            if (oldStage == newStage) return;

            string stationName = (newStage >= 0 && newStage < StationNames.Length) ? StationNames[newStage] : $"stage {newStage}";

            await NotificationService.NotifyAdminAsync(_context, order.OrderID, "ProductionAdvanced",
                "Production Updated",
                $"Order #ORD-{order.OrderID:D3} — {order.CustomerName}'s order reached {stationName}.",
                stationIndex: newStage);

            if (newStage == ProductionWorkflow.ReadyForPickupIndex)
            {
                await NotificationService.NotifyCustomerAsync(_context, order.OrderID, "ReadyForPickup",
                    "Ready for pickup!",
                    $"Order #ORD-{order.OrderID:D5} is ready for pickup. See you soon!");
            }
            else
            {
                await NotificationService.NotifyCustomerAsync(_context, order.OrderID, "InProduction",
                    "Order update",
                    $"Order #ORD-{order.OrderID:D5} has moved to {stationName}.");
            }
        }

        // GET: /Production/pHistory — full production stage-change log
        // (Models/ProductionTracking.cs), one row per "Mark as Done"/
        // pSetStage/pMarkCompleted call across every order. Admin sees
        // every station; a Production account is automatically scoped to
        // just their own assigned station's history (same restriction as
        // pStation/pIndex above) — no manual filter needed for them.
        [HttpGet]
        public async Task<IActionResult> pHistory(string? search, int page = 1)
        {
            int pageSize = 15;

            string? myStationName = ProductionStations.NameFor(MyStationIndex);

            var query = _context.ProductionTracking
                .Include(t => t.Order)
                .AsQueryable();

            if (myStationName != null)
                query = query.Where(t => t.StageName == myStationName);

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool hasOrderIdMatch = ConceptFactory.Utils.OrderSearchHelper.TryParseOrderId(search, out int parsedOrderId);
                query = query.Where(t =>
                    (t.Order != null && t.Order.CustomerName.Contains(search)) ||
                    (hasOrderIdMatch && t.OrderID == parsedOrderId));
            }

            int totalCount = await query.CountAsync();
            var rows = await query
                .OrderByDescending(t => t.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.MyStationName = myStationName;

            ViewData["Title"] = "Production History";
            ViewData["ActivePage"] = "Production";
            ViewData["ActiveStation"] = "History";

            return View(rows);
        }
    }
}