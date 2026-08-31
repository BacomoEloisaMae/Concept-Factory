using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;
using ConceptFactory.Filters;

namespace ConceptFactory.Controllers
{
    // Order Management — per the module spec, Sales/Admin can View Order,
    // View Customer Details, Print Job Order (client-side, see
    // _OrderDetailsBody.cshtml), Edit Order Details (before production
    // starts, see oEdit below), and Cancel Order. They CANNOT verify
    // payments or update production stages from here — those live in
    // BillingController and the (future) Production module respectively,
    // which is why there's no payment-status or production-stage action
    // in this controller anymore.
    [AdminAuthFilter(Roles = "Admin,Sales")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Tabs on the page map to real order Status values. "Processing"
        // and "In Production" each cover a few granular statuses grouped
        // together for this simplified view.
        private static readonly Dictionary<string, string[]> TabToStatuses = new()
        {
            ["Processing"] = new[]
            {
                "Pending Down Payment",
                "Pending Cash Payment",
                "Pending Payment Verification",
                "Confirmed"
            },
            ["InProduction"] = new[] { "In Production", "Ready for Pickup" },
            ["Completed"] = new[] { "Completed" },
            ["Cancel"] = new[] { "Cancelled" },
        };

        // GET: /Orders/oIndex
        public async Task<IActionResult> oIndex(string? tab, string? paymentStatus, string? search, DateTime? date, int page = 1)
        {
            int pageSize = 10;

            var baseQuery = _context.Orders
                .Include(o => o.OrderDetails)
                .AsQueryable();

            // Stat cards always reflect the FULL set of orders, regardless
            // of whatever tab/search/filter is currently applied below.
            ViewBag.TotalOrders     = await _context.Orders.CountAsync();
            ViewBag.ProcessingCount = await _context.Orders.CountAsync(o => TabToStatuses["Processing"].Contains(o.Status));
            ViewBag.InProductionCount = await _context.Orders.CountAsync(o => TabToStatuses["InProduction"].Contains(o.Status));
            ViewBag.CompletedCount  = await _context.Orders.CountAsync(o => o.Status == "Completed");
            ViewBag.CancelledCount  = await _context.Orders.CountAsync(o => o.Status == "Cancelled");

            var query = baseQuery;

            if (!string.IsNullOrWhiteSpace(tab) && TabToStatuses.TryGetValue(tab, out var statusValues))
                query = query.Where(o => statusValues.Contains(o.Status));

            if (!string.IsNullOrWhiteSpace(paymentStatus))
                query = query.Where(o => o.PaymentStatus == paymentStatus);

            if (date.HasValue)
                query = query.Where(o => o.OrderDate.Date == date.Value.Date);

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool hasOrderIdMatch = ConceptFactory.Utils.OrderSearchHelper.TryParseOrderId(search, out int parsedOrderId);
                query = query.Where(o =>
                    o.CustomerName.Contains(search) ||
                    (hasOrderIdMatch && o.OrderID == parsedOrderId) ||
                    o.OrderDetails.Any(d => (d.ProductNameSnapshot ?? "").Contains(search) ||
                                            (d.Product != null && d.Product.ProductName.Contains(search))));
            }

            int totalCount = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tab = tab ?? "All";
            ViewBag.PaymentStatus = paymentStatus;
            ViewBag.Search = search;
            ViewBag.Date = date;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            ViewData["Title"] = "Orders";
            ViewData["ActivePage"] = "Orders";

            return View(orders);
        }

