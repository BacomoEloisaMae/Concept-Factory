using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Utils;
using ConceptFactory.Models;
using ConceptFactory.Filters;

namespace ConceptFactory.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public HomeController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> hIndex(int? categoryId, string? search)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue)
            {
                var categoryIds = await GetCategoryAndChildIdsAsync(categoryId.Value);
                query = query.Where(p => categoryIds.Contains(p.CategoryID));
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            const int homePageLimit = 15;

            int totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.DateAdded)
                .Take(homePageLimit)
                .ToListAsync();

            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.CategoryThumbs = categories.ToDictionary(
                c => c.CategoryID,
                c => CategoryImageResolver.GetDefaultThumb(_environment, c.CategoryName, "/images/categories/t-shirt.png"));
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;
            ViewBag.TotalCount = totalCount;
            ViewBag.ShowViewAll = totalCount > homePageLimit;

            return View(products);
        }

        // A category "and its children" — e.g. selecting "Jackets" (a parent
        // with no products of its own) should also show every Varsity
        // Jacket, Zip-Up, Full Zip Hoodie, etc. Just one level deep, since
        // that's all the hierarchy currently goes.
        private async Task<List<int>> GetCategoryAndChildIdsAsync(int categoryId)
        {
            var ids = await _context.Categories
                .Where(c => c.CategoryID == categoryId || c.ParentCategoryID == categoryId)
                .Select(c => c.CategoryID)
                .ToListAsync();
            if (!ids.Contains(categoryId)) ids.Add(categoryId);
            return ids;
        }

        // GET: /Home/hProducts — dedicated full catalog page (linked from the "Products" nav item)
        public async Task<IActionResult> hProducts(int? categoryId, string? search, string? sort)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue)
            {
                var categoryIds = await GetCategoryAndChildIdsAsync(categoryId.Value);
                query = query.Where(p => categoryIds.Contains(p.CategoryID));
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name" => query.OrderBy(p => p.ProductName),
                _ => query.OrderByDescending(p => p.DateAdded)
            };

            var products = await query.ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.CategoryThumbs = categories.ToDictionary(
                c => c.CategoryID,
                c => CategoryImageResolver.GetDefaultThumb(_environment, c.CategoryName, "/images/categories/t-shirt.png"));
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;
            ViewBag.Sort = sort;
            ViewBag.TotalCount = products.Count;

            return View(products);
        }

        public async Task<IActionResult> hDetails(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ColorImages)
                .FirstOrDefaultAsync(p => p.ProductID == id && !p.IsDeleted);

            if (product == null) return NotFound();

            // Printing services — same active-services list offered on the
            // Customize page (Controllers/CustomizeController.cs), so a
            // pre-designed product can also have a printing service added
            // on top (e.g. adding a name/number print to a plain hoodie).
            ViewBag.Services = await _context.Services
                .Where(s => !s.IsDeleted && s.Status == "Active")
                .ToListAsync();

            return View(product);
        }

        // GET: /Home/pIndex — Admin Dashboard.
        [AdminAuthFilter]
        public async Task<IActionResult> pIndex()
        {
            ViewData["Title"] = "Dashboard";
            ViewData["ActivePage"] = "Dashboard";

            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var allOrders = _context.Orders.Include(o => o.OrderDetails).AsQueryable();

            // ── Top stat cards ──
            ViewBag.TotalOrders = await allOrders.CountAsync();
            ViewBag.CompletedOrders = await allOrders.CountAsync(o => o.Status == "Completed");
            // "In Production" here groups "In Production" + "Ready for
            // Pickup" — same grouping Order Management's "In Production"
            // tab uses (see OrdersController.TabToStatuses), and it has to
            // match the donut below: its "Orders in Production" center
            // figure already sums every stage including Ready for Pickup,
            // so this card and that number would otherwise disagree on the
            // same page.
            ViewBag.InProduction = await allOrders.CountAsync(o => o.Status == "In Production" || o.Status == "Ready for Pickup");
            ViewBag.PendingPayment = await allOrders.CountAsync(o =>
                o.Status == "Pending Down Payment" || o.Status == "Pending Cash Payment" || o.Status == "Pending Payment Verification");

            // Revenue = whatever's actually been collected this month (down
            // payments + full settlements) — not the full order value of
            // unpaid/partially-paid orders, so this lines up with the
            // "Amount Paid" figure Reports → Billing shows for the same range.
            // Revenue = only payments that have been approved/confirmed.
            // Waiting for Verification and Rejected payments are NOT revenue.
            //
            // Partially Paid = approved down payment
            // Fully Paid     = full order amount
            // Everything else = 0
            var monthOrders = await allOrders
                .Where(o => o.OrderDate >= monthStart &&
                            o.OrderDate < monthEnd &&
                            o.Status != "Cancelled")
                .ToListAsync();

            ViewBag.TotalRevenue = monthOrders.Sum(o =>
                o.PaymentStatus == "Fully Paid"
                    ? o.TotalAmount
                    : o.PaymentStatus == "Partially Paid"
                        ? o.DownPaymentAmount
                        : 0m);

            // ── Production Overview donut — orders currently In Production,
            // broken down by which of the 6 stations they're sitting at ──
            var stageNames = new[] { "Cutting", "Printing", "Sewing", "Trimming", "Quality Check", "Ready for Pickup" };
            var inProductionOrders = await allOrders.Where(o => o.Status == "In Production" || o.Status == "Ready for Pickup").ToListAsync();
            var stageCounts = new int[6];
            foreach (var o in inProductionOrders)
            {
                int idx = o.Status == "Ready for Pickup" ? 5 : Math.Clamp(o.ProductionStage, 0, 4);
                stageCounts[idx]++;
            }
            ViewBag.StageNames = stageNames;
            ViewBag.StageCounts = stageCounts;
            ViewBag.StageTotal = stageCounts.Sum();

            // ── Revenue Overview bar chart — collected revenue by week,
            // this month ──
            var weeks = new decimal[5];
            foreach (var o in monthOrders)
            {
                int weekIdx = Math.Min((o.OrderDate.Day - 1) / 7, 4);
                decimal amt = o.PaymentStatus == "Fully Paid"
                    ? o.TotalAmount
                    : o.PaymentStatus == "Partially Paid"
                        ? o.DownPaymentAmount
                        : 0m; 
                weeks[weekIdx] += amt;
            }
            ViewBag.WeeklyRevenue = weeks;

            // ── Recent Orders ──
            ViewBag.RecentOrders = await allOrders.OrderByDescending(o => o.OrderDate).Take(5).ToListAsync();

            // ── Production Alerts ──
            var stalledOrders = await allOrders.Where(o => o.Status == "In Production" &&
                o.ProductionStageUpdatedAt != null && o.ProductionStageUpdatedAt < now.AddDays(-3)).CountAsync();
            ViewBag.StalledCount = stalledOrders;
            ViewBag.WaitingVerificationCount = await allOrders.CountAsync(o => o.Status == "Pending Payment Verification");
            ViewBag.ReadyForPickupCount = await allOrders.CountAsync(o => o.Status == "Ready for Pickup");

            // ── System Overview ──
            ViewBag.TotalProducts = await _context.Products.CountAsync(p => !p.IsDeleted);
            ViewBag.TotalCustomers = await allOrders
                .Select(o => (o.CustomerEmail ?? o.CustomerPhone ?? o.CustomerName).ToLower())
                .Distinct().CountAsync();

            return View();
        }

        public IActionResult hWishlist()
        {
            return View();
        }

        // GET: /Home/hAbout — static "About Us" page, no auth, no DB reads.
        //
        // Connected to:
        //   View  : Views/Home/hAbout.cshtml
        //   Styles: wwwroot/css/home/about.css
        public IActionResult hAbout()
        {
            return View();
        }

        public IActionResult hCart()
        {
            return View();
        }

        // ── Account-tied Cart / Wishlist sync ───────────────────────
        // Guests (not logged in) work purely off localStorage in
        // storefront.js, same as always. These four endpoints are what
        // storefront.js calls ONLY when a Customer is logged in, so the
        // cart/wishlist follow the account instead of being stuck in one
        // browser — see CartItem.cs / WishlistItem.cs for why whole-list
        // replace is used instead of per-line diffing.

        // GET: /Home/hGetCart — the logged-in Customer's saved cart.
        // Empty array if not logged in (storefront.js only calls this
        // when it already knows CF_CUSTOMER_LOGGED_IN is true, but this
        // guards against a stale session either way).
        [HttpGet]
        public async Task<IActionResult> hGetCart()
        {
            int? customerId = HttpContext.Session.GetInt32("CustomerID");
            if (customerId == null) return Json(new object[0]);

            var items = await _context.CartItems.AsNoTracking()
                .Where(c => c.CustomerID == customerId.Value)
                .OrderBy(c => c.CartItemID)
                .Select(c => new
                {
                    lineId = "srv_" + c.CartItemID,
                    productId = c.ProductID,
                    name = c.Name,
                    price = c.Price,
                    image = c.Image,
                    qty = c.Qty,
                    size = c.Size,
                    color = c.Color,
                    service = c.Service,
                    servicePrice = c.ServicePrice,
                    printLocation = c.PrintLocation,
                    stockQuantity = c.StockQuantity,
                    isCustomOrder = c.IsCustomOrder,
                    designImage = c.DesignImage,
                    locationNote = c.LocationNote,
                    hasFrontDesign = c.HasFrontDesign,
                    hasBackDesign = c.HasBackDesign,
                    designCount = c.DesignCount
                })
                .ToListAsync();

            return Json(items);
        }

        // POST: /Home/hSyncCart — replaces the logged-in Customer's saved
        // cart with the array storefront.js currently has (whole-cart
        // replace, same semantics as saveCart() client-side).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomerAuthFilter]
        public async Task<IActionResult> hSyncCart(string itemsJson)
        {
            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;

            var existing = _context.CartItems.Where(c => c.CustomerID == customerId);
            _context.CartItems.RemoveRange(existing);

            if (!string.IsNullOrWhiteSpace(itemsJson))
            {
                List<CartSyncLine>? lines = null;
                try { lines = System.Text.Json.JsonSerializer.Deserialize<List<CartSyncLine>>(itemsJson); }
                catch { /* malformed payload — treat as empty cart rather than fail the request */ }

                if (lines != null)
                {
                    foreach (var l in lines)
                    {
                        _context.CartItems.Add(new CartItem
                        {
                            CustomerID = customerId,
                            LineKey = string.Join("|", l.productId, l.size, l.color, l.service, l.printLocation),
                            ProductID = l.productId,
                            Name = l.name,
                            Price = l.price,
                            Image = l.image,
                            Qty = l.qty <= 0 ? 1 : l.qty,
                            Size = l.size,
                            Color = l.color,
                            Service = l.service,
                            ServicePrice = l.servicePrice,
                            PrintLocation = l.printLocation,
                            StockQuantity = l.stockQuantity,
                            IsCustomOrder = l.isCustomOrder,
                            DesignImage = l.designImage,
                            LocationNote = l.locationNote,
                            HasFrontDesign = l.hasFrontDesign,
                            HasBackDesign = l.hasBackDesign,
                            DesignCount = l.designCount,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }

        // GET: /Home/hGetWishlist — the logged-in Customer's saved wishlist.
        [HttpGet]
        public async Task<IActionResult> hGetWishlist()
        {
            int? customerId = HttpContext.Session.GetInt32("CustomerID");
            if (customerId == null) return Json(new object[0]);

            var items = await _context.WishlistItems.AsNoTracking()
                .Where(w => w.CustomerID == customerId.Value)
                .OrderBy(w => w.WishlistItemID)
                .Select(w => new { productId = w.ProductID, name = w.Name, price = w.Price, image = w.Image, category = w.Category })
                .ToListAsync();

            return Json(items);
        }

        // POST: /Home/hSyncWishlist — replaces the logged-in Customer's
        // saved wishlist, same whole-list replace semantics as hSyncCart.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomerAuthFilter]
        public async Task<IActionResult> hSyncWishlist(string itemsJson)
        {
            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;

            var existing = _context.WishlistItems.Where(w => w.CustomerID == customerId);
            _context.WishlistItems.RemoveRange(existing);

            if (!string.IsNullOrWhiteSpace(itemsJson))
            {
                List<WishlistSyncLine>? lines = null;
                try { lines = System.Text.Json.JsonSerializer.Deserialize<List<WishlistSyncLine>>(itemsJson); }
                catch { /* malformed payload — treat as empty wishlist rather than fail the request */ }

                if (lines != null)
                {
                    foreach (var l in lines)
                    {
                        _context.WishlistItems.Add(new WishlistItem
                        {
                            CustomerID = customerId,
                            ProductID = l.productId,
                            Name = l.name,
                            Price = l.price,
                            Image = l.image,
                            Category = l.category
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }

        // Shapes matching the JSON storefront.js already sends for a cart
        // line (see buildCartItem() in product-details.js) / wishlist
        // entry — kept private since nothing outside this sync pair needs
        // them.
        private class CartSyncLine
        {
            public int productId { get; set; }
            public string? name { get; set; }
            public decimal price { get; set; }
            public string? image { get; set; }
            public int qty { get; set; }
            public string? size { get; set; }
            public string? color { get; set; }
            public string? service { get; set; }
            public decimal servicePrice { get; set; }
            public string? printLocation { get; set; }
            public int? stockQuantity { get; set; }
            public bool isCustomOrder { get; set; }
            public string? designImage { get; set; }
            public string? locationNote { get; set; }
            public bool hasFrontDesign { get; set; }
            public bool hasBackDesign { get; set; }
            public int designCount { get; set; }
        }

        private class WishlistSyncLine
        {
            public int productId { get; set; }
            public string? name { get; set; }
            public decimal price { get; set; }
            public string? image { get; set; }
            public string? category { get; set; }
        }

        // GET: /Home/hBilling — Billing & Payment page. Normally requires a
        // logged-in Customer (billing info pulled straight from their
        // account, read-only). Sales staff reach this same page from the
        // admin "Storefront" link (same browser session, so StaffRole is
        // already set — no separate login step needed) to ring up walk-in
        // customers; for them there's no Customer account to read from, so
        // the Billing Information fields render editable instead, and the
        // sales rep types the walk-in customer's details in directly.
        //
        // Connected to:
        //   View   : Views/Home/hBilling.cshtml
        //   Styles : wwwroot/css/home/billing-payment.css
        //   Script : wwwroot/js/billing-payment.js
        public async Task<IActionResult> hBilling()
        {
            bool isSalesMode = HttpContext.Session.GetString("StaffRole") == "Sales";
            int? customerId = HttpContext.Session.GetInt32("CustomerID");

            if (!isSalesMode && customerId == null)
                return RedirectToAction("Login", "Account", new { returnUrl = "/Home/hBilling" });

            ViewBag.IsSalesMode = isSalesMode;

            if (isSalesMode)
            {
                ViewBag.BillingName = "";
                ViewBag.BillingEmail = "";
                ViewBag.BillingPhone = "";
                ViewBag.BillingAddress = "";
            }
            else
            {
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerID == customerId);
                ViewBag.BillingName = customer == null ? "Guest Customer" : $"{customer.FirstName} {customer.LastName}";
                ViewBag.BillingEmail = customer?.Email ?? "";
                ViewBag.BillingPhone = customer?.Phone ?? "";
                ViewBag.BillingAddress = customer?.Address ?? "";
            }
            return View();
        }

        // GET: /Home/hOrderDetails/5 — customer-facing single-order status
        // page. Reached two ways, distinguished by the "from" query
        // param: (1) the "View Order Status" button right after checkout
        // (no "from"), and (2) clicking an order card on the Track Order
        // list (from=track — see hTrackOrder.cshtml), which used to open
        // this same content in a popup modal and now navigates here as a
        // full page instead. The view adjusts its heading/back-link/
        // bottom actions based on which flow it came from.
        //
        // Connected to:
        //   View   : Views/Home/hOrderDetails.cshtml
        //   Styles : wwwroot/css/home/order-details.css
        [CustomerAuthFilter]
        public async Task<IActionResult> hOrderDetails(int id, string? from)
        {
            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Service)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Category)
                .FirstOrDefaultAsync(o => o.OrderID == id && o.CustomerID == customerId);

            if (order == null) return NotFound();

            // Same ActivityLogs-based history hOrderDetailsPanel used to
            // load for the popup — needed here now too so "Order
            // Timeline" is populated on this page as well.
            ViewBag.TimelineLogs = await _context.ActivityLogs
                .Where(l => l.OrderID == id && (l.Category == "Billing" || l.Category == "Production"))
                .OrderBy(l => l.Timestamp)
                .ToListAsync();

            ViewBag.From = from;

            return View(order);
        }

        // GET: /Home/hTrackOrder — the "Track Order" nav link. Requires a
        // logged-in Customer (see CustomerAuthFilter); lists every order
        // placed under their account.
        [CustomerAuthFilter]
        public async Task<IActionResult> hTrackOrder(int page = 1)
        {
            ViewData["Title"] = "Track Order";
            const int pageSize = 10;

            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;

            var query = _context.Orders
                .Where(o => o.CustomerID == customerId)
                .OrderByDescending(o => o.OrderDate);

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page < 1) page = 1;

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.StartItem = totalCount == 0 ? 0 : (page - 1) * pageSize + 1;
            ViewBag.EndItem = Math.Min(page * pageSize, totalCount);

            var orders = await query
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Home/hOrderDetailsPanel/5 — the same order-tracking body
        // shown on hOrderDetails, returned as a partial for the Track
        // Order list's popup modal (see loadMyOrderDetails() in
        // hTrackOrder.cshtml). Mirrors OrdersController.oDetailsPanel.
        [HttpGet]
        [CustomerAuthFilter]
        public async Task<IActionResult> hOrderDetailsPanel(int id)
        {
            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Service)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Category)
                .FirstOrDefaultAsync(o => o.OrderID == id && o.CustomerID == customerId);

            if (order == null) return NotFound();

            // "Order Timeline" card — same ActivityLogs-based history as
            // the admin Overview popup (ProductionController.pDetailsPanel),
            // reused here for the customer-facing view.
            ViewBag.TimelineLogs = await _context.ActivityLogs
                .Where(l => l.OrderID == id && (l.Category == "Billing" || l.Category == "Production"))
                .OrderBy(l => l.Timestamp)
                .ToListAsync();

            return PartialView("_CustomerOrderTrackingBody", order);
        }

        // Shared by every hNotifications* action below — a Customer-
        // audience Notification is "mine" if it's attached to an order
        // placed under my account.
        private IQueryable<Notification> MyNotificationsQuery()
        {
            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;

            return _context.Notifications
                .Include(n => n.Order)
                .Where(n => n.Audience == "Customer" && n.Order != null && n.Order.CustomerID == customerId);
        }

        // GET: /Home/hNotifications — full notification list, reached from
        // the navbar bell's "View all" link.
        [CustomerAuthFilter]
        public async Task<IActionResult> hNotifications(string? tab, int page = 1)
        {
            int pageSize = 20;
            var baseQuery = MyNotificationsQuery();

            ViewBag.UnreadCount = await baseQuery.CountAsync(n => !n.IsRead);

            var query = baseQuery;
            if (string.Equals(tab, "Unread", StringComparison.OrdinalIgnoreCase))
                query = query.Where(n => !n.IsRead);

            int totalCount = await query.CountAsync();
            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tab = tab ?? "All";
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewData["Title"] = "Notifications";

            return View(notifications);
        }

        // GET: /Home/hNotificationsBadge — polled by navbar-notifications.js.
        [HttpGet]
        public async Task<IActionResult> hNotificationsBadge()
        {
            if (HttpContext.Session.GetInt32("CustomerID") == null) return Json(new { count = 0 });
            int count = await MyNotificationsQuery().CountAsync(n => !n.IsRead);
            return Json(new { count });
        }

        // GET: /Home/hNotificationsDropdown — small recent panel for the
        // navbar bell's popover.
        [HttpGet]
        public async Task<IActionResult> hNotificationsDropdown()
        {
            if (HttpContext.Session.GetInt32("CustomerID") == null) return PartialView("_CustomerNotificationDropdown", new List<Notification>());
            var recent = await MyNotificationsQuery().OrderByDescending(n => n.CreatedAt).Take(8).ToListAsync();
            return PartialView("_CustomerNotificationDropdown", recent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomerAuthFilter]
        public async Task<IActionResult> hMarkNotificationRead(int id)
        {
            var notif = await MyNotificationsQuery().FirstOrDefaultAsync(n => n.NotificationID == id);
            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomerAuthFilter]
        public async Task<IActionResult> hMarkAllNotificationsRead()
        {
            var unread = await MyNotificationsQuery().Where(n => !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction(nameof(hNotifications));
        }

        // POST: /Home/hCancelOrder/5 — customer-side "Cancel Order" from
        // the Track Order popup. Mirrors OrdersController.IsEditableStatus:
        // once production has started (or the order is already done/
        // cancelled), it can no longer be cancelled.
        private static bool IsCancellableStatus(string status) =>
            status is "Pending Down Payment" or "Pending Cash Payment" or "Pending Payment Verification"
                   or "Confirmed" or "Design Review";

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomerAuthFilter]
        public async Task<IActionResult> hCancelOrder(int id)
        {
            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id && o.CustomerID == customerId);
            if (order == null) return NotFound();

            if (!IsCancellableStatus(order.Status))
            {
                return Json(new { success = false, message = $"This order can no longer be cancelled — it's already {order.Status}." });
            }

            order.Status = "Cancelled";
            await StockService.RestoreStockAsync(_context, order);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /Home/hReuploadProof — lets a customer fix a rejected Gcash
        // payment (e.g. wrong/blurry screenshot) by uploading a new proof
        // and/or correcting the reference number, instead of the order
        // staying Cancelled forever. Only offered for Gcash — Cash orders
        // have no proof to dispute and never reach "Rejected" (see
        // BillingController.bReject's Cash guard). Stock was given back
        // when the order was rejected, so it's re-reserved here; if the
        // item has since sold out elsewhere, the resubmission is blocked
        // rather than silently overselling.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueLengthLimit = 50_000_000, MultipartBodyLengthLimit = 50_000_000)]
        [CustomerAuthFilter]
        public async Task<IActionResult> hReuploadProof(int id, string referenceNumber, IFormFile proofFile)
        {
            int customerId = HttpContext.Session.GetInt32("CustomerID")!.Value;
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderID == id && o.CustomerID == customerId);
            if (order == null) return NotFound();

            if (order.PaymentMethod != "Gcash" || order.Status != "Cancelled" || order.PaymentStatus != "Rejected")
                return Json(new { success = false, message = "This order isn't eligible for a proof re-upload." });

            if (string.IsNullOrWhiteSpace(referenceNumber))
                return Json(new { success = false, message = "Please enter your Gcash reference number." });

            if (proofFile == null || proofFile.Length == 0)
                return Json(new { success = false, message = "Please attach your payment screenshot." });

            var requestedQty = order.OrderDetails
                .Where(d => d.ProductID.HasValue)
                .GroupBy(d => d.ProductID!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

            string? stockError = await StockService.TryReserveStockAsync(_context, requestedQty);
            if (stockError != null)
                return Json(new { success = false, message = stockError });

            string folder = Path.Combine(GetWwwRoot(), "uploads", "payment-proofs");
            Directory.CreateDirectory(folder);
            string fileName = Guid.NewGuid() + Path.GetExtension(proofFile.FileName);
            using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
            {
                await proofFile.CopyToAsync(stream);
            }

            order.ProofFilePath = "/uploads/payment-proofs/" + fileName;
            order.ReferenceNumber = referenceNumber;
            order.Status = "Pending Payment Verification";
            order.PaymentStatus = "Waiting for Verification";
            order.PaymentNote = null;
            await _context.SaveChangesAsync();

            // A resubmission is a NEW payment attempt on top of the earlier
            // rejected one — keep both rows so the audit trail shows the
            // full history rather than overwriting the rejected attempt.
            _context.Payments.Add(new Payment
            {
                OrderID = order.OrderID,
                PaymentType = "Down Payment",
                Amount = order.DownPaymentAmount,
                PaymentMethod = order.PaymentMethod,
                ReferenceNumber = order.ReferenceNumber,
                ProofFilePath = order.ProofFilePath,
                PaymentStatus = "Waiting for Verification",
                PaymentDate = DateTime.Now,
                Remarks = "Resubmitted after rejection"
            });
            await _context.SaveChangesAsync();

            await ActivityLogger.LogAsync(_context, HttpContext, "Billing", "Re-uploaded Payment Proof",
                $"{order.CustomerName} re-submitted payment proof for order #ORD-{order.OrderID:D3}.", order.OrderID);
            await NotificationService.NotifyAdminAsync(_context, order.OrderID, "ProofResubmitted",
                "Payment Proof Re-submitted",
                $"Order #ORD-{order.OrderID:D3} — {order.CustomerName} re-uploaded their payment proof for review.");

            return Json(new { success = true });
        }


        // from the shopper's selected cart lines and records the payment
        // method/reference/proof. Cart lines are sent as a JSON string
        // (cartLinesJson) because the cart itself lives in localStorage,
        // not a server session.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueLengthLimit = 50_000_000, MultipartBodyLengthLimit = 50_000_000)]
        public async Task<IActionResult> SubmitOrder(
            string fullName, string email, string phone, string? address,
            string paymentMethod, string? referenceNumber,
            string cartLinesJson, IFormFile? proofFile, decimal? amountPaid)
        {
            // Wrapped in try/catch and always returning Json(): this is an
            // AJAX endpoint (billing-payment.js expects JSON back no matter
            // what), so any exception here must NOT be allowed to fall
            // through to the app-wide UseExceptionHandler("/Auth/Login") in
            // Program.cs — that redirect returns HTML, which the front-end's
            // response.json() call can't parse, and that failure was
            // showing up to the shopper as a misleading "Could not reach
            // the server" message even though the server was reached fine.
            try
            {
                int? customerId = HttpContext.Session.GetInt32("CustomerID");
                bool isSalesMode = HttpContext.Session.GetString("StaffRole") == "Sales";
                if (customerId == null && !isSalesMode)
                {
                    return Json(new
                    {
                        success = false,
                        requireLogin = true,
                        message = "Please log in or create an account to check out.",
                        redirectUrl = Url.Action("Login", "Account", new { returnUrl = "/Home/hBilling" })
                    });
                }

                return await ProcessSubmitOrder(customerId, fullName, email, phone, address, paymentMethod, referenceNumber, cartLinesJson, proofFile, amountPaid);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Something went wrong while placing your order: " + ex.Message });
            }
        }

        private async Task<IActionResult> ProcessSubmitOrder(
            int? customerId, string fullName, string email, string phone, string? address,
            string paymentMethod, string? referenceNumber,
            string cartLinesJson, IFormFile? proofFile, decimal? amountPaid)
        {
            List<CheckoutLine>? lines;
            try
            {
                lines = System.Text.Json.JsonSerializer.Deserialize<List<CheckoutLine>>(
                    cartLinesJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return Json(new { success = false, message = "Could not read your cart items. Please try again." });
            }

            if (lines == null || lines.Count == 0)
                return Json(new { success = false, message = "No items were selected for checkout." });

            if (string.IsNullOrWhiteSpace(paymentMethod))
                paymentMethod = "Gcash";

            if (paymentMethod == "Gcash" && string.IsNullOrWhiteSpace(referenceNumber))
                return Json(new { success = false, message = "Please enter your Gcash reference number." });

            string? proofPath = null;
            if (proofFile != null && proofFile.Length > 0)
            {
                string folder = Path.Combine(GetWwwRoot(), "uploads", "payment-proofs");
                Directory.CreateDirectory(folder);
                string fileName = Guid.NewGuid() + Path.GetExtension(proofFile.FileName);
                using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
                {
                    await proofFile.CopyToAsync(stream);
                }
                proofPath = "/uploads/payment-proofs/" + fileName;
            }

            // Resolve each line's real ProductID (if any) up front, so we can
            // validate/reserve stock BEFORE creating the order — a line that
            // fails the stock check should abort the whole order, not leave
            // some products already decremented.
            var lineProductIds = new Dictionary<CheckoutLine, int?>();
            foreach (var line in lines)
            {
                var m = System.Text.RegularExpressions.Regex.Match(line.ProductId ?? "", @"^custom-(\d+)$");
                if (!m.Success && int.TryParse(line.ProductId, out int pid))
                {
                    bool exists = await _context.Products.AnyAsync(p => p.ProductID == pid);
                    lineProductIds[line] = exists ? pid : (int?)null;
                }
                else
                {
                    lineProductIds[line] = null;
                }
            }

            var requestedQtyByProduct = new Dictionary<int, int>();
            foreach (var line in lines)
            {
                if (lineProductIds[line].HasValue)
                {
                    int pid = lineProductIds[line]!.Value;
                    requestedQtyByProduct[pid] = requestedQtyByProduct.GetValueOrDefault(pid) + line.Qty;
                }
            }

            string? stockError = await StockService.TryReserveStockAsync(_context, requestedQtyByProduct);
            if (stockError != null)
                return Json(new { success = false, message = stockError });

            decimal subtotal = lines.Sum(l => (l.Price + l.ServicePrice) * l.Qty);

            // Customers are no longer locked to exactly 50% — they can pay
            // any amount at or above the 50% minimum (e.g. 70%) via the
            // "Amount you paid/will pay" box on the Gcash/Cash panel
            // (billing-payment.js). Whatever they typed comes in as
            // amountPaid; the 50% minimum is still enforced here since the
            // client-side check can be bypassed.
            decimal minDownPayment = Math.Round(subtotal * 0.5m, 2);
            decimal downPayment = amountPaid.HasValue && amountPaid.Value > 0
                ? Math.Round(amountPaid.Value, 2)
                : minDownPayment;

            if (downPayment < minDownPayment)
                return Json(new { success = false, message = $"Amount paid must be at least ₱{minDownPayment:N2} (50% of the total)." });

            // Cap at the subtotal — an over-typed amount shouldn't produce a
            // negative remaining balance.
            if (downPayment > subtotal)
                downPayment = subtotal;

            decimal remaining = subtotal - downPayment;

            // Payment always starts "Waiting for Verification" — whether Gcash
            // or Cash, staff still need to manually confirm the money
            // actually came in (matches the confirmation screen the
            // customer sees) before it moves to Partially Paid / Fully Paid
            // from the admin Billing → Payment Verification screen.
            string paymentStatus = "Waiting for Verification";

            var order = new Order
            {
                CustomerID = customerId,
                CustomerName = string.IsNullOrWhiteSpace(fullName) ? "Guest Customer" : fullName,
                CustomerEmail = email,
                CustomerPhone = phone,
                CustomerAddress = address,
                TotalAmount = subtotal,
                // Gcash orders already have their proof/reference number by
                // this point (validated above), so they land straight in
                // "Pending Payment Verification". Cash orders haven't paid
                // anything yet, so they land in "Pending Cash Payment" —
                // both get bumped to "Confirmed" automatically once Billing
                // approves the payment (see PaymentWorkflow.ApplyPaymentStatus).
                Status = paymentMethod == "Cash" ? "Pending Cash Payment" : "Pending Payment Verification",
                PaymentMethod = paymentMethod,
                PaymentStatus = paymentStatus,
                ReferenceNumber = referenceNumber,
                ProofFilePath = proofPath,
                DownPaymentAmount = downPayment,
                RemainingBalance = remaining,
                ProductionStage = 0
            };

            foreach (var line in lines)
            {
                var detail = new OrderDetail
                {
                    Quantity = line.Qty,
                    UnitPrice = line.Price + line.ServicePrice,
                    PrintLocation = line.PrintLocation,
                    SelectedSize = line.Size,
                    SelectedColor = line.Color,
                    ProductNameSnapshot = line.Name,
                    ServiceNameSnapshot = line.Service,
                    ServicePriceSnapshot = line.ServicePrice,
                    ImageSnapshotPath = SaveImageSnapshotIfPresent(line.Image),
                    DesignFilePath = SaveImageSnapshotIfPresent(line.DesignImage, "order-designs")
                };

                var customMatch = System.Text.RegularExpressions.Regex.Match(line.ProductId ?? "", @"^custom-(\d+)$");
                if (customMatch.Success)
                {
                    detail.IsCustomOrder = true;
                    detail.CategoryID = int.Parse(customMatch.Groups[1].Value);
                }
                else if (lineProductIds[line].HasValue)
                {
                    detail.ProductID = lineProductIds[line]!.Value;
                }
                else if (int.TryParse(line.ProductId, out _))
                {
                    // Parsed as a number but didn't match a real product
                    // (already checked above) — treat as a one-off item.
                    detail.IsCustomOrder = true;
                }
                else
                {
                    detail.IsCustomOrder = true;
                }

                order.OrderDetails.Add(detail);
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Real, auditable record of this down payment attempt — see
            // Models/Payment.cs. Order.PaymentStatus/DownPaymentAmount etc.
            // still drive every existing screen; this just adds the trail.
            _context.Payments.Add(new Payment
            {
                OrderID = order.OrderID,
                PaymentType = "Down Payment",
                Amount = order.DownPaymentAmount,
                PaymentMethod = order.PaymentMethod,
                ReferenceNumber = order.ReferenceNumber,
                ProofFilePath = order.ProofFilePath,
                PaymentStatus = "Waiting for Verification",
                PaymentDate = DateTime.Now
            });
            await _context.SaveChangesAsync();

            await NotificationService.NotifyAdminAsync(_context, order.OrderID, "OrderPlaced",
                "New Order Placed",
                $"Order #ORD-{order.OrderID:D3} — {order.CustomerName} placed a new order (₱{order.TotalAmount:N2}).");

            return Json(new { success = true, orderId = order.OrderID, lineIds = lines.Select(l => l.LineId) });
        }

        // Shape of each cart line as sent from billing-payment.js — mirrors
        // the localStorage cart item shape from storefront.js.
        public class CheckoutLine
        {
            public string? LineId { get; set; }

            // Real products store productId as a JS number; custom orders
            // store it as a string like "custom-3" — this reads either.
            [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleStringConverter))]
            public string? ProductId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public decimal ServicePrice { get; set; }
            public int Qty { get; set; } = 1;
            public string? Size { get; set; }
            public string? Color { get; set; }
            public string? Service { get; set; }
            public string? PrintLocation { get; set; }

            // Composited thumbnail from the cart (garment + design, or the
            // plain product photo) as a base64 data URL — saved to disk in
            // SubmitOrder() so admin Order Details has a real image to show.
            public string? Image { get; set; }

            // Raw uploaded design file (no garment behind it) as a base64
            // data URL — set only for Customize-page lines. Saved to disk
            // in SubmitOrder() and stored on OrderDetail.DesignFilePath,
            // separate from Image/ImageSnapshotPath above.
            public string? DesignImage { get; set; }
        }

        // Decodes a "data:image/...;base64,...." string and saves it under
        // wwwroot/uploads/order-items/, returning the relative path to store
        // on the OrderDetail row. Returns null for anything that isn't a
        // data URL (e.g. already a server-relative path, or missing).
        // Decodes a "data:image/...;base64,...." string and saves it under
        // wwwroot/uploads/order-items/, returning the relative path to store
        // on the OrderDetail row. Custom (Customize-page) orders send a
        // composited canvas thumbnail this way. Pre-designed catalog items
        // never do — their cart image is just the product photo's normal
        // server path (e.g. "/images/categories/t-shirt.png") — so that
        // path is stored directly instead of being discarded, which is why
        // pre-designed line items previously showed no thumbnail at all in
        // the admin production panel.
        private string? SaveImageSnapshotIfPresent(string? dataUrl, string subfolder = "order-items")
        {
            if (string.IsNullOrWhiteSpace(dataUrl))
                return null;

            if (!dataUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return dataUrl; // already a real, servable path — use as-is

            try
            {
                int commaIndex = dataUrl.IndexOf(',');
                if (commaIndex < 0) return null;

                string header = dataUrl.Substring(0, commaIndex); // e.g. "data:image/jpeg;base64"
                string base64 = dataUrl.Substring(commaIndex + 1);
                byte[] bytes = Convert.FromBase64String(base64);

                string ext = header.Contains("png") ? ".png" : header.Contains("webp") ? ".webp" : ".jpg";

                string folder = Path.Combine(GetWwwRoot(), "uploads", subfolder);
                Directory.CreateDirectory(folder);
                string fileName = Guid.NewGuid() + ext;
                System.IO.File.WriteAllBytes(Path.Combine(folder, fileName), bytes);

                return "/uploads/" + subfolder + "/" + fileName;
            }
            catch
            {
                return null; // don't let a bad image snapshot block the whole order
            }
        }

        // Reads a JSON string OR number token into a C# string — needed
        // because cart line "productId" is a number for real products but
        // a string like "custom-3" for Customize-page orders.
        public class FlexibleStringConverter : System.Text.Json.Serialization.JsonConverter<string?>
        {
            public override string? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            {
                return reader.TokenType switch
                {
                    System.Text.Json.JsonTokenType.String => reader.GetString(),
                    System.Text.Json.JsonTokenType.Number => reader.TryGetInt64(out long l) ? l.ToString() : reader.GetDouble().ToString(),
                    System.Text.Json.JsonTokenType.Null => null,
                    _ => null
                };
            }
            public override void Write(System.Text.Json.Utf8JsonWriter writer, string? value, System.Text.Json.JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }

        private string GetWwwRoot()
        {
            if (!string.IsNullOrEmpty(_environment.WebRootPath) &&
                Directory.Exists(_environment.WebRootPath))
                return _environment.WebRootPath;
            return Path.Combine(_environment.ContentRootPath, "wwwroot");
        }
    }
}
