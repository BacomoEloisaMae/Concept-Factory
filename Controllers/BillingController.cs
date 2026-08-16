using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;

namespace ConceptFactory.Controllers
{
    // Admin "Billing" screen — a.k.a. Payment Verification. There's no
    // separate Payments table: every order collects one down payment at
    // checkout, at least 50% of the total but the customer can choose to
    // pay more (see HomeController.SubmitOrder), so the payment fields
    // already living on Order (PaymentMethod, PaymentStatus,
    // ReferenceNumber, ProofFilePath, DownPaymentAmount, RemainingBalance)
    // ARE the payment record. This controller just gives admins a
    // dedicated, payment-focused view/workflow over that same data —
    // approving or rejecting here updates the same Order row shown in
    // Order Management, so the two screens can never drift out of sync.
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static readonly Dictionary<string, string> TabToStatus = new()
        {
            ["Waiting"] = "Waiting for Verification",
            ["Approved"] = "Partially Paid",
            ["FullyPaid"] = "Fully Paid",
            ["Rejected"] = "Rejected",
        };

        // GET: /Billing/bIndex
        public async Task<IActionResult> bIndex(string? tab, string? paymentMethod, string? search, int page = 1)
        {
            int pageSize = 10;

            // "Fully Paid" is included here (not just Waiting/Partially
            // Paid/Rejected) so a payment doesn't disappear from Billing
            // entirely once the admin marks the remaining balance as paid —
            // it moves to its own "Fully Paid" tab instead of vanishing.
            var baseQuery = _context.Orders.Where(o =>
                            o.PaymentStatus == "Waiting for Verification" ||
                            o.PaymentStatus == "Partially Paid" ||
                            o.PaymentStatus == "Fully Paid" ||
                            o.PaymentStatus == "Rejected");

            // Stat cards always reflect the FULL set of payments, regardless
            // of whatever tab/search/filter is currently applied below.
            ViewBag.WaitingCount       = await _context.Orders.CountAsync(o => o.PaymentStatus == "Waiting for Verification");
            ViewBag.ApprovedCount       = await _context.Orders.CountAsync(o => o.PaymentStatus == "Partially Paid");
            ViewBag.FullyPaidCount     = await _context.Orders.CountAsync(o => o.PaymentStatus == "Fully Paid");
            ViewBag.RejectedCount      = await _context.Orders.CountAsync(o => o.PaymentStatus == "Rejected");

            var query = baseQuery;

            if (!string.IsNullOrWhiteSpace(tab) && TabToStatus.TryGetValue(tab, out var statusValue))
                query = query.Where(o => o.PaymentStatus == statusValue);

            if (!string.IsNullOrWhiteSpace(paymentMethod))
                query = query.Where(o => o.PaymentMethod == paymentMethod);

            if (!string.IsNullOrWhiteSpace(search))
            {
                bool hasOrderIdMatch = ConceptFactory.Utils.OrderSearchHelper.TryParseOrderId(search, out int parsedOrderId);
                query = query.Where(o =>
                    o.CustomerName.Contains(search) ||
                    (hasOrderIdMatch && o.OrderID == parsedOrderId) ||
                    (o.ReferenceNumber ?? "").Contains(search));
            }

            int totalCount = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tab = tab ?? "All";
            ViewBag.PaymentMethod = paymentMethod;
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            ViewData["Title"] = "Billing";
            ViewData["ActivePage"] = "Billing";

            return View(orders);
        }

        // GET: /Billing/bDetailsPanel/5 — the "Payment Verification" modal
        // content: customer + payment info, uploaded proof screenshot, and
        // the order amount breakdown, with Approve/Reject actions.
        [HttpGet]
        public async Task<IActionResult> bDetailsPanel(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Service)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            // Cash's "Received By" field is display-only (no per-payment
            // audit trail persisted) — just shows whoever is currently
            // logged into the admin panel confirming it.
            ViewBag.AdminName = HttpContext.Session.GetString("AdminName");

            return PartialView("_PaymentVerificationPanel", order);
        }

        // POST: /Billing/bApprove — confirms the down payment. Button reads
        // "Approve Payment" for Gcash / "Confirm Payment" for Cash in the
        // UI, but both hit this same action and land on "Partially Paid"
        // (only the 50% down payment has actually been collected at this
        // point). Also bumps the order's own Status to "Confirmed" via
        // PaymentWorkflow, so Order Management reflects this immediately.
        // For Cash, amountReceived is the physical cash handed over
        // (Billing → Payment Verification's "Amount Received" field) —
        // must cover at least the required down payment; the change given
        // back is derived (amountReceived - DownPaymentAmount) rather than
        // stored, since DownPaymentAmount is what's actually applied.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> bApprove(int id, string? note, decimal? amountReceived)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                if (order.PaymentMethod == "Cash" && order.PaymentStatus == "Waiting for Verification")
                {
                    if (!amountReceived.HasValue || amountReceived.Value < order.DownPaymentAmount)
                    {
                        TempData["Success"] = $"Amount received is less than the required down payment — order #ORD-{order.OrderID:D3} was left unchanged.";
                        return RedirectToAction(nameof(bIndex));
                    }
                    order.CashAmountReceived = Math.Round(amountReceived.Value, 2);
                }

                PaymentWorkflow.ApplyPaymentStatus(order, "Partially Paid", note);
                await _context.SaveChangesAsync();
                string method = order.PaymentMethod == "Cash" ? "Confirmed Cash Payment" : "Approved Gcash Payment";
                await ActivityLogger.LogAsync(_context, HttpContext, "Billing", method,
                    $"₱{order.DownPaymentAmount:N2} down payment for {order.CustomerName}.", order.OrderID);
                TempData["Success"] = $"Payment for order #ORD-{order.OrderID:D3} confirmed — order moved to Confirmed.";
            }
            return RedirectToAction(nameof(bIndex));
        }

        // POST: /Billing/bReject — also bumps order Status to "Cancelled"
        // via PaymentWorkflow (only while the order is still one of the
        // "Pending ..." states; an order already past Confirmed is left
        // alone).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> bReject(int id, string? note)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                // Cash payments have no online proof to dispute — the
                // "Reject Payment" button is hidden for them in the UI, but
                // guard here too in case this is hit directly.
                if (order.PaymentMethod == "Cash")
                {
                    TempData["Success"] = $"Cash payments can't be rejected here — order #ORD-{order.OrderID:D3} was left unchanged.";
                    return RedirectToAction(nameof(bIndex));
                }

                PaymentWorkflow.ApplyPaymentStatus(order, "Rejected", note);
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Billing", "Rejected Payment",
                    string.IsNullOrWhiteSpace(note) ? $"For {order.CustomerName}." : $"For {order.CustomerName} — {note}", order.OrderID);
                TempData["Success"] = $"Payment for order #ORD-{order.OrderID:D3} rejected.";
            }
            return RedirectToAction(nameof(bIndex));
        }

        // POST: /Billing/bMarkFullyPaid — once the remaining balance also
        // comes in (e.g. on pickup/delivery), admin marks the payment
        // fully settled. Only meaningful once the down payment is already
        // "Partially Paid".
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> bMarkFullyPaid(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order != null)
            {
                decimal settledAmount = order.RemainingBalance;
                PaymentWorkflow.MarkFullyPaid(order);
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Billing", "Marked Fully Paid",
                    $"₱{settledAmount:N2} remaining balance settled for {order.CustomerName}.", order.OrderID);
                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} marked as Fully Paid.";
            }
            return RedirectToAction(nameof(bIndex));
        }
    }
}
