SELECT TOP (1000) [CategoryID]
      ,[CategoryName]
  FROM [ConceptFactoryDB].[dbo].[Categories]


  USE ConceptFactoryDB;
  INSERT INTO [ConceptFactoryDB].[dbo].[Categories] (CategoryName)
VALUES 
('T-Shirts'),
('Jackets'),
('Sweaters'),
('Tote Bags'),
('Compressions');

DELETE FROM Categories WHERE CategoryName = 'Apparel';
DELETE FROM Categories WHERE CategoryName = 'Polo Shirts';

-- Check if any products use these categories
SELECT p.ProductName, c.CategoryName
FROM Products p
JOIN Categories c ON p.CategoryID = c.CategoryID
WHERE c.CategoryName IN ('Apparel', 'Polo Shirts');

UPDATE Products 
SET CategoryID = (SELECT CategoryID FROM Categories WHERE CategoryName = 'T-Shirts')
WHERE CategoryID = (SELECT CategoryID FROM Categories WHERE CategoryName = 'Apparel');

-- First move all products to no category (or delete them too)
-- Check if products exist first
SELECT COUNT(*) FROM [ConceptFactoryDB].[dbo].[Products];

-- Delete all categories (only works if no products are linked)
DELETE FROM [ConceptFactoryDB].[dbo].[Categories];

-- Step 1: Delete all products first
DELETE FROM [ConceptFactoryDB].[dbo].[Products];

-- Step 2: Then delete all categories
DELETE FROM [ConceptFactoryDB].[dbo].[Categories];

-- Step 3: Reset the ID counter back to 1
DBCC CHECKIDENT ('[ConceptFactoryDB].[dbo].[Categories]', RESEED, 0);