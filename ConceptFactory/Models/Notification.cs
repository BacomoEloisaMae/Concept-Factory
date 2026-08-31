using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // A single in-app notification, written by NotificationService (Utils/)
    // from the same points in the order lifecycle that already write to
    // ActivityLogs — order placed, billing verified/rejected/fully paid,
    // each production stage change, and completed/picked up. Two rows are
    // typically written per real-world event (one Admin-audience, one
    // Customer-audience), each with its own wording, since the admin and
    // the customer care about different things at the same moment.
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        // Which order this concerns — every notification is order-scoped.
        public int OrderID { get; set; }

        [ForeignKey(nameof(OrderID))]
        public Order? Order { get; set; }

        // "Admin" or "Customer" — which bell this shows up under. Customer
        // notifications are matched to an account the same way Track
        // Order is (via the order's CustomerID — see
        // HomeController.hTrackOrder / MyNotificationsQuery).
        [StringLength(20)]
        public string Audience { get; set; } = "Admin";

        // Drives the icon/color in the dropdown/list — see
        // NotificationDisplay.cs for the single mapping. One of:
        // OrderPlaced, PaymentVerified, PaymentRejected, FullyPaid,
        // ProductionStarted, InProduction, ReadyForPickup, Completed.
        [StringLength(40)]
        public string Type { get; set; } = string.Empty;

        // Which production station (see Utils/ProductionStations.cs, index
        // 0-5) this notification concerns — set only for the production
        // stage-change types (ProductionStarted/ProductionAdvanced/
        // Completed) written from ProductionController. Null for every
        // other type (OrderPlaced, PaymentVerified, etc.). Used by
        // NotificationsController to show Production staff only the
        // notifications for their own assigned station.
        public int? StationIndex { get; set; }

        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [StringLength(300)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;
    }
}