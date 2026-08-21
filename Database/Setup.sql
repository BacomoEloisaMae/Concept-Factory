-- ============================================================
--  ConceptFactory — FULL DATABASE SETUP
--  One file, run top to bottom. That's it.
--
--  Safe to run more than once — every step checks IF NOT EXISTS
--  first, so re-running this won't duplicate data, drop anything,
--  or error out. That also means it's safe to run against a
--  database that already has real products/orders in it: it only
--  adds what's missing and never touches existing rows except to
--  fold in a couple of known one-time cleanups (see CATEGORIES).
--
--  How to run (SQL Server Management Studio):
--    1. Open this file in SSMS
--    2. Make sure you're connected to your SQL Server instance
--    3. Click Execute (or press F5)
--
--  Default admin login (seeded below):
--    Email:    admin@conceptfactory.com
--    Password: admin123
--  Change this before deploying anywhere real.
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ConceptFactoryDB')
BEGIN
    CREATE DATABASE ConceptFactoryDB;
    PRINT 'Database ConceptFactoryDB created.';
END
GO

USE ConceptFactoryDB;
GO


-- ============================================================
-- SECTION 1 — ACCOUNTS  (Roles, Users, AdminUsers)
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Roles] (
        RoleID   INT IDENTITY(1,1) PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL
    );
    INSERT INTO [dbo].[Roles] (RoleName) VALUES ('Admin'), ('Customer'), ('Staff');
    PRINT 'Roles table created.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Users] (
        UserID     INT IDENTITY(1,1) PRIMARY KEY,
        RoleID     INT NOT NULL REFERENCES [dbo].[Roles](RoleID),
        FirstName  NVARCHAR(100) NOT NULL,
        LastName   NVARCHAR(100) NOT NULL,
        Email      NVARCHAR(200) NOT NULL UNIQUE,
        Password   NVARCHAR(255) NOT NULL,
        ContactNum NVARCHAR(20),
        Status     NVARCHAR(20) NOT NULL DEFAULT 'Active',
        CreatedAt  DATETIME NOT NULL DEFAULT GETDATE()
    );
    INSERT INTO [dbo].[Users] (RoleID, FirstName, LastName, Email, Password, Status)
    VALUES (1, 'Admin', 'User', 'admin@conceptfactory.com', 'Admin@123', 'Active');
    PRINT 'Users table created.';
END
GO

-- Session-based admin login used by the admin panel (separate from the
-- general Users table above).
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdminUsers]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[AdminUsers] (
        AdminID   INT IDENTITY(1,1) PRIMARY KEY,
        FullName  NVARCHAR(100) NOT NULL,
        Email     NVARCHAR(150) NOT NULL UNIQUE,
        Phone     NVARCHAR(30) NULL,
        Password  NVARCHAR(255) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'AdminUsers table created.';
END
GO

-- Existing installs created before Phone existed
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AdminUsers]') AND name = 'Phone')
    ALTER TABLE [dbo].[AdminUsers] ADD Phone NVARCHAR(30) NULL;
GO

-- Audit trail of admin actions (Billing approvals/rejections, production
-- stage changes, order edits/cancellations, product/service CRUD, sign-in/
-- out) — read-only from the app, see Views/Logs and Utils/ActivityLogger.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ActivityLogs]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ActivityLogs] (
        LogID     INT IDENTITY(1,1) PRIMARY KEY,
        Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
        AdminName NVARCHAR(100) NULL,
        Category  NVARCHAR(50) NOT NULL,
        Action    NVARCHAR(200) NOT NULL,
        Details   NVARCHAR(500) NULL,
        OrderID   INT NULL
    );
    CREATE INDEX IX_ActivityLogs_Timestamp ON [dbo].[ActivityLogs] (Timestamp DESC);
    PRINT 'ActivityLogs table created.';
END
GO

IF NOT EXISTS (SELECT * FROM [dbo].[AdminUsers] WHERE Email = 'admin@conceptfactory.com')
BEGIN
    INSERT INTO [dbo].[AdminUsers] (FullName, Email, Phone, Password)
    VALUES ('Admin', 'admin@conceptfactory.com', '0917 123 4567', 'admin123');
    PRINT 'Default admin account seeded.';
END
ELSE
BEGIN
    UPDATE [dbo].[AdminUsers] SET Phone = '0917 123 4567'
    WHERE Email = 'admin@conceptfactory.com' AND Phone IS NULL;
