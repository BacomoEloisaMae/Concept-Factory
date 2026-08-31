using System.Linq;
using System.Text.RegularExpressions;

namespace ConceptFactory.Utils
{
    // Shared password strength rule for every customer-facing password
    // entry point (Register, ResetPassword): at least 8 characters, at
    // least one uppercase letter, one lowercase letter, one digit, and no
    // special characters at all (letters + digits only). Kept in one
    // place so AccountController's Register and ResetPassword actions
    // can't drift out of sync with each other or with the client-side
    // checklist in wwwroot/js/auth.js.
    public static class PasswordPolicy
    {
        public const string RequirementsText =
            "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, and a number. Special characters are not allowed.";

        // Letters and digits only, 8+ characters, at least one of each
        // required case + a digit.
        private static readonly Regex AllowedCharsOnly = new(@"^[A-Za-z0-9]+$", RegexOptions.Compiled);

        public static bool IsValid(string? password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (password.Length < 8) return false;
            if (!AllowedCharsOnly.IsMatch(password)) return false; // blocks any special character
            if (!password.Any(char.IsUpper)) return false;
            if (!password.Any(char.IsLower)) return false;
            if (!password.Any(char.IsDigit)) return false;
            return true;
        }
    }
}
