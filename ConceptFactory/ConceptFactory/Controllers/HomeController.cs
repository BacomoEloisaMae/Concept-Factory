using Microsoft.AspNetCore.Mvc;

namespace ConceptFactory.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Products");
        }
    }
}
