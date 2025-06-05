using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Kutip.Models;
using Kutip.Data;
using Kutip.Constants;

namespace Kutip.Controllers
{
    [Authorize] 
    public class LocationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LocationController(ApplicationDbContext context) 
        {
            _context = context;
        }

        // GET: LocationController
        public async Task<IActionResult> Index()
        {
            try
            {
                var locations = await _context.Locations.ToListAsync(); 
                return View(locations);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading locations: " + ex.Message;
                return View(new List<Location>());
            }
        }

        // GET: LocationController/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var location = await _context.Locations
                    .Include(l => l.Bins) 
                                         
                    .FirstOrDefaultAsync(l => l.l_ID == id);

                if (location == null)
                {
                    TempData["ErrorMessage"] = "Location not found.";
                    return RedirectToAction(nameof(Index));
                }


                return View(location);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading location details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: LocationController/Create
        [Authorize(Roles = "Operator")]
        public IActionResult Create() 
        {
            return View();
        }

        // POST: LocationController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Operator")]
        public async Task<IActionResult> Create(
            [Bind("l_ID,l_Address1,l_Address2,l_District,l_Postcode,l_State")] Location location) 
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var existingLocation = await _context.Locations
                        .FirstOrDefaultAsync(l => l.l_Address1 == location.l_Address1 &&
                                                 l.l_Postcode == location.l_Postcode);

                    if (existingLocation != null)
                    {
                        ModelState.AddModelError("", "A location with this address and postcode already exists.");
                        return View(location);
                    }

                    _context.Add(location);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Location created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                return View(location);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error creating location: " + ex.Message;
                return View(location);
            }
        }

        // GET: LocationController/Edit/5
        [Authorize(Roles = "Operator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var location = await _context.Locations
                    .FirstOrDefaultAsync(l => l.l_ID == id);

                if (location == null)
                {
                    TempData["ErrorMessage"] = "Location not found.";
                    return RedirectToAction(nameof(Index));
                }
                return View(location);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading location for editing: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: LocationController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Operator")]
        public async Task<IActionResult> Edit(int id,
            [Bind("l_ID,l_Address1,l_Address2,l_District,l_Postcode,l_State")] Location location) 
        {
            if (id != location.l_ID)
            {
                TempData["ErrorMessage"] = "Invalid location ID.";
                return NotFound();
            }
            try
            {
                if (ModelState.IsValid)
                {
                    var existingLocation = await _context.Locations
                        .AsNoTracking()
                        .FirstOrDefaultAsync(l => l.l_Address1 == location.l_Address1 &&
                                                 l.l_Postcode == location.l_Postcode &&
                                                 l.l_ID != location.l_ID);

                    if (existingLocation != null)
                    {
                        ModelState.AddModelError("", "Another location with this address and postcode already exists.");
                        return View(location);
                    }

                    _context.Update(location);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Location updated successfully!";
                    return RedirectToAction(nameof(Index));
                }

                return View(location);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await LocationExists(location.l_ID))
                {
                    TempData["ErrorMessage"] = "Location not found.";
                    return NotFound();
                }
                else
                {
                    TempData["ErrorMessage"] = "Concurrency error occurred. The location might have been modified by another user. Please try again.";
                    return View(location);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating location: " + ex.Message;
                return View(location);
            }
        }

        // GET: LocationController/Delete/5
        [Authorize(Roles = "Operator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var location = await _context.Locations
                    .Include(l => l.Bins)
                    .FirstOrDefaultAsync(l => l.l_ID == id);

                if (location == null)
                {
                    TempData["ErrorMessage"] = "Location not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(location);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading location for deletion: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: LocationController/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Operator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var location = await _context.Locations
                    .Include(l => l.Bins)
                    .FirstOrDefaultAsync(l => l.l_ID == id);

                if (location == null)
                {
                    TempData["ErrorMessage"] = "Location not found. It might have already been deleted.";
                    return RedirectToAction(nameof(Index));
                }

                if (location.Bins != null && location.Bins.Any())
                {
                    TempData["ErrorMessage"] = $"Cannot delete location. It has {location.Bins.Count} associated bin(s). Please remove or reassign the bins first.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }

                _context.Locations.Remove(location);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Location deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting location: " + ex.Message;
                return RedirectToAction(nameof(Delete), new { id = id });
            }
        }

        private async Task<bool> LocationExists(int id)
        {
            return await _context.Locations.AnyAsync(e => e.l_ID == id);
        }

        // GET: LocationController/Search
        public async Task<IActionResult> Search(string searchTerm)
        {
            IQueryable<Location> locationsQuery = _context.Locations;

            if (!string.IsNullOrEmpty(searchTerm))
            {
                locationsQuery = locationsQuery.Where(l => l.l_Address1.Contains(searchTerm) ||
                                 l.l_District.Contains(searchTerm) ||
                                 l.l_State.Contains(searchTerm) ||
                                 l.l_Postcode.Contains(searchTerm));
            }

            try
            {
                var locations = await locationsQuery.ToListAsync();

                ViewBag.SearchTerm = searchTerm;
                return View("Index", locations);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error searching locations: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}