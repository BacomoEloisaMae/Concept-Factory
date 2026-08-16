namespace ConceptFactory.Utils
{
    // Shared by BillingController/OrdersController/ProductionController's
    // search boxes. Admin/staff naturally type the Order ID the way it's
    // shown on screen — "#ORD-00015", "ord-15", "ORD00015" — not the bare
    // integer the DB stores, and letter-casing shouldn't matter either.
    // Stripping everything but digits and parsing what's left handles all
    // of those in one place instead of every controller re-deriving its
    // own (previously exact-match-only: OrderID.ToString() == search).
    public static class OrderSearchHelper
    {
        public static bool TryParseOrderId(string? search, out int orderId)
        {
            orderId = 0;
            if (string.IsNullOrWhiteSpace(search)) return false;

            string digitsOnly = new string(search.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length == 0) return false;

            return int.TryParse(digitsOnly, out orderId);
        }
    }
}
