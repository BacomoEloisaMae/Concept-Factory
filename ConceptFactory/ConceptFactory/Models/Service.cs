using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    [Table("Services")]
    public class Service
    {
        [Key]
        public int ServiceID { get; set; }

        [Required(ErrorMessage = "Service name is required.")]
        [StringLength(200)]
        [Display(Name = "Service Name")]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? ServiceDescription { get; set; }

        [Required(ErrorMessage = "Service price is required.")]
        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "Price must be greater than 0.")]
        [Display(Name = "Price (₱)")]
        public decimal ServicePrice { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
}
