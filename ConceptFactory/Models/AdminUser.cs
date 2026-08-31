using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    [Table("AdminUsers")]
    public class AdminUser
    {
        [Key]
        public int AdminID { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [StringLength(30)]
        public string? Phone { get; set; }

        // Stored as a PBKDF2 hash (see Utils/PasswordHasher.cs) — never
        // plaintext, even though the seeded login keeps its original
        // "admin123" value from the admin's point of view (Utils/
        // PasswordMigration.cs hashes it in place at app startup).
        [Required]
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        // ── Login security (CIA — account lockout) ──────────────────
        // 3 wrong passwords in a row locks the account for 10 seconds;
        // see Utils/AccountSecurity.cs.
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
