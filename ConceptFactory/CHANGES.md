# CHANGES — Login, Roles & Account Security (ported onto your newer build)

This upload was newer than the version I last added the login system to — it
already had the stock reservation/restoration work, the Gcash proof
re-upload flow, and the improved Reports revenue math, but none of the
login/security work yet. This delta ports that login system onto THIS
codebase, without touching any of the newer stuff. Everything below is the
same login system from before; nothing about stock, production estimates,
or reports was changed.

## What changed

**Admin login** — still the same seeded `admin@conceptfactory.com` /
`admin123`, but the password is now hashed (PBKDF2, salted) instead of
stored as plain text. Existing installs get their password hashed in place
automatically the first time the app starts (`Utils/PasswordMigration.cs`).

**Sales & Production staff logins** — `UsersController` (User Management →
Add Staff) now takes a real password when you create a Sales or Production
account. They log in at the same `/Auth/Login` screen as Admin. Once in:

- **Sales** sees Orders, Billing, Notifications, and a "Storefront" link in
  the sidebar for walk-in orders.
- **Production** sees the Production module (Overview + all stations) and
  Notifications only.
- **Admin** still sees everything, unchanged.

Straying outside their area (typing the URL directly) bounces them to their
own default page instead of a dead end.

**Customer accounts** — new Register/Login at `/Account`. Browsing the
storefront never requires an account. Checking out, Track Order, order
details, notifications, cancelling an order, and re-uploading a rejected
Gcash proof now all require one — you'll be sent to `/Account/Login` with a
return trip back to checkout after logging in.

**Account lockout (CIA)** — three wrong passwords in a row locks an account
(Admin, Staff, or Customer) for 10 seconds before the next attempt, with a
"try again in N seconds" message.

**Password recovery (Customer only)** — "Forgot Password?" sends a 6-digit
code to the account's email via Gmail SMTP, valid 15 minutes. Fill in
`appsettings.json` → `EmailSettings` with a Gmail address and an **App
Password** (Google Account → Security → 2-Step Verification → App
Passwords). Until that's set, the code shows on-screen in Development so
you can still test it.

## New files
- `Utils/PasswordHasher.cs`, `Utils/AccountSecurity.cs`, `Utils/PasswordMigration.cs`, `Utils/EmailService.cs`
- `Models/Customer.cs`
- `Filters/CustomerAuthFilter.cs` (and `Filters/AdminAuthFilter.cs` rewritten to be role-aware)
- `Controllers/AccountController.cs` + `Views/Account/*.cshtml`

## Touched (login-only additions — your other logic in these files is untouched)
- `AuthController` (unified Admin + Staff login, hashing, lockout)
- `UsersController` + `uCreate`/`uEdit` views (real staff passwords)
- `HomeController` — checkout, track order, order details, notifications,
  `hCancelOrder`, and `hReuploadProof` are now gated to the logged-in
  customer (ownership checked via `CustomerID`); your `StockService`
  calls and the re-upload flow itself are unchanged
- `Models/AdminUser.cs`, `Models/User.cs` (lockout fields), `Controllers/Order.cs` (CustomerID + Customer nav)
- `Data/ApplicationDbContext.cs`, `Database/Setup.sql` (Customers table, new columns — added in the same spots, your commented-out cleanup queries at the bottom of Setup.sql are untouched)
- `Views/Shared/_AdminLayout.cshtml` (role-based sidebar), `Views/Shared/_Layout.cshtml` (account menu)
- `wwwroot/js/billing-payment.js` (redirects to login if checkout is attempted while logged out)
- `Program.cs`, `appsettings.json` (EmailService + startup password migration)
- Role filters (`[AdminAuthFilter(Roles=...)]`) added to Orders/Billing/Products/Services/Reports/Logs/Notifications/Production controllers

## Not done / worth knowing
- Staff (Sales/Production) don't have self-service "Forgot Password" — an
  Admin resets it for them from Edit Staff. Only Customers get the Gmail
  recovery flow.
- Old orders placed before this change have `CustomerID = NULL` — they
  still display fine everywhere, they just aren't tied to a specific
  account.
- `hReuploadProof` is now also gated to the order's owner, same as
  `hCancelOrder` — that wasn't explicitly requested but follows the same
  "customers can only touch their own orders" rule; flag it if you'd
  rather that one stay open.
