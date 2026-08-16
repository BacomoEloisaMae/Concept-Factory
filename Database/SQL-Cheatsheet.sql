/* ============================================================
   CONCEPTFACTORY — SQL CHEATSHEET
   ------------------------------------------------------------
   A reference for common things you might need to do while
   working on the database. This file is NOT meant to be run
   top-to-bottom like Setup.sql — copy the one query/section you
   need and run just that.

   Run these in SSMS (or Azure Data Studio) with the ConceptFactory
   database selected.
============================================================ */


/* ============================================================
   0. YOUR TABLES + HOW THEY CONNECT
   ------------------------------------------------------------
   This is the map you need before deleting anything. An arrow
   means "this table points at that one" (has a foreign key to it).

   Roles
     └─ Users.RoleID

   Categories (self-referencing: ParentCategoryID -> Categories.CategoryID)
     └─ Products.CategoryID
     └─ OrderDetails.CategoryID

   Products
     └─ ProductColorImages.ProductID   (ON DELETE CASCADE — auto-deletes)
     └─ ProductServices.ProductID      (ON DELETE CASCADE — auto-deletes)
     └─ OrderDetails.ProductID         (nullable, NOT cascade — blocks delete)

   Services
     └─ ProductServices.ServiceID      (ON DELETE CASCADE — auto-deletes)
     └─ OrderDetails.ServiceID         (nullable, NOT cascade — blocks delete)

   Orders
     └─ OrderDetails.OrderID           (ON DELETE CASCADE — auto-deletes)

   AdminUsers — standalone, nothing references it.

   "ON DELETE CASCADE" means SQL Server deletes the child rows
   for you automatically. Anything NOT marked cascade will block
   the delete with an error until you deal with those rows first
   — see section 3.
============================================================ */


/* ============================================================
   1. VIEWING DATA
============================================================ */

-- See every table that exists in the database.
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;

-- Peek at a table's contents (swap the table name; TOP 50 keeps it small).
SELECT TOP 50 * FROM [dbo].[Products] ORDER BY ProductID DESC;
SELECT TOP 50 * FROM [dbo].[Orders] ORDER BY OrderID DESC;
SELECT TOP 50 * FROM [dbo].[OrderDetails] ORDER BY OrderDetailID DESC;
SELECT TOP 50 * FROM [dbo].[Categories] ORDER BY CategoryID;
SELECT TOP 50 * FROM [dbo].[Services] ORDER BY ServiceID;
SELECT TOP 50 * FROM [dbo].[Users] ORDER BY UserID DESC;
SELECT TOP 50 * FROM [dbo].[AdminUsers];

-- Row count for every table at once (handy "what's in here" overview).
SELECT
    t.NAME AS TableName,
    p.rows AS RowCount
FROM sys.tables t
JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
ORDER BY t.NAME;

-- Categories with their parent's name next to them (easier to read than
-- raw ParentCategoryID numbers) — useful after the subcategory fix.
SELECT c.CategoryID, c.CategoryName, parent.CategoryName AS ParentCategory
FROM [dbo].[Categories] c
LEFT JOIN [dbo].[Categories] parent ON c.ParentCategoryID = parent.CategoryID
ORDER BY ISNULL(parent.CategoryName, c.CategoryName), c.CategoryName;

-- A specific product by name (LIKE = partial match, case-insensitive by default).
SELECT * FROM [dbo].[Products] WHERE ProductName LIKE '%hoodie%';

-- A specific order and all its line items together.
SELECT o.*, d.*
FROM [dbo].[Orders] o
JOIN [dbo].[OrderDetails] d ON d.OrderID = o.OrderID
WHERE o.OrderID = 12;   -- <-- change this


/* ============================================================
   2. DELETING ONE ROW (a single value)
   ------------------------------------------------------------
   Always SELECT the row first to make sure you're targeting the
   right one, THEN delete. Never delete based on a name/text match
   alone without checking — IDs are safer.
============================================================ */

-- Step 1: confirm what you're about to delete.
SELECT * FROM [dbo].[Products] WHERE ProductID = 25;

-- Step 2: delete it (only if nothing blocks it — see section 3 if it errors).
DELETE FROM [dbo].[Products] WHERE ProductID = 25;

-- Same pattern for any table:
DELETE FROM [dbo].[Categories] WHERE CategoryID = 10;
DELETE FROM [dbo].[Services]   WHERE ServiceID = 3;
DELETE FROM [dbo].[Orders]     WHERE OrderID = 12;   -- also deletes its OrderDetails automatically (cascade)

-- Products actually use SOFT delete in this app (an IsDeleted flag,
-- not a real DELETE) — that's what the admin "Delete" button does, and
-- what shows up on the admin "Deleted Items" page. Prefer this over a
-- real DELETE for products so it stays undoable:
UPDATE [dbo].[Products] SET IsDeleted = 1, DeletedAt = GETDATE() WHERE ProductID = 25;
-- To restore it:
UPDATE [dbo].[Products] SET IsDeleted = 0, DeletedAt = NULL WHERE ProductID = 25;


/* ============================================================
   3. DELETING A ROW THAT HAS A FOREIGN KEY POINTING AT IT
   ------------------------------------------------------------
   If you try to delete a row that something else still references,
   SQL Server blocks it with an error like:
     "The DELETE statement conflicted with the REFERENCE constraint..."
   That's SQL Server protecting you from orphaned data. You have two
   options: delete the referencing rows first, or point them
   somewhere else / clear the link.
============================================================ */