        // GET: /Orders/oDetails/5 — full standalone page (e.g. linked to
        // from Billing). Shares its body markup with the oIndex popup via
        // _OrderDetailsBody.cshtml.
        public async Task<IActionResult> oDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Service)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Category)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            ViewData["Title"] = $"Order #{order.OrderID}";
            ViewData["ActivePage"] = "Orders";

            return View(order);
        }

        // GET: /Orders/oDetailsPanel/5 — the same details, returned as a
        // partial for the oIndex popup modal (see loadOrderDetails() in
        // oIndex.cshtml).
        [HttpGet]
        public async Task<IActionResult> oDetailsPanel(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Service)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Category)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            return PartialView("_OrderDetailsPanel", order);
        }

        // GET/POST: /Orders/oEdit/5 — "Edit Order Details (only before
        // production starts)". Only exposes the customer contact fields;
        // everything about what was ordered is locked in at checkout.
        // Gated both here AND in the view (canEdit in
        // _OrderDetailsBody.cshtml) — a direct POST after the order has
        // already moved into production is rejected.
        private static bool IsEditableStatus(string status) =>
            status is
                "Pending Down Payment"
                or "Pending Cash Payment"
                or "Pending Payment Verification"
                or "Confirmed";

        [HttpGet]
        public async Task<IActionResult> oEdit(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order == null) return NotFound();

            if (!IsEditableStatus(order.Status))
            {
                TempData["Success"] = null;
                TempData["Error"] = $"Order #ORD-{order.OrderID:D3} can no longer be edited — it's already {order.Status}.";
                return RedirectToAction(nameof(oIndex), new { open = id });
            }

            ViewData["Title"] = $"Edit Order #{order.OrderID}";
            ViewData["ActivePage"] = "Orders";
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> oEdit(int id, string customerName, string? customerEmail, string? customerPhone, string? customerAddress)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order == null) return NotFound();

            if (!IsEditableStatus(order.Status))
            {
                TempData["Error"] = $"Order #ORD-{order.OrderID:D3} can no longer be edited — it's already {order.Status}.";
                return RedirectToAction(nameof(oIndex), new { open = id });
            }

            if (string.IsNullOrWhiteSpace(customerName))
            {
                ModelState.AddModelError("customerName", "Customer name is required.");
                ViewData["Title"] = $"Edit Order #{order.OrderID}";
                ViewData["ActivePage"] = "Orders";
                return View(order);
            }

            order.CustomerName = customerName;
            order.CustomerEmail = customerEmail;
            order.CustomerPhone = customerPhone;
            order.CustomerAddress = customerAddress;
            await _context.SaveChangesAsync();
            await ActivityLogger.LogAsync(_context, HttpContext, "Orders", "Edited Order Details",
                $"Customer info updated for {order.CustomerName}.", order.OrderID);

            TempData["Success"] = $"Order #ORD-{order.OrderID:D3} details updated.";
            return RedirectToAction(nameof(oIndex), new { open = id });
        }

        // POST: /Orders/oUpdateStatus — used for Cancel Order (the only
        // status change Sales/Admin can make directly). "reason" is
        // required by the confirmation popup in the UI (see
        // _OrderDetailsBody.cshtml/orders-index.js) for Cancelled; it isn't
        // persisted as its own column, but it's recorded in the activity
        // log and passed on to the customer so they know why.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> oUpdateStatus(int id, string status, string? reason, string? returnUrl)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                // Once production has started (or the order is already
                // done/cancelled), it can no longer be cancelled — mirrors
                // IsEditableStatus above and the canCancel check in
                // _OrderDetailsBody.cshtml/oIndex.cshtml, enforced here too
                // in case of a direct POST.
                if (status == "Cancelled" && !IsEditableStatus(order.Status))
                {
                    TempData["Error"] = $"Order #ORD-{order.OrderID:D3} can no longer be cancelled — production has already started.";
                }
                else
                {
                    string previousStatus = order.Status;
                    order.Status = status;
                    await _context.SaveChangesAsync();
                    string trimmedReason = string.IsNullOrWhiteSpace(reason) ? "" : reason.Trim();
                    string logDetail = $"{order.CustomerName} — {previousStatus} → {status}."
                        + (trimmedReason == "" ? "" : $" Reason: {trimmedReason}");
                    await ActivityLogger.LogAsync(_context, HttpContext, "Orders", $"Marked Order {status}",
                        logDetail, order.OrderID);

                    if (status == "Cancelled")
                    {
                        await NotificationService.NotifyCustomerAsync(_context, order.OrderID, "OrderCancelled",
                            "Order cancelled",
                            $"Your order #ORD-{order.OrderID:D5} has been cancelled."
                                + (trimmedReason == "" ? "" : $" Reason: {trimmedReason}"));
                    }

                    TempData["Success"] = $"Order #ORD-{order.OrderID:D3} marked as {status}.";
                }
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(oIndex), new { open = id });
        }
    }
}
