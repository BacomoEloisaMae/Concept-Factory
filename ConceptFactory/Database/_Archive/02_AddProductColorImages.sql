-- Run this against your ConceptFactory database to add color-image support.
-- Lets each product color swatch have its own photo, so the storefront can
-- swap the main image when the customer picks a color.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductColorImages')
BEGIN
    CREATE TABLE ProductColorImages (
        ProductColorImageID INT IDENTITY(1,1) PRIMARY KEY,
        ProductID           INT NOT NULL,
        ColorHex            NVARCHAR(20) NOT NULL,
        ColorName           NVARCHAR(100) NULL,
        ImagePath           NVARCHAR(500) NULL,
        DisplayOrder        INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_ProductColorImages_Products
            FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
            ON DELETE CASCADE
    );
END
GO
