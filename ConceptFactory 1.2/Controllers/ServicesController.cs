using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;

namespace ConceptFactory.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Services
        public async Task<IActionResult> sIndex(string? search, string? status, int page = 1)
        {
            int pageSize = 12;

            var query = _context.Services
                .Where(s => !s.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.ServiceName.Contains(search));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(s => s.Status == status);

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var services = await query
                .OrderByDescending(s => s.DateAdded)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(services);
        }

        // GET: Services/Deleted
        public async Task<IActionResult> sDeleted(string? search, int page = 1)
        {
            int pageSize = 12;

            var query = _context.Services
                .Where(s => s.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.ServiceName.Contains(search));

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var services = await query
                .OrderByDescending(s => s.DeletedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(services);
        }

        // GET: Services/Create
        public IActionResult sCreate()
        {
            return View(new Service());
        }

        // POST: Services/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> sCreate(Service service)
        {
            if (ModelState.IsValid)
            {
                service.DateAdded = DateTime.Now;
                _context.Add(service);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Service added successfully.";
                return RedirectToAction(nameof(sIndex));
            }
            return View(service);
        }

        // GET: Services/Edit/5
        public async Task<IActionResult> sEdit(int? id)
        {
            if (id == null) return NotFound();

            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            return View(service);
        }

        // POST: Services/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> sEdit(int id, Service service)
        {
            if (id != service.ServiceID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Services.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ServiceID == id);
                    service.DateAdded = existing?.DateAdded ?? DateTime.Now;

                    _context.Update(service);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Service updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Services.Any(s => s.ServiceID == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(sIndex));
            }
            return View(service);
        }

        // POST: Services/SoftDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> sSoftDelete(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                service.IsDeleted = true;
                service.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"\"{service.ServiceName}\" moved to deleted items.";
            }
            return RedirectToAction(nameof(sIndex));
        }

        // POST: Services/HardDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> sHardDelete(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"\"{service.ServiceName}\" permanently deleted.";
            }
            return RedirectToAction(nameof(sDeleted));
        }

        // POST: Services/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> sRestore(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                service.IsDeleted = false;
                service.DeletedAt = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"\"{service.ServiceName}\" restored successfully.";
            }
            return RedirectToAction(nameof(sDeleted));
        }

        // POST: Services/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> sToggleStatus(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            service.Status = service.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();

            return Json(new { success = true, status = service.Status });
        }
    }
}
