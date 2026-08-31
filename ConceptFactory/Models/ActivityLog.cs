using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // A single audit-trail entry. Written by ActivityLogger (Utils/) from
    // whichever controller action just made a change — Billing approvals/
    // rejections, production stage changes, order edits/cancellations,
    // product/service CRUD, and admin sign-in/out. Read-only from the UI
    // (Views/Logs) — nothing in the app ever edits or deletes a row here,
    // since the whole point is an untampered record of who did what, when.
    [Table("ActivityLogs")]
    public class ActivityLog
    {
        [Key]
        public int LogID { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Null for actions where no admin is signed in yet (a failed login
        // attempt) — the attempted email/identifier goes in Details instead.
        [StringLength(100)]
        public string? AdminName { get; set; }

        // Which admin area the action happened in — "Auth", "Billing",
        // "Orders", "Production", "Products", "Services" — drives the
        // filter dropdown and badge color on the Logs page.
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        // Short verb phrase, e.g. "Approved Payment", "Cancelled Order",
        // "Advanced Production Stage".
        [StringLength(200)]
        public string Action { get; set; } = string.Empty;

        // Free-text specifics — which order/product/service, old vs new
        // values, amounts, notes — whatever makes the entry meaningful on
        // its own without needing to cross-reference other tables.
        [StringLength(500)]
        public string? Details { get; set; }

        // Optional link back to the order the action concerned, so the
        // Logs page can filter/search by order number the same way
        // Billing/Orders/Production already do.
        public int? OrderID { get; set; }
    }
}
