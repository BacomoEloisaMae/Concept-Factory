using ConceptFactory.Data;
using ConceptFactory.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConceptFactory.Controllers
{
    public class CustomizeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomizeController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Custom(int? categoryId)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);

            var product = await query
                .OrderBy(p => p.ProductID)
                .FirstOrDefaultAsync();

            // Fall back to any active product if the chosen category is empty
            if (product == null && categoryId.HasValue)
            {
                product = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => !p.IsDeleted && p.Status == "Active")
                    .OrderBy(p => p.ProductID)
                    .FirstOrDefaultAsync();
            }

            if (product == null)
                return NotFound();

            ViewBag.Services = await _context.Services
                .Where(s => !s.IsDeleted && s.Status == "Active")
                .ToListAsync();

            return View(product);
        }
    }
}
