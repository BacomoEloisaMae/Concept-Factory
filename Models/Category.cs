using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    [Table("Categories")]
    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        // Null = a top-level category (e.g. "Jackets", "Tote Bags"). Set =
        // this is a specific sub-type nested under that parent (e.g.
        // "Varsity Jackets" under "Jackets"). Lets the storefront show
        // "Jackets" as one browsable category containing every jacket
        // sub-type, while still letting a shopper (or the admin, when
        // adding a product) drill down to the exact sub-type.
        public int? ParentCategoryID { get; set; }

        [ForeignKey("ParentCategoryID")]
        public virtual Category? ParentCategory { get; set; }

        public virtual ICollection<Category> ChildCategories { get; set; } = new List<Category>();

        // Navigation
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
