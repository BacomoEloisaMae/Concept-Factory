using Microsoft.AspNetCore.Http;
using ConceptFactory.Data;
using ConceptFactory.Models;

namespace ConceptFactory.Utils
{
    // Centralizes writing to ActivityLogs so every controller logs the same
    // way — one line at the point of change instead of each controller
    // building its own ActivityLog row. AdminName is read from the session
    // by default (matches how every other admin screen identifies "who's
    // logged in"); pass adminNameOverride for the two auth edge cases where
    // that doesn't work — a failed login (no session yet) and logout
    // (session already cleared by the time you'd want to log it).
    public static class ActivityLogger
    {
        public static async Task LogAsync(
            ApplicationDbContext context,
            HttpContext httpContext,
            string category,
            string action,
            string? details = null,
            int? orderId = null,
            string? adminNameOverride = null)
        {
            context.ActivityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now,
                AdminName = adminNameOverride ?? httpContext.Session.GetString("AdminName"),
                Category = category,
                Action = action,
                Details = details,
                OrderID = orderId
            });
            await context.SaveChangesAsync();
        }
    }
}
