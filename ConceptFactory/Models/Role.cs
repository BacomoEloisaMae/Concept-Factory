using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConceptFactory.Models
{
    // Maps the [dbo].[Roles] lookup table (seeded in Database/Setup.sql
    // with 'Admin', 'Customer', 'Staff'). Only "Staff" is actually used by
    // the app right now — see User.cs and UsersController for why.
    [Table("Roles")]
    public class Role
    {
        [Key]
        public int RoleID { get; set; }

        [Required]
        [StringLength(50)]
        public string RoleName { get; set; } = string.Empty;
    }
}
