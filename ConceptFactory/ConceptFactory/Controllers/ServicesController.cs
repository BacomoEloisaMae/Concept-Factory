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
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            int pageSize = 12;

            var query = _context.Services.AsQueryable();

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

        // GET: Services/Create
        public IActionResult Create()
        {
            return View(new Service());
        }

        // POST: Services/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Service service)
        {
            if (ModelState.IsValid)
            {
                service.DateAdded = DateTime.Now;
                _context.Add(service);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Service added successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(service);
        }

        // GET: Services/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            return View(service);
        }

        // POST: Services/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Service service)
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
                return RedirectToAction(nameof(Index));
            }
            return View(service);
        }

        // POST: Services/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Service deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Services/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            service.Status = service.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();

            return Json(new { success = true, status = service.Status });
        }
    }
}
