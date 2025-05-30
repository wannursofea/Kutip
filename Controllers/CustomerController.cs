using Kutip.Data;
using Kutip.Models;
using Kutip.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Kutip.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        
        public CustomerController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            List<Customer> customers;
            customers=_context.Customers.ToList();

            return View(customers);
        }

        public IActionResult Create()
        {
            return View(new CustomerBinLocationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CustomerBinLocationViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine("Validation error: " + error.ErrorMessage);
                }
                return View(viewModel);
            }

            // Save Location first
            _context.Locations.Add(viewModel.Location);
            _context.SaveChanges(); // to get location ID

            // Save Customer
            _context.Customers.Add(viewModel.Customer);
            _context.SaveChanges(); // to get customer ID

            // Save Bin, link to location and customer
            viewModel.Bin.c_ID = viewModel.Customer.c_ID;
            viewModel.Bin.l_ID = viewModel.Location.l_ID;
            _context.Bins.Add(viewModel.Bin);
            _context.SaveChanges();

            return RedirectToAction("Index");
            
        }
    }
}
