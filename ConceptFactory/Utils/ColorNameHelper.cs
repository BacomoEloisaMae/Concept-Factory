using System.Globalization;

namespace ConceptFactory.Utils
{
    // Product/order colors are stored as either a plain name ("Red") from
    // the older preset Customize color list, or a raw hex string
    // ("#e53e3e") from the admin's native <input type="color"> swatch
    // picker (Views/Products/pCreate.cshtml/pEdit.cshtml — see
    // wwwroot/js/admin/product-create.js). Customers should never see a
    // raw hex code as a color's "name" anywhere in the UI (Product
    // Details, Cart, Billing, Order Details, Track Order, Production) —
    // this maps a hex value to the closest common color name so it always
    // reads as "Red" instead of "#e53e3e". Already-a-name values pass
    // through unchanged (just title-cased for consistent display).
    public static class ColorNameHelper
    {
        // (Name, R, G, B) — kept close to the swatch hexes already used
        // around the app (see the colorMap dictionaries in
        // Views/Home/hDetails.cshtml and Views/Customize/custom.cshtml)
        // plus the standard set a customer would actually say out loud.
        private static readonly (string Name, byte R, byte G, byte B)[] Palette =
        {
            ("Black",        0x1A, 0x20, 0x2C),
            ("White",        0xF5, 0xF5, 0xF5),
            ("Gray",         0x71, 0x80, 0x96),
            ("Dark Gray",    0x4A, 0x55, 0x68),
            ("Red",          0xE5, 0x3E, 0x3E),
            ("Maroon",       0x74, 0x2A, 0x2A),
            ("Orange",       0xDD, 0x6B, 0x20),
            ("Yellow",       0xD6, 0x9E, 0x2E),
            ("Beige",        0xE8, 0xDC, 0xC4),
            ("Brown",        0x74, 0x42, 0x10),
            ("Dark Brown",   0x4A, 0x2C, 0x17),
            ("Green",        0x38, 0xA1, 0x69),
            ("Olive Green",  0x55, 0x6B, 0x2F),
            ("Teal",         0x2C, 0x7A, 0x7B),
            ("Petrol Blue",  0x0F, 0x3D, 0x3E),
            ("Blue",         0x31, 0x82, 0xCE),
            ("Navy Blue",    0x1A, 0x36, 0x5D),
            ("Purple",       0x80, 0x5A, 0xD5),
            ("Pink",         0xD5, 0x3F, 0x8C),
        };

        // True only for actual hex color codes ("#e53e3e" / "e53e3e"),
        // never for a stored name like "Red" or "Navy Blue".
        public static bool IsHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim().TrimStart('#');
            return (v.Length == 6 || v.Length == 3) && v.All(Uri.IsHexDigit);
        }

        // Returns a human-readable color name for display. Hex input snaps
        // to the closest named color above; a value that's already a name
        // is just title-cased so "navy blue" and "Navy Blue" render the
        // same everywhere.
        public static string GetName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();

            if (!IsHex(trimmed))
                return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());

            string hex = trimmed.TrimStart('#');
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(c => new string(c, 2)));

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);

            string closest = "Custom";
            double bestDist = double.MaxValue;
            foreach (var (name, pr, pg, pb) in Palette)
            {
                double dist = Math.Pow(r - pr, 2) + Math.Pow(g - pg, 2) + Math.Pow(b - pb, 2);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    closest = name;
                }
            }
            return closest;
        }
    }
}