END
GO


-- ============================================================
-- SECTION 2 — CATEGORIES
--   Everything about categories lives in this one section now:
--   base table -> the specific sub-type categories (Cotton
--   T-Shirts, Varsity Jackets, etc.) -> the parent/child grouping
--   (T-Shirts, Jackets) -> a one-time cleanup of leftover
--   duplicates from before that grouping existed.
-- ============================================================

-- 2a. Base table + original categories.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Categories] (
        CategoryID   INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL
    );
    INSERT INTO [dbo].[Categories] (CategoryName)
    VALUES ('T-Shirts'), ('Hoodies'), ('Polo Shirts'), ('Tote Bags'), ('Apparel');
    PRINT 'Categories table created.';
END
GO

-- 2b. ParentCategoryID column, so specific sub-types can nest under a
-- broader category (added here rather than only in the CREATE TABLE
-- above, since this also needs to run against a database that already
-- has a Categories table from before this column existed).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND name = 'ParentCategoryID'
)
BEGIN
    ALTER TABLE [dbo].[Categories] ADD ParentCategoryID INT NULL;
    ALTER TABLE [dbo].[Categories] ADD CONSTRAINT FK_Categories_ParentCategory
        FOREIGN KEY (ParentCategoryID) REFERENCES [dbo].[Categories](CategoryID);
    PRINT 'Added ParentCategoryID to Categories.';
END
GO

-- 2c. Add the specific sub-type categories that match the color-mockup
-- photos in wwwroot/images/custom/, plus "Jackets" as a new parent.
-- This only ADDS categories — it never touches or deletes existing ones,
-- since products may already be linked to them (CategoryID has a FK
-- restrict on delete).
INSERT INTO [dbo].[Categories] (CategoryName)
SELECT v.CategoryName
FROM (VALUES
    ('Compression T-Shirts'),
    ('Cotton T-Shirts'),
    ('Spandex T-Shirts'),
    ('Waffle T-Shirts'),
    ('Polo Shirts'),
    ('Jacket Hoodies'),
    ('Jacket Zip-Up'),
    ('Full Zip Hoodies'),
    ('Varsity Jackets'),
    ('Sweaters'),
    ('Tote Bags')
) AS v(CategoryName)
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Categories] c WHERE c.CategoryName = v.CategoryName
);
GO

-- 2d. Nest the specific sub-types under their parent, so shoppers can
-- browse "Jackets" or "T-Shirts" as one group, or drill into an exact
-- sub-type. Sweaters, Tote Bags, and Polo Shirts stay standalone
-- top-level categories (no children) since there's only one variant of
-- each so far — add more the same way later, e.g.:
--   UPDATE Categories SET ParentCategoryID =
--       (SELECT CategoryID FROM Categories WHERE CategoryName = 'Jackets')
--   WHERE CategoryName = 'Some New Jacket Type';
UPDATE [dbo].[Categories]
SET ParentCategoryID = (SELECT CategoryID FROM [dbo].[Categories] WHERE CategoryName = 'T-Shirts')
WHERE CategoryName IN ('Compression T-Shirts', 'Cotton T-Shirts', 'Spandex T-Shirts', 'Waffle T-Shirts');

-- Looks up either 'Jackets' (first run) or 'Hoodies & Jackets' (every
-- run after 2g below renames it) — otherwise this silently nulls the
-- ParentCategoryID on every re-run once the rename has happened, which
-- un-nests these back to top-level categories.
DECLARE @JacketsParentId INT = (
    SELECT CategoryID FROM [dbo].[Categories] WHERE CategoryName IN ('Jackets', 'Hoodies & Jackets')
);
IF @JacketsParentId IS NOT NULL
BEGIN
    UPDATE [dbo].[Categories]
    SET ParentCategoryID = @JacketsParentId
    WHERE CategoryName IN ('Jacket Hoodies', 'Jacket Zip-Up', 'Full Zip Hoodies', 'Varsity Jackets');
END
GO

