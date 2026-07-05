using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;

namespace ConceptFactory.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Public homepage — shows product catalog from DB
        public async Task<IActionResult> hIndex(int? categoryId, string? search)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            var products = await query.OrderByDescending(p => p.DateAdded).ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;

            return View(products);
        }

        // Product detail page
        public async Task<IActionResult> hDetails(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductID == id && !p.IsDeleted);

            if (product == null) return NotFound();

            return View(product);
        }

        // Admin dashboard
        public IActionResult pIndex()
        {
            return View();
        }
    }
}
