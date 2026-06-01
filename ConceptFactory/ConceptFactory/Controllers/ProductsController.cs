using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;

namespace ConceptFactory.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Products
        public async Task<IActionResult> Index(string? search, int? categoryId, string? status, int page = 1)
        {
            int pageSize = 12;

            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(p => p.Status == status);

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var products = await query
                .OrderByDescending(p => p.DateAdded)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryID", "CategoryName", categoryId);

            return View(products);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // GET: Products/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesDropdown();
            return View(new Product());
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    product.ImagePath = await SaveImageAsync(imageFile);
                }

                product.DateAdded = DateTime.Now;
                _context.Add(product);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Product added successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
        {
            if (id != product.ProductID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Products.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProductID == id);

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(existing?.ImagePath))
                            DeleteImage(existing.ImagePath);

                        product.ImagePath = await SaveImageAsync(imageFile);
                    }
                    else
                    {
                        product.ImagePath = existing?.ImagePath;
                    }

                    product.DateAdded = existing?.DateAdded ?? DateTime.Now;
                    _context.Update(product);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Product updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductID)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImagePath))
                    DeleteImage(product.ImagePath);

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Product deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Products/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Status = product.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();

            return Json(new { success = true, status = product.Status });
        }

        // ─── Helpers ────────────────────────────────────────────────────

        private bool ProductExists(int id) =>
            _context.Products.Any(p => p.ProductID == id);

        private async Task PopulateCategoriesDropdown(int? selectedId = null)
        {
            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.CategoryID = new SelectList(categories, "CategoryID", "CategoryName", selectedId);
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "products");
            Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return "/images/products/" + uniqueFileName;
        }

        private void DeleteImage(string imagePath)
        {
            string fullPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
    }
}
