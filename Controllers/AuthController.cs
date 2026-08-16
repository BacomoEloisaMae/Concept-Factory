using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Utils;

namespace ConceptFactory.Controllers
{
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
            // Already logged in → go to dashboard
            if (HttpContext.Session.GetString("AdminName") != null)
                return RedirectToAction("pIndex", "Products");

            return View();
        }

        // POST: /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            var admin = await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.Email == Email && a.Password == Password);

            if (admin == null)
            {
                // No session admin to attribute this to — log the attempted
                // email itself so a string of failed logins is traceable.
                await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Failed Login Attempt", $"Attempted email: {Email}");
                TempData["Error"] = "Invalid email or password.";
                return RedirectToAction("Login");
            }

            HttpContext.Session.SetInt32("AdminID", admin.AdminID);
            HttpContext.Session.SetString("AdminName", admin.FullName);
            await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Logged In");

            return RedirectToAction("pIndex", "Products");
        }

        // GET: /Auth/Logout
        public async Task<IActionResult> Logout()
        {
            string? adminName = HttpContext.Session.GetString("AdminName");
            HttpContext.Session.Clear();
            // Session is already gone, so pass the name explicitly rather
            // than relying on ActivityLogger's default session lookup.
            await ActivityLogger.LogAsync(_context, HttpContext, "Auth", "Logged Out", adminNameOverride: adminName);
            return RedirectToAction("Login");
        }
    }
}