-- 2e. One-time cleanup: fold any stray standalone "Compression..."
-- category (from before "Compression T-Shirts" existed as the real
-- T-Shirts sub-type) into "Compression T-Shirts", and collapse any
-- duplicate/singular "Polo Shirt(s)" rows down to one "Polo Shirts".
-- Products on a removed row are moved to the surviving, correctly-named
-- category first — nothing is deleted or reassigned to a DIFFERENT
-- category, it just collapses duplicates of the SAME category onto one row.
DECLARE @CompressionTargetId INT = (
    SELECT CategoryID FROM [dbo].[Categories] WHERE CategoryName = 'Compression T-Shirts'
);
IF @CompressionTargetId IS NOT NULL
BEGIN
    UPDATE p SET p.CategoryID = @CompressionTargetId
    FROM [dbo].[Products] p JOIN [dbo].[Categories] c ON p.CategoryID = c.CategoryID
    WHERE c.CategoryID <> @CompressionTargetId AND c.CategoryName LIKE '%Compression%';

    UPDATE child SET child.ParentCategoryID = @CompressionTargetId
    FROM [dbo].[Categories] child JOIN [dbo].[Categories] c ON child.ParentCategoryID = c.CategoryID
    WHERE c.CategoryID <> @CompressionTargetId AND c.CategoryName LIKE '%Compression%';

    DELETE c FROM [dbo].[Categories] c
    WHERE c.CategoryID <> @CompressionTargetId AND c.CategoryName LIKE '%Compression%';

    PRINT 'Cleaned up stray Compression category (if one existed).';
END
GO

-- Keeps "Polo Shirts" (plural) as the canonical name — folds in both any
-- duplicate "Polo Shirts" rows AND a singular "Polo Shirt" row if one
-- exists (same category, just saved under two different spellings).
DECLARE @PoloKeepId INT = (
    SELECT MIN(CategoryID) FROM [dbo].[Categories] WHERE CategoryName = 'Polo Shirts'
);
IF @PoloKeepId IS NOT NULL
BEGIN
    UPDATE p SET p.CategoryID = @PoloKeepId
    FROM [dbo].[Products] p JOIN [dbo].[Categories] c ON p.CategoryID = c.CategoryID
    WHERE c.CategoryID <> @PoloKeepId AND c.CategoryName IN ('Polo Shirts', 'Polo Shirt');

    UPDATE child SET child.ParentCategoryID = @PoloKeepId
    FROM [dbo].[Categories] child JOIN [dbo].[Categories] c ON child.ParentCategoryID = c.CategoryID
    WHERE c.CategoryID <> @PoloKeepId AND c.CategoryName IN ('Polo Shirts', 'Polo Shirt');

    DELETE c FROM [dbo].[Categories] c
    WHERE c.CategoryID <> @PoloKeepId AND c.CategoryName IN ('Polo Shirts', 'Polo Shirt');

    PRINT 'Collapsed duplicate/singular Polo Shirt(s) categories down to one.';
END
GO

