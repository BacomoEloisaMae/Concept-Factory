using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Filters;

namespace ConceptFactory.Controllers
{
    // Admin-side notification bell — reads Notification rows written by
    // NotificationService (see Utils/NotificationService.cs) wherever an
    // order changes state (placed, payment verified/rejected/fully paid,
    // each production stage change, completed). Audience == "Admin" only;
    // the customer side of the same feed is HomeController.hNotifications.
    [AdminAuthFilter(Roles = "Admin,Sales,Production")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Applies the "what can this role see" rule shared by nIndex,
        // nBadgeCount, and nDropdown below:
        //   - Admin sees everything (no filter).
        //   - Production only sees production notifications for their OWN
        //     assigned station (User.StationIndex, read from session as
        //     "StaffStationIndex" — see AuthController.TryLoginStaffAsync)
        //     — i.e. "a new order arrived at my station" and nothing else
        //     (no OrderPlaced/PaymentVerified/etc, no other stations).
        //   - Sales sees everything EXCEPT the granular per-station
        //     "ProductionAdvanced" pings — just Production Started and
        //     Ready for Pickup/Completed, so they aren't buried in every
        //     single stage change.
        private IQueryable<Notification> FilterForRole(IQueryable<Notification> query)
        {
            string staffRole = HttpContext.Session.GetString("StaffRole") ?? "Admin";

            if (staffRole == "Production")
            {
                int? myStation = HttpContext.Session.GetInt32("StaffStationIndex");
                return query.Where(n =>
                    (n.Type == "ProductionStarted" || n.Type == "ProductionAdvanced" || n.Type == "Completed")
                    && n.StationIndex == myStation);
            }

            if (staffRole == "Sales")
            {
                return query.Where(n => n.Type != "ProductionAdvanced");
            }

            return query;
        }

        // GET: /Notifications/nIndex
        public async Task<IActionResult> nIndex(string? tab, int page = 1)
        {
            int pageSize = 20;
            var baseQuery = FilterForRole(_context.Notifications.Where(n => n.Audience == "Admin"));

            ViewBag.UnreadCount = await baseQuery.CountAsync(n => !n.IsRead);
            ViewBag.TotalCount2 = await baseQuery.CountAsync();

            var query = baseQuery;
            if (string.Equals(tab, "Unread", StringComparison.OrdinalIgnoreCase))
                query = query.Where(n => !n.IsRead);

            int totalCount = await query.CountAsync();
            var notifications = await query
                .Include(n => n.Order)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tab = tab ?? "All";
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            ViewData["Title"] = "Notifications";
            ViewData["ActivePage"] = "Notifications";

            return View(notifications);
        }

        // GET: /Notifications/nBadgeCount — polled by admin-notifications.js
        // to keep the sidebar bell badge live without a full page reload.
        [HttpGet]
        public async Task<IActionResult> nBadgeCount()
        {
            int count = await FilterForRole(_context.Notifications.Where(n => n.Audience == "Admin")).CountAsync(n => !n.IsRead);
            return Json(new { count });
        }

        // GET: /Notifications/nDropdown — small recent-notifications panel
        // for the sidebar bell's popover.
        [HttpGet]
        public async Task<IActionResult> nDropdown()
        {
            var recent = await FilterForRole(_context.Notifications.Where(n => n.Audience == "Admin"))
                .OrderByDescending(n => n.CreatedAt)
                .Take(8)
                .ToListAsync();
            return PartialView("_NotificationDropdown", recent);
        }

        // POST: /Notifications/nMarkRead/5 — marking a notification read
        // also redirects to its order when possible, since clicking one
        // is almost always "take me to that order".
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> nMarkRead(int id)
        {
            var notif = await FilterForRole(_context.Notifications.Where(n => n.Audience == "Admin"))
                .FirstOrDefaultAsync(n => n.NotificationID == id);
            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        // POST: /Notifications/nMarkAllRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> nMarkAllRead()
        {
            var unread = await FilterForRole(_context.Notifications.Where(n => n.Audience == "Admin" && !n.IsRead)).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction(nameof(nIndex));
        }
    }
}
