using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Filters;

namespace ConceptFactory.Controllers
{
    // Reports & Analytics — four report types (Sales/Billing/Orders/
    // Production), all sharing one page and one date-range filter. Every
    // count/sum below is scoped to Order.OrderDate falling inside the
    // selected range UNLESS noted otherwise (Production's "currently in
    // production" figures are live/operational, not historical, so they
    // intentionally ignore the date range — an order started last month
    // that's still on the floor today still belongs in "Total Orders in
    // Production").
    [AdminAuthFilter(Roles = "Admin,Sales")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly string[] StageNames =
            { "Cutting", "Printing", "Sewing", "Trimming", "Quality Check", "Ready for Pickup" };

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Money actually collected on an order so far — the shared
        // "revenue"/"amount paid" definition used across every report tab
        // and the Dashboard, so the numbers agree with each other.
        private static decimal CollectedAmount(Order o) => o.PaymentStatus switch
        {
            "Fully Paid" => o.TotalAmount,
            "Partially Paid" => o.DownPaymentAmount,
            _ => 0m // "Waiting for Verification" / "Rejected" — nothing collected yet
        };

        private static int ProgressPercent(Order o) => o.Status switch
        {
            "Completed" => 100,
            "Confirmed" or "Design Review" => 0,
            _ when o.ProductionStage >= 0 => (int)Math.Round((o.ProductionStage + 1) / 6.0 * 100),
            _ => 0
        };

        public async Task<IActionResult> rIndex(string? type, DateTime? from, DateTime? to, string? search, int page = 1)
        {
            type = string.IsNullOrWhiteSpace(type) ? "Sales" : type;
            int pageSize = 10;

            var now = DateTime.Now;
            DateTime rangeFrom = (from ?? new DateTime(now.Year, now.Month, 1)).Date;
            DateTime rangeTo = (to ?? rangeFrom.AddMonths(1).AddDays(-1)).Date;
            DateTime rangeToExclusive = rangeTo.AddDays(1);

            ViewBag.Type = type;
            ViewBag.From = rangeFrom;
            ViewBag.To = rangeTo;
            // Raw, possibly-null values as the person actually typed them —
            // separate from rangeFrom/rangeTo above, which always default to
            // the current month for computing stats. The date <input>s use
            // these instead, so "Clear Filter" actually empties them instead
            // of silently re-filling them with this month's dates.
            ViewBag.FromRaw = from;
            ViewBag.ToRaw = to;
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewData["Title"] = "Reports & Analytics";
            ViewData["ActivePage"] = "Reports";

            // Accountability trail — see Models/ReportLog.cs. Doesn't
            // affect anything the report actually computes below.
            _context.ReportLogs.Add(new ReportLog
            {
                ReportType = type,
                DateFrom = rangeFrom,
                DateTo = rangeTo,
                GeneratedByName = HttpContext.Session.GetString("AdminName"),
                GeneratedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            // The table at the bottom of each tab gets its own search box
            // beside the title — same "Order ID / customer / product" match
            // every other module's search uses (see OrdersController /
            // ProductionController), scoped to whichever report table is on
            // screen. It only narrows the TABLE — the stat cards and the
            // breakdown widget above it keep reflecting the full date range,
            // same convention Billing already uses ("stat cards always
            // reflect the full set, regardless of search").
            bool hasSearch = !string.IsNullOrWhiteSpace(search);
            int parsedOrderId = 0;
            bool hasOrderIdMatch = hasSearch && ConceptFactory.Utils.OrderSearchHelper.TryParseOrderId(search!, out parsedOrderId);

            // Plain in-memory predicate (not an IQueryable expression) since
            // every branch below already has its full list loaded before
            // this gets applied — it only narrows what's shown in the table.
            bool MatchesSearch(Order o, bool matchReference = false)
            {
                if (!hasSearch) return true;
                if (o.CustomerName.Contains(search!, StringComparison.OrdinalIgnoreCase)) return true;
                if (hasOrderIdMatch && o.OrderID == parsedOrderId) return true;
                if (matchReference)
                    return (o.ReferenceNumber ?? "").Contains(search!, StringComparison.OrdinalIgnoreCase);
                return o.OrderDetails.Any(d => (d.ProductNameSnapshot ?? "").Contains(search!, StringComparison.OrdinalIgnoreCase));
            }

            var rangeOrders = _context.Orders.Include(o => o.OrderDetails)
                .Where(o => o.OrderDate >= rangeFrom && o.OrderDate < rangeToExclusive);

            if (type == "Billing")
            {
                var all = await rangeOrders.OrderByDescending(o => o.OrderDate).ToListAsync();
                decimal totalBilling = all.Sum(o => o.TotalAmount);
                decimal amountPaid = all.Sum(CollectedAmount);
                decimal downPayments = all.Where(o => o.PaymentStatus is "Partially Paid" or "Fully Paid").Sum(o => o.DownPaymentAmount);
                decimal balanceDue = totalBilling - amountPaid;

                ViewBag.TotalBilling = totalBilling;
                ViewBag.AmountPaid = amountPaid;
                ViewBag.DownPayments = downPayments;
                ViewBag.BalanceDue = balanceDue;
                ViewBag.GcashCount = all.Count(o => o.PaymentMethod == "Gcash");
                ViewBag.CashCount = all.Count(o => o.PaymentMethod == "Cash");

                var filtered = all.Where(o => MatchesSearch(o, matchReference: true)).ToList();
                int totalCount = filtered.Count;
                var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                ViewBag.TotalCount = totalCount;
                return View(paged);
            }
            else if (type == "Orders")
            {
                var all = await rangeOrders.OrderByDescending(o => o.OrderDate).ToListAsync();
                int totalOrders = all.Count;
                int completed = all.Count(o => o.Status == "Completed");
                int inProduction = all.Count(o => o.Status is "In Production" or "Ready for Pickup");
                int cancelled = all.Count(o => o.Status == "Cancelled");
                int processing = totalOrders - completed - inProduction - cancelled;

                ViewBag.OrdersTotalOrders = totalOrders;
                ViewBag.OrdersCompleted = completed;
                ViewBag.OrdersInProduction = inProduction;
                ViewBag.OrdersCancelled = cancelled;
                ViewBag.OrdersProcessing = processing;

                var filtered = all.Where(o => MatchesSearch(o)).ToList();
                int totalCount = filtered.Count;
                var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                ViewBag.TotalCount = totalCount;
                return View(paged);
            }
            else if (type == "Production")
            {
                // Live/operational — not date-filtered, see class comment.
                // Stats/breakdown always reflect every in-production order;
                // only the table below narrows by search.
                var inProdOrders = await _context.Orders.Include(o => o.OrderDetails)
                    .Where(o => o.Status == "In Production").ToListAsync();

                ViewBag.TotalInProduction = inProdOrders.Count;
                ViewBag.AvgProgress = inProdOrders.Count > 0 ? (int)Math.Round(inProdOrders.Average(ProgressPercent)) : 0;

                // "Completed This Period" / "Avg Production Time" DO use the
                // date range — they're historical, scoped to when the order
                // was completed (last stage update), not when it was placed.
                var completedInRange = await _context.Orders
                    .Where(o => o.Status == "Completed" && o.ProductionStageUpdatedAt != null
                             && o.ProductionStageUpdatedAt >= rangeFrom && o.ProductionStageUpdatedAt < rangeToExclusive)
                    .ToListAsync();
                ViewBag.CompletedThisPeriod = completedInRange.Count;

                var completedIds = completedInRange.Select(o => o.OrderID).ToList();
                // GroupBy+Min, not a straight ToDictionary — an order can
                // pick up more than one "ProductionStarted" notification
                // (e.g. if it re-enters production), and a duplicate OrderID
                // key would throw. The earliest one is "when it started".
                var startedLog = await _context.Notifications
                    .Where(n => n.Type == "ProductionStarted" && completedIds.Contains(n.OrderID))
                    .GroupBy(n => n.OrderID)
                    .Select(g => new { OrderID = g.Key, CreatedAt = g.Min(n => n.CreatedAt) })
                    .ToDictionaryAsync(x => x.OrderID, x => x.CreatedAt);
                var durations = completedInRange
                    .Where(o => startedLog.ContainsKey(o.OrderID) && o.ProductionStageUpdatedAt != null)
                    .Select(o => (o.ProductionStageUpdatedAt!.Value - startedLog[o.OrderID]).TotalDays)
                    .Where(d => d >= 0)
                    .ToList();
                ViewBag.AvgProductionDays = durations.Count > 0 ? Math.Round(durations.Average(), 1) : (double?)null;

                // Per-stage breakdown — how many orders currently sit at each
                // station, and how far along each station's own quantity
                // tally is on average (same quantity-based progress concept
                // pStation.cshtml uses per order, averaged here per stage).
                var stageCounts = new int[6];
                var stageAvgProgress = new int[6];

                foreach (var o in inProdOrders)
                {
                    int idx = Math.Clamp(o.ProductionStage, 0, 5);

                    stageCounts[idx]++;

                    // Progress based on the current production stage
                    stageAvgProgress[idx] = (int)Math.Round((idx + 1) / 6.0 * 100);
                }
                ViewBag.StageNames = StageNames;
                ViewBag.StageCounts = stageCounts;
                ViewBag.StageAvgProgress = stageAvgProgress;

                var filteredInProd = inProdOrders.Where(o => MatchesSearch(o)).ToList();

                // "Started Date" for the table — same technique as
                // ProductionController.pIndex. Same duplicate-key guard as
                // startedLog above.
                var orderIds = filteredInProd.Select(o => o.OrderID).ToList();
                var startedDates = await _context.Notifications
                    .Where(n => n.Type == "ProductionStarted" && orderIds.Contains(n.OrderID))
                    .GroupBy(n => n.OrderID)
                    .Select(g => new { OrderID = g.Key, CreatedAt = g.Min(n => n.CreatedAt) })
                    .ToDictionaryAsync(x => x.OrderID, x => x.CreatedAt);
                ViewBag.StartedDates = startedDates;

                int totalCount = filteredInProd.Count;
                var paged = filteredInProd.OrderByDescending(o => o.OrderDate).Skip((page - 1) * pageSize).Take(pageSize).ToList();
                ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                ViewBag.TotalCount = totalCount;
                return View(paged);
            }
            else // Sales (default)
            {
                var all = await rangeOrders
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                int totalOrders = all.Count;

                int completed = all.Count(o =>
                    o.Status == "Completed");

                int cancelled = all.Count(o =>
                    o.Status == "Cancelled");

                int pending = totalOrders - completed - cancelled;

                // TOTAL SALES:
                // Only confirmed/approved sales are counted.
                // Waiting for Verification = NOT counted
                // Rejected = NOT counted
                // Cancelled = NOT counted
                //
                // Partially Paid = full confirmed order sale
                // Fully Paid     = full confirmed order sale
                decimal totalSales = all
                    .Where(o => o.Status != "Cancelled" &&
                                (o.PaymentStatus == "Partially Paid" ||
                                 o.PaymentStatus == "Fully Paid"))
                    .Sum(o => o.TotalAmount);

                // REVENUE:
                // Only money actually collected is counted.
                //
                // Fully Paid     = full order amount
                // Partially Paid = approved down payment
                // Waiting        = 0
                // Rejected       = 0
                decimal revenue = all
                    .Where(o => o.Status != "Cancelled")
                    .Sum(CollectedAmount);

                ViewBag.SalesTotalSales = totalSales;
                ViewBag.SalesTotalOrders = totalOrders;
                ViewBag.SalesCompleted = completed;
                ViewBag.SalesPending = pending;
                ViewBag.SalesCancelled = cancelled;
                ViewBag.SalesRevenue = revenue;

                var filtered = all
                    .Where(o => MatchesSearch(o))
                    .ToList();

                int totalCount = filtered.Count;

                var paged = filtered
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                ViewBag.TotalPages =
                    (int)Math.Ceiling(totalCount / (double)pageSize);

                ViewBag.TotalCount = totalCount;

                return View(paged);
            }
        }
    }
}
