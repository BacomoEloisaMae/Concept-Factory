using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;

namespace ConceptFactory.Controllers
{
    // Staff/Admin login only — Admin (AdminUsers table, seeded credentials
    // unchanged) and Sales/Production staff (Users table, RoleID = Staff,
    // created from the admin panel via UsersController). Customer login/
    // register lives separately in AccountController, since it's a
    // completely different audience with its own layout/flow.
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Auth/Login
        [HttpGet]
        public IActionResult Login()
        {
            // Already logged in → go to whatever's theirs.
            string? staffRole = HttpContext.Session.GetString("StaffRole");
            if (staffRole != null)
            {
                return staffRole switch
                {
                    "Production" => RedirectToAction("pStation", "Production", new { stage = HttpContext.Session.GetInt32("StaffStationIndex") ?? 0 }),
                    "Sales" => RedirectToAction("oIndex", "Orders"),
                    _ => RedirectToAction("pIndex", "Home")
                };
            }

            return View();
        }

        // POST: /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                TempData["Error"] = "Please enter your email and password.";
                return RedirectToAction("Login");
            }
            Email = Email.Trim();

            var admin = await _context.AdminUsers.FirstOrDefaultAsync(a => a.Email == Email);
            if (admin != null)
                return await TryLoginAdminAsync(admin, Password);

            int staffRoleId = (await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleName == "Staff"))?.RoleID ?? -1;
            var staff = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email && u.RoleID == staffRoleId && !u.IsDeleted);
            if (staff != null)
                return await TryLoginStaffAsync(staff, Password);

            // No account at all with this email — no session to attribute
            // this to, so log the attempted email itself.
            await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Failed Login Attempt", $"Attempted email: {Email}");
            TempData["Error"] = "Invalid email or password.";
            return RedirectToAction("Login");
        }

        private async Task<IActionResult> TryLoginAdminAsync(AdminUser admin, string password)
        {
            int? locked = AccountSecurity.GetLockoutSecondsRemaining(admin.LockoutEnd);
            if (locked.HasValue)
            {
                TempData["Error"] = $"Too many failed attempts. Please try again in {locked} second(s).";
                return RedirectToAction("Login");
            }

            if (!PasswordHasher.Verify(password, admin.Password))
            {
                var (attempts, lockoutEnd) = AccountSecurity.RegisterFailedAttempt(admin.FailedLoginAttempts, admin.LockoutEnd);
                admin.FailedLoginAttempts = attempts;
                admin.LockoutEnd = lockoutEnd;
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Failed Login Attempt", $"Attempted email: {admin.Email}");

                TempData["Error"] = lockoutEnd.HasValue
                    ? "Too many failed attempts. This account is locked for 10 seconds."
                    : "Invalid email or password.";
                return RedirectToAction("Login");
            }

            admin.FailedLoginAttempts = 0;
            admin.LockoutEnd = null;
            await _context.SaveChangesAsync();

            HttpContext.Session.Clear();
            HttpContext.Session.SetInt32("AdminID", admin.AdminID);
            HttpContext.Session.SetString("AdminName", admin.FullName);
            HttpContext.Session.SetString("StaffRole", "Admin");
            await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Logged In");

            return RedirectToAction("pIndex", "Home");
        }

        private async Task<IActionResult> TryLoginStaffAsync(User staff, string password)
        {
            if (!string.Equals(staff.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "This staff account is inactive. Please contact your administrator.";
                return RedirectToAction("Login");
            }

            int? locked = AccountSecurity.GetLockoutSecondsRemaining(staff.LockoutEnd);
            if (locked.HasValue)
            {
                TempData["Error"] = $"Too many failed attempts. Please try again in {locked} second(s).";
                return RedirectToAction("Login");
            }

            if (!PasswordHasher.Verify(password, staff.Password))
            {
                var (attempts, lockoutEnd) = AccountSecurity.RegisterFailedAttempt(staff.FailedLoginAttempts, staff.LockoutEnd);
                staff.FailedLoginAttempts = attempts;
                staff.LockoutEnd = lockoutEnd;
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Failed Login Attempt", $"Attempted email: {staff.Email}");

                TempData["Error"] = lockoutEnd.HasValue
                    ? "Too many failed attempts. This account is locked for 10 seconds."
                    : "Invalid email or password.";
                return RedirectToAction("Login");
            }

            staff.FailedLoginAttempts = 0;
            staff.LockoutEnd = null;
            await _context.SaveChangesAsync();

            string role = string.Equals(staff.Department, "Production", StringComparison.OrdinalIgnoreCase) ? "Production" : "Sales";

            HttpContext.Session.Clear();
            HttpContext.Session.SetInt32("StaffID", staff.UserID);
            HttpContext.Session.SetString("AdminName", $"{staff.FirstName} {staff.LastName}");
            HttpContext.Session.SetString("StaffRole", role);
            if (role == "Production")
            {
                int stationIndex = staff.StationIndex ?? 0;
                HttpContext.Session.SetInt32("StaffStationIndex", stationIndex);
                HttpContext.Session.SetString("StaffStationName", ConceptFactory.Utils.ProductionStations.NameFor(stationIndex) ?? "Cutting");
            }
            await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Logged In");

            return role == "Production"
                ? RedirectToAction("pStation", "Production", new { stage = staff.StationIndex ?? 0 })
                : RedirectToAction("oIndex", "Orders");
        }

        // GET: /Auth/Logout
        public async Task<IActionResult> Logout()
        {
            string? staffName = HttpContext.Session.GetString("AdminName");
            HttpContext.Session.Clear();
            // Session is already gone, so pass the name explicitly rather
            // than relying on ActivityLogger's default session lookup.
            await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Logged Out", adminNameOverride: staffName);
            return RedirectToAction("Login");
        }
    }
}
