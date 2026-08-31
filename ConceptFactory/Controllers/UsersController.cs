using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;
using ConceptFactory.Utils;
using ConceptFactory.Filters;

namespace ConceptFactory.Controllers
{
    // "User Management" — the admin sidebar's old dead "Customers" link.
    // Admin-only: Sales/Production staff shouldn't be able to create their
    // own (or each other's) accounts. "Every user in the system" (uIndex)
    // is built from three sources:
    //   - AdminUsers  -> Role "Admin",    read-only here (real login accounts;
    //                    editing them belongs to account/login setup, not
    //                    this screen)
    //   - Users table -> Role "Staff",    fully CRUD-able below (Sales/
    //                    Production team, with real login — see uCreate/
    //                    uEdit's password field and AuthController.Login)
    //   - Customers   -> Role "Customer", read-only, one row per real
    //                    Customer account (Models/Customer.cs)
    [AdminAuthFilter]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Lightweight, not EF-mapped — just what the table/stat cards need.
        public class UserRow
        {
            public int? Id { get; set; } // Users.UserID — only set for Staff rows (the only ones CRUD-able here)
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
            public string? Phone { get; set; }
            public string Role { get; set; } = ""; // "Admin", "Sales", "Production", or "Customer"
            public string? Station { get; set; } // Production rows only — e.g. "Cutting"
            public string? Status { get; set; } // Staff only — "Active"/"Inactive"
            public DateTime DateJoined { get; set; }
            public int? OrdersCount { get; set; }
            public decimal? TotalSpent { get; set; }
            public bool CanManage { get; set; } // true only for Staff rows
        }

