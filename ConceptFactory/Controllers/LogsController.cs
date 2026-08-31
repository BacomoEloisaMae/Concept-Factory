using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Utils;
using ConceptFactory.Filters;

namespace ConceptFactory.Controllers
{
    // Read-only view over ActivityLogs (see Models/ActivityLog.cs and
    // Utils/ActivityLogger.cs, which is what actually writes the rows from
    // Auth/Billing/Orders/Production/Products/Services). There's
    // intentionally no create/edit/delete here — an audit trail that can be
    // edited from the app isn't one.
    [AdminAuthFilter(Roles = "Admin")]
    public class LogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public static readonly string[] Categories =
        {
            "Auth", "Billing", "Orders", "Production", "Products", "Services"
        };

        public LogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Logs/lIndex
        public async Task<IActionResult> lIndex(string? category, string? search, DateTime? date, int page = 1)
        {
            int pageSize = 20;
            var query = _context.ActivityLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && Categories.Contains(category))
                query = query.Where(l => l.Category == category);

            if (date.HasValue)
                query = query.Where(l => l.Timestamp.Date == date.Value.Date);

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool hasOrderIdMatch = OrderSearchHelper.TryParseOrderId(search, out int parsedOrderId);
                query = query.Where(l =>
                    (l.AdminName ?? "").Contains(search) ||
                    l.Action.Contains(search) ||
                    (l.Details ?? "").Contains(search) ||
                    (hasOrderIdMatch && l.OrderID == parsedOrderId));
            }

            int totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Category = category ?? "All";
            ViewBag.Search = search;
            ViewBag.Date = date;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.TodayCount = await _context.ActivityLogs.CountAsync(l => l.Timestamp.Date == DateTime.Today);

            ViewData["Title"] = "Logs";
            ViewData["ActivePage"] = "Logs";

            return View(logs);
        }
    }
}
