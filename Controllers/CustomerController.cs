using Kutip.Data;
using Kutip.Models;
using Kutip.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

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
            customers = _context.Customers.ToList();

            return View(customers);
        }

        public IActionResult Create()
        {
            var viewModel = new CustomerBinLocationViewModel
            {
                ColAreaList = GetColArea()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CustomerBinLocationViewModel viewModel)
        {

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

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var customer = _context.Customers.FirstOrDefault(c => c.c_ID == id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirm(int id)
        {
            var customer = _context.Customers.FirstOrDefault(c => c.c_ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            // Find related bin
            var bin = _context.Bins.FirstOrDefault(b => b.c_ID == customer.c_ID);

            // Find related location (if the bin exists and linked to a location)
            if (bin != null)
            {
                var location = _context.Locations.FirstOrDefault(l => l.l_ID == bin.l_ID);

                // Remove bin
                _context.Bins.Remove(bin);

                // Optional: Remove location
                if (location != null)
                {
                    _context.Locations.Remove(location);
                }
            }

            // Remove customer
            _context.Customers.Remove(customer);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        private List<SelectListItem> GetColArea()
        {
            List<SelectListItem> selColArea = new List<SelectListItem>();

            selColArea.Add(new SelectListItem
            {
                Value = "",
                Text = "Select Collection Area"
            });

            selColArea.Add(new SelectListItem
            {
                Value = "Area A",
                Text = "Area A"
            });

            selColArea.Add(new SelectListItem
            {
                Value = "Area B",
                Text = "Area B"
            });

            selColArea.Add(new SelectListItem
            {
                Value = "Area C",
                Text = "Area C"
            });

            return selColArea;
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var customer = _context.Customers.FirstOrDefault(c => c.c_ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            var bin = _context.Bins.FirstOrDefault(b => b.c_ID == id);
            var location = bin != null ? _context.Locations.FirstOrDefault(l => l.l_ID == bin.l_ID) : null;

            var viewModel = new CustomerBinLocationViewModel
            {
                Customer = customer,
                Bin = bin ?? new Bin(),
                Location = location ?? new Location()
            };
            viewModel.ColAreaList = GetColArea();
            viewModel.ColAreaList.ForEach(item =>
            {
                if (item.Value == viewModel.Location.l_ColArea)
                    item.Selected = true;
            });
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, CustomerBinLocationViewModel viewModel)
        {

            var customer = _context.Customers.FirstOrDefault(c => c.c_ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            var bin = _context.Bins.FirstOrDefault(b => b.b_ID == viewModel.Bin.b_ID);
            var location = _context.Locations.FirstOrDefault(l => l.l_ID == viewModel.Location.l_ID);

            // Update Customer
            customer.c_Name = viewModel.Customer.c_Name;
            customer.c_Email = viewModel.Customer.c_Email;
            customer.c_ContactNo = viewModel.Customer.c_ContactNo;
            _context.Customers.Update(customer);

            // Update Bin
            if (bin != null)
            {
                bin.b_PlateNo = viewModel.Bin.b_PlateNo;
                _context.Bins.Update(bin);
            }

            // Update Location
            if (location != null)
            {
                location.l_Address1 = viewModel.Location.l_Address1;
                location.l_Address2 = viewModel.Location.l_Address2;
                location.l_Postcode = viewModel.Location.l_Postcode;
                location.l_District = viewModel.Location.l_District;
                location.l_State = viewModel.Location.l_State;
                location.l_ColArea = viewModel.Location.l_ColArea;
                _context.Locations.Update(location);

            }

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        public IActionResult Details(int id)
        {
            // Load customer by ID
            var customer = _context.Customers.FirstOrDefault(c => c.c_ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            // Load related bin
            var bin = _context.Bins.FirstOrDefault(b => b.c_ID == id);

            // Load related location (assumes bin has l_ID)
            Location location = null;
            if (bin != null)
            {
                location = _context.Locations.FirstOrDefault(l => l.l_ID == bin.l_ID);
            }

            var viewModel = new CustomerBinLocationViewModel
            {
                Customer = customer,
                Bin = bin ?? new Bin(), // to prevent null reference
                Location = location ?? new Location()
            };

            return View(viewModel);
        }


    }
}