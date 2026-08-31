using ConceptFactory.Data;
using ConceptFactory.Models;

namespace ConceptFactory.Utils
{
    // Centralizes writing to Notifications, the same way ActivityLogger
    // centralizes ActivityLogs — one call at the point of change instead
    // of each controller building its own Notification row. Called from
    // the exact same spots ActivityLogger already is: HomeController
    // (order placed), BillingController (verified/rejected/fully paid),
    // and ProductionController (each stage change, completed/picked up).
    public static class NotificationService
    {
        // stationIndex (0-5, see Utils/ProductionStations.cs) is only
        // passed by ProductionController for the production stage-change
        // types — everything else (OrderPlaced, PaymentVerified, etc.)
        // leaves it null. NotificationsController uses it to show
        // Production staff only the notifications for their own station.
        public static async Task NotifyAdminAsync(
            ApplicationDbContext context,
            int orderId,
            string type,
            string title,
            string message,
            int? stationIndex = null)
        {
            context.Notifications.Add(new Notification
            {
                OrderID = orderId,
                Audience = "Admin",
                Type = type,
                Title = title,
                Message = message,
                StationIndex = stationIndex,
                CreatedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
        }

        // Matched to a customer the same way Track Order is (see
        // HomeController.hTrackOrder / MyNotificationsQuery) — by the
        // order's CustomerID against the logged-in account. No matching
        // needed here at write time; the row is just tagged Customer-
        // audience and OrderID, and HomeController.hNotifications does
        // the matching at read time exactly like the Track Order list does.
        public static async Task NotifyCustomerAsync(
            ApplicationDbContext context,
            int orderId,
            string type,
            string title,
            string message)
        {
            context.Notifications.Add(new Notification
            {
                OrderID = orderId,
                Audience = "Customer",
                Type = type,
                Title = title,
                Message = message,
                CreatedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }
}