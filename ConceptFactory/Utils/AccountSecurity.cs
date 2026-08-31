namespace ConceptFactory.Utils
{
    // Shared login-lockout policy for every account type (AdminUsers,
    // Users/Staff, Customers): 3 wrong passwords in a row locks the
    // account out for 10 seconds. Each entity keeps its own
    // FailedLoginAttempts/LockoutEnd pair; this class just holds the rule
    // so it can't drift between AuthController and AccountController.
    public static class AccountSecurity
    {
        public const int MaxFailedAttempts = 3;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(10);

        // Null = not currently locked out. Otherwise, how many whole
        // seconds are left — used for the "try again in N seconds" message.
        public static int? GetLockoutSecondsRemaining(DateTime? lockoutEnd)
        {
            if (lockoutEnd == null || lockoutEnd.Value <= DateTime.Now) return null;
            return (int)Math.Ceiling((lockoutEnd.Value - DateTime.Now).TotalSeconds);
        }

        // Called after a wrong password. Returns the new
        // (attempts, lockoutEnd) pair to save back onto the entity. Once
        // the 3rd wrong attempt lands, the counter resets to 0 and
        // LockoutEnd is set 10 seconds out — so the next attempt (even a
        // correct one) is blocked until the timer elapses.
        public static (int Attempts, DateTime? LockoutEnd) RegisterFailedAttempt(int currentAttempts, DateTime? currentLockoutEnd)
        {
            int attempts = currentAttempts + 1;
            DateTime? lockoutEnd = currentLockoutEnd;

            if (attempts >= MaxFailedAttempts)
            {
                lockoutEnd = DateTime.Now.Add(LockoutDuration);
                attempts = 0;
            }

            return (attempts, lockoutEnd);
        }
    }
}
