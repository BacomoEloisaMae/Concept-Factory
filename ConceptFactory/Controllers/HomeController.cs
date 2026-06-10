using Microsoft.AspNetCore.Mvc;

namespace ConceptFactory.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult pIndex()
        {
            return View();
        }
    }
}
