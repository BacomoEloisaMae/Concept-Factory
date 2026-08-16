using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

namespace ConceptFactory.Utils
{
    // Resolves a Category's name to the wwwroot/images/custom/{key}/ folder
    // that holds its plain-color mockup photos, and reads whatever colors
    // (and angles of each color) are actually sitting in that folder — so
    // adding a new color or a new angle is just dropping a file in, no code
    // changes needed.
    //
    // Folder convention: wwwroot/images/custom/{key}/{color-slug}.png is the
    // FRONT/main photo. Other angles of the SAME color use a named suffix:
    //   {color-slug}-back.png     — back view
    //   {color-slug}-sleeve.png   — sleeve close-up
    //   {color-slug}-neck.png     — neck/collar close-up
    //   {color-slug}-extra.png    — any other angle with no specific name
    // A trailing "-2", "-3", etc. can follow any of these if there's more
    // than one photo of the same angle (e.g. "red-sleeve-2.png").
    //
    // The view TYPE matters beyond just display: only "front" and "back"
    // photos are appropriate to show an uploaded design on (see
    // Views/Customize/custom.cshtml + product-details.js) — a sleeve or
    // neck close-up isn't a sensible place to preview a full print.
    public static class CategoryImageResolver
    {
        private static readonly (string Key, string[] Keywords)[] Map =
        {
            ("full-zip-hoodie",    new[] { "full zip hoodie", "full-zip-hoodie", "full zip" }),
            ("zip-up-jacket",      new[] { "zip up", "zip-up", "zipup" }),
            ("varsity-jacket",     new[] { "varsity" }),
            ("compression-tshirt", new[] { "compression" }),
            ("spandex-tshirt",     new[] { "spandex" }),
            ("waffle-tshirt",      new[] { "waffle" }),
            ("cotton-tshirt",      new[] { "cotton", "regular" }),
            ("polo-shirt",         new[] { "polo" }),
            ("sweater",            new[] { "sweater", "sweat" }),
            ("tote-bag",           new[] { "tote", "bag" }),
            ("jacket-hoodie",      new[] { "jacket", "hoodie" }),
            ("tshirt",             new[] { "t-shirt", "tshirt", "shirt" }),
        };

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
        private static readonly string[] KnownViewTypes = { "back","neck" , "sleeve", "extra" };

        // Matches "{base}-{viewType}" or "{base}-{viewType}-{dedupeNumber}",
        // e.g. "red-sleeve" or "red-sleeve-2" -> base "red", view "sleeve".
        private static readonly Regex ViewSuffix = new(
            @"^(?<base>.+)-(?<view>back|neck|sleeve|extra)(-\d+)?$", RegexOptions.Compiled);

        public static string GetKey(string? categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return "default";
            var n = categoryName.ToLowerInvariant();
            foreach (var (key, keywords) in Map)
                if (keywords.Any(k => n.Contains(k))) return key;
            return "default";
        }

        // One photo in a color's gallery: its view type (front/back/sleeve/
        // neck/other) and its web path.
        public record GalleryPhoto(string ViewType, string ImagePath);

        // Reads wwwroot/images/custom/{key}/ and groups files into one entry
        // per color: its front/main photo, plus every other angle found for
        // that same color, each tagged with its view type.
        public static List<(string ColorName, string MainImagePath, List<GalleryPhoto> Gallery)> GetAvailableColors(IWebHostEnvironment env, string key)
        {
            var results = new List<(string, string, List<GalleryPhoto>)>();
            var folder = Path.Combine(env.WebRootPath, "images", "custom", key);
            if (!Directory.Exists(folder)) return results;

            var files = Directory.GetFiles(folder)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var slugs = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();

            bool IsAngleOf(string slug, string colorSlug)
            {
                var m = ViewSuffix.Match(slug);
                return m.Success
                    && m.Groups["base"].Value.Equals(colorSlug, StringComparison.OrdinalIgnoreCase)
                    && slugs.Contains(colorSlug, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var file in files)
            {
                var slug = Path.GetFileNameWithoutExtension(file);
                var m = ViewSuffix.Match(slug);
                bool isMain = !(m.Success && slugs.Any(s => string.Equals(s, m.Groups["base"].Value, StringComparison.OrdinalIgnoreCase))); if (!isMain) continue; // handled as part of its color's gallery below

                var displayName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.Replace("-", " "));
                var mainPath = $"/images/custom/{key}/{Path.GetFileName(file)}";

                var gallery = new List<GalleryPhoto> { new GalleryPhoto("front", mainPath) };
                gallery.AddRange(
                    files.Where(f => IsAngleOf(Path.GetFileNameWithoutExtension(f), slug))
                         .OrderBy(f => ViewOrder(Path.GetFileNameWithoutExtension(f)))
                         .Select(f => new GalleryPhoto(
                             ViewSuffix.Match(Path.GetFileNameWithoutExtension(f)).Groups["view"].Value,
                             $"/images/custom/{key}/{Path.GetFileName(f)}")));

                results.Add((displayName, mainPath, gallery));
            }
            return results;
        }

        // Keeps the gallery in a sensible order: back right after front, then sleeve, neck, extra.
        private static int ViewOrder(string slug)
        {
            var m = ViewSuffix.Match(slug);
            if (!m.Success) return 99;
            return Array.IndexOf(KnownViewTypes, m.Groups["view"].Value) is var i && i >= 0 ? i : 99;
        }

        public static string GetDefaultThumb(IWebHostEnvironment env, string? categoryName, string fallback, string? preferredColor = null)
        {
            var key = GetKey(categoryName);
            var colors = GetAvailableColors(env, key);
            if (colors.Count == 0) return fallback;

            if (!string.IsNullOrEmpty(preferredColor))
            {
                var match = colors.FirstOrDefault(c => c.ColorName.Equals(preferredColor, StringComparison.OrdinalIgnoreCase));
                if (match.MainImagePath != null) return match.MainImagePath;
            }
            foreach (var pref in new[] { "White", "Beige" })
            {
                var match = colors.FirstOrDefault(c => c.ColorName.Equals(pref, StringComparison.OrdinalIgnoreCase));
                if (match.MainImagePath != null) return match.MainImagePath;
            }
            return colors[0].MainImagePath;
        }
    }
}
