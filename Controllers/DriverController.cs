using Kutip.Data;
using Kutip.Models;
using Kutip.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kutip.Controllers
{
    [Authorize(Roles = "Driver")]
    public class DriverController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DriverController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string period = "daily", DateTime? startDate = null, DateTime? endDate = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge(); 
            }
            var driverId = currentUser.Id;

            var (filterStart, filterEnd) = GetDateRange(period, startDate, endDate);

            var todaySchedules = await _context.Schedules
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Customer)
                .Include(s => s.Location)
                .Where(s => s.s_Date.Date == DateTime.Today && s.AssignedUser_ID == driverId)
                .OrderBy(s => s.s_PickupTime)
                .ToListAsync();

            var driverSchedulesInPeriod = await _context.Schedules
                .Where(s => s.AssignedUser_ID == driverId && s.s_Date >= filterStart && s.s_Date <= filterEnd)
                .ToListAsync();

            var completedToday = todaySchedules.Count(s => s.PickedUpBins == s.TotalBins && s.TotalBins > 0);
            var pendingToday = todaySchedules.Count(s => s.PickedUpBins < s.TotalBins);
            var totalSchedulesToday = todaySchedules.Count;

            var driverEfficiencyTrendData = await GenerateDriverEfficiencyTrendData(driverId, filterStart, filterEnd);
            var driverDistanceTrendData = await GenerateDriverDistanceTrendData(driverId, filterStart, filterEnd);

            var totalTripsCompleted = driverSchedulesInPeriod.Count(s => s.PickedUpBins == s.TotalBins && s.TotalBins > 0);

            var totalBinsCollected = driverSchedulesInPeriod.Sum(s => s.PickedUpBins);
            var totalBinsScheduledInPeriod = driverSchedulesInPeriod.Sum(s => s.TotalBins);

            var averageBinsPerTrip = totalTripsCompleted > 0 ? (double)totalBinsCollected / totalTripsCompleted : 0.0;

            var onTimeSchedules = driverSchedulesInPeriod.Count(s => s.PickedUpBins == s.TotalBins && s.TotalBins > 0 && s.s_Date.Date <= DateTime.Today.Date);
            var onTimePerformance = totalBinsScheduledInPeriod > 0 ? (double)onTimeSchedules / driverSchedulesInPeriod.Count * 100 : 0.0;

            const double averageDistancePerTrip = 15.0; 
            var totalDistanceDriven = totalTripsCompleted * averageDistancePerTrip;

            var viewModel = new DriverDashboardViewModel
            {
                TodaySchedules = todaySchedules,
                WeeklySchedules = driverSchedulesInPeriod,
                CompletedToday = completedToday,
                PendingToday = pendingToday,
                TotalSchedulesToday = totalSchedulesToday,
                DriverEfficiencyTrendData = driverEfficiencyTrendData,
                DriverDistanceTrendData = driverDistanceTrendData,
                TotalTripsCompleted = totalTripsCompleted,
                AverageBinsPerTrip = Math.Round(averageBinsPerTrip, 1),
                OnTimePerformance = Math.Round(onTimePerformance, 1),
                TotalDistanceDriven = Math.Round(totalDistanceDriven, 0),
                FilterStartDate = filterStart,
                FilterEndDate = filterEnd,
                SelectedPeriod = period
            };

            ViewData["Title"] = "Driver Dashboard";
            return View(viewModel);
        }

        public async Task<IActionResult> MySchedules()
        {
            var user = await _userManager.GetUserAsync(User);
            var schedules = await _context.Schedules
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Customer)
                .Include(s => s.Location)
                .Where(s => s.AssignedUser_ID == user.Id)
                .OrderBy(s => s.s_Date)
                .ThenBy(s => s.s_PickupTime)
                .ToListAsync();

            return View(schedules);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var schedule = await _context.Schedules
                .Include(s => s.Bin)
                    .ThenInclude(b => b.Customer)
                .Include(s => s.Location)
                .Include(s => s.AssignedUser) 
                .Include(s => s.Truck)
                .FirstOrDefaultAsync(m => m.s_ID == id && m.AssignedUser_ID == currentUser.Id); 

            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }


        public async Task<IActionResult> RouteOptimization(string selectedAddress2)
        {
            var address2List = await _context.Locations
                .Where(l => !string.IsNullOrEmpty(l.l_Address2))
                .Select(l => l.l_Address2)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            ViewBag.Address2List = new SelectList(address2List, selectedAddress2);

            List<BinLocationDto> binsInArea;
            if (!string.IsNullOrEmpty(selectedAddress2))
            {
                binsInArea = await _context.Bins
                    .Include(b => b.Location)
                    .Where(b => b.Location != null && b.Location.l_Address2 == selectedAddress2)
                    .Select(b => new BinLocationDto
                    {
                        BinId = b.b_ID,
                        PlateNo = b.b_PlateNo,
                        Address1 = b.Location.l_Address1,
                        Address2 = b.Location.l_Address2,
                        Latitude = b.Location.Latitude,
                        Longitude = b.Location.Longitude,
                        CustomerName = b.Customer.c_Name,
                        ContactNo = b.Customer.c_ContactNo
                    })
                    .ToListAsync();
            }
            else
            {
                binsInArea = await _context.Bins
                    .Include(b => b.Location)
                    .Where(b => b.Location != null)
                    .Select(b => new BinLocationDto
                    {
                        BinId = b.b_ID,
                        PlateNo = b.b_PlateNo,
                        Address1 = b.Location.l_Address1,
                        Address2 = b.Location.l_Address2,
                        Latitude = b.Location.Latitude,
                        Longitude = b.Location.Longitude,
                        CustomerName = b.Customer.c_Name,
                        ContactNo = b.Customer.c_ContactNo
                    })
                    .ToListAsync();
            }

            ViewData["BinsInAreaJson"] = JsonConvert.SerializeObject(binsInArea);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetBinsByAddress2(string address2)
        {
            if (string.IsNullOrEmpty(address2))
            {
                return Json(new List<BinLocationDto>());
            }

            var bins = await _context.Bins
                .Include(b => b.Location)
                .Include(b => b.Customer)
                .Where(b => b.Location != null && b.Location.l_Address2 == address2)
                .Select(b => new BinLocationDto
                {
                    BinId = b.b_ID,
                    PlateNo = b.b_PlateNo,
                    Address1 = b.Location.l_Address1,
                    Address2 = b.Location.l_Address2,
                    Latitude = b.Location.Latitude,
                    Longitude = b.Location.Longitude,
                    CustomerName = b.Customer.c_Name,
                    ContactNo = b.Customer.c_ContactNo
                })
                .ToListAsync();

            return Json(bins);
        }

        [HttpGet]
        public async Task<IActionResult> GetBinDetails(int binId)
        {
            var bin = await _context.Bins
                .Include(b => b.Customer)
                .Include(b => b.Location)
                .FirstOrDefaultAsync(b => b.b_ID == binId);

            if (bin == null)
            {
                return NotFound();
            }

            return Json(new
            {
                PlateNo = bin.b_PlateNo,
                CustomerName = bin.Customer?.c_Name,
                ContactNo = bin.Customer?.c_ContactNo,
                Email = bin.Customer?.c_Email,
                Address = $"{bin.Location?.l_Address1}, {bin.Location?.l_Address2}, {bin.Location?.l_Postcode} {bin.Location?.l_District}, {bin.Location?.l_State}"
            });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDriverReport(string period = "daily", DateTime? startDate = null, DateTime? endDate = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var driverId = currentUser.Id;
            var (filterStart, filterEnd) = GetDateRange(period, startDate, endDate);

            var schedules = await _context.Schedules
                .Include(s => s.Bin).ThenInclude(b => b.Customer)
                .Include(s => s.Location)
                .Where(s => s.AssignedUser_ID == driverId && s.s_Date >= filterStart && s.s_Date <= filterEnd)
                .OrderBy(s => s.s_Date)
                .ThenBy(s => s.s_PickupTime)
                .ToListAsync();

            var completedToday = schedules.Count(s => s.s_Date.Date == DateTime.Today && s.PickedUpBins == s.TotalBins && s.TotalBins > 0);
            var pendingToday = schedules.Count(s => s.s_Date.Date == DateTime.Today && s.PickedUpBins < s.TotalBins);
            var totalTripsCompleted = schedules.Count(s => s.PickedUpBins == s.TotalBins && s.TotalBins > 0);
            var totalBinsCollected = schedules.Sum(s => s.PickedUpBins);
            var totalBinsScheduled = schedules.Sum(s => s.TotalBins);
            var averageBinsPerTrip = totalTripsCompleted > 0 ? (double)totalBinsCollected / totalTripsCompleted : 0;
            var onTimeSchedules = schedules.Count(s => s.PickedUpBins == s.TotalBins && s.TotalBins > 0 && s.s_Date.Date <= DateTime.Today.Date);
            var onTimePerformance = totalBinsScheduled > 0 ? (double)onTimeSchedules / schedules.Count * 100 : 0;
            const double avgDistancePerTrip = 15.0;
            var totalDistance = totalTripsCompleted * avgDistancePerTrip;

            var efficiencyTrend = await GenerateDriverEfficiencyTrendData(driverId, filterStart, filterEnd);
            var distanceTrend = await GenerateDriverDistanceTrendData(driverId, filterStart, filterEnd);

            var csv = new StringBuilder();

            csv.AppendLine($"Kutip Waste Management - Driver Report");
            csv.AppendLine($"Driver:,{currentUser.Name ?? currentUser.Email}");
            csv.AppendLine($"Period:,{filterStart:yyyy-MM-dd} to {filterEnd:yyyy-MM-dd}");
            csv.AppendLine($"Generated:,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine();
            csv.AppendLine($"Total Schedules:,{schedules.Count}");
            csv.AppendLine($"Completed Today:,{completedToday}");
            csv.AppendLine($"Pending Today:,{pendingToday}");
            csv.AppendLine($"Total Trips Completed:,{totalTripsCompleted}");
            csv.AppendLine($"Bins Collected:,{totalBinsCollected}");
            csv.AppendLine($"Bins Scheduled:,{totalBinsScheduled}");
            csv.AppendLine($"Average Bins per Trip:,{averageBinsPerTrip:F1}");
            csv.AppendLine($"On-Time Performance:,{onTimePerformance:F1}%");
            csv.AppendLine($"Estimated Distance Driven:,{totalDistance} km");
            csv.AppendLine();

            csv.AppendLine("Efficiency Trend");
            csv.AppendLine("Date,Efficiency %");
            foreach (var d in efficiencyTrend)
                csv.AppendLine($"{d.Label},{d.Value}");
            csv.AppendLine();

            csv.AppendLine("Distance Trend");
            csv.AppendLine("Date,Estimated Distance (km)");
            foreach (var d in distanceTrend)
                csv.AppendLine($"{d.Label},{d.Value}");
            csv.AppendLine();

            csv.AppendLine("Detailed Schedule");
            csv.AppendLine("Date,Pickup Time,End Time,Customer,Location,Bins Scheduled,Bins Collected,Bins Missed,Status");

            foreach (var s in schedules)
            {
                var missed = s.TotalBins - s.PickedUpBins;
                var status = s.PickedUpBins == s.TotalBins && s.TotalBins > 0 ? "Completed" :
                             s.PickedUpBins > 0 && s.PickedUpBins < s.TotalBins ? "Partial" :
                             s.s_Date.Date < DateTime.Today.Date ? "Missed" : "Pending";

                csv.AppendLine($"{s.s_Date:yyyy-MM-dd}," +
                               $"{new DateTime().Add(s.s_PickupTime):hh\\:mm}," +
                               $"{new DateTime().Add(s.s_PickupEnd):hh\\:mm}," +
                               $"\"{s.Bin?.Customer?.c_Name ?? "N/A"}\"," +
                               $"\"{s.Location?.l_Address1}, {s.Location?.l_Address2}\"," +
                               $"{s.TotalBins},{s.PickedUpBins},{missed},{status}");
            }

            var fileName = $"driver-report-{currentUser.Name?.Replace(" ", "-") ?? "unknown"}-{filterStart:yyyyMMdd}-{filterEnd:yyyyMMdd}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }


        [HttpGet]
        public async Task<IActionResult> DownloadTodayScheduleCsv()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var driverId = currentUser.Id;
            var today = DateTime.Today;

            var schedules = await _context.Schedules
                .Include(s => s.Bin).ThenInclude(b => b.Customer)
                .Include(s => s.Location)
                .Where(s => s.AssignedUser_ID == driverId && s.s_Date.Date == today)
                .OrderBy(s => s.s_PickupTime)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Kutip Waste Management - Driver's Today's Schedule");
            csv.AppendLine($"Driver:,{currentUser.Name ?? currentUser.Email}");
            csv.AppendLine($"Date:,{today:yyyy-MM-dd}");
            csv.AppendLine($"Generated:,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine();

            csv.AppendLine("Pickup Time,End Time,Customer,Location Address,Latitude,Longitude,Bins Scheduled,Bins Collected,Bins Missed,Pickup Duration (min),Status");

            foreach (var s in schedules)
            {
                string status = s.PickedUpBins == s.TotalBins && s.TotalBins > 0 ? "Completed" :
                                s.PickedUpBins > 0 ? "Partial" :
                                s.s_Date.Date < DateTime.Today ? "Missed" : "Pending";

                int duration = (s.s_PickupEnd.Hours * 60 + s.s_PickupEnd.Minutes) - (s.s_PickupTime.Hours * 60 + s.s_PickupTime.Minutes);

                csv.AppendLine(string.Join(",", new[]
                {
            new DateTime().Add(s.s_PickupTime).ToString("hh\\:mm tt"),
            new DateTime().Add(s.s_PickupEnd).ToString("hh\\:mm tt"),
            $"\"{s.Bin?.Customer?.c_Name ?? "N/A"}\"",
            $"\"{s.Location?.l_Address1}, {s.Location?.l_Address2}\"",
            s.Location?.Latitude.ToString() ?? "N/A",
            s.Location?.Longitude.ToString() ?? "N/A",
            s.TotalBins.ToString(),
            s.PickedUpBins.ToString(),
            duration.ToString(),
            status
        }));
            }

            var fileName = $"driver-today-schedule-{currentUser.Name?.Replace(" ", "-") ?? "unknown"}-{today:yyyyMMdd}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }


        private async Task<List<ChartDataPoint>> GenerateDriverEfficiencyTrendData(string driverId, DateTime start, DateTime end)
        {
            var data = new List<ChartDataPoint>();
            var totalDays = (end - start).TotalDays;

            if (totalDays <= 7) 
            {
                for (int i = 0; i <= totalDays; i++)
                {
                    var date = start.AddDays(i).Date;
                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date.Date == date && s.AssignedUser_ID == driverId)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var efficiency = totalScheduled > 0 ? (int)((double)totalCollected / totalScheduled * 100) : 0;

                    data.Add(new ChartDataPoint
                    {
                        Label = date.ToString("MMM dd"),
                        Value = efficiency,
                        Date = date
                    });
                }
            }
            else if (totalDays <= 31)
            {
                var weeks = GetWeeksInRange(start, end);
                foreach (var week in weeks)
                {
                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date >= week.start && s.s_Date <= week.end && s.AssignedUser_ID == driverId)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var efficiency = totalScheduled > 0 ? (int)((double)totalCollected / totalScheduled * 100) : 0;

                    data.Add(new ChartDataPoint
                    {
                        Label = $"Week {week.start:MMM dd}",
                        Value = efficiency,
                        Date = week.start
                    });
                }
            }
            else if (totalDays <= 366) 
            {
                var months = GetMonthsInRange(start, end);
                foreach (var month in months)
                {
                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date >= month.start && s.s_Date <= month.end && s.AssignedUser_ID == driverId)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var efficiency = totalScheduled > 0 ? (int)((double)totalCollected / totalScheduled * 100) : 0;

                    data.Add(new ChartDataPoint
                    {
                        Label = month.start.ToString("MMM yyyy"),
                        Value = efficiency,
                        Date = month.start
                    });
                }
            }
            else
            {
                var years = Enumerable.Range(start.Year, end.Year - start.Year + 1);
                foreach (var year in years)
                {
                    var yearStart = new DateTime(year, 1, 1);
                    var yearEnd = new DateTime(year, 12, 31);

                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date >= yearStart && s.s_Date <= yearEnd && s.AssignedUser_ID == driverId)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var efficiency = totalScheduled > 0 ? (int)((double)totalCollected / totalScheduled * 100) : 0;

                    data.Add(new ChartDataPoint
                    {
                        Label = year.ToString(),
                        Value = efficiency,
                        Date = yearStart
                    });
                }
            }

            if (!data.Any())
            {
                data.Add(new ChartDataPoint
                {
                    Label = DateTime.Today.ToString("MMM dd"),
                    Value = 0,
                    Date = DateTime.Today
                });
            }

            return data;
        }

        private async Task<List<ChartDataPoint>> GenerateDriverDistanceTrendData(string driverId, DateTime start, DateTime end)
        {
            var data = new List<ChartDataPoint>();
            var totalDays = (end - start).TotalDays;
            const double averageDistancePerTrip = 15.0;

            if (totalDays <= 7) 
            {
                for (int i = 0; i <= totalDays; i++)
                {
                    var date = start.AddDays(i).Date;
                    var completedTrips = await _context.Schedules
                        .Where(s => s.s_Date.Date == date && s.AssignedUser_ID == driverId && s.PickedUpBins == s.TotalBins && s.TotalBins > 0)
                        .CountAsync();

                    data.Add(new ChartDataPoint
                    {
                        Label = date.ToString("MMM dd"),
                        Value = (int)(completedTrips * averageDistancePerTrip),
                        Date = date
                    });
                }
            }
            else if (totalDays <= 31)
            {
                var weeks = GetWeeksInRange(start, end);
                foreach (var week in weeks)
                {
                    var completedTrips = await _context.Schedules
                        .Where(s => s.s_Date >= week.start && s.s_Date <= week.end && s.AssignedUser_ID == driverId && s.PickedUpBins == s.TotalBins && s.TotalBins > 0)
                        .CountAsync();

                    data.Add(new ChartDataPoint
                    {
                        Label = $"Week {week.start:MMM dd}",
                        Value = (int)(completedTrips * averageDistancePerTrip),
                        Date = week.start
                    });
                }
            }
            else if (totalDays <= 366) 
            {
                var months = GetMonthsInRange(start, end);
                foreach (var month in months)
                {
                    var completedTrips = await _context.Schedules
                        .Where(s => s.s_Date >= month.start && s.s_Date <= month.end && s.AssignedUser_ID == driverId && s.PickedUpBins == s.TotalBins && s.TotalBins > 0)
                        .CountAsync();

                    data.Add(new ChartDataPoint
                    {
                        Label = month.start.ToString("MMM yyyy"),
                        Value = (int)(completedTrips * averageDistancePerTrip),
                        Date = month.start
                    });
                }
            }
            else
            {
                var years = Enumerable.Range(start.Year, end.Year - start.Year + 1);
                foreach (var year in years)
                {
                    var yearStart = new DateTime(year, 1, 1);
                    var yearEnd = new DateTime(year, 12, 31);

                    var completedTrips = await _context.Schedules
                        .Where(s => s.s_Date >= yearStart && s.s_Date <= yearEnd && s.AssignedUser_ID == driverId && s.PickedUpBins == s.TotalBins && s.TotalBins > 0)
                        .CountAsync();

                    data.Add(new ChartDataPoint
                    {
                        Label = year.ToString(),
                        Value = (int)(completedTrips * averageDistancePerTrip),
                        Date = yearStart
                    });
                }
            }

            if (!data.Any())
            {
                data.Add(new ChartDataPoint
                {
                    Label = DateTime.Today.ToString("MMM dd"),
                    Value = 0,
                    Date = DateTime.Today
                });
            }

            return data;
        }

        private (DateTime start, DateTime end) GetDateRange(string period, DateTime? startDate = null, DateTime? endDate = null)
        {
            DateTime filterStart, filterEnd;

            if (startDate.HasValue && endDate.HasValue)
            {
                filterStart = startDate.Value.Date;
                filterEnd = endDate.Value.Date;
            }
            else
            {
                filterEnd = DateTime.Today.Date;
                filterStart = period switch
                {
                    "weekly" => DateTime.Today.AddDays(-7).Date,
                    "monthly" => DateTime.Today.AddMonths(-1).Date,
                    "quarterly" => DateTime.Today.AddMonths(-3).Date,
                    "yearly" => DateTime.Today.AddYears(-1).Date,
                    _ => DateTime.Today.AddDays(-6).Date,
                };
            }

            return (filterStart, filterEnd.AddDays(1).AddTicks(-1));
        }

        private List<(DateTime start, DateTime end)> GetWeeksInRange(DateTime startDate, DateTime endDate)
        {
            var weeks = new List<(DateTime start, DateTime end)>();
            var current = startDate.Date;

            while (current <= endDate.Date)
            {
                var weekStart = current.AddDays(-(int)current.DayOfWeek);
                var weekEnd = weekStart.AddDays(6); 

                if (weekEnd > endDate.Date)
                    weekEnd = endDate.Date;

                weeks.Add((weekStart, weekEnd));
                current = weekEnd.AddDays(1);
            }

            return weeks;
        }

        private List<(DateTime start, DateTime end)> GetMonthsInRange(DateTime startDate, DateTime endDate)
        {
            var months = new List<(DateTime start, DateTime end)>();
            var current = new DateTime(startDate.Year, startDate.Month, 1);
            while (current <= endDate)
            {
                var monthEnd = current.AddMonths(1).AddDays(-1); 
                if (monthEnd > endDate)
                    monthEnd = endDate;
                months.Add((current, monthEnd));
                current = current.AddMonths(1); 
            }

            return months;
        }
    }

    public class BinLocationDto
    {
        public int BinId { get; set; }
        public string PlateNo { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string CustomerName { get; set; }
        public string ContactNo { get; set; }
    }
}
