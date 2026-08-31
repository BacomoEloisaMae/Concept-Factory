-- Cleans up leftover duplicate categories from before the T-Shirts/Jackets
-- hierarchy existed:
--   1. A standalone "Compression"-style category (e.g. "Compressions") that
--      predates "Compression T-Shirts" (the proper T-Shirts sub-type added
--      in 03_AddColorCategoryTypes.sql / 04_AddCategoryHierarchy.sql).
--   2. Any duplicate "Polo Shirts" rows (Categories.CategoryName has no
--      UNIQUE constraint, so more than one row with that exact name can
--      end up existing if categories were seeded more than once).
--
-- Any products filed under a category this script removes are re-pointed
-- to the surviving, correctly-named category first — nothing gets
-- deleted or reassigned to a DIFFERENT category, they just move off the
-- stray duplicate row and onto the one everything else already uses.
-- Safe to re-run: once cleaned up, there's nothing left to do.

-- ============================================================
-- 1. Fold any stray "Compression..." category into "Compression T-Shirts"
-- ============================================================
DECLARE @CompressionTargetId INT = (
    SELECT CategoryID FROM Categories WHERE CategoryName = 'Compression T-Shirts'
);

IF @CompressionTargetId IS NOT NULL
BEGIN
    -- Move any products off the stray category and onto "Compression T-Shirts"
    UPDATE p
    SET p.CategoryID = @CompressionTargetId
    FROM Products p
    JOIN Categories c ON p.CategoryID = c.CategoryID
    WHERE c.CategoryID <> @CompressionTargetId
      AND c.CategoryName LIKE '%Compression%';

    -- In case anything was ever nested under the stray category, re-parent it too
    UPDATE child
    SET child.ParentCategoryID = @CompressionTargetId
    FROM Categories child
    JOIN Categories c ON child.ParentCategoryID = c.CategoryID
    WHERE c.CategoryID <> @CompressionTargetId
      AND c.CategoryName LIKE '%Compression%';

    -- Now safe to remove the stray category row(s)
    DELETE c
    FROM Categories c
    WHERE c.CategoryID <> @CompressionTargetId
      AND c.CategoryName LIKE '%Compression%';

    PRINT 'Cleaned up stray Compression category (if one existed).';
END
GO

-- ============================================================
-- 2. Collapse duplicate "Polo Shirts" rows down to just one
-- ============================================================
DECLARE @PoloKeepId INT = (
    SELECT MIN(CategoryID) FROM Categories WHERE CategoryName = 'Polo Shirts'
);

IF @PoloKeepId IS NOT NULL
BEGIN
    UPDATE p
    SET p.CategoryID = @PoloKeepId
    FROM Products p
    JOIN Categories c ON p.CategoryID = c.CategoryID
    WHERE c.CategoryName = 'Polo Shirts' AND c.CategoryID <> @PoloKeepId;

    UPDATE child
    SET child.ParentCategoryID = @PoloKeepId
    FROM Categories child
    JOIN Categories c ON child.ParentCategoryID = c.CategoryID
    WHERE c.CategoryName = 'Polo Shirts' AND c.CategoryID <> @PoloKeepId;

    DELETE c
    FROM Categories c
    WHERE c.CategoryName = 'Polo Shirts' AND c.CategoryID <> @PoloKeepId;

    PRINT 'Collapsed duplicate Polo Shirts categories down to one (if any existed).';
END
GO
