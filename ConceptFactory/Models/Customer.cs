using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // Real customer accounts (register/login), separate from AdminUsers
    // (admin panel) and Users (Sales/Production staff). A customer must
    // have one of these to check out — see CustomerAuthFilter and
    // HomeController.hBilling/SubmitOrder. Browsing the storefront itself
    // never requires this.
    [Table("Customers")]
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        // Required — used for both login and the notification/recovery
        // emails (order updates, password reset codes).
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        // ── Login security (CIA — account lockout) ──────────────────
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }

        // ── Password recovery (Gmail-sent code) ──────────────────────
        [StringLength(10)]
        public string? ResetCode { get; set; }
        public DateTime? ResetCodeExpiry { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
