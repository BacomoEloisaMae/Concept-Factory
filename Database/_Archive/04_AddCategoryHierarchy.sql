-- Adds a parent/child category hierarchy: broad categories (Jackets,
-- T-Shirts) that a shopper can browse as one group, while each specific
-- sub-type (Varsity Jackets, Cotton T-Shirts, etc.) still exists as its
-- own category underneath. Safe to re-run.

-- 1. Add the ParentCategoryID column if it isn't there yet
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND name = 'ParentCategoryID'
)
BEGIN
    ALTER TABLE Categories ADD ParentCategoryID INT NULL;
    ALTER TABLE Categories ADD CONSTRAINT FK_Categories_ParentCategory
        FOREIGN KEY (ParentCategoryID) REFERENCES Categories(CategoryID);
    PRINT 'Added ParentCategoryID to Categories.';
END
GO

-- 2. Make sure the parent categories exist.
-- "T-Shirts" already comes from 01_Schema.sql's seed data, so it's reused
-- as-is. "Jackets" doesn't exist yet, so it's added here.
INSERT INTO Categories (CategoryName)
SELECT 'Jackets'
WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE CategoryName = 'Jackets');
GO

-- 3. Nest the specific sub-types under their parent.
UPDATE Categories
SET ParentCategoryID = (SELECT CategoryID FROM Categories WHERE CategoryName = 'T-Shirts')
WHERE CategoryName IN ('Compression T-Shirts', 'Cotton T-Shirts', 'Spandex T-Shirts', 'Waffle T-Shirts');

UPDATE Categories
SET ParentCategoryID = (SELECT CategoryID FROM Categories WHERE CategoryName = 'Jackets')
WHERE CategoryName IN ('Jacket Hoodies', 'Jacket Zip-Up', 'Full Zip Hoodies', 'Varsity Jackets');
GO

-- Sweaters, Tote Bags, and Polo Shirts are left as standalone top-level
-- categories (no children) since there's only one variant of each so far.
-- Add more child categories the same way later, e.g.:
--   UPDATE Categories SET ParentCategoryID =
--       (SELECT CategoryID FROM Categories WHERE CategoryName = 'Jackets')
--   WHERE CategoryName = 'Some New Jacket Type';
