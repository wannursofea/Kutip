using Kutip.Data;
using Kutip.Models;
using Kutip.Services;
using Kutip.Services.Interfaces;
using Kutip.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kutip.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGeocodingService _geocodingService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ApplicationDbContext context, IGeocodingService geocodingService, ILogger<CustomerController> logger)
        {
            _context = context;
            _geocodingService = geocodingService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                                          .Include(c => c.Bins)
                                              .ThenInclude(b => b.Location)
                                          .ToListAsync();
            return View(customers);
        }

        public IActionResult Create()
        {
            var viewModel = new CustomerBinLocationViewModel
            {
                Location = new Location(),
                Bin = new Bin(),
                Customer = new Customer(),
                ColAreaList = GetColArea()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerBinLocationViewModel viewModel)
        {
            ModelState.Remove("Customer.c_ID");
            ModelState.Remove("Bin.b_ID");
            ModelState.Remove("Bin.c_ID");
            ModelState.Remove("Bin.l_ID");
            ModelState.Remove("Location.l_ID");
            ModelState.Remove("Location.l_ColArea");
            ModelState.Remove("Bin.Customer");
            ModelState.Remove("Bin.Location");
            ModelState.Remove("Bin.c_ID");
            ModelState.Remove("Bin.l_ID");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid after removing auto-generated ID and navigation property errors.");
                foreach (var modelStateEntry in ModelState.Values)
                {
                    foreach (var error in modelStateEntry.Errors)
                    {
                        if (!string.IsNullOrEmpty(error.ErrorMessage))
                        {
                            _logger.LogError($"Model Error: {error.ErrorMessage}");
                        }
                        else if (error.Exception != null)
                        {
                            _logger.LogError($"Model Exception: {error.Exception.Message}");
                        }
                    }
                }
                viewModel.ColAreaList = GetColArea(); 
                return View(viewModel);
            }

            try
            {
                _context.Customers.Add(viewModel.Customer);
                await _context.SaveChangesAsync();

                _context.Locations.Add(viewModel.Location);
                await _context.SaveChangesAsync();

                viewModel.Bin.c_ID = viewModel.Customer.c_ID;
                viewModel.Bin.l_ID = viewModel.Location.l_ID;

                _context.Bins.Add(viewModel.Bin);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created Customer, Location, and Bin.");
                return RedirectToAction(nameof(Index)); 
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "An error occurred while saving the new Customer, Location, or Bin to the database.");
                ModelState.AddModelError("", "An error occurred while saving the data. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during customer creation.");
                ModelState.AddModelError("", "An unexpected error occurred. Please contact support.");
            }

            viewModel.ColAreaList = GetColArea();
            return View(viewModel);
        }


        [HttpGet]
        public async Task<IActionResult> Delete(int? id) 
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                                         .FirstOrDefaultAsync(c => c.c_ID == id); 
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }



        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirm(int id)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.c_ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            var bins = await _context.Bins
                                     .Where(b => b.c_ID == customer.c_ID)
                                     .ToListAsync();

            foreach (var bin in bins)
            {
                int binId = bin.b_ID;
                int locationId = bin.l_ID;

                var schedulesForBin = await _context.Schedules
                                                    .Where(s => s.b_ID == binId)
                                                    .ToListAsync();
                _context.Schedules.RemoveRange(schedulesForBin);

                var schedulesForLocation = await _context.Schedules
                                                         .Where(s => s.l_ID == locationId)
                                                         .ToListAsync();
                _context.Schedules.RemoveRange(schedulesForLocation);

                _context.Bins.Remove(bin);

                var location = await _context.Locations.FirstOrDefaultAsync(l => l.l_ID == locationId);
                if (location != null)
                {
                    _context.Locations.Remove(location);
                }
            }

            await _context.SaveChangesAsync();                     

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

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

            var johorAreas = new Dictionary<string, List<string>>
            {
                { "Johor Bahru", new List<string> { "Jelutong", "Plentong", "Pulai", "Sungai Tiram", "Tanjung Kupang", "Tebrau", "Bandar Johor Bahru" } },
                { "Kulai", new List<string> { "Kulai", "Senai", "Sedenak", "Bukit Batu", "Bandar Kulai" } },
                { "Pontian", new List<string> { "Ayer Baloi", "Air Masin", "Api-Api", "Benut", "Jeram Batu", "Pengkalan Raja", "Pontian", "Rimba Terjun", "Serkat", "Sungai Karang", "Sungei Pinggan", "Bandar Benut", "Bandar Pontian Kechil", "Pekan Nenas" } },
                { "Kota Tinggi", new List<string> { "Ulu Sungai Johor", "Ulu Sungei Sedili Besar", "Johor Lama", "Kambau", "Kota Tinggi", "Pantai Timur", "Pengerang", "Sedili Besar", "Sedili Kechil", "Tanjung Surat", "Bandar Kota Tinggi" } },
                { "Kluang", new List<string> { "Ulu Benut", "Kahang", "Kluang", "Layang-Layang", "Machap", "Niyor", "Paloh", "Renggam", "Bandar Kluang", "Bandar Paloh", "Bandar Renggam" } },
                { "Batu Pahat", new List<string> { "Bagan", "Chaah Bahru", "Kampung Bahru", "Linau", "Lubok", "Minyak Beku", "Peserai", "Sri Gading", "Sri Medan", "Simpang Kanan", "Simpang Kiri", "Sungai Kluang", "Sungai Punggor", "Tanjung Sembrong" } },
                { "Muar", new List<string> { "Ayer Hitam", "Bandar Maharani", "Bukit Kepong", "Jalan Bakri", "Jorak", "Lenga", "Parit Bakar", "Parit Jawa", "Sri Menanti", "Sungai Balang", "Sungai Raya", "Sungai Terap" } },
                { "Tangkak", new List<string> { "Tangkak", "Grisek", "Serom", "Bukit Serampang", "Kesang", "Kundang" } },
                { "Segamat", new List<string> { "Bekok", "Buloh Kasap", "Chaah", "Gemas", "Gemereh", "Jabi", "Jementah", "Labis", "Pogoh", "Sermin", "Sungai Segamat" } },
                { "Mersing", new List<string> { "Jemaluang", "Lenggor", "Mersing", "Padang Endau", "Penyabong", "Pulau Aur", "Pulau Babi", "Pulau Pemanggil", "Pulau Sibu", "Pulau Tinggi", "Sembrong", "Tenggaroh", "Tenglu", "Triang" } }
            };

            foreach (var district in johorAreas)
            {
                var districtGroup = new SelectListGroup { Name = district.Key };

                foreach (var mukim in district.Value)
                {
                    selColArea.Add(new SelectListItem
                    {
                        Value = mukim,
                        Text = mukim,
                        Group = districtGroup
                    });
                }
            }

            return selColArea;
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id) 
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.c_ID == id); 
            if (customer == null)
            {
                return NotFound();
            }

            var bin = await _context.Bins.Include(b => b.Location).FirstOrDefaultAsync(b => b.c_ID == id);
            var location = bin?.Location; 

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
        public async Task<IActionResult> Edit(int id, CustomerBinLocationViewModel viewModel) 
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.c_ID == id); 
            if (customer == null)
            {
                return NotFound();
            }

            var bin = await _context.Bins.FirstOrDefaultAsync(b => b.b_ID == viewModel.Bin.b_ID); 
            var location = await _context.Locations.FirstOrDefaultAsync(l => l.l_ID == viewModel.Location.l_ID); 

            customer.c_Name = viewModel.Customer.c_Name;
            customer.c_Email = viewModel.Customer.c_Email;
            customer.c_ContactNo = viewModel.Customer.c_ContactNo;
            _context.Customers.Update(customer);

            if (bin != null)
            {
                bin.b_PlateNo = viewModel.Bin.b_PlateNo;
                _context.Bins.Update(bin);
            }

            if (location != null)
            {
                location.l_Address1 = viewModel.Location.l_Address1;
                location.l_Address2 = viewModel.Location.l_Address2;
                location.l_Postcode = viewModel.Location.l_Postcode;
                location.l_District = viewModel.Location.l_District;
                location.l_State = viewModel.Location.l_State;
                location.l_ColArea = viewModel.Location.l_ColArea;
                location.Latitude = viewModel.Location.Latitude;
                location.Longitude = viewModel.Location.Longitude;
                _context.Locations.Update(location);
            }

            await _context.SaveChangesAsync(); 

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Details(int id) 
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.c_ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            var bin = await _context.Bins.Include(b => b.Location).FirstOrDefaultAsync(b => b.c_ID == id);
            var location = bin?.Location;

            var viewModel = new CustomerBinLocationViewModel
            {
                Customer = customer,
                Bin = bin ?? new Bin(),
                Location = location ?? new Location()
            };

            return View(viewModel);
        }
    }
}
