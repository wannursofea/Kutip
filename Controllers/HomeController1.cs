using Kutip.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Kutip.Controllers
{
    [Authorize]
    public class PlateRecognitionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlateRecognitionController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Scan()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPlate(string plateNumber)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var currentDate = DateTime.Today;

            var schedule = await _context.Schedules
                .Include(s => s.Bin)
                .FirstOrDefaultAsync(s =>
                    s.AssignedUser_ID == currentUser.Id &&
                    s.s_Date.Date == currentDate.Date &&
                    s.Bin.b_PlateNo == plateNumber);

            if (schedule == null)
            {
                return Json(new
                {
                    success = false,
                    message = "No matching schedule found for this plate number."
                });
            }

            if (schedule.PickedUpBins >= schedule.TotalBins)
            {
                return Json(new
                {
                    success = false,
                    message = "All bins for this schedule have already been picked up."
                });
            }

            schedule.PickedUpBins++;
            _context.Update(schedule);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Plate verified! Picked up bins updated to {schedule.PickedUpBins}/{schedule.TotalBins}"
            });
        }
    }
}