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

        public async Task<IActionResult> pIndex(string? search, int? categoryId, string? status, int page = 1)
        {
            int pageSize = 12;
            var query = _context.Products.Include(p => p.Category).Where(p => !p.IsDeleted).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.ProductName.Contains(search));
            if (categoryId.HasValue) query = query.Where(p => p.CategoryID == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

            int totalCount = await query.CountAsync();
            var products = await query.OrderByDescending(p => p.DateAdded).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Search = search; ViewBag.CategoryId = categoryId; ViewBag.Status = status;
            ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryID", "CategoryName", categoryId);
            return View(products);
        }

        public async Task<IActionResult> pDeleted(string? search, int? categoryId, int page = 1)
        {
            int pageSize = 12;
            var query = _context.Products.Include(p => p.Category).Where(p => p.IsDeleted).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.ProductName.Contains(search));
            if (categoryId.HasValue) query = query.Where(p => p.CategoryID == categoryId.Value);

            int totalCount = await query.CountAsync();
            var products = await query.OrderByDescending(p => p.DeletedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Search = search; ViewBag.CategoryId = categoryId;
            ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryID", "CategoryName", categoryId);
            return View(products);
        }

        public async Task<IActionResult> pCreate()
        {
            await PopulateCategoriesDropdown();
            return View(new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pCreate(Product product, IFormFile? imageFile, List<IFormFile>? showcaseFiles)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                    product.ImagePath = await SaveImageAsync(imageFile);

                if (showcaseFiles != null && showcaseFiles.Count > 0)
                    product.AdditionalImages = await SaveMultipleImagesAsync(showcaseFiles);

                product.DateAdded = DateTime.Now;
                _context.Add(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Item added successfully.";
                return RedirectToAction(nameof(pIndex));
            }
            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        public async Task<IActionResult> pEdit(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products.FirstOrDefaultAsync(m => m.ProductID == id);
            if (product == null) return NotFound();
            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pEdit(int id,
            [Bind("ProductID,ProductName,CategoryID,Size,Color,Price,StockQuantity,ProductDescription,Status,ImagePath,AdditionalImages,DateAdded")]
            Product product,
            IFormFile? imageFile,
            List<IFormFile>? showcaseFiles,
            string? removedShowcaseImages)
        {
            if (id != product.ProductID) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    // Main image
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(product.ImagePath)) DeleteImage(product.ImagePath);
                        product.ImagePath = await SaveImageAsync(imageFile);
                    }

                    // Handle removed showcase images
                    var existingShowcase = string.IsNullOrEmpty(product.AdditionalImages)
                        ? new List<string>()
                        : product.AdditionalImages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(s => s.Trim()).ToList();

                    if (!string.IsNullOrEmpty(removedShowcaseImages))
                    {
                        var toRemove = removedShowcaseImages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                             .Select(s => s.Trim()).ToList();
                        foreach (var path in toRemove)
                        {
                            DeleteImage(path);
                            existingShowcase.Remove(path);
                        }
                    }

                    // Add new showcase images
                    if (showcaseFiles != null && showcaseFiles.Count > 0)
                    {
                        var newPaths = await SaveMultipleImagesAsync(showcaseFiles);
                        if (!string.IsNullOrEmpty(newPaths))
                            existingShowcase.AddRange(newPaths.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                    }

                    product.AdditionalImages = existingShowcase.Count > 0 ? string.Join(",", existingShowcase) : null;

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductID)) return NotFound();
                    else throw;
                }
                TempData["Success"] = "Item updated successfully.";
                return RedirectToAction(nameof(pIndex));
            }
            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> pSoftDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null) { product.IsDeleted = true; product.DeletedAt = DateTime.Now; await _context.SaveChangesAsync(); TempData["Success"] = $"\"{product.ProductName}\" moved to deleted items."; }
            return RedirectToAction(nameof(pIndex));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> pHardDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImagePath)) DeleteImage(product.ImagePath);
                if (!string.IsNullOrEmpty(product.AdditionalImages))
                    foreach (var p in product.AdditionalImages.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        DeleteImage(p.Trim());
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"\"{product.ProductName}\" permanently deleted.";
            }
            return RedirectToAction(nameof(pDeleted));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> pRestore(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null) { product.IsDeleted = false; product.DeletedAt = null; await _context.SaveChangesAsync(); TempData["Success"] = $"\"{product.ProductName}\" restored successfully."; }
            return RedirectToAction(nameof(pDeleted));
        }

        [HttpPost]
        public async Task<IActionResult> pToggleStatus(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            product.Status = product.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();
            return Json(new { success = true, status = product.Status });
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private bool ProductExists(int id) => _context.Products.Any(p => p.ProductID == id);

        private async Task PopulateCategoriesDropdown(int? selectedId = null)
        {
            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.CategoryID = new SelectList(categories, "CategoryID", "CategoryName", selectedId);
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            string folder = Path.Combine(GetWwwRoot(), "images", "products");
            Directory.CreateDirectory(folder);
            string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create);
            await file.CopyToAsync(stream);
            return "/images/products/" + fileName;
        }

        private async Task<string> SaveMultipleImagesAsync(List<IFormFile> files)
        {
            var paths = new List<string>();
            foreach (var file in files)
            {
                if (file.Length > 0 && file.ContentType.StartsWith("image/"))
                    paths.Add(await SaveImageAsync(file));
            }
            return string.Join(",", paths);
        }

        private void DeleteImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;
            string full = Path.Combine(GetWwwRoot(), imagePath.TrimStart('/'));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }

        // Resolves the real project wwwroot — not the temp obj/Debug copy.
        // Ensures uploaded images persist across hot-reloads and restarts.
        private string GetWwwRoot()
        {
            if (!string.IsNullOrEmpty(_environment.WebRootPath) &&
                Directory.Exists(_environment.WebRootPath))
                return _environment.WebRootPath;
            return Path.Combine(_environment.ContentRootPath, "wwwroot");
        }
    }
}
