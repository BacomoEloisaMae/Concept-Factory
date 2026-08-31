using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // Server-side mirror of a cart line, tied to a Customer account — see
    // wwwroot/js/storefront.js, which is where the SAME shape lives
    // client-side in localStorage. Guests (not logged in) still work
    // purely off localStorage, same as before; once a Customer is logged
    // in, storefront.js also syncs here via HomeController's
    // hGetCart/hSyncCart, so the cart follows the account across logout/
    // login and across devices instead of being tied to one browser.
    //
    // Whole-cart replace semantics: hSyncCart wipes and re-inserts a
    // Customer's rows every time, mirroring how saveCart() in
    // storefront.js already treats the array as the full source of
    // truth — no per-line diffing needed on either side.
    [Table("CartItems")]
    public class CartItem
    {
        [Key]
        public int CartItemID { get; set; }

        [Required]
        public int CustomerID { get; set; }

        // productId|size|color|service|printLocation — same line-matching
        // key storefront.js computes client-side (see addToCart's lineKey),
        // stored here so a guest cart merging in on login can be matched
        // against existing server lines without re-deriving it server-side.
        [StringLength(300)]
        public string? LineKey { get; set; }

        [Required]
        public int ProductID { get; set; }

        [StringLength(200)]
        public string? Name { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Image { get; set; } // may be a composited data: URL for custom orders

        public int Qty { get; set; } = 1;

        [StringLength(50)]
        public string? Size { get; set; }

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(200)]
        public string? Service { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal ServicePrice { get; set; }

        [StringLength(100)]
        public string? PrintLocation { get; set; }

        public int? StockQuantity { get; set; }

        public bool IsCustomOrder { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? DesignImage { get; set; } // raw uploaded artwork, Customize orders only

        [StringLength(500)]
        public string? LocationNote { get; set; }

        public bool HasFrontDesign { get; set; }
        public bool HasBackDesign { get; set; }
        public int DesignCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public virtual Customer? Customer { get; set; }
    }
}
