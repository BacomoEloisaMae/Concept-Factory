using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // Server-side mirror of a wishlist entry, tied to a Customer account —
    // same idea as CartItem. Same whole-list replace semantics as
    // saveWishlist() in storefront.js.
    [Table("WishlistItems")]
    public class WishlistItem
    {
        [Key]
        public int WishlistItemID { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [Required]
        public int ProductID { get; set; }

        [StringLength(200)]
        public string? Name { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [StringLength(500)]
        public string? Image { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Customer? Customer { get; set; }
    }
}
