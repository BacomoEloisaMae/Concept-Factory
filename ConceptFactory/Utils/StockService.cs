using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Models;

namespace ConceptFactory.Utils
{
    // Centralizes stock decrement/restore so every place that can create or
    // undo an order (HomeController.SubmitOrder, hCancelOrder,
    // BillingController.bReject) touches Product.StockQuantity the same way.
    // Only OrderDetail rows with a resolved ProductID count — custom/one-off
    // items (IsCustomOrder) aren't tied to a catalog Product and have no
    // stock to track. "Out of Stock" itself isn't a stored status: it's
    // just StockQuantity <= 0, checked wherever the storefront needs it, so
    // it never fights with the separate Active/Inactive Status field.
    public static class StockService
    {
        // Validates there's enough stock for every line, then decrements it.
        // Returns null on success, or an error message naming the first
        // product that doesn't have enough left (caller should abort the
        // whole order rather than save a partial decrement).
        public static async Task<string?> TryReserveStockAsync(
            ApplicationDbContext context, Dictionary<int, int> requestedQtyByProductId)
        {
            if (requestedQtyByProductId.Count == 0) return null;

            var ids = requestedQtyByProductId.Keys.ToList();
            var products = await context.Products
                .Where(p => ids.Contains(p.ProductID))
                .ToDictionaryAsync(p => p.ProductID);

            foreach (var (productId, qty) in requestedQtyByProductId)
            {
                if (!products.TryGetValue(productId, out var product))
                    continue; // shouldn't happen — caller already validated existence

                if (qty > product.StockQuantity)
                {
                    return $"Sorry, \"{product.ProductName}\" only has {product.StockQuantity} left in stock (you requested {qty}).";
                }
            }

            foreach (var (productId, qty) in requestedQtyByProductId)
            {
                products[productId].StockQuantity -= qty;
            }

            return null;
        }

        // Gives stock back for every real-product line on an order — used
        // when an order that already reserved stock is cancelled/rejected
        // before production. order.OrderDetails must already be loaded.
        public static async Task RestoreStockAsync(ApplicationDbContext context, Order order)
        {
            var productIds = order.OrderDetails
                .Where(d => d.ProductID.HasValue)
                .Select(d => d.ProductID!.Value)
                .Distinct()
                .ToList();
            if (productIds.Count == 0) return;

            var products = await context.Products
                .Where(p => productIds.Contains(p.ProductID))
                .ToDictionaryAsync(p => p.ProductID);

            foreach (var detail in order.OrderDetails.Where(d => d.ProductID.HasValue))
            {
                if (products.TryGetValue(detail.ProductID!.Value, out var product))
                    product.StockQuantity += detail.Quantity;
            }
        }
    }
}
