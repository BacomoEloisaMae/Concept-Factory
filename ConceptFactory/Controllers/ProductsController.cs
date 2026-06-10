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
        public async Task<IActionResult> pIndex(string? search, int? categoryId, string? status, int page = 1)
        {
            int pageSize = 12;

            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
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

        // GET: Products/Deleted
        public async Task<IActionResult> pDeleted(string? search, int? categoryId, int page = 1)
        {
            int pageSize = 12;

            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var products = await query
                .OrderByDescending(p => p.DeletedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryID", "CategoryName", categoryId);

            return View(products);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> pDetails(int? id)
        {
            if (id == null) return NotFound();

            // Updated to load additional showcase images along with the category relationship
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // GET: Products/Create
        public async Task<IActionResult> pCreate()
        {
            await PopulateCategoriesDropdown();
            return View(new Product());
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pCreate(Product product, IFormFile? imageFile, List<IFormFile>? additionalImageFiles)
        {
            if (ModelState.IsValid)
            {
                // 1. Process and save the single primary image thumbnail
                if (imageFile != null && imageFile.Length > 0)
                    product.ImagePath = await SaveImageAsync(imageFile);

                product.DateAdded = DateTime.Now;
                _context.Add(product);

                // Save here first to commit the parent row and generate its ProductID identity key
                await _context.SaveChangesAsync();

                // 2. Loop through and process incoming gallery display selections
                if (additionalImageFiles != null && additionalImageFiles.Any())
                {
                    foreach (var file in additionalImageFiles)
                    {
                        if (file.Length > 0)
                        {
                            string savedPath = await SaveImageAsync(file);
                            var newImg = new ProductImage
                            {
                                ProductID = product.ProductID, // Maps straight to our newly saved product record id
                                ImagePath = savedPath
                            };
                            _context.ProductImages.Add(newImg);
                        }
                    }
                    // Save child data properties down to database 
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Product added successfully.";
                return RedirectToAction(nameof(pIndex));
            }

            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> pEdit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(m => m.ProductID == id);

            if (product == null) return NotFound();

            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pEdit(int id, [Bind("ProductID,ProductName,CategoryID,Size,Price,StockQuantity,ProductDescription,Status,ImagePath,DateAdded")] Product product, IFormFile? imageFile, List<IFormFile>? additionalImageFiles, List<int>? deletedImageIds)
        {
            if (id != product.ProductID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(product.ImagePath)) DeleteImage(product.ImagePath);
                        product.ImagePath = await SaveImageAsync(imageFile);
                    }

                    if (deletedImageIds != null && deletedImageIds.Any())
                    {
                        foreach (var imgId in deletedImageIds)
                        {
                            var imgRecord = await _context.ProductImages.FindAsync(imgId);
                            if (imgRecord != null)
                            {
                                DeleteImage(imgRecord.ImagePath);
                                _context.ProductImages.Remove(imgRecord);
                            }
                        }
                    }

                    if (additionalImageFiles != null && additionalImageFiles.Any())
                    {
                        foreach (var file in additionalImageFiles)
                        {
                            if (file.Length > 0)
                            {
                                string savedPath = await SaveImageAsync(file);
                                var newImg = new ProductImage
                                {
                                    ProductID = product.ProductID,
                                    ImagePath = savedPath
                                };
                                _context.ProductImages.Add(newImg);
                            }
                        }
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(pIndex));
            }
            await PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // POST: Products/SoftDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pSoftDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.IsDeleted = true;
                product.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"\"{product.ProductName}\" moved to deleted items.";
            }
            return RedirectToAction(nameof(pIndex));
        }

        // POST: Products/HardDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pHardDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImagePath))
                    DeleteImage(product.ImagePath);

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"\"{product.ProductName}\" permanently deleted.";
            }
            return RedirectToAction(nameof(pDeleted));
        }

        // POST: Products/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pRestore(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.IsDeleted = false;
                product.DeletedAt = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"\"{product.ProductName}\" restored successfully.";
            }
            return RedirectToAction(nameof(pDeleted));
        }

        // POST: Products/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> pToggleStatus(int id)
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