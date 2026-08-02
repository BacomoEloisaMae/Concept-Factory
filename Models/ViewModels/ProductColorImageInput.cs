using Microsoft.AspNetCore.Http;

namespace ConceptFactory.Models.ViewModels
{
    // Bound from the "Color Variants" rows in pCreate/pEdit. One instance
    // per color swatch the admin has added.
    public class ProductColorImageInput
    {
        // 0 (or missing) on Create, and on any brand-new row added during
        // an Edit. On Edit, existing rows carry their real ID so we know
        // to update (not re-insert) them, and so we can tell which rows
        // were removed by the admin (their ID just won't be resubmitted).
        public int ProductColorImageID { get; set; }

        public string ColorHex { get; set; } = string.Empty;

        // Path of the image already saved for this row (Edit only). Kept
        // as-is unless a NewImage file is uploaded to replace it.
        public string? ExistingImagePath { get; set; }

        // A newly chosen file for this specific color swatch. Optional —
        // a color can be saved without a dedicated photo.
        public IFormFile? NewImage { get; set; }
    }
}
