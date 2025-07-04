using Kutip.Models;
using Kutip.Data;
using Kutip.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering; 
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Kutip.Controllers
{
    [Authorize(Roles = nameof(Roles.Operator))] 
    public class TrucksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public TrucksController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index(string searchPlateNo)
        {
            var trucksQuery = _context.Trucks.Include(t => t.Schedules).AsQueryable();

            if (!string.IsNullOrEmpty(searchPlateNo))
            {
                trucksQuery = trucksQuery.Where(t => t.t_PlateNo.Contains(searchPlateNo));
                ViewBag.SearchPlateNo = searchPlateNo; 
            }

            var allTrucks = await _context.Trucks.Include(t => t.Schedules).ToListAsync(); 
            var filteredTrucks = await trucksQuery.ToListAsync(); 

            ViewBag.TotalTrucks = allTrucks.Count;
            ViewBag.AvailableTrucks = allTrucks.Count(t => t.t_Status == "Available");
            ViewBag.MaintenanceTrucks = allTrucks.Count(t => t.t_Status == "Maintenance");

            ViewBag.TrucksWithSchedules = await _context.Schedules
                                                        .Select(s => s.t_ID)
                                                        .Distinct()
                                                        .CountAsync();

            return View(filteredTrucks); 
        }

        public async Task<IActionResult> Details(int? id, DateTime? searchDate, string searchLocationAddress2, string searchDriverId)
        {
            if (id == null)
            {
                return NotFound();
            }

            var truck = await _context.Trucks
                .FirstOrDefaultAsync(m => m.t_ID == id);
            if (truck == null)
            {
                return NotFound();
            }

            var schedulesQuery = _context.Schedules
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Location)
                .Include(s => s.AssignedUser)
                .Where(s => s.t_ID == id)
                .AsQueryable();

            if (searchDate.HasValue)
            {
                schedulesQuery = schedulesQuery.Where(s => s.s_Date.Date == searchDate.Value.Date);
            }

            if (!string.IsNullOrEmpty(searchLocationAddress2))
            {
                schedulesQuery = schedulesQuery.Where(s => s.Bin.Location.l_Address2 == searchLocationAddress2);
            }

            if (!string.IsNullOrEmpty(searchDriverId))
            {
                schedulesQuery = schedulesQuery.Where(s => s.AssignedUser_ID == searchDriverId);
            }

            schedulesQuery = schedulesQuery.OrderByDescending(s => s.s_Date).ThenByDescending(s => s.s_PickupTime);

            var schedules = await schedulesQuery.ToListAsync();

            ViewBag.Schedules = schedules;
            ViewBag.SearchDate = searchDate;
            ViewBag.SearchLocationAddress2 = searchLocationAddress2;
            ViewBag.SearchDriverId = searchDriverId;

            ViewBag.UniqueLocations = await _context.Locations
                .Select(l => l.l_Address2)
                .Distinct()
                .OrderBy(address => address)
                .ToListAsync();

            var driverRoleId = (await _roleManager.FindByNameAsync(Roles.Driver.ToString()))?.Id;

            var allDrivers = new List<dynamic>();
            if (driverRoleId != null)
            {
                allDrivers = await _context.UserRoles
                    .Where(ur => (driverRoleId != null && ur.RoleId == driverRoleId))
                    .Join(_context.Users,
                        userRole => userRole.UserId,
                        user => user.Id,
                        (userRole, user) => new
                        {
                            Id = user.Id,
                            Name = user.Name
                        })
                    .Distinct()
                    .OrderBy(u => u.Name)
                    .ToListAsync<dynamic>(); 
            }
            ViewBag.AllDrivers = allDrivers;

            return View(truck);
        }

        public IActionResult Create()
        {
            ViewBag.StatusOptions = new SelectList(new List<string> { "Available", "Maintenance" }, "Available");
            return View(new Truck { t_Status = "Available" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("t_ID,t_PlateNo,t_Model,t_Capacity,t_Status")] Truck truck)
        {
            if (ModelState.IsValid)
            {
                _context.Add(truck);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.StatusOptions = new SelectList(new List<string> { "Available", "Maintenance" }, truck.t_Status); // Re-populate on error
            return View(truck);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var truck = await _context.Trucks.FindAsync(id);
            if (truck == null)
            {
                return NotFound();
            }
            ViewBag.StatusOptions = new SelectList(new List<string> { "Available", "Maintenance" }, truck.t_Status);
            return View(truck);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("t_ID,t_PlateNo,t_Model,t_Capacity,t_Status")] Truck truck)
        {
            if (id != truck.t_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(truck);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TruckExists(truck.t_ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.StatusOptions = new SelectList(new List<string> { "Available", "Maintenance" }, truck.t_Status);
            return View(truck);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var truck = await _context.Trucks
                .FirstOrDefaultAsync(m => m.t_ID == id);
            if (truck == null)
            {
                return NotFound();
            }

            return View(truck);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var truck = await _context.Trucks.FindAsync(id);
            if (truck != null)
            {
                _context.Trucks.Remove(truck);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TruckExists(int id)
        {
            return _context.Trucks.Any(e => e.t_ID == id);
        }
    }
}