-- 2f. Keep this sub-type named "Cotton T-Shirts" (it's the plain/basic
-- T-Shirt option, sitting alongside Compression/Spandex/Waffle — "Regular"
-- is just how it's described casually, not its stored name). This also
-- rolls back anyone who already ran an earlier version of this script that
-- renamed it to "Regular T-Shirts".
UPDATE [dbo].[Categories]
SET CategoryName = 'Cotton T-Shirts'
WHERE CategoryName = 'Regular T-Shirts';
GO

-- 2g. Rename "Jackets" -> "Hoodies & Jackets" — same parent category
-- (still holds Varsity Jackets, Full Zip Hoodies, Jacket Zip-Up, Jacket
-- Hoodies underneath), just a name that reflects it covers hoodies too.
UPDATE [dbo].[Categories]
SET CategoryName = 'Hoodies & Jackets'
WHERE CategoryName = 'Jackets';
GO

-- 2h. Fold the old standalone "Hoodies" top-level category (a leftover
-- from the very first schema, before "Hoodies & Jackets" existed as a
-- proper parent with sub-types) into "Hoodies & Jackets", so it stops
-- showing up as its own empty category. Any products or children
-- accidentally left on it are moved first — nothing is deleted without
-- being reassigned.
DECLARE @HoodiesJacketsId INT = (
    SELECT CategoryID FROM [dbo].[Categories] WHERE CategoryName = 'Hoodies & Jackets'
);
DECLARE @StrayHoodiesId INT = (
    SELECT CategoryID FROM [dbo].[Categories] WHERE CategoryName = 'Hoodies'
);
IF @HoodiesJacketsId IS NOT NULL AND @StrayHoodiesId IS NOT NULL AND @StrayHoodiesId <> @HoodiesJacketsId
BEGIN
    UPDATE p SET p.CategoryID = @HoodiesJacketsId
    FROM [dbo].[Products] p
    WHERE p.CategoryID = @StrayHoodiesId;

    UPDATE c SET c.ParentCategoryID = @HoodiesJacketsId
    FROM [dbo].[Categories] c
    WHERE c.ParentCategoryID = @StrayHoodiesId;

    DELETE FROM [dbo].[Categories] WHERE CategoryID = @StrayHoodiesId;

    PRINT 'Folded stray standalone Hoodies category into Hoodies & Jackets.';
END
GO

-- 2i. Remove the unused "Apparel" placeholder category left over from the
-- very first schema — only if nothing has ever been assigned to it, so
-- this never deletes real data.
DELETE FROM [dbo].[Categories]
WHERE CategoryName = 'Apparel'
  AND NOT EXISTS (SELECT 1 FROM [dbo].[Products] p WHERE p.CategoryID = Categories.CategoryID)
  AND NOT EXISTS (SELECT 1 FROM [dbo].[Categories] c WHERE c.ParentCategoryID = Categories.CategoryID);
GO


-- 2j. General duplicate-category cleanup — covers ANY categories that
-- ended up with the same name (from manual edits, a restored backup,
-- etc.), not just the specific cases handled above. Keeps the
-- lowest CategoryID for each name, moves any Products / child
-- categories pointing at the higher-ID duplicates onto that survivor,
-- then removes the now-empty duplicate rows.
IF OBJECT_ID('tempdb..#DupCategories') IS NOT NULL DROP TABLE #DupCategories;
SELECT CategoryID,
       MIN(CategoryID) OVER (PARTITION BY CategoryName) AS KeepID
INTO #DupCategories
FROM [dbo].[Categories];

UPDATE p SET p.CategoryID = d.KeepID
FROM [dbo].[Products] p JOIN #DupCategories d ON p.CategoryID = d.CategoryID
WHERE d.CategoryID <> d.KeepID;

UPDATE c SET c.ParentCategoryID = d.KeepID
FROM [dbo].[Categories] c JOIN #DupCategories d ON c.ParentCategoryID = d.CategoryID
WHERE d.CategoryID <> d.KeepID;

DELETE c FROM [dbo].[Categories] c JOIN #DupCategories d ON c.CategoryID = d.CategoryID
WHERE d.CategoryID <> d.KeepID;

DROP TABLE #DupCategories;
PRINT 'Deduped any Categories rows that shared the same name.';
GO

-- 2k. Enforce it at the database level going forward: once the dedupe
-- above has run, no two categories can ever share a name again — any
-- future INSERT (from this script or anywhere else) that tries to add
-- a name that already exists will fail loudly instead of silently
-- creating a duplicate.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND name = 'UQ_Categories_CategoryName'
)
BEGIN
    CREATE UNIQUE INDEX UQ_Categories_CategoryName ON [dbo].[Categories](CategoryName);
    PRINT 'Added unique constraint on Categories.CategoryName.';
END
GO


-- ============================================================
-- SECTION 3 — SERVICES  (printing services — standalone, not per-product)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Services] (
        ServiceID          INT IDENTITY(1,1) PRIMARY KEY,
        ServiceName        NVARCHAR(200) NOT NULL,
        ServiceDescription NVARCHAR(MAX),
        ServicePrice       DECIMAL(10,2) NOT NULL,
        Status             NVARCHAR(20)  NOT NULL DEFAULT 'Active',
        DateAdded          DATETIME      NOT NULL DEFAULT GETDATE(),
        IsDeleted          BIT           NOT NULL DEFAULT 0,
        DeletedAt          DATETIME      NULL
    );
    INSERT INTO [dbo].[Services] (ServiceName, ServiceDescription, ServicePrice, Status)
    VALUES
        ('DTG Printing',    'Direct-to-garment printing for full-color designs with fine detail.',        150.00, 'Active'),
        ('Screen Printing', 'High-quality screen printing ideal for bulk orders and simple designs.',     120.00, 'Active'),
        ('Embroidery',      'Premium embroidery service for logos and text on garments.',                 200.00, 'Active'),
        ('Heat Transfer',   'Heat transfer printing for vibrant designs on various materials.',           100.00, 'Active'),
        ('Sublimation',     'Dye-sublimation printing for vibrant, all-over prints on light fabrics.',   130.00, 'Active');
    PRINT 'Services table created.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = 'IsDeleted')
        ALTER TABLE [dbo].[Services] ADD IsDeleted BIT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = 'DeletedAt')
        ALTER TABLE [dbo].[Services] ADD DeletedAt DATETIME NULL;
    PRINT 'Services table already exists — columns verified.';
