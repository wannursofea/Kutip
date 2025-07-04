using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Kutip.Data; // Assuming ApplicationDbContext is here
using Kutip.Models; // Assuming models like Schedule, Bin, Location, Truck, ApplicationUser are here
using Kutip.Constants; // Assuming Roles static class is here

namespace Kutip.Controllers
{
    // [Authorize(Roles = nameof(Roles.Operator))] // Uncomment if you want to restrict access to Operators only
    public class PickupController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PickupController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper to determine pickup status
        private string GetPickupStatus(Schedule schedule, DateTime today)
        {
            if (schedule.PickedUpBins == schedule.TotalBins && schedule.TotalBins > 0)
            {
                return "Completed";
            }
            else if (schedule.s_Date.Date == today && schedule.PickedUpBins == 0 && schedule.TotalBins > 0)
            {
                return "Pending";
            }
            else if (schedule.s_Date.Date < today && schedule.PickedUpBins == 0 && schedule.TotalBins > 0)
            {
                return "Missed";
            }
            else if (schedule.s_Date.Date > today)
            {
                return "Scheduled (Future)";
            }
            return "Unknown";
        }

        // GET: Pickup/Index
        public async Task<IActionResult> Index(
            string searchDate,
            string searchLocationAddress2,
            string searchStatus)
        {
            try
            {
                // Base query with all necessary includes
                var allSchedulesQuery = _context.Schedules
                    .Include(s => s.Bin)
                        .ThenInclude(b => b.Location)
                    .Include(s => s.Bin)
                        .ThenInclude(b => b.Customer)
                    .Include(s => s.AssignedUser)
                    .Include(s => s.Truck)
                    .AsQueryable();

                // Get all schedules first for stats calculation
                var allSchedulesForStats = await allSchedulesQuery.ToListAsync();
                var today = DateTime.Today.Date;

                // Calculate summary metrics from ALL schedules for the stats cards (removed InProgress)
                ViewBag.TotalPickupRecords = allSchedulesForStats.Count();
                ViewBag.CompletedPickups = allSchedulesForStats.Count(s => GetPickupStatus(s, today) == "Completed");
                ViewBag.PendingPickups = allSchedulesForStats.Count(s => GetPickupStatus(s, today) == "Pending");
                ViewBag.MissedPickups = allSchedulesForStats.Count(s => GetPickupStatus(s, today) == "Missed");
                ViewBag.ScheduledFuturePickups = allSchedulesForStats.Count(s => GetPickupStatus(s, today) == "Scheduled (Future)");

                // Start with all schedules for filtering
                var filteredSchedules = allSchedulesForStats.AsEnumerable();

                // Apply date filter if provided
                if (!string.IsNullOrEmpty(searchDate) && DateTime.TryParse(searchDate, out DateTime filterDate))
                {
                    filteredSchedules = filteredSchedules.Where(s => s.s_Date.Date == filterDate.Date);
                }

                // Apply location filter (Address2/Area) if provided
                if (!string.IsNullOrEmpty(searchLocationAddress2))
                {
                    filteredSchedules = filteredSchedules.Where(s =>
                        s.Bin?.Location?.l_Address2 != null &&
                        s.Bin.Location.l_Address2.Equals(searchLocationAddress2, StringComparison.OrdinalIgnoreCase));
                }

                // Apply status filter if provided
                if (!string.IsNullOrEmpty(searchStatus))
                {
                    filteredSchedules = filteredSchedules.Where(s => GetPickupStatus(s, today) == searchStatus);
                }

                // Get unique locations for dropdown (only Address2 values that are not null or empty)
                var uniqueLocations = await _context.Locations
                    .Where(l => !string.IsNullOrEmpty(l.l_Address2))
                    .Select(l => l.l_Address2)
                    .Distinct()
                    .OrderBy(l => l)
                    .ToListAsync();

                // Pass filter values back to view to maintain form state
                ViewBag.SearchDate = searchDate;
                ViewBag.SearchLocationAddress2 = searchLocationAddress2;
                ViewBag.SearchStatus = searchStatus;
                ViewBag.UniqueLocations = uniqueLocations ?? new List<string>();

                // Return filtered results to the view, ordered by date descending (most recent first)
                var finalResults = filteredSchedules
                    .OrderByDescending(s => s.s_Date)
                    .ToList();

                return View(finalResults);
            }
            catch (Exception ex)
            {
                // Log the exception (you might want to use ILogger here)
                // For now, return empty results with error handling
                ViewBag.TotalPickupRecords = 0;
                ViewBag.CompletedPickups = 0;
                ViewBag.PendingPickups = 0;
                ViewBag.MissedPickups = 0;
                ViewBag.ScheduledFuturePickups = 0;
                ViewBag.UniqueLocations = new List<string>();
                ViewBag.SearchDate = searchDate;
                ViewBag.SearchLocationAddress2 = searchLocationAddress2;
                ViewBag.SearchStatus = searchStatus;
                ViewBag.ErrorMessage = "An error occurred while loading pickup records. Please try again.";

                return View(new List<Schedule>());
            }
        }

