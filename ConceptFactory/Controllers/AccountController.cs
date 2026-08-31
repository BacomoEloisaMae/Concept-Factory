using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;

namespace ConceptFactory.Controllers
{
    // Customer-facing register/login/forgot-password — separate from
    // AuthController (Admin/Staff). A Customer account is what
    // CustomerAuthFilter checks for on checkout/track-order/notifications;
    // browsing the storefront itself never needs one.
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _environment;

        public AccountController(ApplicationDbContext context, EmailService emailService, IWebHostEnvironment environment)
        {
            _context = context;
            _emailService = emailService;
            _environment = environment;
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register(string? returnUrl)
        {
            if (HttpContext.Session.GetInt32("CustomerID") != null)
                return RedirectToAction("hIndex", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string FirstName, string LastName, string Email, string? Phone,
            string Password, string ConfirmPassword, string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;

            // Re-populate everything except the password fields — those
            // always come back blank so the customer re-types them rather
            // than trusting a value that just failed validation.
            ViewBag.FirstName = FirstName;
            ViewBag.LastName = LastName;
            ViewBag.Email = Email;
            ViewBag.Phone = Phone;

            var fieldErrors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(FirstName))
                fieldErrors["FirstName"] = "First name is required.";
            if (string.IsNullOrWhiteSpace(LastName))
                fieldErrors["LastName"] = "Last name is required.";

            // Email is required — it's how order notifications and
            // password-recovery codes reach the customer.
            if (string.IsNullOrWhiteSpace(Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email))
                fieldErrors["Email"] = "Please enter a valid email address.";
            else if (await _context.Customers.AnyAsync(c => c.Email == Email && !c.IsDeleted))
                fieldErrors["Email"] = "An account with this email already exists. Try logging in instead.";

            if (string.IsNullOrWhiteSpace(Password))
            {
                fieldErrors["Password"] = "Password is required.";
            }
            else if (Password.Length < 8)
            {
                fieldErrors["Password"] = "Password must be at least 8 characters.";
            }
            else if (!Password.Any(char.IsUpper))
            {
                fieldErrors["Password"] = "Password must contain at least one uppercase letter (A-Z).";
            }
            else if (!Password.Any(char.IsLower))
            {
                fieldErrors["Password"] = "Password must contain at least one lowercase letter (a-z).";
            }
            else if (!Password.Any(char.IsDigit))
            {
                fieldErrors["Password"] = "Password must contain at least one number (0-9).";
            }
            else if (Password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                fieldErrors["Password"] = "Password must not contain special characters.";
            }
            else if (Password != ConfirmPassword)
            {
                fieldErrors["ConfirmPassword"] = "Passwords do not match.";
            }
            if (fieldErrors.Count > 0)
            {
                ViewBag.FieldErrors = fieldErrors;
                return View();
            }

            var customer = new Customer
            {
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                Email = Email.Trim(),
                Phone = Phone,
                PasswordHash = PasswordHasher.Hash(Password),
                CreatedAt = DateTime.Now
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            SignInCustomer(customer);

            TempData["Success"] = $"Welcome, {customer.FirstName}! Your account has been created.";
            return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("hIndex", "Home") : LocalRedirect(returnUrl);
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            if (HttpContext.Session.GetInt32("CustomerID") != null)
                return RedirectToAction("hIndex", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string Email, string Password, string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                TempData["Error"] = "Please enter your email and password.";
                return View();
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == Email.Trim() && !c.IsDeleted);
            if (customer == null)
            {
                TempData["Error"] = "Invalid email or password.";
                return View();
            }

            int? locked = AccountSecurity.GetLockoutSecondsRemaining(customer.LockoutEnd);
            if (locked.HasValue)
            {
                TempData["Error"] = $"Too many failed attempts. Please try again in {locked} second(s).";
                return View();
            }

            if (!PasswordHasher.Verify(Password, customer.PasswordHash))
            {
                var (attempts, lockoutEnd) = AccountSecurity.RegisterFailedAttempt(customer.FailedLoginAttempts, customer.LockoutEnd);
                customer.FailedLoginAttempts = attempts;
                customer.LockoutEnd = lockoutEnd;
                await _context.SaveChangesAsync();

                TempData["Error"] = lockoutEnd.HasValue
                    ? "Too many failed attempts. This account is locked for 10 seconds."
                    : "Invalid email or password.";
                return View();
            }

            customer.FailedLoginAttempts = 0;
            customer.LockoutEnd = null;
            await _context.SaveChangesAsync();

            SignInCustomer(customer);

            return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("hIndex", "Home") : LocalRedirect(returnUrl);
        }

        private void SignInCustomer(Customer customer)
        {
            HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
            HttpContext.Session.SetString("CustomerName", $"{customer.FirstName} {customer.LastName}");
            HttpContext.Session.SetString("CustomerEmail", customer.Email);
        }

        // GET: /Account/Profile — customer's own editable info, reached
        // from the "My Account" dropdown (see _Layout.cshtml). Requires a
        // logged-in Customer (same session check the other Account/Home
        // customer pages use).
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            int? customerId = HttpContext.Session.GetInt32("CustomerID");
            if (customerId == null)
                return RedirectToAction("Login", new { returnUrl = "/Account/Profile" });

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId && !c.IsDeleted);
            if (customer == null) return RedirectToAction("Login");

            return View(customer);
        }

