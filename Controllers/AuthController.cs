using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;

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
                return RedirectToAction("pIndex", "Home");

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
                TempData["Error"] = "Invalid email or password.";
                return RedirectToAction("Login");
            }

            HttpContext.Session.SetInt32("AdminID", admin.AdminID);
            HttpContext.Session.SetString("AdminName", admin.FullName);

            return RedirectToAction("pIndex", "Home");
        }

        // GET: /Auth/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
