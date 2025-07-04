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
using System.Collections.Generic;

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

        public async Task<IActionResult> Index(DateTime? date, string status)
        {
            var schedules = _context.Schedules
                                    .Include(s => s.Location)
                                    .Include(s => s.AssignedUser)
                                    .Include(s => s.Bin)
                                    .Include(s => s.Truck) 
                                    .AsQueryable();

            if (date.HasValue)
            {
                schedules = schedules.Where(s => s.s_Date.Date == date.Value.Date);
                ViewBag.SelectedDate = date.Value.ToString("yyyy-MM-dd");
            }

            if (!string.IsNullOrEmpty(status))
            {
                string lowerStatus = status.ToLower();
                schedules = schedules.Where(s =>
                    (lowerStatus == "completed" && s.PickedUpBins >= s.TotalBins && s.TotalBins > 0) ||
                    (lowerStatus == "past due" && s.s_Date.Date < DateTime.Today.Date && !(s.PickedUpBins >= s.TotalBins && s.TotalBins > 0)) ||
                    (lowerStatus == "in progress" && s.s_Date.Date == DateTime.Today.Date && !(s.PickedUpBins
                     >= s.TotalBins && s.TotalBins > 0)) ||
                    (lowerStatus == "scheduled" && s.s_Date.Date > DateTime.Today.Date)
                );
                ViewBag.SelectedStatus = status;
            }

            return View(await schedules.ToListAsync());
        }

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
                .Include(s => s.Truck) 
                .FirstOrDefaultAsync(m => m.s_ID == id);

            if (schedule == null)
            {
                return NotFound();
            }

            if (!isOperator && schedule.AssignedUser_ID != currentUserId)
            {
                return Forbid();
            }

            var relatedSchedules = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Location)
                    .Include(s => s.Location)
                    .Include(s => s.Truck) 
                .Where(s => s.AssignedUser_ID == schedule.AssignedUser_ID &&
                           s.s_Date.Date == schedule.s_Date.Date &&
                           s.s_PickupTime == schedule.s_PickupTime &&
                           s.s_PickupEnd == schedule.s_PickupEnd &&
                           s.Location.l_Address2 == schedule.Location.l_Address2 &&
                           s.t_ID == schedule.t_ID) 
                .OrderBy(s => s.Bin.b_PlateNo)
                .ToListAsync();
            var totalBins = relatedSchedules.Count;
            var totalPickedUpBins = relatedSchedules.Sum(s => s.PickedUpBins);
            var completionPercentage = totalBins > 0 ? (double)totalPickedUpBins / totalBins * 100 : 0;
            string status;
            if (totalPickedUpBins >= totalBins && totalBins > 0)
                status = "Completed";
            else if (schedule.s_Date.Date < DateTime.Today.Date)
                status = "Past Due";
            else if (schedule.s_Date.Date == DateTime.Today.Date)
                status = "In Progress";
            else
                status = "Scheduled";
            ViewBag.RelatedSchedules = relatedSchedules;
            ViewBag.IsOperator = isOperator;
            ViewBag.TotalBins = totalBins;
            ViewBag.TotalPickedUpBins = totalPickedUpBins;
            ViewBag.CompletionPercentage = completionPercentage;
            ViewBag.Status = status;

            return View(schedule);
        }

        [Authorize(Roles = nameof(Roles.Operator))]
        public async Task<IActionResult> Create()
        {
            var driverRoleId = (await _roleManager.FindByNameAsync(Roles.Driver.ToString()))?.Id;

            var driverAndCollectorUsers = await _context.UserRoles
                .Where(ur => ur.RoleId == driverRoleId)
                .Join(_context.Users,
                    userRole => userRole.UserId,
                    user => user.Id,
                    (userRole, user) => new
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        RoleId = userRole.RoleId
                    })
                .ToListAsync();
            var uniqueLocations = await _context.Locations
                .GroupBy(l => l.l_Address2)
                .Select(g => g.First())
                .ToListAsync();

            var trucks = await _context.Trucks.ToListAsync();

            ViewBag.AssignedUsers = new SelectList(driverAndCollectorUsers, "Id", "Name");
            ViewBag.Bins = new SelectList(await _context.Bins.Include(b => b.Location).ToListAsync(), "b_ID", "b_PlateNo");
            ViewBag.l_ID = new SelectList(uniqueLocations, "l_ID", "l_Address2");
            ViewBag.Trucks = new SelectList(trucks, "t_ID", "t_PlateNo");

            return View(new Schedule
            {
                s_Date = DateTime.Today,
                s_PickupTime = DateTime.Now.TimeOfDay,
                PickedUpBins = 0,
                TotalBins = 1 
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetBinsForLocation(int locationId)
        {
            var location = await _context.Locations.FindAsync(locationId);
            if (location == null)
            {
                return Json(new { success = false, message = "Location not found" });
            }

            var similarLocations = await _context.Locations
                .Where(l => l.l_Address2 == location.l_Address2)
                .Select(l => l.l_ID)
                .ToListAsync();
            var bins = await _context.Bins
                .Where(b => similarLocations.Contains(b.l_ID))
                .Select(b => new { b.b_ID, b.b_PlateNo })
                .ToListAsync();
            return Json(new { success = true, bins = bins });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Roles.Operator))]
        public async Task<IActionResult> Create(
            [Bind("s_PickupTime,s_PickupEnd,l_ID,PickedUpBins,TotalBins,AssignedUser_ID,t_ID")] Schedule schedule,
            int selectedDayOfWeek, int selectedMonth)
        {
            ModelState.Remove("AssignedUser");
            ModelState.Remove("Bin");
            ModelState.Remove("Location");
            ModelState.Remove("s_Date");
            ModelState.Remove("Truck");

            List<int> similarLocationIds = new List<int>();

            var selectedLocation = await _context.Locations.FindAsync(schedule.l_ID);
            if (selectedLocation == null)
            {
                ModelState.AddModelError("l_ID", "Selected location not found.");
            }
            else
            {
                similarLocationIds = await _context.Locations
                    .Where(l => l.l_Address2 == selectedLocation.l_Address2)
                    .Select(l => l.l_ID)
                    .ToListAsync();
                var binsForLocation = await _context.Bins
                    .Where(b => similarLocationIds.Contains(b.l_ID))
                    .ToListAsync();
                if (!binsForLocation.Any())
                {
                    ModelState.AddModelError("l_ID", "No bins available for the selected location.");
                }
                else
                {
                    schedule.TotalBins = binsForLocation.Count;
                }
            }

            if (string.IsNullOrEmpty(schedule.AssignedUser_ID))
            {
                ModelState.AddModelError("AssignedUser_ID", "An assigned driver is required.");
            }

            if (schedule.t_ID <= 0)
            {
                ModelState.AddModelError("t_ID", "A truck must be assigned.");
            }

            if (schedule.s_PickupEnd <= schedule.s_PickupTime)
            {
                ModelState.AddModelError("s_PickupEnd", "End time must be later than start time.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    int year = DateTime.Now.Year;
                    var proposedDates = GetDatesForDayOfWeek(year, selectedMonth, (DayOfWeek)selectedDayOfWeek);

                    foreach (var date in proposedDates)
                    {
                        var conflictExists = await _context.Schedules
                            .AnyAsync(s => s.s_Date.Date == date.Date &&
                                           ((s.s_PickupTime < schedule.s_PickupEnd && s.s_PickupEnd > schedule.s_PickupTime) || 
                                            (s.s_PickupTime == schedule.s_PickupTime && s.s_PickupEnd == schedule.s_PickupEnd)) && 
                                           (s.AssignedUser_ID == schedule.AssignedUser_ID || s.t_ID == schedule.t_ID)); 

                        if (conflictExists)
                        {
                            ModelState.AddModelError("", $"Conflict: The selected driver or truck is already assigned for a pickup on {date.ToShortDateString()} during {schedule.s_PickupTime.ToString(@"hh\:mm")} - {schedule.s_PickupEnd.ToString(@"hh\:mm")}. Please choose a different driver, truck, or time slot.");
                            await PopulateCreateViewBags(schedule.l_ID, schedule.AssignedUser_ID, schedule.t_ID);
                            return View(schedule);
                        }
                    }

                    int createdSchedules = 0;
                    var allBinsForLocation = await _context.Bins
                        .Where(b => similarLocationIds.Contains(b.l_ID))
                        .ToListAsync();

                    foreach (var date in proposedDates)
                    {
                        foreach (var bin in allBinsForLocation)
                        {
                            var newSchedule = new Schedule
                            {
                                s_Date = date,
                                s_PickupTime = schedule.s_PickupTime,
                                s_PickupEnd = schedule.s_PickupEnd,
                                l_ID = bin.l_ID, 
                                PickedUpBins = 0, 
                                TotalBins = 1, 
                                AssignedUser_ID = schedule.AssignedUser_ID,
                                b_ID = bin.b_ID, 
                                t_ID = schedule.t_ID 
                            };

                            _context.Add(newSchedule);
                            createdSchedules++;
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Successfully created {createdSchedules} bin pickup schedules!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"An unexpected error occurred while creating the schedules: {ex.Message}";
                }
            }

            await PopulateCreateViewBags(schedule.l_ID, schedule.AssignedUser_ID, schedule.t_ID);
            return View(schedule);
        }

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
                            .Include(s => s.AssignedUser)
                            .Include(s => s.Bin)
                            .Include(s => s.Location)
                            .Include(s => s.Truck) 
                            .FirstOrDefaultAsync(m => m.s_ID == id);
            if (schedule == null)
            {
                return NotFound();
            }

            if (!isOperator && schedule.AssignedUser_ID != currentUserId)
            {
                return Forbid();
            }

            await PopulateEditViewBags(schedule, isOperator);
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("s_ID,AssignedUser_ID,b_ID,s_Date,s_PickupTime,s_PickupEnd,l_ID,PickedUpBins,TotalBins,t_ID")] Schedule schedule)
        {
            if (id != schedule.s_ID)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);
            var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());
            var existingSchedule = await _context.Schedules
                                .Include(s => s.AssignedUser)
                                .Include(s => s.Bin)
                                .Include(s => s.Location)
                                .Include(s => s.Truck) 
                                .FirstOrDefaultAsync(s => s.s_ID == id);
            if (existingSchedule == null)
            {
                return NotFound();
            }

            if (!isOperator && existingSchedule.AssignedUser_ID != currentUserId)
            {
                return Forbid();
            }

            ModelState.Remove("AssignedUser");
            ModelState.Remove("Bin");
            ModelState.Remove("Location");
            ModelState.Remove("Truck");
            if (!isOperator)
            {
                if (schedule.PickedUpBins < 0)
                {
                    ModelState.AddModelError("PickedUpBins", "Picked Up Bins cannot be negative.");
                }
                if (schedule.PickedUpBins > existingSchedule.TotalBins)
                {
                    ModelState.AddModelError("PickedUpBins", "Picked Up Bins cannot be greater than Total Bins.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(schedule.AssignedUser_ID))
                {
                    ModelState.AddModelError("AssignedUser_ID", "An assigned driver is required.");
                }

                if (schedule.t_ID <= 0)
                {
                    ModelState.AddModelError("t_ID", "A truck must be assigned.");
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
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (!isOperator)
                    {
                        existingSchedule.PickedUpBins = schedule.PickedUpBins;
                        _context.Update(existingSchedule);
                    }
                    else
                    {
                        var originalLocation = await _context.Locations.FindAsync(existingSchedule.l_ID);
                        string originalLocationAddress2 = originalLocation?.l_Address2 ?? "";

                        var originalGroupScheduleIds = await _context.Schedules
                            .Include(s => s.Location)
                            .Where(s => s.AssignedUser_ID == existingSchedule.AssignedUser_ID &&
                                       s.s_Date.Date == existingSchedule.s_Date.Date &&
                                       s.s_PickupTime == existingSchedule.s_PickupTime &&
                                       s.s_PickupEnd == existingSchedule.s_PickupEnd &&
                                       s.Location.l_Address2 == originalLocationAddress2 &&
                                       s.t_ID == existingSchedule.t_ID)
                            .Select(s => s.s_ID)
                            .ToListAsync();

                        var newAssignedUserId = schedule.AssignedUser_ID;
                        var newScheduleDate = schedule.s_Date.Date;
                        var newPickupTime = schedule.s_PickupTime;
                        var newPickupEnd = schedule.s_PickupEnd;
                        var newTruckId = schedule.t_ID;

                        var conflictExists = await _context.Schedules
                            .Include(s => s.Location) 
                            .Where(s => !originalGroupScheduleIds.Contains(s.s_ID) && 
                                        s.s_Date.Date == newScheduleDate &&
                                        ((s.s_PickupTime < newPickupEnd && s.s_PickupEnd > newPickupTime) || 
                                         (s.s_PickupTime == newPickupTime && s.s_PickupEnd == newPickupEnd)) && 
                                        (s.AssignedUser_ID == newAssignedUserId || s.t_ID == newTruckId) 
                            )
                            .AnyAsync();

                        if (conflictExists)
                        {
                            ModelState.AddModelError("", $"Conflict: The selected driver or truck is already assigned for a pickup on {newScheduleDate.ToShortDateString()} during {newPickupTime.ToString(@"hh\:mm")} - {newPickupEnd.ToString(@"hh\:mm")}. Please choose a different driver, truck, or time slot.");
                            await PopulateEditViewBags(schedule, isOperator);
                            return View(schedule);
                        }

                        originalLocation = await _context.Locations.FindAsync(existingSchedule.l_ID);
                        originalLocationAddress2 = originalLocation?.l_Address2 ?? "";

                        var newLocation = await _context.Locations.FindAsync(schedule.l_ID);
                        string newLocationAddress2 = newLocation?.l_Address2 ?? "";

                        var relatedSchedules = await _context.Schedules
                            .Include(s => s.Location)
                            .Where(s => s.AssignedUser_ID == existingSchedule.AssignedUser_ID &&
                                   s.s_Date.Date == existingSchedule.s_Date.Date &&
                                   s.s_PickupTime == existingSchedule.s_PickupTime &&
                                   s.s_PickupEnd == existingSchedule.s_PickupEnd &&
                                   s.Location.l_Address2 == originalLocationAddress2 &&
                                   s.t_ID == existingSchedule.t_ID) 
                            .ToListAsync();

                        if (relatedSchedules.Any())
                        {
                            if (originalLocationAddress2 != newLocationAddress2 || existingSchedule.t_ID != schedule.t_ID)
                            {
                                _context.Schedules.RemoveRange(relatedSchedules);
                                var newSimilarLocations = await _context.Locations
                                    .Where(l => l.l_Address2 == newLocationAddress2)
                                    .Select(l => l.l_ID)
                                    .ToListAsync();
                                var newBinsForLocation = await _context.Bins
                                    .Where(b => newSimilarLocations.Contains(b.l_ID))
                                    .ToListAsync();
                                foreach (var bin in newBinsForLocation)
                                {
                                    var newScheduleEntry = new Schedule 
                                    {
                                        s_Date = schedule.s_Date,
                                        s_PickupTime = schedule.s_PickupTime,
                                        s_PickupEnd = schedule.s_PickupEnd,
                                        l_ID = bin.l_ID,
                                        PickedUpBins = 0, 
                                        TotalBins = 1,
                                        AssignedUser_ID = schedule.AssignedUser_ID,
                                        b_ID = bin.b_ID,
                                        t_ID = schedule.t_ID 
                                    };
                                    _context.Add(newScheduleEntry);
                                }
                            }
                            else
                            {
                                foreach (var relatedSchedule in relatedSchedules)
                                {
                                    relatedSchedule.AssignedUser_ID = schedule.AssignedUser_ID;
                                    relatedSchedule.s_Date = schedule.s_Date;
                                    relatedSchedule.s_PickupTime = schedule.s_PickupTime;
                                    relatedSchedule.s_PickupEnd = schedule.s_PickupEnd;
                                }

                                _context.UpdateRange(relatedSchedules);
                            }

                            await _context.SaveChangesAsync();
                            int updatedCount = relatedSchedules.Count();
                            TempData["SuccessMessage"] = $"Successfully updated {updatedCount} related schedules!";
                        }
                        else
                        {
                            existingSchedule.AssignedUser_ID = schedule.AssignedUser_ID;
                            existingSchedule.s_Date = schedule.s_Date;
                            existingSchedule.s_PickupTime = schedule.s_PickupTime;
                            existingSchedule.s_PickupEnd = schedule.s_PickupEnd;
                            existingSchedule.l_ID = schedule.l_ID;
                            existingSchedule.PickedUpBins = schedule.PickedUpBins;
                            existingSchedule.TotalBins = schedule.TotalBins;
                            existingSchedule.t_ID = schedule.t_ID; 

                            _context.Update(existingSchedule);
                            await _context.SaveChangesAsync();
                            TempData["SuccessMessage"] = "Schedule updated successfully!";
                        }
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ScheduleExists(schedule.s_ID))
                    {
                        TempData["ErrorMessage"] =
                     "Schedule not found. It might have been deleted.";
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
            return View(schedule);
        }

        [Authorize(Roles = "Operator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var schedule = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                .Include(s => s.Location)
                .Include(s => s.Truck) 
                .FirstOrDefaultAsync(m => m.s_ID == id);
            if (schedule == null)
            {
                return NotFound();
            }

            var relatedSchedules = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Location)
                .Include(s => s.Location)
                .Include(s => s.Truck) 
                .Where(s => s.AssignedUser_ID == schedule.AssignedUser_ID &&
                           s.s_Date.Date == schedule.s_Date.Date &&
                           s.s_PickupTime == schedule.s_PickupTime &&
                           s.s_PickupEnd == schedule.s_PickupEnd &&
                           s.Location.l_Address2 == schedule.Location.l_Address2 &&
                           s.t_ID == schedule.t_ID)
                .OrderBy(s => s.Bin.b_PlateNo)
                .ToListAsync();

            ViewBag.RelatedSchedules = relatedSchedules; 

            return View(schedule);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Operator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var scheduleToDelete = await _context.Schedules
                                    .Include(s => s.Location) 
                                    .Include(s => s.Truck) 
                                    .FirstOrDefaultAsync(s => s.s_ID == id);

            if (scheduleToDelete == null)
            {
                TempData["ErrorMessage"] = "The schedule you are trying to delete was not found.";
                return NotFound();
            }

            var assignedUserId = scheduleToDelete.AssignedUser_ID;
            var scheduleDate = scheduleToDelete.s_Date.Date;
            var pickupTime = scheduleToDelete.s_PickupTime;
            var pickupEnd = scheduleToDelete.s_PickupEnd;
            var locationAddress2 = scheduleToDelete.Location?.l_Address2; 
            var truckId = scheduleToDelete.t_ID; 

            var schedulesToDeleteGroup = await _context.Schedules
                .Include(s => s.Location) 
                .Where(s => s.AssignedUser_ID == assignedUserId &&
                           s.s_Date.Date == scheduleDate &&
                           s.s_PickupTime == pickupTime &&
                           s.s_PickupEnd == pickupEnd &&
                           s.Location.l_Address2 == locationAddress2 &&
                           s.t_ID == truckId) 
                .ToListAsync();

            if (schedulesToDeleteGroup.Any())
            {
                _context.Schedules.RemoveRange(schedulesToDeleteGroup);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Successfully deleted {schedulesToDeleteGroup.Count} related bin pickup schedules!";
            }
            else
            {
                TempData["ErrorMessage"] = "No related schedules found to delete for the specified group.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ScheduleExists(int id)
        {
            return _context.Schedules.Any(e => e.s_ID == id);
        }

        private async Task PopulateEditViewBags(Schedule schedule, bool isOperator)
        {
            var uniqueLocations = await _context.Locations
                .GroupBy(l => l.l_Address2)
                .Select(g => g.First())
                .ToListAsync();

            var trucks = await _context.Trucks.ToListAsync(); 

            ViewBag.l_ID = new SelectList(uniqueLocations, "l_ID", "l_Address2", schedule.l_ID);
            ViewBag.Trucks = new SelectList(trucks, "t_ID", "t_PlateNo", schedule.t_ID); 
            if (isOperator)
            {
                var driverRoleId = (await _roleManager.FindByNameAsync(Roles.Driver.ToString()))?.Id;

                var driverAndCollectorUsers = await _context.UserRoles
                    .Where(ur => ur.RoleId == driverRoleId)
                    .Join(_context.Users,
                        userRole => userRole.UserId,
                        user => user.Id,
                        (userRole, user) => new
                        {
                            Id = user.Id,
                            Name = user.Name,
                            Email = user.Email,
                            RoleId = userRole.RoleId
                        })
                    .ToListAsync();
                ViewBag.AssignedUsers = new SelectList(driverAndCollectorUsers, "Id", "Name", schedule.AssignedUser_ID);
                ViewBag.Bins = new SelectList(await _context.Bins.Include(b => b.Location).ToListAsync(), "b_ID", "b_PlateNo", schedule.b_ID);
            }
        }

        private async Task PopulateCreateViewBags(int? selectedLocationId = null, string selectedUserId = null, int? selectedTruckId = null) // NEW: Add selectedTruckId
        {
            var uniqueLocations = await _context.Locations
                .GroupBy(l => l.l_Address2)
                .Select(g => g.First())
                .ToListAsync();

            var driverRoleId = (await _roleManager.FindByNameAsync(Roles.Driver.ToString()))?.Id;
            var driverAndCollectorUsers = await _context.UserRoles
                .Where(ur => ur.RoleId == driverRoleId)
                .Join(_context.Users,
                    userRole => userRole.UserId,
                    user => user.Id,
                    (userRole, user) => new
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        RoleId = userRole.RoleId
                    })
                .ToListAsync();

            var trucks = await _context.Trucks.ToListAsync();

            ViewBag.AssignedUsers = new SelectList(driverAndCollectorUsers, "Id", "Name", selectedUserId);
            ViewBag.l_ID = new SelectList(uniqueLocations, "l_ID", "l_Address2", selectedLocationId);
            ViewBag.Trucks = new SelectList(trucks, "t_ID", "t_PlateNo", selectedTruckId); 
        }

        private List<DateTime> GetDatesForDayOfWeek(int year, int month, DayOfWeek dayOfWeek)
        {
            List<DateTime> dates = new List<DateTime>();
            DateTime currentDate = new DateTime(year, month, 1);

            while (currentDate.DayOfWeek != dayOfWeek)
            {
                currentDate = currentDate.AddDays(1);
            }

            while (currentDate.Month == month)
            {
                if (currentDate.Date >= DateTime.Today.Date)
                {
                    dates.Add(currentDate);
                }
                currentDate = currentDate.AddDays(7); 
            }

            return dates;
        }

        [HttpGet]
        public async Task<JsonResult> GetCurrentUserAndRoles()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { isAuthenticated = false });
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            return Json(new
            {
                isAuthenticated = true,
                userId = currentUser.Id,
                userName = currentUser.Name, 
                roles = roles
            });
        }

        [HttpGet]
        public async Task<JsonResult> GetUniqueLocations()
        {
            var locations = await _context.Locations
                .Select(l => l.l_Address2)
                .Distinct()
                .OrderBy(address => address)
                .ToListAsync();
            return Json(locations);
        }

        [HttpGet]
        public async Task<JsonResult> GetDriversByDate(DateTime date, string locationAddress2 = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new List<object>());
            }

            var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());
            var isDriver = await _userManager.IsInRoleAsync(currentUser, Roles.Driver.ToString());

            IQueryable<Schedule> schedulesQuery = _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin) 
                    .ThenInclude(b => b.Location) 
                .Where(s => s.s_Date.Date == date.Date && s.AssignedUser != null);

            if (!string.IsNullOrEmpty(locationAddress2))
            {
                schedulesQuery = schedulesQuery.Where(s => s.Bin.Location.l_Address2 == locationAddress2);
            }

            if (isDriver && !isOperator) 
            {
                schedulesQuery = schedulesQuery.Where(s => s.AssignedUser_ID == currentUser.Id);
            }

            var driversForDate = await schedulesQuery
                .Select(s => new {
                    id = s.AssignedUser.Id,
                    name = s.AssignedUser.Name
                })
                .Distinct()
                .ToListAsync();

            return Json(driversForDate);
        }

        [HttpGet]
        public async Task<JsonResult> GetDriverRouteDetails(DateTime date, string driverId)
        {
            var routeDetails = await _context.Schedules
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Location)
                .Where(s => s.s_Date.Date == date.Date && s.AssignedUser_ID == driverId && s.Bin != null && s.Bin.Location != null)
                .Select(s => new {
                    binId = s.b_ID,
                    plateNo = s.Bin.b_PlateNo,
                    address1 = s.Bin.Location.l_Address1,
                    address2 = s.Bin.Location.l_Address2,
                    latitude = s.Bin.Location.Latitude,
                    longitude = s.Bin.Location.Longitude
                })
                .OrderBy(s => s.plateNo) 
                .ToListAsync();

            return Json(routeDetails);
        }
    }
}
