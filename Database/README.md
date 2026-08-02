# ConceptFactory — Database Scripts

## Run one file: `Setup.sql`

Open it in SQL Server Management Studio, connect to your SQL Server
instance, hit Execute (F5). It creates the database, every table, and
all seed data (default admin account, categories + the T-Shirts/Jackets
sub-category hierarchy, services, sample products) — organized into
five clearly-labeled sections, top to bottom, in the correct order.

It's safe to run more than once. Every step checks `IF NOT EXISTS`
first, so re-running it won't duplicate data, drop anything, or error
out — that also means it's safe to run against a database that already
has real products/orders in it.

**What's inside `Setup.sql`:**

| Section | What it does |
|---|---|
| 1. Accounts | Roles, Users, AdminUsers tables. Seeds the default admin account. |
| 2. Categories | Base table → the specific sub-type categories (Cotton T-Shirts, Varsity Jackets, etc.) → nesting them under a parent (T-Shirts, Jackets) → one-time cleanup of leftover duplicate categories from before that nesting existed. |
| 3. Services | Printing services offered (DTG, Screen Printing, Embroidery, etc.) |
| 4. Products | Products table, per-color product photos (`ProductColorImages`), and the product↔service link table. Seeds 5 sample products (including a "Waffle Crew T-Shirt" with 6 color photos, for the Waffle T-Shirts sub-category). |
| 5. Orders | Orders + OrderDetails (line items). |

## `_Archive/`

Superseded scripts kept for reference only — **not meant to be run**.
`Setup.sql` already contains everything they did, reorganized into one
file, so there's no need to run these separately anymore.

- `01_Schema.sql`, `02_AddProductColorImages.sql`,
  `03_AddColorCategoryTypes.sql`, `04_AddCategoryHierarchy.sql`,
  `05_CleanupDuplicateCategories.sql` — the original step-by-step
  scripts `Setup.sql` was consolidated from.
- `SQLQuery3_old_scratch_DO_NOT_RUN.sql` — exploratory
  `DELETE FROM Products` / `DELETE FROM Categories` statements from
  earlier development. Running it as-is would wipe your data; kept
  only in case you want to see the reasoning behind an old cleanup.

## Default admin login (seeded by `Setup.sql`)
- Email: `admin@conceptfactory.com`
- Password: `admin123`

Change this before deploying anywhere real.
