using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Utils;
using ConceptFactory.Models;

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

        public IActionResult pIndex()
        {
            return View();
        }

        public IActionResult hWishlist()
        {
            return View();
        }

        public IActionResult hCart()
        {
            return View();
        }

        // GET: /Home/hBilling — Billing & Payment page.
        // No customer accounts/login yet, so the (non-editable) billing
        // info shown here is pulled from the seeded AdminUser record as a
        // stand-in "logged in customer". Once real accounts exist, swap
        // this for the actual logged-in user's info — nothing else on the
        // page needs to change, since it's already rendered as read-only
        // info rather than editable inputs.
        //
        // Connected to:
        //   View   : Views/Home/hBilling.cshtml
        //   Styles : wwwroot/css/home/billing-payment.css
        //   Script : wwwroot/js/billing-payment.js
        public async Task<IActionResult> hBilling()
        {
            var billingInfo = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync();
            ViewBag.BillingName = billingInfo?.FullName ?? "Guest Customer";
            ViewBag.BillingEmail = billingInfo?.Email ?? "";
            ViewBag.BillingPhone = billingInfo?.Phone ?? "";
            // No Address field exists on the stand-in account yet (there's
            // no real customer-accounts system in place), so this starts
            // blank. Order Management's Customer Information card is ready
            // to display it the moment checkout actually starts collecting it.
            ViewBag.BillingAddress = "";
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
        public async Task<IActionResult> hOrderDetails(int id, string? from)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Service)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Category)
                .FirstOrDefaultAsync(o => o.OrderID == id);

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

        // GET: /Home/hTrackOrder — the "Track Order" nav link.
        // No customer accounts/login yet (see hBilling() above for the
        // same pattern), so "the logged-in user" is stood in for by the
        // seeded AdminUser record — every order whose contact info
        // matches that record's email/phone is listed here. Once real
        // accounts exist, swap the match below for the actual logged-in
        // user's ID and this keeps working unchanged.
        public async Task<IActionResult> hTrackOrder(int page = 1)
        {
            ViewData["Title"] = "Track Order";
            const int pageSize = 10;

            var billingInfo = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync();
            var email = billingInfo?.Email;
            var phone = billingInfo?.Phone;

            var query = _context.Orders
                .Where(o => (email != null && o.CustomerEmail != null && o.CustomerEmail.ToLower() == email.ToLower())
                         || (phone != null && o.CustomerPhone != null && o.CustomerPhone == phone))
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
        public async Task<IActionResult> hOrderDetailsPanel(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Service)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Category)
                .FirstOrDefaultAsync(o => o.OrderID == id);

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

        // POST: /Home/hCancelOrder/5 — customer-side "Cancel Order" from
        // the Track Order popup. Mirrors OrdersController.IsEditableStatus:
        // once production has started (or the order is already done/
        // cancelled), it can no longer be cancelled.
        private static bool IsCancellableStatus(string status) =>
            status is "Pending Down Payment" or "Pending Cash Payment" or "Pending Payment Verification"
                   or "Confirmed" or "Design Review";

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> hCancelOrder(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order == null) return NotFound();

            if (!IsCancellableStatus(order.Status))
            {
                return Json(new { success = false, message = $"This order can no longer be cancelled — it's already {order.Status}." });
            }

            order.Status = "Cancelled";
            await _context.SaveChangesAsync();
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
                return await ProcessSubmitOrder(fullName, email, phone, address, paymentMethod, referenceNumber, cartLinesJson, proofFile, amountPaid);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Something went wrong while placing your order: " + ex.Message });
            }
        }

        private async Task<IActionResult> ProcessSubmitOrder(
            string fullName, string email, string phone, string? address,
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
                else if (int.TryParse(line.ProductId, out int realProductId))
                {
                    var productExists = await _context.Products.AnyAsync(p => p.ProductID == realProductId);
                    detail.ProductID = productExists ? realProductId : null;
                    if (!productExists) detail.IsCustomOrder = true;
                }
                else
                {
                    detail.IsCustomOrder = true;
                }

                order.OrderDetails.Add(detail);
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

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
