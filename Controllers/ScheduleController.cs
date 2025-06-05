using Kutip.Models;
using Kutip.Data;
using Kutip.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering; 

namespace Kutip.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ScheduleController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager) 
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }


        // GET: All Schedules authorized to all users
        public async Task<IActionResult> Index(
            DateTime? date, // For the "Date Range" filter
            string locationSearch, // For the "Location" text input
            string status, // For the "Status" dropdown
            string searchInput 
         )
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);
            var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());
            var isCollector = await _userManager.IsInRoleAsync(currentUser, Roles.Collector.ToString());
            var isDriver = await _userManager.IsInRoleAsync(currentUser, Roles.Driver.ToString());

            IQueryable<Schedule> schedules = _context.Schedules
                                                     .Include(s => s.AssignedUser) 
                                                     .Include(s => s.Bin)          
                                                         .ThenInclude(b => b.Location) 
                                                     .Include(s => s.Location);     

            if ((isCollector || isDriver) && !isOperator)
            {
                //schedules = schedules.Where(s => s.AssignedUser_ID == currentUserId);
                schedules = schedules.Where(s => s.AssignedUser_ID == currentUserId).Include(s => s.Location);

            }

            if (date.HasValue)
            {
                schedules = schedules.Where(s => s.s_Date.Date == date.Value.Date);
            }

            if (!string.IsNullOrEmpty(locationSearch))
            {
                schedules = schedules.Where(s => s.Location != null && s.Location.l_Address1.Contains(locationSearch));
            }

            if (!string.IsNullOrEmpty(status))
            {
                switch (status.ToLower())
                {
                    case "complete":
                        schedules = schedules.Where(s => s.PickedUpBins == s.TotalBins && s.TotalBins > 0);
                        break;
                    case "partial":
                        schedules = schedules.Where(s => s.PickedUpBins > 0 && s.PickedUpBins < s.TotalBins);
                        break;
                    case "pending":
                        schedules = schedules.Where(s => s.PickedUpBins == 0 || s.TotalBins == 0);
                        break;
                }
            }

            // 4. Server-Side Search (if you choose to move it from client-side JS)
            // If you want the "Search schedules..." input to filter on the server, uncomment/add this:
            /*
            if (!string.IsNullOrEmpty(searchInput))
            {
                schedules = schedules.Where(s =>
                    s.AssignedUser.Email.Contains(searchInput) || // Example: search by driver/collector email
                    s.Bin.b_Name.Contains(searchInput) ||          // Example: search by bin name
                    s.Location.l_Address1.Contains(searchInput) || // Example: search by location address
                    s.Location.l_Address2.Contains(searchInput)    // Example: search by location address 2
                );
            }
            */

            var orderedSchedules = await schedules.OrderBy(s => s.s_Date)
                                                  .ThenBy(s => s.s_PickupTime)
                                                  .ToListAsync();

            ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd"); 
            ViewBag.SelectedLocationSearch = locationSearch;
            ViewBag.SelectedStatus = status;

            return View(await schedules.ToListAsync());
        }


        // SCHEDULE DETAILS BY ID
        // GET : Schedule/Details/
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);
            var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());

            var schedule = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Location) 
                .Include(s => s.Location) 
                .FirstOrDefaultAsync(m => m.s_ID == id); 

            if (schedule == null)
            {
                return NotFound();
            }

            if (!isOperator && schedule.AssignedUser_ID != currentUserId)
            {
                return Forbid(); 
            }

            return View(schedule);
        }


        // CREATE SCHEDULE
        // GET: Schedule/Create
        [Authorize(Roles = nameof(Roles.Operator))]
        public async Task<IActionResult> Create()
        {
            var collectors = await _userManager.GetUsersInRoleAsync(Roles.Collector.ToString());
            var drivers = await _userManager.GetUsersInRoleAsync(Roles.Driver.ToString());
            var assignedUsers = collectors.Concat(drivers).ToList();

            ViewBag.AssignedUsers = new SelectList(assignedUsers, "Id", "Email");
            ViewBag.Bins = new SelectList(await _context.Bins.Include(b => b.Location).ToListAsync(), "b_ID", "Location.l_Address1");
            ViewBag.l_ID = new SelectList(await _context.Locations.ToListAsync(), "l_ID", "l_Address1");

            return View(new Schedule
            {
                s_Date = DateTime.Today,
                s_PickupTime = DateTime.Now.TimeOfDay,
                PickedUpBins = 0,
                TotalBins = 1
            });
        }

        // POST: Schedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("s_Date,s_PickupTime,s_PickupEnd,l_ID,PickedUpBins,TotalBins,AssignedUser_ID,b_ID")] Schedule schedule)
        {
            if (schedule.s_Date == default(DateTime))
            {
                ModelState.AddModelError("s_Date", "Pickup Date is required.");
            }
            if (schedule.l_ID == 0) 
            {
                ModelState.AddModelError("l_ID", "Location is required.");
            }
            if (schedule.s_PickupTime == TimeSpan.Zero)
            {
                ModelState.AddModelError("s_PickupTime", "Start Time is required.");
            }
            if (schedule.s_PickupEnd == TimeSpan.Zero) 
            {
                ModelState.AddModelError("s_PickupEnd", "End Time is required.");
            }
            if (string.IsNullOrEmpty(schedule.AssignedUser_ID))
            {
                ModelState.AddModelError("AssignedUser_ID", "An assigned user is required.");
            }
            if (schedule.b_ID == 0) 
            {
                ModelState.AddModelError("b_ID", "A bin is required.");
            }

            if (schedule.PickedUpBins < 0)
            {
                ModelState.AddModelError("PickedUpBins", "Picked Up Bins cannot be negative.");
            }
            if (schedule.TotalBins <= 0)
            {
                ModelState.AddModelError("TotalBins", "Total Bins must be greater than zero.");
            }
            if (schedule.PickedUpBins > schedule.TotalBins)
            {
                ModelState.AddModelError("PickedUpBins", "Picked Up Bins cannot be greater than Total Bins.");
            }

            if (schedule.s_PickupEnd <= schedule.s_PickupTime)
            {
                ModelState.AddModelError("s_PickupEnd", "End time must be later than start time.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(schedule); 
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Schedule created successfully!";
                    return RedirectToAction(nameof(Index)); // Redirect to the list of schedules
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"An unexpected error occurred while creating the schedule: {ex.Message}";
                }
            }

            await PopulateCreateViewBags(schedule.l_ID, schedule.AssignedUser_ID, schedule.b_ID);

            TempData["ErrorMessage"] = TempData["ErrorMessage"] ?? "Please correct the errors in the form.";

            return View(schedule);
        }


        // SCHEDULE EDIT BY ID
        // GET: Schedule/Edit/
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);
            var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());

            var schedule = await _context.Schedules
                                    .Include(s => s.Location) 
                                    .FirstOrDefaultAsync(m => m.s_ID == id);

            if (schedule == null)
            {
                return NotFound();
            }

            if (!isOperator && schedule.AssignedUser_ID != currentUserId)
            {
                return Forbid();
            }

            if (isOperator)
            {
                var collectors = await _userManager.GetUsersInRoleAsync(Roles.Collector.ToString());
                var drivers = await _userManager.GetUsersInRoleAsync(Roles.Driver.ToString());
                var assignedUsers = collectors.Concat(drivers).ToList();

                ViewBag.AssignedUsers = new SelectList(assignedUsers, "Id", "Email", schedule.AssignedUser_ID);
                ViewBag.Bins = new SelectList(await _context.Bins.Include(b => b.Location).ToListAsync(), "b_ID", "Location.l_Address1", schedule.b_ID);
                ViewBag.l_ID = new SelectList(await _context.Locations.ToListAsync(), "l_ID", "l_Address1", schedule.l_ID);
            }

            return View(schedule);
        }

        // POST: Schedule/Edit/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("s_ID,AssignedUser_ID,b_ID,s_Date,s_PickupTime,s_PickupEnd,l_ID,PickedUpBins,TotalBins")] Schedule schedule)
        {
            if (id != schedule.s_ID) 
            {
                return NotFound();
            }

            var existingSchedule = await _context.Schedules
                                        .Include(s => s.Location)
                                        .FirstOrDefaultAsync(s => s.s_ID == id); 
            if (existingSchedule == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);
            var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());

            if (!isOperator && existingSchedule.AssignedUser_ID != currentUserId) 
            {
                return Forbid();
            }

            if (schedule.PickedUpBins < 0)
            {
                ModelState.AddModelError("PickedUpBins", "Picked Up Bins cannot be negative.");
            }
            if (schedule.TotalBins <= 0) 
            {
                ModelState.AddModelError("TotalBins", "Total Bins must be greater than zero.");
            }
            if (schedule.PickedUpBins > schedule.TotalBins) 
            {
                ModelState.AddModelError("PickedUpBins", "Picked Up Bins cannot be greater than Total Bins.");
            }

            if (schedule.s_PickupEnd <= schedule.s_PickupTime)
            {
                ModelState.AddModelError("s_PickupEnd", "End time must be later than start time.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (!isOperator) 
                    {
                        existingSchedule.PickedUpBins = schedule.PickedUpBins;
                    }
                    else // Operator: Can update all fields
                    {
                        existingSchedule.AssignedUser_ID = schedule.AssignedUser_ID;
                        existingSchedule.b_ID = schedule.b_ID; 
                        existingSchedule.s_Date = schedule.s_Date; 
                        existingSchedule.s_PickupTime = schedule.s_PickupTime;
                        existingSchedule.s_PickupEnd = schedule.s_PickupEnd;
                        existingSchedule.l_ID = schedule.l_ID;
                        existingSchedule.PickedUpBins = schedule.PickedUpBins;
                        existingSchedule.TotalBins = schedule.TotalBins;
                    }

                    _context.Update(existingSchedule);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Schedule updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ScheduleExists(schedule.s_ID)) 
                    {
                        TempData["ErrorMessage"] = "Schedule not found. It might have been deleted.";
                        return NotFound();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "A concurrency error occurred. The schedule was modified by another user. Please try again.";
                    }
                }
                catch (Exception ex) 
                {
                    TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                }
            }

            await PopulateEditViewBags(schedule, isOperator);
            TempData["ErrorMessage"] = TempData["ErrorMessage"] ?? "Please correct the errors in the form.";

            return View(schedule);
        }


        // SCHEDULE DELETE BY ID
        // GET: Schedule/Delete/
        [Authorize(Roles = nameof(Roles.Operator))] 
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var schedule = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Location) 
                .Include(s => s.Location) 
                .FirstOrDefaultAsync(m => m.s_ID == id); 

            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }

        // POST: Schedule/Delete/
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Roles.Operator))] 
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule == null)
            {
                TempData["ErrorMessage"] = "The schedule to be deleted was not found. It might have already been deleted.";
                return RedirectToAction(nameof(Index)); 
            }

            try
            {
                _context.Schedules.Remove(schedule); 
                await _context.SaveChangesAsync();   

                TempData["SuccessMessage"] = "Schedule deleted successfully!";
                return RedirectToAction(nameof(Index)); 
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while deleting the schedule: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool ScheduleExists(int id)
        {
            return _context.Schedules.Any(e => e.s_ID == id); 
        }

        private async Task PopulateEditViewBags(Schedule schedule, bool isOperator)
        {
            ViewBag.l_ID = new SelectList(await _context.Locations.ToListAsync(), "l_ID", "l_Address1", schedule.l_ID);

            if (isOperator)
            {
                var collectors = await _userManager.GetUsersInRoleAsync(Roles.Collector.ToString());
                var drivers = await _userManager.GetUsersInRoleAsync(Roles.Driver.ToString());
                var assignedUsers = collectors.Concat(drivers).ToList();

                ViewBag.AssignedUsers = new SelectList(assignedUsers, "Id", "Email", schedule.AssignedUser_ID);
                ViewBag.Bins = new SelectList(await _context.Bins.Include(b => b.Location).ToListAsync(), "b_ID", "Location.l_Address1", schedule.b_ID);
            }
        }

        private async Task PopulateCreateViewBags(int? selectedLocationId = null, string selectedUserId = null, int? selectedBinId = null)
        {
            ViewBag.l_ID = new SelectList(await _context.Locations.ToListAsync(), "l_ID", "l_Address1", selectedLocationId);

            var collectors = await _userManager.GetUsersInRoleAsync(Roles.Collector.ToString());
            var drivers = await _userManager.GetUsersInRoleAsync(Roles.Driver.ToString());
            var assignedUsers = collectors.Concat(drivers).ToList();
            ViewBag.AssignedUsers = new SelectList(assignedUsers, "Id", "Email", selectedUserId);

            ViewBag.Bins = new SelectList(await _context.Bins.Include(b => b.Location).ToListAsync(), "b_ID", "Location.l_Address1", selectedBinId);
        }
    }
}