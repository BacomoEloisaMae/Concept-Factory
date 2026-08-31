using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ConceptFactory.Filters
{
    // Gates the customer "account" pages — Checkout, Track Order, order
    // details/notifications — behind a logged-in Customer (Session
    // ["CustomerID"], set by AccountController.Login/Register). Browsing
    // the storefront itself (Home, Products, Customize) never needs this.
    //
    // Only used on plain page GET actions that render a full view — the
    // SubmitOrder AJAX endpoint checks the session itself and returns
    // JSON instead, since a redirect result here would come back as HTML
    // and break the fetch() call in billing-payment.js.
    public class CustomerAuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            if (session.GetInt32("CustomerID") == null)
            {
                string returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
