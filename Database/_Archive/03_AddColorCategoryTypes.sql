-- Adds the new, more specific categories that match the color-mockup photos
-- in wwwroot/images/custom/. This ADDS categories only — it does not touch
-- or delete your existing ones (T-Shirts, Jackets, Sweaters, Tote Bags,
-- Compressions, etc.), since those may already have real products linked
-- to them (Products.CategoryID has a FK restrict on delete).
--
-- Once you're ready, you can manually re-point old products to the new
-- matching category and retire the old generic one, e.g.:
--   UPDATE Products SET CategoryID = (SELECT CategoryID FROM Categories WHERE CategoryName = 'Cotton T-Shirts')
--   WHERE CategoryID = (SELECT CategoryID FROM Categories WHERE CategoryName = 'T-Shirts');

INSERT INTO Categories (CategoryName)
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
    SELECT 1 FROM Categories c WHERE c.CategoryName = v.CategoryName
);
GO
