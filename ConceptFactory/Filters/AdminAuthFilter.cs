using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ConceptFactory.Filters
{
    // Gates every admin-panel controller/action. Session["StaffRole"] is
    // set by AuthController.Login to "Admin", "Sales", or "Production"
    // (see there). Not logged in at all -> straight to Login. Logged in
    // but as a role this action doesn't allow -> sent to THEIR landing
    // page instead of a dead end, so Sales/Production never see a bare
    // "access denied" screen, just their own area.
    //
    // Usage: [AdminAuthFilter] defaults to Admin-only. Open a
    // controller/action to Sales and/or Production too with
    // [AdminAuthFilter(Roles = "Admin,Sales")].
    public class AdminAuthFilter : ActionFilterAttribute
    {
        public string Roles { get; set; } = "Admin";

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            string? staffName = session.GetString("AdminName");
            string? staffRole = session.GetString("StaffRole");

            if (staffName == null || staffRole == null)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            var allowedRoles = Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!allowedRoles.Contains(staffRole))
            {
                context.Result = staffRole switch
                {
                    "Production" => new RedirectToActionResult("pStation", "Production", new { stage = session.GetInt32("StaffStationIndex") ?? 0 }),
                    "Sales" => new RedirectToActionResult("oIndex", "Orders", null),
                    _ => new RedirectToActionResult("pIndex", "Home", null)
                };
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
