-- ============================================================
--  ConceptFactory Database Schema
--  Product & Service Management Module
--  Run this in SQL Server Management Studio
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ConceptFactoryDB')
BEGIN
    CREATE DATABASE ConceptFactoryDB;
END
GO

USE ConceptFactoryDB;
GO

-- ============================================================
-- ROLES TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Roles] (
        RoleID   INT IDENTITY(1,1) PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL
    );

    INSERT INTO [dbo].[Roles] (RoleName) VALUES ('Admin'), ('Customer'), ('Staff');
END
GO

-- ============================================================
-- USERS TABLE
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
END
GO

-- ============================================================
-- CATEGORIES TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Categories] (
        CategoryID   INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL
    );

    INSERT INTO [dbo].[Categories] (CategoryName)
    VALUES ('T-Shirts'), ('Hoodies'), ('Polo Shirts'), ('Tote Bags'), ('Apparel');
END
GO

-- ============================================================
-- PRODUCTS TABLE (with soft delete)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Products] (
        ProductID          INT IDENTITY(1,1) PRIMARY KEY,
        ProductName        NVARCHAR(200) NOT NULL,
        CategoryID         INT NOT NULL REFERENCES [dbo].[Categories](CategoryID),
        ProductDescription NVARCHAR(MAX),
        Size               NVARCHAR(50),
        Price              DECIMAL(10,2) NOT NULL,
        StockQuantity      INT NOT NULL DEFAULT 0,
        Status             NVARCHAR(20) NOT NULL DEFAULT 'Active',
        ImagePath          NVARCHAR(500),
        DateAdded          DATETIME NOT NULL DEFAULT GETDATE(),
        IsDeleted          BIT NOT NULL DEFAULT 0,
        DeletedAt          DATETIME NULL
    );

    INSERT INTO [dbo].[Products] (ProductName, CategoryID, ProductDescription, Size, Price, StockQuantity, Status)
    VALUES
        ('Basic Cotton T-Shirt', 1, 'A stylish and comfortable t-shirt made from soft, high-quality fabric.', 'M', 500.00, 12, 'Active'),
        ('Premium Hoodie', 2, 'Warm and stylish hoodie perfect for cool weather.', 'L', 850.00, 8, 'Active'),
        ('Classic Polo Shirt', 3, 'Professional polo shirt suitable for casual and semi-formal occasions.', 'M', 650.00, 15, 'Inactive'),
        ('Canvas Tote Bag', 4, 'Eco-friendly canvas tote bag, perfect for custom prints.', 'One Size', 350.00, 20, 'Active');
END
ELSE
BEGIN
    -- Add soft delete columns to existing table if missing
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'IsDeleted')
        ALTER TABLE [dbo].[Products] ADD IsDeleted BIT NOT NULL DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'DeletedAt')
        ALTER TABLE [dbo].[Products] ADD DeletedAt DATETIME NULL;
END
GO

-- ============================================================
-- SERVICES TABLE (with soft delete)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Services] (
        ServiceID          INT IDENTITY(1,1) PRIMARY KEY,
        ServiceName        NVARCHAR(200) NOT NULL,
        ServiceDescription NVARCHAR(MAX),
        ServicePrice       DECIMAL(10,2) NOT NULL,
        Status             NVARCHAR(20) NOT NULL DEFAULT 'Active',
        DateAdded          DATETIME NOT NULL DEFAULT GETDATE(),
        IsDeleted          BIT NOT NULL DEFAULT 0,
        DeletedAt          DATETIME NULL
    );

    INSERT INTO [dbo].[Services] (ServiceName, ServiceDescription, ServicePrice, Status)
    VALUES
        ('DTG Printing', 'Direct-to-garment printing for full-color designs with fine detail.', 150.00, 'Active'),
        ('Screen Printing', 'High-quality screen printing ideal for bulk orders and simple designs.', 120.00, 'Active'),
        ('Embroidery', 'Premium embroidery service for logos and text on garments.', 200.00, 'Active'),
        ('Heat Transfer', 'Heat transfer printing for vibrant designs on various materials.', 100.00, 'Active');
END
ELSE
BEGIN
    -- Add soft delete columns to existing table if missing
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = 'IsDeleted')
        ALTER TABLE [dbo].[Services] ADD IsDeleted BIT NOT NULL DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = 'DeletedAt')
        ALTER TABLE [dbo].[Services] ADD DeletedAt DATETIME NULL;
END
GO

PRINT 'ConceptFactory database schema created/updated successfully.';
GO
