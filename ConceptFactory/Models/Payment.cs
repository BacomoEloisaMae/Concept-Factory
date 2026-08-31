using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // A real, append-only audit trail of every payment event on an order —
    // the down payment, any rejected/resubmitted attempt, and the later
    // balance settlement each get their own row here. This sits ALONGSIDE
    // Order's own PaymentStatus/DownPaymentAmount/RemainingBalance/
    // ReferenceNumber/ProofFilePath fields, which stay exactly as they are
    // and keep driving every existing screen (Billing, Order Management,
    // customer Track Order) — nothing that already reads from Order
    // changes. This table only adds the "who verified what, and when"
    // history that Order's single-row-per-order shape can't hold once
    // there's more than one payment event.
    [Table("Payments")]
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }

        [Required]
        public int OrderID { get; set; }

        // "Down Payment" | "Balance Settlement"
        [StringLength(30)]
        public string PaymentType { get; set; } = "Down Payment";

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [StringLength(30)]
        public string? PaymentMethod { get; set; } // "Gcash" | "Cash"

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        public string? ProofFilePath { get; set; }

        // Cash-only: physical amount handed over for THIS payment event.
        [Column(TypeName = "decimal(10,2)")]
        public decimal? CashAmountReceived { get; set; }

        [StringLength(30)]
        public string PaymentStatus { get; set; } = "Waiting for Verification";
        // "Waiting for Verification" | "Verified" | "Rejected"

        // When this payment attempt was submitted (checkout, or a
        // hReuploadProof resubmission).
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // Snapshot of whoever approved/rejected it — same pattern as
        // ActivityLog.AdminName, since a single FK can't cleanly point at
        // both AdminUsers and Users. Null until an admin acts on it.
        [StringLength(200)]
        public string? VerifiedByName { get; set; }

        public DateTime? VerifiedAt { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        public virtual Order? Order { get; set; }
    }
}
