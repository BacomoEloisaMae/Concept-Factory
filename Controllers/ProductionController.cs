using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;

namespace ConceptFactory.Controllers
{
    // Production monitoring — lives inside the admin area for now since
    // there's no separate staff login yet (see AdminAuthFilter). This is
    // the ONLY place ProductionStage gets changed; because it writes to
    // the same Order row Order Management reads, changes here show up
    // there immediately with no extra syncing.
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
        public async Task<IActionResult> pIndex(string? tab, string? search, int? stage, int page = 1)
        {
            int pageSize = 10;

            var baseQuery = _context.Orders
                .Include(o => o.OrderDetails)
                .Where(o => TrackedStatuses.Contains(o.Status))
                .Where(o => o.OrderDetails.Any(d => d.IsCustomOrder))
                .AsQueryable();

            ViewBag.ConfirmedCount = await _context.Orders.CountAsync(o => o.Status == "Confirmed" && o.OrderDetails.Any(d => d.IsCustomOrder));
            ViewBag.InProductionCount = await _context.Orders.CountAsync(o => o.Status == "In Production" && o.OrderDetails.Any(d => d.IsCustomOrder));
            ViewBag.ReadyForPickupCount = await _context.Orders.CountAsync(o => o.Status == "Ready for Pickup" && o.OrderDetails.Any(d => d.IsCustomOrder));
            ViewBag.CompletedCount = await _context.Orders.CountAsync(o => o.Status == "Completed" && o.OrderDetails.Any(d => d.IsCustomOrder));
            ViewBag.TotalOrders = await _context.Orders.CountAsync(o => TrackedStatuses.Contains(o.Status) && o.OrderDetails.Any(d => d.IsCustomOrder));

            var query = baseQuery;

            if (!string.IsNullOrWhiteSpace(tab) && TabToStatus.TryGetValue(tab, out var statusValue))
                query = query.Where(o => o.Status == statusValue);

            // Stage filter � only meaningful for orders "In Production"; picking a
            // specific stage from the dropdown implicitly narrows to that status
            // too, regardless of which tab is selected.
            if (stage.HasValue)
                query = query.Where(o => o.Status == "In Production" && o.ProductionStage == stage.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool hasOrderIdMatch = ConceptFactory.Utils.OrderSearchHelper.TryParseOrderId(search, out int parsedOrderId);
                query = query.Where(o =>
                    o.CustomerName.Contains(search) ||
                    (hasOrderIdMatch && o.OrderID == parsedOrderId) ||
                    o.OrderDetails.Any(d => (d.ProductNameSnapshot ?? "").Contains(search)));
            }

            int totalCount = await query.CountAsync();
            var orders = await query
                .OrderBy(o => o.Status == "Completed" ? 1 : 0)
                .ThenByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tab = tab ?? "All";
            ViewBag.Search = search;
            ViewBag.Stage = stage;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            ViewData["Title"] = "Production";
            ViewData["ActivePage"] = "Production";

            return View(orders);
        }

