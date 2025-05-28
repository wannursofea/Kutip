using Microsoft.AspNetCore.Mvc;

namespace Kutip.Controllers
{
    public class ProductController : Controller
    {
        public IActionResult GetAll()
        { 
            return View();
        }
    }
}