        // GET: Pickup/Details/5 (for modal content)
        public async Task<IActionResult> Details(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || !int.TryParse(id, out int parsedId))
                {
                    return NotFound("Invalid schedule ID provided.");
                }

                var schedule = await _context.Schedules
                    .Include(s => s.Bin)
                        .ThenInclude(b => b.Customer)
                    .Include(s => s.Bin)
                        .ThenInclude(b => b.Location)
                    .Include(s => s.AssignedUser)
                    .Include(s => s.Truck)
                    .FirstOrDefaultAsync(m => m.s_ID == parsedId);

                if (schedule == null)
                {
                    return NotFound("Schedule not found.");
                }

                // Return your existing partial view for pickup details
                return PartialView("_PickupDetailsPartial", schedule);
            }
            catch (Exception ex)
            {
                // Log the exception
                return BadRequest("An error occurred while loading pickup details.");
            }
        }

        // Additional helper method to get pickup statistics (optional - for API calls or other uses)
        public async Task<IActionResult> GetPickupStats()
        {
            try
            {
                var allSchedules = await _context.Schedules
                    .Include(s => s.Bin)
                        .ThenInclude(b => b.Location)
                    .ToListAsync();

                var today = DateTime.Today.Date;

                var stats = new
                {
                    TotalPickupRecords = allSchedules.Count(),
                    CompletedPickups = allSchedules.Count(s => GetPickupStatus(s, today) == "Completed"),
                    PendingPickups = allSchedules.Count(s => GetPickupStatus(s, today) == "Pending"),
                    MissedPickups = allSchedules.Count(s => GetPickupStatus(s, today) == "Missed"),
                    ScheduledFuturePickups = allSchedules.Count(s => GetPickupStatus(s, today) == "Scheduled (Future)")
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                return BadRequest("An error occurred while retrieving pickup statistics.");
            }
        }

        // Optional: Export functionality
        public async Task<IActionResult> ExportPickupRecords(
            string searchDate,
            string searchLocationAddress2,
            string searchStatus,
            string format = "csv")
        {
            try
            {
                // Get filtered data using the same logic as Index
                var allSchedules = await _context.Schedules
                    .Include(s => s.Bin)
                        .ThenInclude(b => b.Location)
                    .Include(s => s.Bin)
                        .ThenInclude(b => b.Customer)
                    .Include(s => s.AssignedUser)
                    .Include(s => s.Truck)
                    .ToListAsync();

                var today = DateTime.Today.Date;
                var filteredSchedules = allSchedules.AsEnumerable();

                // Apply same filters as Index
                if (!string.IsNullOrEmpty(searchDate) && DateTime.TryParse(searchDate, out DateTime filterDate))
                {
                    filteredSchedules = filteredSchedules.Where(s => s.s_Date.Date == filterDate.Date);
                }

                if (!string.IsNullOrEmpty(searchLocationAddress2))
                {
                    filteredSchedules = filteredSchedules.Where(s =>
                        s.Bin?.Location?.l_Address2 != null &&
                        s.Bin.Location.l_Address2.Equals(searchLocationAddress2, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(searchStatus))
                {
                    filteredSchedules = filteredSchedules.Where(s => GetPickupStatus(s, today) == searchStatus);
                }

                var exportData = filteredSchedules
                    .OrderByDescending(s => s.s_Date)
                    .Select(s => new
                    {
                        ScheduleId = s.s_ID,
                        Date = s.s_Date.ToString("yyyy-MM-dd"),
                        PlateNo = s.Bin?.b_PlateNo ?? "N/A",
                        Location = s.Bin?.Location?.l_Address2 ?? "N/A",
                        Status = GetPickupStatus(s, today),
                        Progress = $"{s.PickedUpBins}/{s.TotalBins}",
                        AssignedDriver = s.AssignedUser?.UserName ?? "N/A",
                        Truck = s.Truck?.t_PlateNo ?? "N/A"
                    })
                    .ToList();

                if (format.ToLower() == "csv")
                {
                    var csv = "Schedule ID,Date,Plate No,Location,Status,Progress,Assigned Driver,Truck\n";
                    foreach (var item in exportData)
                    {
                        csv += $"{item.ScheduleId},{item.Date},{item.PlateNo},{item.Location},{item.Status},{item.Progress},{item.AssignedDriver},{item.Truck}\n";
                    }

                    var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                    return File(bytes, "text/csv", $"pickup-records-{DateTime.Now:yyyyMMdd}.csv");
                }

                return BadRequest("Unsupported export format.");
            }
            catch (Exception ex)
            {
                return BadRequest("An error occurred while exporting pickup records.");
            }
        }
    }
}
