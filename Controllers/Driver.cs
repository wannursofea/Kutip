using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kutip.Controllers
{
    [Authorize(Roles = "Driver")]
    public class Driver : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