        // POST: /Account/Profile — save the editable fields (name, email,
        // phone, address). Password change is intentionally handled by the
        // separate Forgot/Reset Password flow, not here.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string FirstName, string LastName, string Email, string? Phone, string? Address)
        {
            int? customerId = HttpContext.Session.GetInt32("CustomerID");
            if (customerId == null)
                return RedirectToAction("Login", new { returnUrl = "/Account/Profile" });

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId && !c.IsDeleted);
            if (customer == null) return RedirectToAction("Login");

            var fieldErrors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(FirstName))
                fieldErrors["FirstName"] = "First name is required.";
            if (string.IsNullOrWhiteSpace(LastName))
                fieldErrors["LastName"] = "Last name is required.";
            if (string.IsNullOrWhiteSpace(Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email))
                fieldErrors["Email"] = "Please enter a valid email address.";
            else if (await _context.Customers.AnyAsync(c => c.Email == Email.Trim() && c.CustomerID != customerId && !c.IsDeleted))
                fieldErrors["Email"] = "Another account already uses this email.";

            if (fieldErrors.Count > 0)
            {
                ViewBag.FieldErrors = fieldErrors;
                // Reflect what they just typed rather than the stale DB
                // values, so a validation error doesn't wipe their edits.
                customer.FirstName = FirstName;
                customer.LastName = LastName;
                customer.Email = Email;
                customer.Phone = Phone;
                customer.Address = Address;
                return View(customer);
            }

            customer.FirstName = FirstName.Trim();
            customer.LastName = LastName.Trim();
            customer.Email = Email.Trim();
            customer.Phone = Phone;
            customer.Address = Address;
            await _context.SaveChangesAsync();

            // The navbar's "My Account" greeting reads CustomerName straight
            // from session — keep it in sync so a name change shows up
            // immediately without needing to log out/in.
            HttpContext.Session.SetString("CustomerName", $"{customer.FirstName} {customer.LastName}");
            HttpContext.Session.SetString("CustomerEmail", customer.Email);

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        // GET: /Account/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("CustomerID");
            HttpContext.Session.Remove("CustomerName");
            HttpContext.Session.Remove("CustomerEmail");
            return RedirectToAction("hIndex", "Home");
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        // POST: /Account/ForgotPassword — always shows the same generic
        // confirmation whether or not the email is on file, so this can't
        // be used to probe which emails have accounts (confidentiality).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string Email)
        {
            if (!string.IsNullOrWhiteSpace(Email))
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == Email.Trim() && !c.IsDeleted);
                if (customer != null)
                {
                    string code = Random.Shared.Next(0, 1_000_000).ToString("D6");
                    customer.ResetCode = code;
                    customer.ResetCodeExpiry = DateTime.Now.AddMinutes(15);
                    await _context.SaveChangesAsync();

                    bool sent = await _emailService.SendPasswordResetCodeAsync(customer.Email, customer.FirstName, code);

                    // Dev convenience only: if Gmail isn't configured yet,
                    // surface the code on-screen so the reset flow can
                    // still be tested end-to-end locally.
                    if (!sent && _environment.IsDevelopment())
                        TempData["DevResetCode"] = code;
                }
            }

            TempData["Success"] = "If an account exists for that email, we've sent a reset code to it.";
            return RedirectToAction("ResetPassword", new { email = Email });
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string? email)
        {
            ViewBag.Email = email;
            return View();
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string Email, string Code, string NewPassword, string ConfirmPassword)
        {
            ViewBag.Email = Email;

            if (string.IsNullOrWhiteSpace(NewPassword) || !PasswordPolicy.IsValid(NewPassword))
            {
                TempData["Error"] = PasswordPolicy.RequirementsText;
                return View();
            }
            if (NewPassword != ConfirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return View();
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == (Email ?? "").Trim() && !c.IsDeleted);
            if (customer == null || string.IsNullOrEmpty(customer.ResetCode) || customer.ResetCode != (Code ?? "").Trim()
                || customer.ResetCodeExpiry == null || customer.ResetCodeExpiry < DateTime.Now)
            {
                TempData["Error"] = "That code is invalid or has expired. Please request a new one.";
                return View();
            }

            customer.PasswordHash = PasswordHasher.Hash(NewPassword);
            customer.ResetCode = null;
            customer.ResetCodeExpiry = null;
            customer.FailedLoginAttempts = 0;
            customer.LockoutEnd = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your password has been reset. Please log in.";
            return RedirectToAction("Login");
        }
    }
}
