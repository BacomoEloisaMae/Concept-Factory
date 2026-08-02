-- ============================================================
--  ConceptFactory Database Schema — Full Setup Script
--  Run this in SQL Server Management Studio
--  Safe to re-run: all tables use IF NOT EXISTS guards
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
-- 1. ROLES
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

-- ============================================================
-- 2. USERS
-- ============================================================
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

-- ============================================================
-- 3. ADMIN USERS  (session-based admin auth)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdminUsers]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[AdminUsers] (
        AdminID   INT IDENTITY(1,1) PRIMARY KEY,
        FullName  NVARCHAR(100) NOT NULL,
        Email     NVARCHAR(150) NOT NULL UNIQUE,
        Password  NVARCHAR(255) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'AdminUsers table created.';
END
GO

IF NOT EXISTS (SELECT * FROM [dbo].[AdminUsers] WHERE Email = 'admin@conceptfactory.com')
BEGIN
    INSERT INTO [dbo].[AdminUsers] (FullName, Email, Password)
    VALUES ('Admin', 'admin@conceptfactory.com', 'admin123');
    PRINT 'Default admin account seeded.';
END
GO

-- ============================================================
-- 4. CATEGORIES
-- ============================================================
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

-- ============================================================
-- 5. SERVICES  (printing services — standalone, not per-product)
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
-- 6. PRODUCTS
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
    -- Upgrade columns if table already exists
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

-- ============================================================
-- 7. PRODUCT_SERVICES  (which services are offered per product)
--    A product can support multiple printing services.
--    If you want ALL services available for every product,
--    you can skip this table and just query Services directly
--    (which is what the current app does via ViewBag.Services).
-- ============================================================
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
-- 8. ORDERS
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Orders] (
        OrderID       INT IDENTITY(1,1) PRIMARY KEY,
        CustomerName  NVARCHAR(200) NOT NULL,
        CustomerEmail NVARCHAR(200),
        CustomerPhone NVARCHAR(30),
        OrderDate     DATETIME      NOT NULL DEFAULT GETDATE(),
        TotalAmount   DECIMAL(10,2) NOT NULL DEFAULT 0,
        Status        NVARCHAR(50)  NOT NULL DEFAULT 'Pending',
        Notes         NVARCHAR(MAX)
    );
    PRINT 'Orders table created.';
END
GO

-- ============================================================
-- 9. ORDER DETAILS  (line items per order)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderDetails]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[OrderDetails] (
        OrderDetailID  INT IDENTITY(1,1) PRIMARY KEY,
        OrderID        INT            NOT NULL REFERENCES [dbo].[Orders](OrderID)   ON DELETE CASCADE,
        ProductID      INT            NOT NULL REFERENCES [dbo].[Products](ProductID),
        ServiceID      INT                 NULL REFERENCES [dbo].[Services](ServiceID),
        Quantity       INT            NOT NULL DEFAULT 1,
        UnitPrice      DECIMAL(10,2)  NOT NULL,
        PrintLocation  NVARCHAR(200),
        DesignNotes    NVARCHAR(MAX),
        DesignFilePath NVARCHAR(500),
        SelectedSize   NVARCHAR(50),
        SelectedColor  NVARCHAR(50)
    );
    PRINT 'OrderDetails table created.';
END
GO

PRINT '========================================';
PRINT 'ConceptFactory DB setup complete.';
PRINT '========================================';
GO
