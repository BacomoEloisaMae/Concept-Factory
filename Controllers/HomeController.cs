using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Utils;

namespace ConceptFactory.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public HomeController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> hIndex(int? categoryId, string? search)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue)
            {
                var categoryIds = await GetCategoryAndChildIdsAsync(categoryId.Value);
                query = query.Where(p => categoryIds.Contains(p.CategoryID));
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            const int homePageLimit = 15;

            int totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.DateAdded)
                .Take(homePageLimit)
                .ToListAsync();

            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.CategoryThumbs = categories.ToDictionary(
                c => c.CategoryID,
                c => CategoryImageResolver.GetDefaultThumb(_environment, c.CategoryName, "/images/categories/t-shirt.png"));
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;
            ViewBag.TotalCount = totalCount;
            ViewBag.ShowViewAll = totalCount > homePageLimit;

            return View(products);
        }

        // A category "and its children" — e.g. selecting "Jackets" (a parent
        // with no products of its own) should also show every Varsity
        // Jacket, Zip-Up, Full Zip Hoodie, etc. Just one level deep, since
        // that's all the hierarchy currently goes.
        private async Task<List<int>> GetCategoryAndChildIdsAsync(int categoryId)
        {
            var ids = await _context.Categories
                .Where(c => c.CategoryID == categoryId || c.ParentCategoryID == categoryId)
                .Select(c => c.CategoryID)
                .ToListAsync();
            if (!ids.Contains(categoryId)) ids.Add(categoryId);
            return ids;
        }

        // GET: /Home/hProducts — dedicated full catalog page (linked from the "Products" nav item)
        public async Task<IActionResult> hProducts(int? categoryId, string? search, string? sort)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue)
            {
                var categoryIds = await GetCategoryAndChildIdsAsync(categoryId.Value);
                query = query.Where(p => categoryIds.Contains(p.CategoryID));
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name" => query.OrderBy(p => p.ProductName),
                _ => query.OrderByDescending(p => p.DateAdded)
            };

            var products = await query.ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.CategoryThumbs = categories.ToDictionary(
                c => c.CategoryID,
                c => CategoryImageResolver.GetDefaultThumb(_environment, c.CategoryName, "/images/categories/t-shirt.png"));
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;
            ViewBag.Sort = sort;
            ViewBag.TotalCount = products.Count;

            return View(products);
        }

        public async Task<IActionResult> hDetails(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ColorImages)
                .FirstOrDefaultAsync(p => p.ProductID == id && !p.IsDeleted);

            if (product == null) return NotFound();

            return View(product);
        }

        public IActionResult pIndex()
        {
            return View();
        }

        public IActionResult hWishlist()
        {
            return View();
        }

        public IActionResult hCart()
        {
            return View();
        }

        // GET: /Home/hBilling — Billing & Payment page.
        // UI PREVIEW ONLY: the order shown is a hardcoded stand-in so the
        // page can be viewed and styled on its own. Nothing here reads a
        // real cart/order yet, and the form doesn't post anywhere —
        // wiring that up (pulling the real selected cart items, saving
        // billing info, handling the actual payment submission) is a
        // separate follow-up step.
        //
        // Connected to:
        //   View   : Views/Home/hBilling.cshtml
        //   Styles : wwwroot/css/home/billing-payment.css
        //   Script : wwwroot/js/billing-payment.js
        public IActionResult hBilling()
        {
            return View();
        }
    }
}