END
GO


-- ============================================================
-- SECTION 4 — PRODUCTS  (+ per-color product photos)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Products] (
        ProductID          INT IDENTITY(1,1) PRIMARY KEY,
        ProductName        NVARCHAR(200)  NOT NULL,
        CategoryID         INT            NOT NULL REFERENCES [dbo].[Categories](CategoryID),
        ProductDescription NVARCHAR(MAX),
        Size               NVARCHAR(200),
        Color              NVARCHAR(500),
        Price              DECIMAL(10,2)  NOT NULL,
        StockQuantity      INT            NOT NULL DEFAULT 0,
        Status             NVARCHAR(20)   NOT NULL DEFAULT 'Active',
        ImagePath          NVARCHAR(500),
        AdditionalImages   NVARCHAR(MAX),
        DateAdded          DATETIME       NOT NULL DEFAULT GETDATE(),
        IsDeleted          BIT            NOT NULL DEFAULT 0,
        DeletedAt          DATETIME       NULL
    );

    INSERT INTO [dbo].[Products] (ProductName, CategoryID, ProductDescription, Size, Price, StockQuantity, Status)
    VALUES
        ('Basic Cotton T-Shirt', 1, 'A stylish and comfortable t-shirt made from soft, high-quality fabric.',    'XS,S,M,L,XL,XXL', 500.00, 12, 'Active'),
        ('Premium Hoodie',       2, 'Warm and stylish hoodie perfect for cool weather.',                         'S,M,L,XL',         850.00,  8, 'Active'),
        ('Classic Polo Shirt',   3, 'Professional polo shirt suitable for casual and semi-formal occasions.',    'M,L,XL',           650.00, 15, 'Active'),
        ('Canvas Tote Bag',      4, 'Eco-friendly canvas tote bag, perfect for custom prints.',                  'One Size',         350.00, 20, 'Active');

    PRINT 'Products table created.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'IsDeleted')
        ALTER TABLE [dbo].[Products] ADD IsDeleted BIT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'DeletedAt')
        ALTER TABLE [dbo].[Products] ADD DeletedAt DATETIME NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'Color')
        ALTER TABLE [dbo].[Products] ADD Color NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'AdditionalImages')
        ALTER TABLE [dbo].[Products] ADD AdditionalImages NVARCHAR(MAX) NULL;
    PRINT 'Products table already exists — columns verified/upgraded.';
END
GO

-- Lets each product's color swatch (in the admin Add/Edit Item form) have
-- its own uploaded photo, so the storefront swaps the main image when a
-- customer picks that color.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductColorImages]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ProductColorImages] (
        ProductColorImageID INT IDENTITY(1,1) PRIMARY KEY,
        ProductID           INT NOT NULL,
        ColorHex            NVARCHAR(20) NOT NULL,
        ColorName           NVARCHAR(100) NULL,
        ImagePath           NVARCHAR(500) NULL,
        DisplayOrder        INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_ProductColorImages_Products
            FOREIGN KEY (ProductID) REFERENCES [dbo].[Products](ProductID)
            ON DELETE CASCADE
    );
    PRINT 'ProductColorImages table created.';
END
GO

