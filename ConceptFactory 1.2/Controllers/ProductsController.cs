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
        public async Task<IActionResult> pCreate(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                    product.ImagePath = await SaveImageAsync(imageFile);

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
        public async Task<IActionResult> pEdit(int id, [Bind("ProductID,ProductName,CategoryID,Size,Color,Price,StockQuantity,ProductDescription,Status,ImagePath,DateAdded")] Product product, IFormFile? imageFile)
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

        private bool ProductExists(int id) => _context.Products.Any(p => p.ProductID == id);

        private async Task PopulateCategoriesDropdown(int? selectedId = null)
        {
            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.CategoryID = new SelectList(categories, "CategoryID", "CategoryName", selectedId);
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            string folder = Path.Combine(_environment.WebRootPath, "images", "products");
            Directory.CreateDirectory(folder);
            string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create);
            await file.CopyToAsync(stream);
            return "/images/products/" + fileName;
        }

        private void DeleteImage(string imagePath)
        {
            string full = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }
    }
}
