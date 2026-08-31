namespace ConceptFactory.Models
{
    // Single source of truth for how a Notification.Type renders (icon +
    // color) in both the admin Notifications page and the customer bell
    // dropdown — same pattern as OrderStatusDisplay for order/payment
    // badges, kept here so the two bells can never drift out of sync.
    public static class NotificationDisplay
    {
        public static string IconClass(string type) => type switch
        {
            "OrderPlaced" => "fa-cart-shopping",
            "PaymentVerified" => "fa-circle-check",
            "PaymentRejected" => "fa-circle-xmark",
            "FullyPaid" => "fa-hand-holding-dollar",
            "ProductionStarted" => "fa-industry",
            "ProductionAdvanced" => "fa-gear",
            "InProduction" => "fa-gear",
            "ReadyForPickup" => "fa-box-open",
            "Completed" => "fa-circle-check",
            _ => "fa-bell"
        };

        // Bare keyword, not a full class name — the admin feed prefixes
        // it "notif-" and the customer bell dropdown prefixes it
        // "nav-notif-", since those are two separate stylesheets
        // (admin/notifications.css vs navbar.css) with their own scoped
        // class names.
        public static string ColorClass(string type) => type switch
        {
            "OrderPlaced" => "blue",
            "PaymentVerified" => "green",
            "PaymentRejected" => "red",
            "FullyPaid" => "green",
            "ProductionStarted" => "purple",
            "ProductionAdvanced" => "orange",
            "InProduction" => "orange",
            "ReadyForPickup" => "teal",
            "Completed" => "green",
            _ => "gray"
        };
    }
}