-- Sample product for the new "Waffle T-Shirts" sub-category (added in
-- Section 2) — it had color mockup photos (wwwroot/images/custom/
-- waffle-tshirt/) but no actual sellable listing yet. One row per color
-- in ProductColorImages so the storefront's color swatches on the product
-- page work the same way they do for the other sample products.
IF NOT EXISTS (SELECT 1 FROM [dbo].[Products] WHERE ProductName = 'Waffle Crew T-Shirt')
BEGIN
    DECLARE @WaffleCategoryId INT = (SELECT CategoryID FROM [dbo].[Categories] WHERE CategoryName = 'Waffle T-Shirts');
    IF @WaffleCategoryId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[Products] (ProductName, CategoryID, ProductDescription, Size, Color, Price, StockQuantity, Status, ImagePath)
        VALUES (
            'Waffle Crew T-Shirt', @WaffleCategoryId,
            'Textured waffle-knit t-shirt with a relaxed, breathable fit — available in six colors.',
            'S,M,L,XL,XXL', 'Black,White,Brown,Green,Red,Blue', 550.00, 10, 'Active',
            '/images/custom/waffle-tshirt/white.png'
        );

        DECLARE @WaffleProductId INT = SCOPE_IDENTITY();

        -- Hex codes match the colorMap already used in Views/Home/hDetails.cshtml
        INSERT INTO [dbo].[ProductColorImages] (ProductID, ColorHex, ColorName, ImagePath, DisplayOrder)
        VALUES
            (@WaffleProductId, '#1a202c', 'Black', '/images/custom/waffle-tshirt/black.png', 0),
            (@WaffleProductId, '#f0f0f0', 'White', '/images/custom/waffle-tshirt/white.png', 1),
            (@WaffleProductId, '#744210', 'Brown', '/images/custom/waffle-tshirt/brown.png', 2),
            (@WaffleProductId, '#38a169', 'Green', '/images/custom/waffle-tshirt/green.png', 3),
            (@WaffleProductId, '#e53e3e', 'Red',   '/images/custom/waffle-tshirt/red.png',   4),
            (@WaffleProductId, '#3182ce', 'Blue',  '/images/custom/waffle-tshirt/blue.png',  5);

        PRINT 'Seeded sample Waffle Crew T-Shirt product with 6 color photos.';
    END
END
GO

-- Which printing services are offered per product. A product can support
-- multiple services. If every service should be available for every
-- product instead, this table can be skipped entirely — the app already
-- queries Services directly for that (ViewBag.Services).
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductServices]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ProductServices] (
        ProductServiceID INT IDENTITY(1,1) PRIMARY KEY,
        ProductID        INT NOT NULL REFERENCES [dbo].[Products](ProductID)  ON DELETE CASCADE,
        ServiceID        INT NOT NULL REFERENCES [dbo].[Services](ServiceID)  ON DELETE CASCADE,
        CONSTRAINT UQ_ProductService UNIQUE (ProductID, ServiceID)
    );
    PRINT 'ProductServices link table created.';
END
GO


-- ============================================================
-- SECTION 5 — ORDERS  (+ line items)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Orders] (
        OrderID           INT IDENTITY(1,1) PRIMARY KEY,
        CustomerName      NVARCHAR(200) NOT NULL,
        CustomerEmail     NVARCHAR(200),
        CustomerPhone     NVARCHAR(30),
        CustomerAddress   NVARCHAR(300),
        OrderDate         DATETIME      NOT NULL DEFAULT GETDATE(),
        TotalAmount       DECIMAL(10,2) NOT NULL DEFAULT 0,
        Status            NVARCHAR(50)  NOT NULL DEFAULT 'Pending',
        Notes             NVARCHAR(MAX),
        PaymentMethod     NVARCHAR(30)  NULL,
        PaymentStatus     NVARCHAR(50)  NOT NULL DEFAULT 'Waiting for Verification',
        ReferenceNumber   NVARCHAR(100) NULL,
        ProofFilePath     NVARCHAR(500) NULL,
        PaymentNote       NVARCHAR(500) NULL,
        DownPaymentAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
        RemainingBalance  DECIMAL(10,2) NOT NULL DEFAULT 0,
        ProductionStage   INT           NOT NULL DEFAULT -1,
        ProductionRemarks       NVARCHAR(500) NULL,
        ProductionStageUpdatedAt DATETIME     NULL
    );
    PRINT 'Orders table created.';
END
GO

-- Existing installs created before the Billing & Payment checkout existed
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'PaymentMethod')
    ALTER TABLE [dbo].[Orders] ADD PaymentMethod NVARCHAR(30) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'CustomerAddress')
    ALTER TABLE [dbo].[Orders] ADD CustomerAddress NVARCHAR(300) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'PaymentStatus')
    ALTER TABLE [dbo].[Orders] ADD PaymentStatus NVARCHAR(50) NOT NULL DEFAULT 'Waiting for Verification';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'ReferenceNumber')
    ALTER TABLE [dbo].[Orders] ADD ReferenceNumber NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'ProofFilePath')
    ALTER TABLE [dbo].[Orders] ADD ProofFilePath NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'PaymentNote')
    ALTER TABLE [dbo].[Orders] ADD PaymentNote NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'DownPaymentAmount')
    ALTER TABLE [dbo].[Orders] ADD DownPaymentAmount DECIMAL(10,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'RemainingBalance')
    ALTER TABLE [dbo].[Orders] ADD RemainingBalance DECIMAL(10,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'ProductionStage')
    ALTER TABLE [dbo].[Orders] ADD ProductionStage INT NOT NULL DEFAULT -1;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'CashAmountReceived')
    ALTER TABLE [dbo].[Orders] ADD CashAmountReceived DECIMAL(10,2) NULL;
