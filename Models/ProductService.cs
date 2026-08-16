using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    /// <summary>
    /// Junction table — which printing services are available for a product.
    /// If you want ALL services available for every product, this is optional.
    /// </summary>
    [Table("ProductServices")]
    public class ProductService
    {
        [Key]
        public int ProductServiceID { get; set; }

        [Required]
        public int ProductID { get; set; }

        [Required]
        public int ServiceID { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product? Product { get; set; }

        [ForeignKey("ServiceID")]
        public virtual Service? Service { get; set; }
    }
}
