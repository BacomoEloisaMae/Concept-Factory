namespace ConceptFactory.Models.ViewModels
{
    // Powers Views/Customize/custom.cshtml. This page represents a custom
    // order for a whole CATEGORY, not any single product — so it no longer
    // depends on a real Product row existing (or being Active) in that
    // category. If the category happens to have priced products already,
    // we use the lowest one as a realistic "starting at" price; otherwise
    // we fall back to a flat default so the page still works.
    public class CustomOrderViewModel
    {
        public int? CategoryID { get; set; }
        public string? CategoryName { get; set; }
        public decimal BasePrice { get; set; }
    }
}