        // Station names/keys indexed by ProductionStage (0-5) — keys are
        // used for asp-route-stage / ViewData["ActiveStation"] matching in
        // the sidebar dropdown (_AdminLayout.cshtml), names are shown to
        // the admin. Kept in sync with ProductionWorkflow's stage comment
        // and _ProductionPanel.cshtml's stepper labels.
        private static readonly string[] StationNames =
        {
            "Cutting", "Printing", "Sewing", "Trimming", "Quality Check", "Ready for Pickup"
        };
        private static readonly string[] StationKeys =
        {
            "Cutting", "Printing", "Sewing", "Trimming", "QualityCheck", "ReadyForPickup"
        };

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
            // Same Customize-only rule as pIndex — see comment there.
            baseQuery = baseQuery.Where(o => o.OrderDetails.Any(d => d.IsCustomOrder));

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
                completedTodayQuery = _context.Orders.Include(o => o.OrderDetails)
                    .Where(o => o.OrderDetails.Any(d => d.IsCustomOrder)
                             && o.ProductionStage == stage + 1
                             && o.ProductionStageUpdatedAt != null
                             && o.ProductionStageUpdatedAt >= todayStart);
            }
            else if (isReadyForPickup)
            {
                pendingFromPreviousQuery = baseQuery.Where(o =>
                    o.ProductionStageUpdatedAt == null || o.ProductionStageUpdatedAt < todayStart);
                inProgressQuery = baseQuery.Where(o =>
                    o.ProductionStageUpdatedAt != null && o.ProductionStageUpdatedAt >= todayStart);
                completedTodayQuery = _context.Orders.Include(o => o.OrderDetails)
                    .Where(o => o.OrderDetails.Any(d => d.IsCustomOrder)
                             && o.Status == "Completed"
                             && o.ProductionStageUpdatedAt != null
                             && o.ProductionStageUpdatedAt >= todayStart);
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
                completedTodayQuery = _context.Orders.Include(o => o.OrderDetails)
                    .Where(o => o.OrderDetails.Any(d => d.IsCustomOrder)
                             && o.ProductionStage == stage + 1
                             && o.ProductionStageUpdatedAt != null
                             && o.ProductionStageUpdatedAt >= todayStart);
            }

            int pendingFromPreviousCount = await pendingFromPreviousQuery.CountAsync();
            int completedTodayCount = await completedTodayQuery.CountAsync();
            int inProgressCount = Math.Max(0, totalCount - pendingFromPreviousCount);

            // Which query backs the actual table depends on the selected tab.
            // Ready for Pickup only has "All" and "Completed Today" — no
            // partial state exists between waiting and picked up, so a
            // stray ?tab=InProgress/PendingFromPrevious there just falls
            // back to the full list.
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

            int tabCount = await query.CountAsync();
            var orders = await query
                .OrderBy(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.StationIndex = stage;
            ViewBag.StationName = StationNames[stage];
            ViewBag.StationImage = StationImages[stage];
            ViewBag.Tab = tab ?? "All";
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(tabCount / (double)pageSize);
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
                .Where(l => l.OrderID == id && (l.Category == "Billing" || l.Category == "Production"))
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

            var customItems = order.OrderDetails.Where(d => d.IsCustomOrder).ToList();
            foreach (var d in customItems)
            {
                int entered = itemQuantities.TryGetValue(d.OrderDetailID, out int q) ? q : 0;
                d.ProductionQuantityDone = Math.Clamp(entered, 0, d.Quantity);
            }
            order.ProductionStageQuantityDone = customItems.Sum(d => d.ProductionQuantityDone);

            // First bit of progress at Cutting — the order is genuinely in
            // production now, so reflect that on Status instead of leaving
            // it at "Confirmed" until someone clicks "Mark as Done".
            if (isCutting && order.Status == "Confirmed" && order.ProductionStageQuantityDone > 0)
            {
                order.Status = "In Production";
                order.ProductionStage = 0;
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                quantity = order.ProductionStageQuantityDone,
                status = order.Status,
                items = customItems.ToDictionary(d => d.OrderDetailID, d => d.ProductionQuantityDone)
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

            var customItems = order.OrderDetails.Where(d => d.IsCustomOrder).ToList();

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

            foreach (var d in customItems)
            {
                if (!itemQuantities.TryGetValue(d.OrderDetailID, out int enteredQty) || enteredQty < d.Quantity)
                {
                    string label = string.IsNullOrEmpty(d.ProductNameSnapshot) ? "an item" : d.ProductNameSnapshot;
                    string variant = string.Join(" / ", new[] { d.SelectedColor, d.SelectedSize }.Where(v => !string.IsNullOrEmpty(v)));
                    string itemDesc = string.IsNullOrEmpty(variant) ? label : $"{label} ({variant})";
                    return BadRequest($"{itemDesc} still needs {d.Quantity} pcs before this station can be marked done.");
                }
            }

            int target = customItems.Sum(d => d.Quantity);

            order.ProductionRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            ProductionWorkflow.SetStage(order, stage + 1); // also resets every item's ProductionQuantityDone back to 0
            await _context.SaveChangesAsync();

            string stationName = (stage >= 0 && stage < StationNames.Length) ? StationNames[stage] : $"stage {stage}";
            await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Completed Station",
                $"{order.CustomerName} — finished {stationName} ({target} pcs across {customItems.Count} item{(customItems.Count == 1 ? "" : "s")}).", order.OrderID);

            return Ok(new { success = true, message = $"Order #ORD-{order.OrderID:D3} marked done at {stationName}." });
        }

        // POST: /Production/pSetStage — clicking a stage in the stepper
        // marks it (and everything before it) done.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pSetStage(int id, int stage)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                ProductionWorkflow.SetStage(order, stage);
                await _context.SaveChangesAsync();
                string stageName = (stage >= 0 && stage < StationNames.Length) ? StationNames[stage] : $"stage {stage}";
                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Advanced Production Stage",
                    $"{order.CustomerName} — reached {stageName}.", order.OrderID);
                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} production updated.";
            }
            return RedirectToAction(nameof(pIndex));
        }

        // POST: /Production/pMarkCompleted
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pMarkCompleted(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                var customItems = order.OrderDetails.Where(d => d.IsCustomOrder).ToList();
                int target = customItems.Sum(d => d.Quantity);

                ProductionWorkflow.MarkCompleted(order);

                // Advance the stage pointer so the stepper + timeline both
                // treat "Completed" (last stage, index 6) as done.
                order.ProductionStage = 6; // stages.Length - 1

                await _context.SaveChangesAsync();

                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Completed Station",
                    $"{order.CustomerName} � finished Ready for Pickup ({target} pcs across {customItems.Count} item{(customItems.Count == 1 ? "" : "s")}).", order.OrderID);
                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Marked Order Completed",
                    $"{order.CustomerName} � items picked up.", order.OrderID);

                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} marked Completed.";
            }
            return RedirectToAction(nameof(pIndex));
        }
    }
}
