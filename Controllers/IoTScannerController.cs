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
using System.Text.Json;

namespace Kutip.Controllers
{
    [Authorize]
    public class IoTScannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IoTScannerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);
            var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());

            var todaysSchedules = await _context.Schedules
                .Include(s => s.Bin)
                .Include(s => s.Location)
                .Include(s => s.AssignedUser)
                .Where(s => s.s_Date.Date == DateTime.Today.Date)
                .Where(s => isOperator || s.AssignedUser_ID == currentUserId)
                .ToListAsync();

            ViewBag.TodaysSchedules = todaysSchedules;
            ViewBag.IsOperator = isOperator;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessScan([FromBody] ScanRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.PlateNumber))
                {
                    return Json(new { success = false, message = "No plate number provided" });
                }

                var cleanPlateNumber = request.PlateNumber.Trim().ToUpper();

                if (cleanPlateNumber.Length != 7)
                {
                    return Json(new { success = false, message = $"Invalid plate number format: '{cleanPlateNumber}' (expected 3 letters + 4 numbers)" });
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(cleanPlateNumber, @"^[A-Z]{3}\d{4}$"))
                {
                    return Json(new { success = false, message = $"Invalid plate number format: '{cleanPlateNumber}' (must be 3 letters followed by 4 numbers)" });
                }

                var currentUserId = _userManager.GetUserId(User);
                var currentUser = await _userManager.GetUserAsync(User);
                var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());

                var bin = await _context.Bins
                    .Include(b => b.Location)
                    .FirstOrDefaultAsync(b => b.b_PlateNo.ToUpper() == cleanPlateNumber);

                if (bin == null)
                {
                    return Json(new { success = false, message = $"Bin with plate number '{cleanPlateNumber}' not found in database" });
                }

                var schedule = await _context.Schedules
                    .Include(s => s.AssignedUser)
                    .Include(s => s.Location)
                    .FirstOrDefaultAsync(s =>
                        s.b_ID == bin.b_ID &&
                        s.s_Date.Date == DateTime.Today.Date &&
                        (isOperator || s.AssignedUser_ID == currentUserId));

                if (schedule == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"No schedule found for bin '{cleanPlateNumber}' today or you're not assigned to this bin"
                    });
                }

                if (schedule.PickedUpBins >= schedule.TotalBins)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Schedule for bin '{cleanPlateNumber}' is already completed ({schedule.PickedUpBins}/{schedule.TotalBins})"
                    });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    schedule.PickedUpBins += 1;

                    schedule.s_ActualPickupTimestamp = DateTime.Now;

                    if (!string.IsNullOrEmpty(request.ImageDataUrl))
                    {
                        schedule.s_ImageUrl = request.ImageDataUrl;
                    }

                    _context.Entry(schedule).State = EntityState.Modified;

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    var isCompleted = schedule.PickedUpBins >= schedule.TotalBins;

                    return Json(new
                    {
                        success = true,
                        message = $"Successfully scanned bin '{cleanPlateNumber}'. Progress: {schedule.PickedUpBins}/{schedule.TotalBins}",
                        plateNumber = cleanPlateNumber,
                        pickedUpBins = schedule.PickedUpBins,
                        totalBins = schedule.TotalBins,
                        isCompleted = isCompleted,
                        location = schedule.Location?.l_Address1 ?? "Unknown Location",
                        scheduleId = schedule.s_ID,
                        scanSource = "camera",
                        imageUrl = schedule.s_ImageUrl
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Database error: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error processing scan: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTodaysSchedules()
        {
            try
            {
                var currentUserId = _userManager.GetUserId(User);
                var currentUser = await _userManager.GetUserAsync(User);
                var isOperator = await _userManager.IsInRoleAsync(currentUser, Roles.Operator.ToString());

                var schedules = await _context.Schedules
                    .Include(s => s.Bin)
                    .Include(s => s.Location)
                    .Include(s => s.AssignedUser)
                    .Where(s => s.s_Date.Date == DateTime.Today.Date)
                    .Where(s => isOperator || s.AssignedUser_ID == currentUserId)
                    .Select(s => new {
                        scheduleId = s.s_ID,
                        plateNumber = s.Bin.b_PlateNo,
                        location = s.Location.l_Address1,
                        pickedUpBins = s.PickedUpBins,
                        totalBins = s.TotalBins,
                        isCompleted = s.PickedUpBins >= s.TotalBins,
                        assignedUser = s.AssignedUser.Name,
                        imageUrl = s.s_ImageUrl
                    })
                    .ToListAsync();

                return Json(new { success = true, schedules = schedules });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    public class ScanRequest
    {
        public string PlateNumber { get; set; }
        public string ImageDataUrl { get; set; }
    }
}
