using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // One row per color variant of a Product. Lets the admin attach a
    // specific photo to each color swatch, so the storefront can swap the
    // main product image when the customer picks a different color.
    [Table("ProductColorImages")]
    public class ProductColorImage
    {
        [Key]
        public int ProductColorImageID { get; set; }

        [Required]
        public int ProductID { get; set; }

        // Hex code, e.g. "#e53e3e" — matches the values already stored in
        // Product.Color, so existing swatch-rendering code keeps working.
        [Required]
        [StringLength(20)]
        public string ColorHex { get; set; } = string.Empty;

        // Optional friendly label (e.g. "Red"). Not required for the
        // color-swap feature to work, but nice to show as a tooltip.
        [StringLength(100)]
        public string? ColorName { get; set; }

        // Photo of the product in this specific color. Nullable — a color
        // can exist without a dedicated photo yet; the storefront just
        // keeps showing the product's default image for that color.
        [StringLength(500)]
        public string? ImagePath { get; set; }

        public int DisplayOrder { get; set; } = 0;

        [ForeignKey("ProductID")]
        public virtual Product? Product { get; set; }
    }
}