-- Production station popups (Cutting/Printing/Sewing/Trimming/Quality
-- Check "Mark as Done" forms) — remarks left by the staff member who
-- completed the stage, and when that stage was last advanced. The latter
-- powers the "Completed Today" / "Pending from Previous" station stat
-- cards (see ProductionController.pStation).
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'ProductionRemarks')
    ALTER TABLE [dbo].[Orders] ADD ProductionRemarks NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'ProductionStageUpdatedAt')
    ALTER TABLE [dbo].[Orders] ADD ProductionStageUpdatedAt DATETIME NULL;
GO

-- Anyone who ran this before the wording settled on "Waiting for
-- Verification" (started as "Unpaid", briefly "Pending Verification" —
-- all mean the same thing: staff hasn't confirmed the payment yet).
-- Normalizes existing rows so old and new installs read the same way.
UPDATE [dbo].[Orders] SET PaymentStatus = 'Waiting for Verification' WHERE PaymentStatus IN ('Unpaid', 'Pending Verification');
GO

-- The Billing → Payment Verification screen introduced a proper
-- Approve/Reject workflow in place of the old single "Paid" status.
-- Existing rows already marked "Paid" become "Approved" (same meaning),
-- which itself was later renamed "Partially Paid" below.
UPDATE [dbo].[Orders] SET PaymentStatus = 'Approved' WHERE PaymentStatus = 'Paid';
GO

-- "Approved" was renamed "Partially Paid" once Billing gained a distinct
-- "Fully Paid" state for when the remaining balance also comes in — a
-- confirmed down payment alone was always only "partial" payment, this
-- just makes that explicit. Existing "Approved" rows carry the same
-- meaning forward.
UPDATE [dbo].[Orders] SET PaymentStatus = 'Partially Paid' WHERE PaymentStatus = 'Approved';
GO

-- Order Status vocabulary was expanded to match the finalized Order
-- Management module spec: the old generic "Pending" is split by payment
-- method into "Pending Cash Payment" / "Pending Payment Verification"
-- (Gcash orders always have proof by the time the row exists, so they
-- never actually land on "Pending Down Payment" in current checkout —
-- that value stays valid for manual/admin-created orders); "Processing"
-- was renamed "Design Review"; and "Rejected" was folded into
-- "Cancelled" since a rejected payment simply means the order doesn't
-- proceed. Existing rows are normalized so old and new installs read
-- the same way.
UPDATE [dbo].[Orders]
   SET Status = CASE WHEN PaymentMethod = 'Cash' THEN 'Pending Cash Payment' ELSE 'Pending Payment Verification' END
 WHERE Status = 'Pending';
UPDATE [dbo].[Orders] SET Status = 'Design Review' WHERE Status = 'Processing';
UPDATE [dbo].[Orders] SET Status = 'Cancelled'     WHERE Status = 'Rejected';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[OrderDetails] (
        OrderDetailID      INT IDENTITY(1,1) PRIMARY KEY,
        OrderID            INT            NOT NULL REFERENCES [dbo].[Orders](OrderID)   ON DELETE CASCADE,
        -- Nullable: custom-order lines (Customize page) have no real Product row.
        ProductID          INT                 NULL REFERENCES [dbo].[Products](ProductID),
        ServiceID          INT                 NULL REFERENCES [dbo].[Services](ServiceID),
        CategoryID         INT                 NULL REFERENCES [dbo].[Categories](CategoryID),
        IsCustomOrder      BIT            NOT NULL DEFAULT 0,
        ProductNameSnapshot NVARCHAR(200) NULL,
        Quantity           INT            NOT NULL DEFAULT 1,
        UnitPrice          DECIMAL(10,2)  NOT NULL,
        PrintLocation      NVARCHAR(200),
        DesignNotes        NVARCHAR(MAX),
        DesignFilePath     NVARCHAR(500),
        SelectedSize       NVARCHAR(50),
        SelectedColor      NVARCHAR(50),
        ImageSnapshotPath  NVARCHAR(500) NULL,
        ServiceNameSnapshot NVARCHAR(300) NULL,
        ServicePriceSnapshot DECIMAL(10,2) NULL
    );
    PRINT 'OrderDetails table created.';
