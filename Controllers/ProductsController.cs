using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Models.ViewModels;
using ConceptFactory.Utils;

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

        // GET/POST: /Products/pCreate
        // Connected to: Views/Products/pIndex.cshtml (Add Item button passes
        // returnUrl = the current filtered/paged URL) and Views/Products/pCreate.cshtml
        // (carries returnUrl back through a hidden field). See RedirectToLocal() below.
        public async Task<IActionResult> pCreate(string? returnUrl)
        {
            await PopulateCategoriesDropdown();
            ViewBag.ReturnUrl = returnUrl;
            return View(new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pCreate(Product product, IFormFile? imageFile, List<IFormFile>? showcaseFiles,
            List<ProductColorImageInput>? ColorImages, string? returnUrl)
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

                await SaveColorImagesAsync(product.ProductID, ColorImages);

                await ActivityLogger.LogAsync(_context, HttpContext, "Products", "Added Product", $"\"{product.ProductName}\"");
                TempData["Success"] = "Item added successfully.";
                return RedirectToLocal(returnUrl);
            }
            await PopulateCategoriesDropdown(product.CategoryID);
            ViewBag.ReturnUrl = returnUrl;
            return View(product);
        }

        // GET/POST: /Products/pEdit
        // Connected to: Views/Products/pIndex.cshtml (each row's Edit link passes
        // returnUrl = the current filtered/paged URL) and Views/Products/pEdit.cshtml
        // (carries returnUrl back through a hidden field). See RedirectToLocal() below.
        public async Task<IActionResult> pEdit(int? id, string? returnUrl)
        {
            if (id == null) return NotFound();
            var product = await _context.Products
                .Include(p => p.ColorImages)
                .FirstOrDefaultAsync(m => m.ProductID == id);
            if (product == null) return NotFound();
            await PopulateCategoriesDropdown(product.CategoryID);
            ViewBag.ReturnUrl = returnUrl;
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> pEdit(int id,
            [Bind("ProductID,ProductName,CategoryID,Size,Color,Price,StockQuantity,ProductDescription,Status,ImagePath,AdditionalImages,DateAdded")]
            Product product,
            IFormFile? imageFile,
            List<IFormFile>? showcaseFiles,
            string? removedShowcaseImages,
            List<ProductColorImageInput>? ColorImages,
            string? returnUrl)
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

                    await SaveColorImagesAsync(product.ProductID, ColorImages);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductID)) return NotFound();
                    else throw;
                }
                await ActivityLogger.LogAsync(_context, HttpContext, "Products", "Edited Product", $"\"{product.ProductName}\"");
                TempData["Success"] = "Item updated successfully.";
                return RedirectToLocal(returnUrl);
            }
            await PopulateCategoriesDropdown(product.CategoryID);
            ViewBag.ReturnUrl = returnUrl;
            return View(product);
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> pSoftDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null) { product.IsDeleted = true; product.DeletedAt = DateTime.Now; await _context.SaveChangesAsync(); await ActivityLogger.LogAsync(_context, HttpContext, "Products", "Deleted Product", $"\"{product.ProductName}\" moved to deleted items."); TempData["Success"] = $"\"{product.ProductName}\" moved to deleted items."; }
            return RedirectToAction(nameof(pIndex));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> pHardDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                string productName = product.ProductName;
                if (!string.IsNullOrEmpty(product.ImagePath)) DeleteImage(product.ImagePath);
                if (!string.IsNullOrEmpty(product.AdditionalImages))
                    foreach (var p in product.AdditionalImages.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        DeleteImage(p.Trim());
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Products", "Permanently Deleted Product", $"\"{productName}\"");
                TempData["Success"] = $"\"{productName}\" permanently deleted.";
            }
            return RedirectToAction(nameof(pDeleted));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> pRestore(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null) { product.IsDeleted = false; product.DeletedAt = null; await _context.SaveChangesAsync(); await ActivityLogger.LogAsync(_context, HttpContext, "Products", "Restored Product", $"\"{product.ProductName}\""); TempData["Success"] = $"\"{product.ProductName}\" restored successfully."; }
            return RedirectToAction(nameof(pDeleted));
        }

        [HttpPost]
        public async Task<IActionResult> pToggleStatus(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            product.Status = product.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();
            await ActivityLogger.LogAsync(_context, HttpContext, "Products", "Toggled Product Status", $"\"{product.ProductName}\" → {product.Status}");
            return Json(new { success = true, status = product.Status });
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private bool ProductExists(int id) => _context.Products.Any(p => p.ProductID == id);

        // Sends the admin back to wherever they came from (their filtered/
        // paged Items view) after saving, instead of always resetting to
        // pIndex's first page. IsLocalUrl guards against being redirected
        // off-site by a tampered returnUrl.
        // Used by: pCreate(POST) and pEdit(POST) above.
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(pIndex));
        }

        // Upserts the color-variant rows for a product: updates rows that
        // still have their ID submitted, inserts brand-new rows (ID == 0),
        // uploads any newly attached photo, and deletes (both the DB row
        // and the file on disk) any existing rows that were removed by
        // the admin on the form.
        private async Task SaveColorImagesAsync(int productId, List<ProductColorImageInput>? submitted)
        {
            submitted ??= new List<ProductColorImageInput>();
            submitted = submitted.Where(c => !string.IsNullOrWhiteSpace(c.ColorHex)).ToList();

            var existingRows = await _context.ProductColorImages
                .Where(pci => pci.ProductID == productId)
                .ToListAsync();

            var submittedIds = submitted.Where(c => c.ProductColorImageID > 0)
                                         .Select(c => c.ProductColorImageID)
                                         .ToHashSet();

            // Remove rows the admin deleted from the form
            foreach (var row in existingRows.Where(r => !submittedIds.Contains(r.ProductColorImageID)))
            {
                if (!string.IsNullOrEmpty(row.ImagePath)) DeleteImage(row.ImagePath);
                _context.ProductColorImages.Remove(row);
            }

            for (int i = 0; i < submitted.Count; i++)
            {
                var input = submitted[i];
                ProductColorImage? row = input.ProductColorImageID > 0
                    ? existingRows.FirstOrDefault(r => r.ProductColorImageID == input.ProductColorImageID)
                    : null;

                string? imagePath = input.ExistingImagePath;
                if (input.NewImage != null && input.NewImage.Length > 0)
                {
                    if (!string.IsNullOrEmpty(imagePath)) DeleteImage(imagePath);
                    imagePath = await SaveImageAsync(input.NewImage);
                }

                if (row != null)
                {
                    row.ColorHex = input.ColorHex;
                    row.ImagePath = imagePath;
                    row.DisplayOrder = i;
                }
                else
                {
                    _context.ProductColorImages.Add(new ProductColorImage
                    {
                        ProductID = productId,
                        ColorHex = input.ColorHex,
                        ImagePath = imagePath,
                        DisplayOrder = i
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task PopulateCategoriesDropdown(int? selectedId = null)
        {
            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            var items = new List<SelectListItem>();

            var topLevel = categories.Where(c => c.ParentCategoryID == null).OrderBy(c => c.CategoryName);
            foreach (var parent in topLevel)
            {
                var children = categories.Where(c => c.ParentCategoryID == parent.CategoryID)
                                          .OrderBy(c => c.CategoryName)
                                          .ToList();

                if (children.Any())
                {
                    // Broader category with specific sub-types underneath (e.g.
                    // "Jackets" -> Varsity Jackets, Jacket Zip-Up, ...): the
                    // parent name becomes the optgroup label ONLY — it doesn't
                    // also get its own selectable option, or it'd show up twice
                    // in the dropdown ("Jackets" as a plain item, then "Jackets"
                    // again as the group heading right below it).
                    foreach (var child in children)
                    {
                        items.Add(new SelectListItem
                        {
                            Value = child.CategoryID.ToString(),
                            Text = child.CategoryName,
                            Selected = child.CategoryID == selectedId,
                            Group = new SelectListGroup { Name = parent.CategoryName }
                        });
                    }
                }
                else
                {
                    // No sub-types (yet) — stays a plain, directly-selectable
                    // option, same as before (e.g. "Polo Shirts", "Sweaters").
                    items.Add(new SelectListItem
                    {
                        Value = parent.CategoryID.ToString(),
                        Text = parent.CategoryName,
                        Selected = parent.CategoryID == selectedId
                    });
                }
            }

            ViewBag.CategoryID = items;
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
