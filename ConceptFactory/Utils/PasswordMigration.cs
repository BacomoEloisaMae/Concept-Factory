using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;

namespace ConceptFactory.Utils
{
    // One-time, idempotent upgrade run at app startup (see Program.cs):
    // hashes any AdminUsers/Users row whose Password column is still the
    // plaintext value Setup.sql seeds (e.g. the default admin's
    // "admin123", or a staff row's random placeholder from before staff
    // logins existed). Safe to run on every boot — a row already hashed
    // (starts with "PBKDF2$") is left untouched, so this never re-hashes
    // an already-hashed password and never needs its own "have I run
    // before" flag.
    //
    // This is what lets the seeded admin keep logging in with the exact
    // same email/password from Database/Setup.sql while the column itself
    // now only ever holds a hash.
    public static class PasswordMigration
    {
        public static async Task EnsureHashedAsync(ApplicationDbContext context)
        {
            var admins = await context.AdminUsers
                .Where(a => !a.Password.StartsWith("PBKDF2$"))
                .ToListAsync();
            foreach (var admin in admins)
                admin.Password = PasswordHasher.Hash(admin.Password);

            var staff = await context.Users
                .Where(u => !u.Password.StartsWith("PBKDF2$"))
                .ToListAsync();
            foreach (var user in staff)
                user.Password = PasswordHasher.Hash(user.Password);

            if (admins.Count > 0 || staff.Count > 0)
                await context.SaveChangesAsync();
        }
    }
}