END
GO

-- Existing installs: relax ProductID to nullable + add the new columns
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'ProductID' AND is_nullable = 0)
BEGIN
    -- Drop the NOT NULL FK constraint first (name may vary by install), then re-add as nullable
    DECLARE @fkName NVARCHAR(200) = (
        SELECT fk.name FROM sys.foreign_keys fk
        WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[OrderDetails]')
          AND EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc
                      WHERE fkc.constraint_object_id = fk.object_id
                        AND fkc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[OrderDetails]'), 'ProductID', 'ColumnId'))
    );
    IF @fkName IS NOT NULL
        EXEC('ALTER TABLE [dbo].[OrderDetails] DROP CONSTRAINT [' + @fkName + ']');
    ALTER TABLE [dbo].[OrderDetails] ALTER COLUMN ProductID INT NULL;
    ALTER TABLE [dbo].[OrderDetails] ADD CONSTRAINT FK_OrderDetails_Products FOREIGN KEY (ProductID) REFERENCES [dbo].[Products](ProductID);
    PRINT 'OrderDetails.ProductID relaxed to nullable for custom orders.';
END
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'CategoryID')
    ALTER TABLE [dbo].[OrderDetails] ADD CategoryID INT NULL REFERENCES [dbo].[Categories](CategoryID);
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'IsCustomOrder')
    ALTER TABLE [dbo].[OrderDetails] ADD IsCustomOrder BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'ProductNameSnapshot')
    ALTER TABLE [dbo].[OrderDetails] ADD ProductNameSnapshot NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'ImageSnapshotPath')
    ALTER TABLE [dbo].[OrderDetails] ADD ImageSnapshotPath NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'ServiceNameSnapshot')
    ALTER TABLE [dbo].[OrderDetails] ADD ServiceNameSnapshot NVARCHAR(300) NULL;
-- Combined price of whatever service(s) were picked at checkout for this
-- line (see ServiceNameSnapshot above — a line can combine more than one
-- service, so this isn't a single ServiceID/Services.ServicePrice lookup).
-- UnitPrice already includes this amount; it's stored separately so the
-- admin/customer order screens can show it as its own line item.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'ServicePriceSnapshot')
    ALTER TABLE [dbo].[OrderDetails] ADD ServicePriceSnapshot DECIMAL(10,2) NULL;
-- Per-item station progress (Cutting/Printing/Sewing/Trimming/Quality
-- Check autosave) — replaces the old single Orders.ProductionStageQuantityDone
-- number, which lumped every color/size line into one count. Reset to 0
-- by ProductionWorkflow.SetStage whenever the order moves to the next
-- station. See ProductionController.pSaveStationProgress/pCompleteStation.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND name = 'ProductionQuantityDone')
    ALTER TABLE [dbo].[OrderDetails] ADD ProductionQuantityDone INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Orders]')
      AND name = 'ProductionStageQuantityDone'
)
BEGIN
    ALTER TABLE [dbo].[Orders]
    ADD ProductionStageQuantityDone INT NOT NULL DEFAULT 0;

    PRINT 'ProductionStageQuantityDone added.';
END
GO

USE ConceptFactoryDB;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Orders]')
      AND name = 'ProductionStageQuantityDone'
)
BEGIN
    ALTER TABLE [dbo].[Orders]
    ADD ProductionStageQuantityDone INT NOT NULL
        CONSTRAINT DF_Orders_ProductionStageQuantityDone DEFAULT 0;

    PRINT 'ProductionStageQuantityDone added successfully.';
END
ELSE
BEGIN
    PRINT 'ProductionStageQuantityDone already exists.';
END
GO


PRINT '========================================';
PRINT 'ConceptFactory full setup complete.';
PRINT '========================================';
GO



--SELECT
--    OrderDetailID,
--    ServiceID,
--    ServiceNameSnapshot,
--    ServicePriceSnapshot,
--    UnitPrice
--FROM OrderDetails;