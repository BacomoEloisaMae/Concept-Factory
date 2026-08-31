using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // A record of every time someone views/generates a report from
    // Reports & Analytics — who ran it, which report type, and what date
    // range they filtered to. Reports itself stays fully live/computed
    // (see ReportsController.rIndex) — nothing about how the numbers are
    // calculated changes. This table only adds the accountability trail
    // ("who looked at this report, and when") that a purely computed page
    // can't answer on its own.
    [Table("ReportLogs")]
    public class ReportLog
    {
        [Key]
        public int ReportLogID { get; set; }

        // "Sales" | "Billing" | "Orders" | "Production"
        [Required]
        [StringLength(30)]
        public string ReportType { get; set; } = string.Empty;

        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        // Snapshot of whoever ran it — same pattern as ActivityLog.AdminName
        // / Payment.VerifiedByName, since a single FK can't cleanly point
        // at both AdminUsers and Users.
        [StringLength(200)]
        public string? GeneratedByName { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }
}
