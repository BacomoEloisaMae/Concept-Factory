namespace ConceptFactory.Models
{
    // Centralizes what happens to an Order's overall Status whenever its
    // PaymentStatus changes. Used by BOTH OrdersController (Manage Order's
    // payment dropdown) and BillingController (Approve/Reject/Mark Fully
    // Paid), so the two admin screens can never drift out of sync with
    // each other — there is exactly one place this logic lives.
    public static class PaymentWorkflow
    {
        public static void ApplyPaymentStatus(Order order, string newPaymentStatus, string? note = null)
        {
            order.PaymentStatus = newPaymentStatus;
            if (note != null)
                order.PaymentNote = string.IsNullOrWhiteSpace(note) ? null : note;

            // Only auto-advance the order's own lifecycle while it's still
            // sitting in one of the pre-confirmation "Pending ..." states.
            // Once staff has manually moved it into Design Review/In
            // Production/Ready for Pickup/Completed, payment changes no
            // longer touch Status — a later "Fully Paid" mark-up, for
            // example, shouldn't reset an order that's already Completed.
            bool stillAwaitingConfirmation =
                order.Status == "Pending Down Payment" ||
                order.Status == "Pending Cash Payment" ||
                order.Status == "Pending Payment Verification";

            if (newPaymentStatus == "Partially Paid" && stillAwaitingConfirmation)
            {
                order.Status = "Confirmed";
                order.ProductionStage = -1;
            }
            else if (newPaymentStatus == "Rejected" && stillAwaitingConfirmation)
                // There's no separate "Rejected" order status — a rejected
                // payment simply means the order doesn't proceed.
                order.Status = "Cancelled";
            // "Fully Paid" and "Waiting for Verification" never change Status.
        }

        // Marking an order "Fully Paid" means the remaining balance came
        // in, so the ledger fields should reflect that too — not just the
        // status label.
        public static void MarkFullyPaid(Order order)
        {
            order.PaymentStatus = "Fully Paid";
            order.DownPaymentAmount = order.TotalAmount;
            order.RemainingBalance = 0;
        }
    }
}
