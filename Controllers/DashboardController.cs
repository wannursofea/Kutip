using Kutip.Data;
using Kutip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kutip.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(currentUser);
            var isOperator = userRoles.Contains("Operator");
            var isDriver = userRoles.Contains("Driver");

            var totalCustomers = await _context.Customers.CountAsync();
            var totalLocations = await _context.Locations.CountAsync();
            var totalBins = await _context.Bins.CountAsync();

            var today = DateTime.Today;
            var todaysSchedules = await _context.Schedules
                .Include(s => s.Location)
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                .ThenInclude(b => b.Customer)
                .Where(s => s.s_Date.Date == today)
                .ToListAsync();

            if (isDriver)
            {
                todaysSchedules = todaysSchedules.Where(s => s.AssignedUser_ID == currentUser.Id).ToList();
            }

            var totalSchedulesToday = todaysSchedules.Count;
            var binsCollected = todaysSchedules.Sum(s => s.PickedUpBins);
            var totalBinsScheduled = todaysSchedules.Sum(s => s.TotalBins);
            var binsMissed = totalBinsScheduled - binsCollected;
            var completedSchedules = todaysSchedules.Where(s => s.PickedUpBins >= s.TotalBins && s.TotalBins > 0).Count();
            var pendingSchedules = totalSchedulesToday - completedSchedules;

            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var weekEnd = weekStart.AddDays(7);

            var weeklySchedulesData = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Where(s => s.s_Date >= weekStart && s.s_Date < weekEnd)
                .ToListAsync();

            if (isDriver)
            {
                weeklySchedulesData = weeklySchedulesData.Where(s => s.AssignedUser_ID == currentUser.Id).ToList();
            }

            var weeklyCollectionData = weeklySchedulesData
                .GroupBy(s => s.s_Date.DayOfWeek)
                .Select(g => new {
                    Day = g.Key,
                    Scheduled = g.Sum(x => x.TotalBins),
                    Collected = g.Sum(x => x.PickedUpBins),
                    Schedules = g.Count()
                })
                .OrderBy(x => x.Day)
                .ToList();

            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthlyData = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Where(s => s.s_Date >= monthStart && s.s_Date <= today)
                .ToListAsync();

            if (isDriver)
            {
                monthlyData = monthlyData.Where(s => s.AssignedUser_ID == currentUser.Id).ToList();
            }

            var monthlyStats = new
            {
                TotalBinsCollected = monthlyData.Sum(s => s.PickedUpBins),
                TotalBinsScheduled = monthlyData.Sum(s => s.TotalBins),
                TotalSchedules = monthlyData.Count,
                CompletedSchedules = monthlyData.Where(s => s.PickedUpBins >= s.TotalBins && s.TotalBins > 0).Count()
            };

            var truckMovements = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Location)
                .Where(s => s.s_Date.Date == today)
                .ToListAsync();

            if (isDriver)
            {
                truckMovements = truckMovements.Where(s => s.AssignedUser_ID == currentUser.Id).ToList();
            }

            var truckData = truckMovements
                .GroupBy(s => s.AssignedUser_ID)
                .Select(g => new {
                    DriverId = g.Key,
                    DriverName = g.First().AssignedUser?.Name ?? g.First().AssignedUser?.Email ?? "Unknown",
                    TotalStops = g.Count(),
                    CompletedStops = g.Count(x => x.PickedUpBins >= x.TotalBins && x.TotalBins > 0),
                    TotalBinsCollected = g.Sum(x => x.PickedUpBins),
                    Areas = g.Select(x => x.Location?.l_ColArea).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList()
                })
                .ToList();

            var recentSchedules = await _context.Schedules
                .Include(s => s.Location)
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                .ThenInclude(b => b.Customer)
                .Where(s => s.s_Date >= today.AddDays(-7))
                .OrderByDescending(s => s.s_Date)
                .ThenByDescending(s => s.s_PickupTime)
                .Take(10)
                .ToListAsync();

            if (isDriver)
            {
                recentSchedules = recentSchedules.Where(s => s.AssignedUser_ID == currentUser.Id).ToList();
            }

            var upcomingSchedules = await _context.Schedules
                .Include(s => s.Location)
                .Include(s => s.AssignedUser)
                .Include(s => s.Bin)
                .ThenInclude(b => b.Customer)
                .Where(s => s.s_Date > today)
                .OrderBy(s => s.s_Date)
                .ThenBy(s => s.s_PickupTime)
                .Take(5)
                .ToListAsync();

            if (isDriver)
            {
                upcomingSchedules = upcomingSchedules.Where(s => s.AssignedUser_ID == currentUser.Id).ToList();
            }

            var areaPerformance = await _context.Schedules
                .Include(s => s.Location)
                .Where(s => s.s_Date >= monthStart)
                .ToListAsync();

            if (isDriver)
            {
                areaPerformance = areaPerformance.Where(s => s.AssignedUser_ID == currentUser.Id).ToList();
            }

            var areaStats = areaPerformance
                .GroupBy(s => s.Location?.l_ColArea ?? "Unassigned")
                .Select(g => new {
                    Area = g.Key,
                    TotalSchedules = g.Count(),
                    BinsCollected = g.Sum(x => x.PickedUpBins),
                    BinsScheduled = g.Sum(x => x.TotalBins),
                    EfficiencyRate = g.Sum(x => x.TotalBins) > 0 ? (double)g.Sum(x => x.PickedUpBins) / g.Sum(x => x.TotalBins) * 100 : 0
                })
                .OrderByDescending(x => x.EfficiencyRate)
                .ToList();

            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.TotalLocations = totalLocations;
            ViewBag.TotalBins = totalBins;
            ViewBag.TotalSchedulesToday = totalSchedulesToday;
            ViewBag.BinsCollected = binsCollected;
            ViewBag.BinsMissed = binsMissed;
            ViewBag.TotalBinsScheduled = totalBinsScheduled;
            ViewBag.CompletedSchedules = completedSchedules;
            ViewBag.PendingSchedules = pendingSchedules;
            ViewBag.CollectionRate = totalBinsScheduled > 0 ? (int)((double)binsCollected / totalBinsScheduled * 100) : 0;
            ViewBag.WeeklyCollectionData = weeklyCollectionData;
            ViewBag.MonthlyStats = monthlyStats;
            ViewBag.TruckData = truckData;
            ViewBag.RecentSchedules = recentSchedules;
            ViewBag.UpcomingSchedules = upcomingSchedules;
            ViewBag.AreaStats = areaStats;
            ViewBag.IsOperator = isOperator;
            ViewBag.IsDriver = isDriver;
            ViewBag.UserName = currentUser?.Name ?? currentUser?.Email ?? "User";
            ViewBag.UserRole = userRoles.FirstOrDefault() ?? "User";

            return View();
        }
    }
}
