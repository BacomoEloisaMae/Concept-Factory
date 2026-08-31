using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;
using ConceptFactory.Filters;

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
    [AdminAuthFilter(Roles = "Admin,Sales")]
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

        // GET: /Billing/bPrintInvoice/5 and /Billing/bPrintReceipt/5 — both
        // open in a new tab as standalone printable documents (Layout =
        // null), built entirely from the order's existing data — no
        // separate Invoice/Receipt records. Only available once the down
        // payment has actually been approved (Partially Paid or Fully
        // Paid); Waiting/Rejected orders redirect back with an error since
        // there's nothing confirmed yet to put on either document.
        [HttpGet]
        public async Task<IActionResult> bPrintInvoice(int id)
        {
            var order = await GetOrderForPrintAsync(id);
            if (order == null) return NotFound();
            if (order.PaymentStatus != "Partially Paid" && order.PaymentStatus != "Fully Paid")
            {
                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} isn't approved yet — nothing to print.";
                return RedirectToAction(nameof(bIndex));
            }
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> bPrintReceipt(int id)
        {
            var order = await GetOrderForPrintAsync(id);
            if (order == null) return NotFound();
            if (order.PaymentStatus != "Partially Paid" && order.PaymentStatus != "Fully Paid")
            {
                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} isn't approved yet — nothing to print.";
                return RedirectToAction(nameof(bIndex));
            }
            return View(order);
        }

        private async Task<Order?> GetOrderForPrintAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);
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
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);
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

                // Auto-estimate a production target date the moment the
                // order is actually confirmed, so Production Overview
                // isn't blank until an admin sets one manually — see
                // ProductionEstimator for the quantity -> working-days
                // tiers. Only fills it in if nothing's set yet, so a
                // manual override from the Overview table is never
                // clobbered by a re-approval.
                if (order.Status == "Confirmed" && order.ProductionTargetDate == null)
                {
                    // Every order line goes through the production stations
                    // (see ProductionController, which counts all OrderDetails
                    // with no IsCustomOrder filter) — IsCustomOrder only marks
                    // lines that came from the Customize flow, not which lines
                    // require production, so it must not be used to filter here.
                    int totalQty = order.OrderDetails.Sum(d => d.Quantity);
                    if (totalQty > 0)
                        order.ProductionTargetDate = ProductionEstimator.EstimateTargetDate(DateTime.Today, totalQty);
                }

                await _context.SaveChangesAsync();

                // Verify the latest pending Payment row for this order —
                // real audit trail of who approved it and when.
                var pendingPayment = await _context.Payments
                    .Where(p => p.OrderID == order.OrderID && p.PaymentStatus == "Waiting for Verification")
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync();
                if (pendingPayment != null)
                {
                    pendingPayment.PaymentStatus = "Verified";
                    pendingPayment.VerifiedByName = HttpContext.Session.GetString("AdminName");
                    pendingPayment.VerifiedAt = DateTime.Now;
                    pendingPayment.Remarks = note;
                    if (order.CashAmountReceived.HasValue)
                        pendingPayment.CashAmountReceived = order.CashAmountReceived;
                    await _context.SaveChangesAsync();
                }

                string method = order.PaymentMethod == "Cash" ? "Confirmed Cash Payment" : "Approved Gcash Payment";
                await ActivityLogger.LogAsync(_context, HttpContext, "Billing", method,
                    $"₱{order.DownPaymentAmount:N2} down payment for {order.CustomerName}.", order.OrderID);
                await NotificationService.NotifyAdminAsync(_context, order.OrderID, "PaymentVerified",
                    "Payment Verified",
                    $"Order #ORD-{order.OrderID:D3} — {order.CustomerName}'s down payment was confirmed.");
                await NotificationService.NotifyCustomerAsync(_context, order.OrderID, "PaymentVerified",
                    "Payment confirmed",
                    $"Your payment for order #ORD-{order.OrderID:D5} was confirmed — your order is now Confirmed.");
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
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id);
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

                string previousStatus = order.Status;
                PaymentWorkflow.ApplyPaymentStatus(order, "Rejected", note);
                if (order.Status == "Cancelled" && previousStatus != "Cancelled")
                    await StockService.RestoreStockAsync(_context, order);
                await _context.SaveChangesAsync();

                // Reject the latest pending Payment row for this order —
                // same audit trail as approval.
                var pendingPayment = await _context.Payments
                    .Where(p => p.OrderID == order.OrderID && p.PaymentStatus == "Waiting for Verification")
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync();
                if (pendingPayment != null)
                {
                    pendingPayment.PaymentStatus = "Rejected";
                    pendingPayment.VerifiedByName = HttpContext.Session.GetString("AdminName");
                    pendingPayment.VerifiedAt = DateTime.Now;
                    pendingPayment.Remarks = note;
                    await _context.SaveChangesAsync();
                }

                await ActivityLogger.LogAsync(_context, HttpContext, "Billing", "Rejected Payment",
                    string.IsNullOrWhiteSpace(note) ? $"For {order.CustomerName}." : $"For {order.CustomerName} — {note}", order.OrderID);
                await NotificationService.NotifyAdminAsync(_context, order.OrderID, "PaymentRejected",
                    "Payment Rejected",
                    $"Order #ORD-{order.OrderID:D3} — {order.CustomerName}'s payment was rejected.");
                await NotificationService.NotifyCustomerAsync(_context, order.OrderID, "PaymentRejected",
                    "Payment rejected",
                    $"Your payment for order #ORD-{order.OrderID:D5} was rejected." + (string.IsNullOrWhiteSpace(note) ? "" : $" Reason: {note}"));
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

                // The balance settlement is its own payment event — a
                // second row in the audit trail, verified immediately
                // since admin is the one recording it.
                _context.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    PaymentType = "Balance Settlement",
                    Amount = settledAmount,
                    PaymentMethod = order.PaymentMethod,
                    PaymentStatus = "Verified",
                    PaymentDate = DateTime.Now,
                    VerifiedByName = HttpContext.Session.GetString("AdminName"),
                    VerifiedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                await ActivityLogger.LogAsync(_context, HttpContext, "Billing", "Marked Fully Paid",
                    $"₱{settledAmount:N2} remaining balance settled for {order.CustomerName}.", order.OrderID);
                await NotificationService.NotifyAdminAsync(_context, order.OrderID, "FullyPaid",
                    "Order Fully Paid",
                    $"Order #ORD-{order.OrderID:D3} — {order.CustomerName} settled the remaining ₱{settledAmount:N2}.");
                await NotificationService.NotifyCustomerAsync(_context, order.OrderID, "FullyPaid",
                    "Payment complete",
                    $"Order #ORD-{order.OrderID:D5} is now fully paid. Thank you!");
                TempData["Success"] = $"Order #ORD-{order.OrderID:D3} marked as Fully Paid.";
            }
            return RedirectToAction(nameof(bIndex));
        }
    }
}
