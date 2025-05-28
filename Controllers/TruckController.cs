using Microsoft.AspNetCore.Mvc;

namespace Kutip.Controllers
{
    public class TruckController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
