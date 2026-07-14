using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;

namespace ConceptFactory.Controllers
{
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StoreController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Store/Index (Landing page)
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

        // GET: /Store/Browse (Full catalog)
        public async Task<IActionResult> Browse(string? search, int? categoryId, string? sort)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search) ||
                                        (p.ProductDescription != null && p.ProductDescription.Contains(search)));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);

            query = sort switch
            {
                "price_asc"  => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name"       => query.OrderBy(p => p.ProductName),
                _            => query.OrderByDescending(p => p.DateAdded)
            };

            var items = await query.ToListAsync();

            ViewBag.Categories  = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.Search      = search;
            ViewBag.CategoryId  = categoryId;
            ViewBag.Sort        = sort;
            ViewBag.TotalCount  = items.Count;

            return View(items);
        }

        // GET: /Store/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var item = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductID == id && !p.IsDeleted);

            if (item == null) return NotFound();

            ViewBag.Services = await _context.Services
                .Where(s => !s.IsDeleted && s.Status == "Active")
                .ToListAsync();

            return View(item);
        }
    }
}
