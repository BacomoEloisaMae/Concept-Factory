namespace ConceptFactory.Models
{
    // Single source of truth for how Order.Status and Order.PaymentStatus
    // render as badges (CSS class + icon) across every admin view —
    // Order Management's list, its details page, its popup modal, and
    // Billing. Previously each view repeated its own switch expression,
    // which is exactly how they drifted out of sync before; now there's
    // only one mapping to update when a status is added or renamed.
    public static class OrderStatusDisplay
    {
        public static string BadgeClass(string status) => status switch
        {
            "Pending Down Payment" => "status-pendingdp",
            "Pending Cash Payment" => "status-pendingcash",
            "Pending Payment Verification" => "status-pendingverif",
            "Confirmed" => "status-confirmed",
            "Design Review" => "status-designreview",
            "In Production" => "status-inproduction",
            "Ready for Pickup" => "status-readypickup",
            "Completed" => "status-completed",
            "Cancelled" => "status-cancelled",
            _ => "status-pendingdp"
        };

        public static string BadgeIcon(string status) => status switch
        {
            "Pending Down Payment" => "fa-wallet",
            "Pending Cash Payment" => "fa-money-bill-wave",
            "Pending Payment Verification" => "fa-magnifying-glass",
            "Confirmed" => "fa-thumbs-up",
            "Design Review" => "fa-pen-ruler",
            "In Production" => "fa-gear",
            "Ready for Pickup" => "fa-box-open",
            "Completed" => "fa-circle-check",
            "Cancelled" => "fa-circle-xmark",
            _ => "fa-circle"
        };

        public static string PaymentBadgeClass(string status) => status switch
        {
            "Partially Paid" => "status-approved",
            "Fully Paid" => "status-fullypaid",
            "Rejected" => "status-rejected",
            _ => "status-waiting"
        };
        public static string PaymentBadgeIcon(string status) => status switch
        {
            "Partially Paid" => "fa-circle-half-stroke",
            "Fully Paid" => "fa-circle-check",
            "Rejected" => "fa-circle-xmark",
            _ => "fa-hourglass-half"
        };
        public static string BillingBadgeClass(string status) => status switch
        {
            "Partially Paid" => "status-approved",
            "Fully Paid" => "status-fullypaid",     
            "Waiting for Verification" => "status-waiting",
            "Rejected" => "status-rejected",
            _ => "status-waiting"
        };
        public static string BillingBadgeIcon(string status) => status switch
        {
            "Partially Paid" => "fa-circle-check",
            "Fully Paid" => "fa-circle-check",
            "Waiting for Verification" => "fa-clock",
            "Rejected" => "fa-circle-xmark",
            _ => "fa-hourglass-half"
        };


    }
}