        private async Task<int> StaffRoleIdAsync() =>
            (await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleName == "Staff"))?.RoleID
                ?? throw new InvalidOperationException("The 'Staff' role is missing from the Roles table — check Database/Setup.sql was run.");

        public async Task<IActionResult> uIndex(string? role, string? search, int page = 1)
        {
            int pageSize = 10;
            ViewData["Title"] = "User Management";
            ViewData["ActivePage"] = "Customers";

            var admins = await _context.AdminUsers.AsNoTracking().ToListAsync();
            var adminRows = admins.Select(a => new UserRow
            {
                Name = a.FullName,
                Email = a.Email,
                Phone = a.Phone,
                Role = "Admin",
                DateJoined = a.CreatedAt,
                CanManage = false
            });

            int staffRoleId = await StaffRoleIdAsync();
            var staff = await _context.Users.AsNoTracking()
                .Where(u => u.RoleID == staffRoleId && !u.IsDeleted).ToListAsync();
            var staffRows = staff.Select(u => new UserRow
            {
                Id = u.UserID,
                Name = $"{u.FirstName} {u.LastName}",
                Email = u.Email,
                Phone = u.ContactNum,
                Role = u.Department ?? "Staff",
                Station = u.Department == "Production" ? ConceptFactory.Utils.ProductionStations.NameFor(u.StationIndex) : null,
                Status = u.Status,
                DateJoined = u.CreatedAt,
                CanManage = true
            });

            var customers = await _context.Customers.AsNoTracking().Where(c => !c.IsDeleted).ToListAsync();
            var orders = await _context.Orders.AsNoTracking().ToListAsync();
            var customerRows = customers.Select(c =>
            {
                var myOrders = orders.Where(o => o.CustomerID == c.CustomerID
                    || (o.CustomerEmail != null && o.CustomerEmail.Equals(c.Email, StringComparison.OrdinalIgnoreCase))).ToList();
                return new UserRow
                {
                    Name = $"{c.FirstName} {c.LastName}",
                    Email = c.Email,
                    Phone = c.Phone,
                    Role = "Customer",
                    DateJoined = c.CreatedAt,
                    OrdersCount = myOrders.Count,
                    TotalSpent = myOrders.Sum(o => o.PaymentStatus switch
                    {
                        "Fully Paid" => o.TotalAmount,
                        "Partially Paid" => o.DownPaymentAmount,
                        _ => 0m
                    }),
                    CanManage = false
                };
            });

            var allUsers = adminRows.Concat(staffRows).Concat(customerRows).ToList();

            // ── Stat cards (computed off the full set, before filtering) ──
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.TotalUsers = allUsers.Count;
            ViewBag.AdminCount = allUsers.Count(u => u.Role == "Admin");
            ViewBag.StaffCount = allUsers.Count(u => u.Role == "Sales" || u.Role == "Production" || u.Role == "Staff");
            ViewBag.CustomerCount = allUsers.Count(u => u.Role == "Customer");
            ViewBag.NewThisMonthCount = allUsers.Count(u => u.DateJoined >= monthStart);

            // ── Filtering — "Staff" groups both Sales and Production ──
            var filtered = allUsers.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(role) && role != "All")
            {
                filtered = role == "Staff"
                    ? filtered.Where(u => u.Role == "Sales" || u.Role == "Production" || u.Role == "Staff")
                    : filtered.Where(u => u.Role == role);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                filtered = filtered.Where(u =>
                    u.Name.ToLower().Contains(s) ||
                    u.Email.ToLower().Contains(s) ||
                    (u.Phone != null && u.Phone.ToLower().Contains(s)));
            }

            var filteredList = filtered.OrderByDescending(u => u.DateJoined).ToList();
            int totalCount = filteredList.Count;
            var paged = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Role = string.IsNullOrWhiteSpace(role) ? "All" : role;
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            return View(paged);
        }

        // ══════════════════════════════════════════════════════════════
        // Staff CRUD — the only role manageable from this screen.
        // ══════════════════════════════════════════════════════════════

        public IActionResult uCreate()
        {
            ViewData["Title"] = "Add Staff";
            ViewData["ActivePage"] = "Customers";
            ViewBag.Stations = ConceptFactory.Utils.ProductionStations.Options();
            return View(new User { Department = "Sales", Status = "Active" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> uCreate(User user, string Password, string ConfirmPassword)
        {
            ModelState.Remove(nameof(user.Password));
            ModelState.Remove(nameof(user.RoleID));
            if (user.Department != "Sales" && user.Department != "Production")
                ModelState.AddModelError(nameof(user.Department), "Choose a team.");

            // Station only applies to (and is required for) Production —
            // a Sales row should never carry a stale station value.
            if (user.Department == "Production")
            {
                if (!user.StationIndex.HasValue || user.StationIndex.Value < 0 || user.StationIndex.Value >= ConceptFactory.Utils.ProductionStations.Names.Length)
                    ModelState.AddModelError(nameof(user.StationIndex), "Choose which station they're assigned to.");
            }
            else
            {
                user.StationIndex = null;
            }

            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
                ModelState.AddModelError(nameof(Password), "Password must be at least 6 characters.");
            else if (Password != ConfirmPassword)
                ModelState.AddModelError(nameof(ConfirmPassword), "Passwords do not match.");

            bool emailTaken = await _context.Users.AnyAsync(u => u.Email == user.Email && !u.IsDeleted)
                || await _context.AdminUsers.AnyAsync(a => a.Email == user.Email);
            if (emailTaken)
                ModelState.AddModelError(nameof(user.Email), "This email is already in use.");

            if (ModelState.IsValid)
            {
                user.RoleID = await StaffRoleIdAsync();
                user.Password = PasswordHasher.Hash(Password);
                user.CreatedAt = DateTime.Now;
                user.IsDeleted = false;
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                string stationNote = user.Department == "Production"
                    ? $" (Station: {ConceptFactory.Utils.ProductionStations.NameFor(user.StationIndex)})"
                    : "";
                await ActivityLogger.LogAsync(_context, HttpContext, "Users", "Added Staff",
                    $"\"{user.FirstName} {user.LastName}\" added to the {user.Department} team{stationNote}.");
                TempData["Success"] = $"\"{user.FirstName} {user.LastName}\" added successfully.";
                return RedirectToAction(nameof(uIndex));
            }
            ViewData["Title"] = "Add Staff";
            ViewData["ActivePage"] = "Customers";
            ViewBag.Stations = ConceptFactory.Utils.ProductionStations.Options();
            return View(user);
        }

        public async Task<IActionResult> uEdit(int? id)
        {
            if (id == null) return NotFound();
            int staffRoleId = await StaffRoleIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.RoleID == staffRoleId && !u.IsDeleted);
            if (user == null) return NotFound();
            ViewData["Title"] = "Edit Staff";
            ViewData["ActivePage"] = "Customers";
            ViewBag.Stations = ConceptFactory.Utils.ProductionStations.Options();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> uEdit(int id,
            [Bind("UserID,FirstName,LastName,Email,ContactNum,Department,Status,StationIndex")] User form, string? NewPassword)
        {
            if (id != form.UserID) return NotFound();
            ModelState.Remove(nameof(form.Password));
            ModelState.Remove(nameof(form.RoleID));
            if (form.Department != "Sales" && form.Department != "Production")
                ModelState.AddModelError(nameof(form.Department), "Choose a team.");

            if (form.Department == "Production")
            {
                if (!form.StationIndex.HasValue || form.StationIndex.Value < 0 || form.StationIndex.Value >= ConceptFactory.Utils.ProductionStations.Names.Length)
                    ModelState.AddModelError(nameof(form.StationIndex), "Choose which station they're assigned to.");
            }
            else
            {
                form.StationIndex = null;
            }

            if (!string.IsNullOrWhiteSpace(NewPassword) && NewPassword.Length < 6)
                ModelState.AddModelError(nameof(NewPassword), "Password must be at least 6 characters.");

            if (ModelState.IsValid)
            {
                int staffRoleId = await StaffRoleIdAsync();
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.RoleID == staffRoleId && !u.IsDeleted);
                if (user == null) return NotFound();

                user.FirstName = form.FirstName;
                user.LastName = form.LastName;
                user.Email = form.Email;
                user.ContactNum = form.ContactNum;
                user.Department = form.Department;
                user.Status = form.Status;
                user.StationIndex = form.StationIndex;

                if (!string.IsNullOrWhiteSpace(NewPassword))
                {
                    user.Password = PasswordHasher.Hash(NewPassword);
                    // A fresh password shouldn't be handicapped by whatever
                    // lockout state was already in effect.
                    user.FailedLoginAttempts = 0;
                    user.LockoutEnd = null;
                }

                await _context.SaveChangesAsync();

                string stationNote = user.Department == "Production"
                    ? $" (Station: {ConceptFactory.Utils.ProductionStations.NameFor(user.StationIndex)})"
                    : "";
                await ActivityLogger.LogAsync(_context, HttpContext, "Users", "Updated Staff",
                    $"\"{user.FirstName} {user.LastName}\" ({user.Department}) updated{stationNote}.");
                TempData["Success"] = $"\"{user.FirstName} {user.LastName}\" updated successfully.";
                return RedirectToAction(nameof(uIndex));
            }
            ViewData["Title"] = "Edit Staff";
            ViewData["ActivePage"] = "Customers";
            ViewBag.Stations = ConceptFactory.Utils.ProductionStations.Options();
            return View(form);
        }

        // Same soft/hard delete + restore + archive-list pattern as
        // ProductsController (pSoftDelete/pHardDelete/pRestore/pDeleted).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> uSoftDelete(int id)
        {
            int staffRoleId = await StaffRoleIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.RoleID == staffRoleId);
            if (user != null)
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Users", "Deleted Staff",
                    $"\"{user.FirstName} {user.LastName}\" moved to deleted items.");
                TempData["Success"] = $"\"{user.FirstName} {user.LastName}\" moved to deleted items.";
            }
            return RedirectToAction(nameof(uIndex));
        }

        public async Task<IActionResult> uDeleted(string? search, int page = 1)
        {
            int pageSize = 10;
            ViewData["Title"] = "Deleted Staff";
            ViewData["ActivePage"] = "Customers";

            int staffRoleId = await StaffRoleIdAsync();
            var query = _context.Users.AsNoTracking().Where(u => u.RoleID == staffRoleId && u.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(u => u.FirstName.ToLower().Contains(s) || u.LastName.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
            }

            int totalCount = await query.CountAsync();
            var deleted = await query.OrderByDescending(u => u.DeletedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            return View(deleted);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> uRestore(int id)
        {
            int staffRoleId = await StaffRoleIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.RoleID == staffRoleId);
            if (user != null)
            {
                user.IsDeleted = false;
                user.DeletedAt = null;
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Users", "Restored Staff", $"\"{user.FirstName} {user.LastName}\"");
                TempData["Success"] = $"\"{user.FirstName} {user.LastName}\" restored successfully.";
            }
            return RedirectToAction(nameof(uDeleted));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> uHardDelete(int id)
        {
            int staffRoleId = await StaffRoleIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.RoleID == staffRoleId);
            if (user != null)
            {
                string name = $"{user.FirstName} {user.LastName}";
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                await ActivityLogger.LogAsync(_context, HttpContext, "Users", "Permanently Deleted Staff", $"\"{name}\"");
                TempData["Success"] = $"\"{name}\" permanently deleted.";
            }
            return RedirectToAction(nameof(uDeleted));
        }
    }
}
