using System.Security.Cryptography;

namespace ConceptFactory.Utils
{
    // Salted PBKDF2 password hashing (SHA-256, 100k iterations) — used for
    // every account type in the system (AdminUsers, Users/Staff, Customers).
    // Storage format: "PBKDF2$<iterations>$<saltBase64>$<hashBase64>".
    // The "PBKDF2$" prefix lets PasswordMigration tell an already-hashed
    // value apart from the plaintext passwords the app shipped with
    // (seeded admin, placeholder staff rows) so it knows what still needs
    // upgrading — see Utils/PasswordMigration.cs.
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2$";
        private const int SaltSize = 16;   // bytes
        private const int KeySize = 32;    // bytes
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool IsHashed(string? stored) => !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix, StringComparison.Ordinal);

        // Constant-time compare against a stored hash. Returns false (never
        // throws) for anything malformed or still-plaintext, so a legacy
        // unmigrated row simply fails to verify rather than crashing login.
        public static bool Verify(string password, string? stored)
        {
            if (!IsHashed(stored)) return false;

            try
            {
                string[] parts = stored!.Substring(Prefix.Length).Split('$');
                if (parts.Length != 3) return false;

                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expectedKey = Convert.FromBase64String(parts[2]);

                byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);
                return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
            }
            catch
            {
                return false;
            }
        }
    }
}
