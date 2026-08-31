using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // A real, append-only history of every production-stage transition an
    // order goes through — one row per stage change. This sits ALONGSIDE
    // Order's own ProductionStage/ProductionStageUpdatedAt/ProductionRemarks
    // fields (which remain the "current state" every existing Production
    // screen reads from — see ProductionWorkflow.SetStage) and simply adds
    // the timeline those single "current stage" columns can't hold on
    // their own: when did Cutting finish, when did Printing finish, etc.
    [Table("ProductionTracking")]
    public class ProductionTracking
    {
        [Key]
        public int ProductionTrackingID { get; set; }

        [Required]
        public int OrderID { get; set; }

        // Index -1=Waiting for Production, 0=Cutting, 1=Printing, 2=Sewing,
        // 3=Trimming, 4=Quality Check, 5=Ready for Pickup, 6=Completed —
        // matches ProductionWorkflow's stage indices.
        public int ProductionStage { get; set; }

        [StringLength(50)]
        public string? StageName { get; set; }

        [StringLength(30)]
        public string StageStatus { get; set; } = "Reached";
        // "Reached" | "Completed" (Completed only used for the final pickup row)

        // Snapshot of whoever advanced the stage — same pattern as
        // ActivityLog.AdminName / Payment.VerifiedByName.
        [StringLength(200)]
        public string? UpdatedByName { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Remarks { get; set; }

        public virtual Order? Order { get; set; }
    }
}
