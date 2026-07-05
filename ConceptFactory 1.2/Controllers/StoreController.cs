using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;

namespace ConceptFactory.Controllers
{
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StoreController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: / (Storefront landing page with catalog preview)
        public async Task<IActionResult> Index()
        {
            var featuredItems = await _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .OrderByDescending(p => p.DateAdded)
                .Take(8)
                .ToListAsync();

            return View(featuredItems);
        }

        // GET: /Store/Browse (Full catalog page)
        public async Task<IActionResult> Browse(string? search, int? categoryId)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);

            var items = await query.OrderByDescending(p => p.DateAdded).ToListAsync();

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            return View(items);
        }

        // GET: /Store/Details/5 (Single item detail / "customize" page)
        public async Task<IActionResult> Details(int id)
        {
            var item = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductID == id && !p.IsDeleted);

            if (item == null) return NotFound();

            return View(item);
        }
    }
}
