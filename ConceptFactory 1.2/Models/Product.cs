using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    [Table("Products")]
    public class Product
    {
        [Key]
        public int ProductID { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        [StringLength(200)]
        [Display(Name = "Item Name")]
        public string ProductName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        [Display(Name = "Category")]
        public int CategoryID { get; set; }

        [Display(Name = "Description")]
        public string? ProductDescription { get; set; }

        [StringLength(200)]
        [Display(Name = "Size")]
        public string? Size { get; set; }

        [StringLength(500)]
        [Display(Name = "Color")]
        public string? Color { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, 999999.99)]
        [Display(Name = "Price (₱)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Stock quantity is required.")]
        [Range(0, int.MaxValue)]
        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [StringLength(500)]
        [Display(Name = "Item Image")]
        public string? ImagePath { get; set; }

        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        [ForeignKey("CategoryID")]
        public virtual Category? Category { get; set; }
    }
}
