using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // Maps the [dbo].[Users] table (seeded in Database/Setup.sql alongside
    // Roles). Only used for Staff accounts (Sales/Production team, via
    // UsersController's CRUD) — real admin-panel logins for the single
    // Admin account still go through the separate AdminUsers table, and
    // customers now have their own Customers table/login (see
    // Models/Customer.cs). Staff rows created here DO log in with the
    // Email/Password set on this row — see AuthController.Login, which
    // falls back to this table when the email doesn't match AdminUsers.
    [Table("Users")]
    public class User
    {
        [Key]
        public int UserID { get; set; }

        [Required]
        public int RoleID { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        // Stored as a PBKDF2 hash (see Utils/PasswordHasher.cs) once set
        // via UsersController.uCreate/uEdit's password field.
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        // ── Login security (CIA — account lockout) ──────────────────
        // 3 wrong passwords in a row locks the account for 10 seconds;
        // see Utils/AccountSecurity.cs.
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }

        [StringLength(20)]
        [Display(Name = "Contact Number")]
        public string? ContactNum { get; set; }

        // "Sales" or "Production" — only meaningful when RoleID is Staff.
        [StringLength(50)]
        [Display(Name = "Team")]
        public string? Department { get; set; }

        // Which of the 6 production stations (see Utils/ProductionStations.cs)
        // this staff member is assigned to — only meaningful when
        // Department == "Production". Drives the "My Station" sidebar
        // restriction (ProductionController/_AdminLayout) and which
        // production notifications this account is shown (NotificationsController).
        [Display(Name = "Station")]
        public int? StationIndex { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        [Display(Name = "Date Added")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("RoleID")]
        public virtual Role? Role { get; set; }
    }
}
