using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kutip.Controllers
{
    [Authorize(Roles = "Collector")] // This ensures only users with the "Collector" role can access this controller
    public class CollectorController : Controller
    {
        public IActionResult Index()
        {
            // This action will return the Views/Collector/Index.cshtml
            return View();
        }

        // You can add other actions specific to the Collector role here
        // For example:
        // public IActionResult Dashboard()
        // {
        //     return View();
        // }
    }
}