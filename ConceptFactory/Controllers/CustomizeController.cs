using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConceptFactory.Controllers
{
    public class CustomizeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public CustomizeController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }
        // GET: /Customize — lets the shopper pick which kind of item to customize
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            // Category-card thumbnails: a real color photo if one exists for
            // that category, otherwise the generic fallback picture.
            var thumbs = new Dictionary<int, string>();
            foreach (var cat in categories)
            {
                var key = CategoryImageResolver.GetKey(cat.CategoryName);
                thumbs[cat.CategoryID] = CategoryImageResolver.GetDefaultThumb(_environment, cat.CategoryName, GetLegacyThumb(cat.CategoryName));
            }
            ViewBag.CategoryThumbs = thumbs;

            return View(categories);
        }

        // Fallback picture used when a category has no color photos yet —
        // same generic artwork the Customize page has always used.
        private static string GetLegacyThumb(string? cat)
        {
            if (string.IsNullOrEmpty(cat)) return "/images/categories/default.png";
            var n = cat.ToLower();
            return n switch
            {
                var x when x.Contains("jacket") => "/images/custom/hoodies.png",
                var x when x.Contains("hoodie") => "/images/custom/hoodies.png",
                var x when x.Contains("sweater") => "/images/custom/sweats.png",
                var x when x.Contains("sweat") => "/images/custom/sweats.png",
                var x when x.Contains("tote") => "/images/custom/tote.png",
                var x when x.Contains("bag") => "/images/custom/tote.png",
                var x when x.Contains("compression") => "/images/custom/compressions.png",
                var x when x.Contains("t-shirt") => "/images/custom/tshirt.png",
                var x when x.Contains("tshirt") => "/images/custom/tshirt.png",
                var x when x.Contains("shirt") => "/images/custom/tshirt.png",
                var x when x.Contains("polo") => "/images/categories/polo.png",
                _ => "/images/categories/default.png"
            };
        }

        public async Task<IActionResult> Custom(int? categoryId)
        {
            const decimal defaultBasePrice = 350m; // flat fallback when the category has no priced products yet

            ConceptFactory.Models.Category? category = null;
            if (categoryId.HasValue)
            {
                category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryID == categoryId.Value);
            }

            // Use the category's lowest active product price as a realistic
            // "starting at" price if any exist; otherwise fall back to the default.
            decimal basePrice = defaultBasePrice;
            if (categoryId.HasValue)
            {
                var lowestActivePrice = await _context.Products
                    .Where(p => !p.IsDeleted && p.Status == "Active" && p.CategoryID == categoryId.Value)
                    .OrderBy(p => p.Price)
                    .Select(p => (decimal?)p.Price)
                    .FirstOrDefaultAsync();

                if (lowestActivePrice.HasValue)
                    basePrice = lowestActivePrice.Value;
            }

            var model = new ConceptFactory.Models.ViewModels.CustomOrderViewModel
            {
                CategoryID = category?.CategoryID,
                CategoryName = category?.CategoryName,
                BasePrice = basePrice
            };

            ViewBag.Services = await _context.Services
                .Where(s => !s.IsDeleted && s.Status == "Active")
                .ToListAsync();

            // Real color photos available for this category (scanned from
            // wwwroot/images/custom/{key}/). Empty list -> the view falls
            // back to its generic 12-swatch list with no photo swapping.
            var key = CategoryImageResolver.GetKey(category?.CategoryName);
            ViewBag.AvailableColors = CategoryImageResolver.GetAvailableColors(_environment, key);
            ViewBag.CategoryThumb = CategoryImageResolver.GetDefaultThumb(_environment, category?.CategoryName, GetLegacyThumb(category?.CategoryName));

            return View(model);
        }
    }
}
