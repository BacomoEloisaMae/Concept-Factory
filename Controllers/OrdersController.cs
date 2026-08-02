using Microsoft.AspNetCore.Mvc;

namespace ConceptFactory.Controllers
{
    // UI-only for now — oIndex renders with static/dummy data.
    // Wire this up to _context.Orders once the Orders table/backend is ready.
    public class OrdersController : Controller
    {
        public IActionResult oIndex()
        {
            return View();
        }
    }
}
