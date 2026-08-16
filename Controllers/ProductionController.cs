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
        public async Task<IActionResult> pIndex(string? tab, string? search, int page = 1)
        {
            int pageSize = 10;

            var baseQuery = _context.Orders
                .Include(o => o.OrderDetails)
                .Where(o => TrackedStatuses.Contains(o.Status))
                // Production only tracks Customize-page orders — a
                // pre-designed/catalog item has nothing to cut, print, or
                // sew, so an order made up entirely of catalog items never
                // shows up here even once Billing has confirmed it.
                .Where(o => o.OrderDetails.Any(d => d.IsCustomOrder))
                .AsQueryable();

            ViewBag.ConfirmedCount      = await _context.Orders.CountAsync(o => o.Status == "Confirmed" && o.OrderDetails.Any(d => d.IsCustomOrder));
            ViewBag.InProductionCount   = await _context.Orders.CountAsync(o => o.Status == "In Production" && o.OrderDetails.Any(d => d.IsCustomOrder));
            ViewBag.ReadyForPickupCount = await _context.Orders.CountAsync(o => o.Status == "Ready for Pickup" && o.OrderDetails.Any(d => d.IsCustomOrder));
            ViewBag.CompletedCount      = await _context.Orders.CountAsync(o => o.Status == "Completed" && o.OrderDetails.Any(d => d.IsCustomOrder));

            var query = baseQuery;

            if (!string.IsNullOrWhiteSpace(tab) && TabToStatus.TryGetValue(tab, out var statusValue))
                query = query.Where(o => o.Status == statusValue);

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
                .OrderBy(o => o.Status == "Completed" ? 1 : 0) // active orders float to the top
                .ThenByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tab = tab ?? "All";
            ViewBag.Search = search;
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
        public async Task<IActionResult> pStation(int stage, string? search, int page = 1)
        {
            stage = Math.Clamp(stage, 0, ProductionWorkflow.ReadyForPickupIndex);
            int pageSize = 10;
            bool isReadyForPickup = stage == ProductionWorkflow.ReadyForPickupIndex;
            bool isCutting = stage == 0;

            var baseQuery = _context.Orders.Include(o => o.OrderDetails).AsQueryable();
            baseQuery = isReadyForPickup
                ? baseQuery.Where(o => o.Status == "Ready for Pickup")
                : isCutting
                    ? baseQuery.Where(o => o.Status == "Confirmed" || (o.Status == "In Production" && o.ProductionStage == 0))
                    : baseQuery.Where(o => o.Status == "In Production" && o.ProductionStage == stage);
            // Same Customize-only rule as pIndex — see comment there.
            baseQuery = baseQuery.Where(o => o.OrderDetails.Any(d => d.IsCustomOrder));

            var query = baseQuery;
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
                .OrderBy(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.StationIndex = stage;
            ViewBag.StationName = StationNames[stage];
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            // Stat cards: Total (totalCount, above) / In Progress /
            // Completed Today / Pending from Previous. "Pending from
            // Previous" = orders that have been sitting at this station
            // since before today (arrived on an earlier day and haven't
            // moved); "Completed Today" = orders that left this exact
            // station today. Both lean on ProductionStageUpdatedAt, which
            // ProductionWorkflow.SetStage stamps every time a stage
            // advances — see that class for details.
            DateTime todayStart = DateTime.Today;
            int pendingFromPreviousCount;
            int completedTodayCount;

            if (isCutting)
            {
                pendingFromPreviousCount = await _context.Orders.CountAsync(o =>
                    o.Status == "Confirmed" && o.OrderDate < todayStart && o.OrderDetails.Any(d => d.IsCustomOrder));
                completedTodayCount = await _context.Orders.CountAsync(o =>
                    o.ProductionStage == stage + 1 && o.ProductionStageUpdatedAt != null &&
                    o.ProductionStageUpdatedAt >= todayStart && o.OrderDetails.Any(d => d.IsCustomOrder));
            }
            else if (isReadyForPickup)
            {
                pendingFromPreviousCount = await _context.Orders.CountAsync(o =>
                    o.Status == "Ready for Pickup" &&
                    (o.ProductionStageUpdatedAt == null || o.ProductionStageUpdatedAt < todayStart) &&
                    o.OrderDetails.Any(d => d.IsCustomOrder));
                completedTodayCount = await _context.Orders.CountAsync(o =>
                    o.Status == "Completed" && o.ProductionStageUpdatedAt != null &&
                    o.ProductionStageUpdatedAt >= todayStart && o.OrderDetails.Any(d => d.IsCustomOrder));
            }
            else
            {
                pendingFromPreviousCount = await _context.Orders.CountAsync(o =>
                    o.Status == "In Production" && o.ProductionStage == stage &&
                    (o.ProductionStageUpdatedAt == null || o.ProductionStageUpdatedAt < todayStart) &&
                    o.OrderDetails.Any(d => d.IsCustomOrder));
                completedTodayCount = await _context.Orders.CountAsync(o =>
                    o.ProductionStage == stage + 1 && o.ProductionStageUpdatedAt != null &&
                    o.ProductionStageUpdatedAt >= todayStart && o.OrderDetails.Any(d => d.IsCustomOrder));
            }

            ViewBag.PendingFromPreviousCount = pendingFromPreviousCount;
            ViewBag.CompletedTodayCount = completedTodayCount;
            ViewBag.InProgressCount = Math.Max(0, totalCount - pendingFromPreviousCount);

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

        // POST: /Production/pCompleteStation — the popup's "Mark as Done"
        // submit. Advances the order past the given station (stage ->
        // stage+1), stamping ProductionStageUpdatedAt/ProductionRemarks.
        // Only succeeds if the order is genuinely still sitting at that
        // station, so a stale/duplicate submit can't double-advance it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pCompleteStation(int id, int stage, int quantity, string? remarks)
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

            int target = order.OrderDetails.Where(d => d.IsCustomOrder).Sum(d => d.Quantity);
            if (quantity < target)
                return BadRequest($"Quantity must reach {target} pcs before marking this station done.");

            order.ProductionRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            ProductionWorkflow.SetStage(order, stage + 1);
            await _context.SaveChangesAsync();

            string stationName = (stage >= 0 && stage < StationNames.Length) ? StationNames[stage] : $"stage {stage}";
            await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Completed Station",
                $"{order.CustomerName} — finished {stationName} ({quantity} pcs).", order.OrderID);

            return Ok(new { success = true, message = $"Order #ORD-{order.OrderID:D5} marked done at {stationName}." });
        }

        // POST: /Production/pSetStage — clicking a stage in the stepper
        // marks it (and everything before it) done.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pSetStage(int id, int stage)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                ProductionWorkflow.SetStage(order, stage);
                await _context.SaveChangesAsync();
                string stageName = (stage >= 0 && stage < StationNames.Length) ? StationNames[stage] : $"stage {stage}";
                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Advanced Production Stage",
                    $"{order.CustomerName} — reached {stageName}.", order.OrderID);
                TempData["Success"] = $"Order #ORD-{order.OrderID:D5} production updated.";
            }
            return RedirectToAction(nameof(pIndex));
        }

        // POST: /Production/pMarkCompleted
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pMarkCompleted(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                ProductionWorkflow.MarkCompleted(order);
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Production", "Marked Order Completed",
                    $"{order.CustomerName}'s order picked up / completed.", order.OrderID);
                TempData["Success"] = $"Order #ORD-{order.OrderID:D5} marked Completed.";
            }
            return RedirectToAction(nameof(pIndex));
        }
    }
}