-- Example: deleting Product #25, which is used in past orders.
-- First, see what's actually pointing at it:
SELECT * FROM [dbo].[OrderDetails] WHERE ProductID = 25;
SELECT * FROM [dbo].[ProductColorImages] WHERE ProductID = 25;   -- auto-deletes, just FYI
SELECT * FROM [dbo].[ProductServices] WHERE ProductID = 25;      -- auto-deletes, just FYI

-- OrderDetails.ProductID is NOT cascade, so it blocks the delete.
-- Since past orders should keep their history, DON'T delete those rows —
-- instead just soft-delete the product (see section 2) so it disappears
-- from the storefront but old orders still make sense.
-- If you genuinely want a hard delete anyway (e.g. a test product with
-- no real orders), clear the link first:
UPDATE [dbo].[OrderDetails] SET ProductID = NULL WHERE ProductID = 25;
DELETE FROM [dbo].[Products] WHERE ProductID = 25;

-- Example: deleting a Category that has Products in it.
SELECT * FROM [dbo].[Products] WHERE CategoryID = 10;
-- Move those products to a different category first...
UPDATE [dbo].[Products] SET CategoryID = 1 WHERE CategoryID = 10;   -- 1 = whatever category should hold them
-- ...then the category is safe to delete.
DELETE FROM [dbo].[Categories] WHERE CategoryID = 10;

-- Example: deleting a parent Category (like "Hoodies & Jackets") that
-- has subcategories nested under it.
SELECT * FROM [dbo].[Categories] WHERE ParentCategoryID = 6;   -- children of category 6
-- Either delete/reassign the children first, or just un-nest them:
UPDATE [dbo].[Categories] SET ParentCategoryID = NULL WHERE ParentCategoryID = 6;
DELETE FROM [dbo].[Categories] WHERE CategoryID = 6;


/* ============================================================
   4. DELETING / EMPTYING AN ENTIRE TABLE
   ------------------------------------------------------------
   Two ways to clear a table out:
     DELETE FROM [table]   — removes all rows, keeps the table
                              structure, respects foreign keys,
                              can be wrapped in a transaction.
     TRUNCATE TABLE [table] — faster, resets identity counters,
                              but SQL Server WON'T let you truncate
                              a table that other tables reference
                              (you'll get an error) — use DELETE
                              instead for anything with children.
     DROP TABLE [table]    — removes the table itself, structure
                              and all. Only do this if you actually
                              want the table gone, not just emptied.
============================================================ */

-- Empty a table but keep its structure (respects FKs — do children first).
DELETE FROM [dbo].[OrderDetails];
DELETE FROM [dbo].[Orders];

-- TRUNCATE is fine for tables nothing else points at:
TRUNCATE TABLE [dbo].[AdminUsers];

-- Drop a table completely (structure and all). Safe order matters —
-- drop the tables that HAVE foreign keys before the ones they point to:
DROP TABLE [dbo].[OrderDetails];   -- has FKs to Orders/Products/Services/Categories
DROP TABLE [dbo].[Orders];
DROP TABLE [dbo].[ProductServices]; -- has FKs to Products/Services
DROP TABLE [dbo].[ProductColorImages]; -- has FK to Products
DROP TABLE [dbo].[Products];       -- has FK to Categories
DROP TABLE [dbo].[Categories];
DROP TABLE [dbo].[Services];
DROP TABLE [dbo].[Users];
DROP TABLE [dbo].[Roles];
DROP TABLE [dbo].[AdminUsers];


/* ============================================================
   5. SAFETY NET — TEST BEFORE YOU COMMIT
   ------------------------------------------------------------
   Wrap risky DELETE/UPDATE statements in a transaction so you can
   check the result and back out if it's wrong, instead of finding
   out after the fact.
============================================================ */

BEGIN TRANSACTION;

    DELETE FROM [dbo].[Products] WHERE ProductID = 25;

    -- Check the result looks right...
    SELECT * FROM [dbo].[Products] WHERE ProductID = 25;   -- should return nothing now

-- If it looks correct:
COMMIT TRANSACTION;
-- If it looks wrong, undo everything above instead:
-- ROLLBACK TRANSACTION;


-- Quick backup of a table before a risky bulk change — makes a full
-- copy under a new name you can restore from if something goes wrong.
SELECT * INTO [dbo].[Products_backup_20260806] FROM [dbo].[Products];
-- ...to restore from it later (ProductID is an IDENTITY column, so you
-- need IDENTITY_INSERT ON to put the original IDs back exactly):
-- DELETE FROM [dbo].[Products];
-- SET IDENTITY_INSERT [dbo].[Products] ON;
-- INSERT INTO [dbo].[Products] (ProductID, ProductName, CategoryID, ProductDescription, Size, Color, Price, StockQuantity, Status, ImagePath, AdditionalImages, DateAdded, IsDeleted, DeletedAt)
-- SELECT ProductID, ProductName, CategoryID, ProductDescription, Size, Color, Price, StockQuantity, Status, ImagePath, AdditionalImages, DateAdded, IsDeleted, DeletedAt FROM [dbo].[Products_backup_20260806];
-- SET IDENTITY_INSERT [dbo].[Products] OFF;
-- ...and once you're confident you don't need the backup anymore:
-- DROP TABLE [dbo].[Products_backup_20260806];
