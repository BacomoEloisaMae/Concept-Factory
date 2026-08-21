using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int OrderID { get; set; }

        [Required]
        [StringLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? CustomerEmail { get; set; }

        [StringLength(30)]
        public string? CustomerPhone { get; set; }

        [StringLength(300)]
        public string? CustomerAddress { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        // Overall order/production lifecycle. Every order starts in one of
        // the three "Pending ..." states depending on payment method —
        // production must not begin until the down payment has been
        // verified. Billing's Approve/Reject actions bump this
        // automatically (Pending * → Confirmed/Cancelled) via
        // PaymentWorkflow; everything past Confirmed is set manually by
        // admin from Order Management (Design Review → In Production →
        // Ready for Pickup → Completed), or Cancelled at any point.
        [StringLength(50)]
        public string Status { get; set; } = "Pending Down Payment";
        // "Pending Down Payment"        — order placed, down payment not yet made (Gcash, no proof yet)
        // "Pending Cash Payment"        — Cash selected, not yet paid in-store
        // "Pending Payment Verification"— proof uploaded, awaiting admin review
        // "Confirmed"                   — down payment verified, order accepted
        // "Design Review"                — design being reviewed/prepared before production
        // "In Production"                — production has started
        // "Ready for Pickup"             — production complete, ready for release
        // "Completed"                    — customer received the order
        // "Cancelled"                    — order cancelled

        public string? Notes { get; set; }

        // ── Billing & Payment (Checkout) ─────────────────────────────
        [StringLength(30)]
        public string? PaymentMethod { get; set; } // "Gcash" | "Cash"

        // Tracks how much of the 50% down payment has actually been
        // verified/collected, separately from the order's own lifecycle
        // above. "Partially Paid" = the down payment is confirmed but the
        // remaining balance is still outstanding; "Fully Paid" = the whole
        // order amount has now been collected (admin marks this once the
        // remaining balance comes in).
        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Waiting for Verification";
        // "Waiting for Verification" | "Partially Paid" | "Fully Paid" | "Rejected"

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        public string? ProofFilePath { get; set; }

        // Admin's note left when approving/rejecting a payment from the
        // Billing → Payment Verification screen.
        [StringLength(500)]
        public string? PaymentNote { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal DownPaymentAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal RemainingBalance { get; set; }

        // Cash-only: the physical amount of cash the customer actually
        // handed over, recorded by admin/staff when confirming a Cash
        // payment from Billing → Payment Verification (see
        // BillingController.bApprove). Null for Gcash orders and for Cash
        // orders not yet confirmed. Change given back = this minus
        // DownPaymentAmount; DownPaymentAmount itself is what's actually
        // applied/recorded against the order either way.
        [Column(TypeName = "decimal(10,2)")]
        public decimal? CashAmountReceived { get; set; }

        // Payment ID shown throughout the admin UI (Order Management,
        // Billing) is intentionally just the OrderID re-labelled with a
        // "PAY-" prefix instead of "ORD-" — there's one payment per order
        // (the 50% down payment collected at checkout), so a separate
        // auto-incrementing ID would only invite confusion between the two
        // numbers. This keeps #ORD-00011 and #PAY-00011 always in sync.
        [NotMapped]
        public string PaymentID => "PAY-" + OrderID.ToString("D3");

        // Index into the production pipeline shown on the admin Order
        // Details page: -1=Waiting to Start Production 0=Cutting, 1=Printing, 2=Sewing, 3=Trimming,
        // 4=Quality Check, 5=Ready for Pickup. Everything up to and
        // including this index renders as "done" in the stepper. Sales/
        // Admin (Order Management) can only VIEW this — it's advanced by
        // the separate Production module.
        public int ProductionStage { get; set; } = -1;

        // Remarks left by the station staff member on their most recent
        // "Mark as Done" popup submission (Cutting/Printing/Sewing/
        // Trimming/Quality Check). Optional — overwritten each time a
        // stage completes, so it always reflects the latest station only.
        [StringLength(500)]
        public string? ProductionRemarks { get; set; }

        // Quantity entered so far at the CURRENT station, autosaved as the
        // staff member types (ProductionController.pSaveStationProgress) —
        // separate from actually advancing the stage. This lets progress
        // survive a closed modal/page refresh without requiring "Mark as
        // Done" to be clicked; the order only moves to the next station
        // once that quantity reaches the order total AND "Mark as Done" is
        // submitted. Reset to 0 whenever the stage advances (see
        // ProductionWorkflow.SetStage) since it always reflects whatever
        // station the order is currently sitting at.
        public int ProductionStageQuantityDone { get; set; } = 0;

        // When ProductionStage was last advanced. Powers the "Completed
        // Today" / "Pending from Previous" stat cards on each station's
        // page (ProductionController.pStation) — see ProductionWorkflow.SetStage.
        public DateTime? ProductionStageUpdatedAt { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }

    [Table("OrderDetails")]
    public class OrderDetail
    {
        [Key]
        public int OrderDetailID { get; set; }

        [Required]
        public int OrderID { get; set; }

        // Nullable: custom-order line items (Customize page) don't
        // reference a real Products row, only a Category.
        public int? ProductID { get; set; }

        public int? ServiceID { get; set; }

        // Set for custom orders (Customize page), where there's no real
        // Product row to point to — captures which category the custom
        // order was built from instead.
        public int? CategoryID { get; set; }

        public bool IsCustomOrder { get; set; } = false;

        // Snapshot of the product/service name shown to the customer at
        // the time of ordering, so admin Order Management can render the
        // "Products" column without depending on ProductID being set
        // (custom orders) or the Product row still existing later.
        [StringLength(200)]
        public string? ProductNameSnapshot { get; set; }

        // Free-text snapshot of the printing service(s) chosen at checkout
        // (e.g. "DTF Printing, Embroidery") — a cart line can combine more
        // than one service, so this isn't a single ServiceID lookup.
        [StringLength(300)]
        public string? ServiceNameSnapshot { get; set; }

        // Combined price of whatever service(s) were picked at checkout for
        // this line (a line can combine more than one service, so this isn't
        // a single ServiceID/Services.ServicePrice lookup). UnitPrice already
        // includes this amount; it's stored separately so the admin/customer
        // order screens can show it as its own line item.
        [Column(TypeName = "decimal(10,2)")]
        public decimal? ServicePriceSnapshot { get; set; }

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [StringLength(200)]
        public string? PrintLocation { get; set; }

        public string? DesignNotes { get; set; }

        [StringLength(500)]
        public string? DesignFilePath { get; set; }

        // A flattened snapshot of what the customer saw in their cart
        // (garment + design composited, or the plain product photo) —
        // saved to disk at checkout so the admin Order Details page can
        // show a real thumbnail for the line item.
        [StringLength(500)]
        public string? ImageSnapshotPath { get; set; }

        [StringLength(50)]
        public string? SelectedSize { get; set; }

        [StringLength(50)]
        public string? SelectedColor { get; set; }

        // Per-item autosave progress at whichever station the order
        // currently sits at (Cutting/Printing/Sewing/Trimming/Quality
        // Check) — different color/size lines get cut, printed, etc.
        // separately, so this can't be a single number on the Order.
        // Reset to 0 by ProductionWorkflow.SetStage whenever the order
        // hands off to the next station. See ProductionController.
        // pSaveStationProgress / pCompleteStation.
        public int ProductionQuantityDone { get; set; } = 0;

        [ForeignKey("OrderID")]
        public virtual Order? Order { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product? Product { get; set; }

        [ForeignKey("ServiceID")]
        public virtual Service? Service { get; set; }

        [ForeignKey("CategoryID")]
        public virtual Category? Category { get; set; }
    }
}
