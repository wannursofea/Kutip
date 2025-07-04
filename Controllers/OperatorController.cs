using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Kutip.Data;
using Kutip.Models;
using Kutip.Constants;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Kutip.ViewModels;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Kutip.Controllers
{
    [Authorize(Roles = nameof(Roles.Operator))]
    public class OperatorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OperatorController> _logger;

        public OperatorController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IEmailSender emailSender, ApplicationDbContext context, ILogger<OperatorController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string period = "daily", DateTime? startDate = null, DateTime? endDate = null, string searchTruck = "", string searchDriver = "")
        {
            var (filterStart, filterEnd) = GetDateRange(period, startDate, endDate);

            var totalCustomers = await _context.Customers.CountAsync();
            var totalBins = await _context.Bins.CountAsync();
            var totalLocations = await _context.Locations.CountAsync();
            var totalTrucks = await _context.Trucks.CountAsync();
            var totalSchedules = await _context.Schedules.CountAsync();
            var todaySchedules = await _context.Schedules.CountAsync(s => s.s_Date.Date == DateTime.Today);
            var completedToday = await _context.Schedules.CountAsync(s => s.s_Date.Date == DateTime.Today && s.PickedUpBins == s.TotalBins);

            var periodSchedules = await _context.Schedules
                .Where(s => s.s_Date >= filterStart && s.s_Date <= filterEnd)
                .ToListAsync();

            var binsCollected = periodSchedules.Sum(s => s.PickedUpBins);
            var totalBinsScheduled = periodSchedules.Sum(s => s.TotalBins);
            var binsMissed = periodSchedules
                .Where(s => s.s_Date.Date < DateTime.Today && s.TotalBins > 0 && s.PickedUpBins == 0)
                .Sum(s => s.TotalBins);
            var collectionEfficiency = totalBinsScheduled > 0 ? (double)binsCollected / totalBinsScheduled * 100 : 0;

            var recentSchedules = await _context.Schedules
                .Include(s => s.Bin).ThenInclude(b => b.Customer)
                .Include(s => s.Location)
                .OrderByDescending(s => s.s_Date)
                .Take(10)
                .ToListAsync();

            var recentCustomers = await _context.Customers
                .OrderByDescending(c => c.c_ID)
                .Take(5)
                .ToListAsync();

            var collectionTrendData = await GenerateCollectionTrendData(filterStart, filterEnd);
            var efficiencyTrendData = await GenerateEfficiencyTrendData(filterStart, filterEnd);
            var missedBinsTrendData = await GenerateMissedBinsTrendData(filterStart, filterEnd);

            // NEW: Generate truck movement data

            var collectionStatusData = GenerateCollectionStatusData(binsCollected, binsMissed);
            var periodSummaries = await GeneratePeriodSummaries(period, filterStart, filterEnd);

            // Get truck movement summary with search
            var truckMovementSummary = await GetTruckMovementSummary(filterStart, filterEnd, searchTruck);

            // NEW: Get driver performance summary
            var driverPerformanceSummary = await GetDriverPerformanceSummary(filterStart, filterEnd, searchDriver);

            // Add these lines before creating the viewModel
            var truckList = await GetTruckList();
            var driverList = await GetDriverList();

            var viewModel = new OperatorDashboardViewModel
            {
                TotalCustomers = totalCustomers,
                TotalBins = totalBins,
                TotalLocations = totalLocations,
                TotalTrucks = totalTrucks,
                TotalSchedules = totalSchedules,
                TodaySchedules = todaySchedules,
                CompletedToday = completedToday,
                RecentSchedules = recentSchedules,
                RecentCustomers = recentCustomers,
                BinsCollected = binsCollected,
                BinsMissed = binsMissed,
                TotalBinsScheduled = totalBinsScheduled,
                CollectionEfficiency = Math.Round(collectionEfficiency, 2),
                SelectedPeriod = period,
                FilterStartDate = filterStart,
                FilterEndDate = filterEnd,
                CollectionTrendData = collectionTrendData,
                EfficiencyTrendData = efficiencyTrendData,
                MissedBinsTrendData = missedBinsTrendData,
                CollectionStatusData = collectionStatusData,
                PeriodSummaries = periodSummaries,
                TruckMovementSummary = truckMovementSummary,
                DriverPerformanceSummary = driverPerformanceSummary,
                SearchTruck = searchTruck,
                SearchDriver = searchDriver,
                TruckList = truckList,
                DriverList = driverList,
            };

            ViewData["Title"] = "Operator Dashboard";
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetTruckData(string searchTruck = "", DateTime? startDate = null, DateTime? endDate = null, string searchDriver = "")
        {
            var (filterStart, filterEnd) = GetDateRange("custom", startDate, endDate);
            var truckMovementSummary = await GetTruckMovementSummary(filterStart, filterEnd, searchTruck);

            return PartialView("_TruckDataPartial", truckMovementSummary);
        }

        [HttpGet]
        public async Task<IActionResult> GetDriverData(string searchDriver = "", DateTime? startDate = null, DateTime? endDate = null, string searchTruck = "")
        {
            var (filterStart, filterEnd) = GetDateRange("custom", startDate, endDate);
            var driverPerformanceSummary = await GetDriverPerformanceSummary(filterStart, filterEnd, searchDriver);

            return PartialView("_DriverDataPartial", driverPerformanceSummary);
        }

        private async Task<List<TruckMovementSummary>> GetTruckMovementSummary(DateTime start, DateTime end, string searchTruck = "")
        {
            var query = _context.Schedules
                .Include(s => s.Truck)
                .Include(s => s.AssignedUser)
                .Where(s => s.s_Date >= start && s.s_Date <= end);

            if (!string.IsNullOrEmpty(searchTruck))
            {
                query = query.Where(s => s.Truck.t_PlateNo.Contains(searchTruck));
            }

            var truckSummaries = await query
                .GroupBy(s => new { s.t_ID, s.Truck.t_PlateNo })
                .Select(g => new TruckMovementSummary
                {
                    TruckId = g.Key.t_ID,
                    PlateNumber = g.Key.t_PlateNo,
                    TotalSchedules = g.Count(),
                    CompletedSchedules = g.Count(s => s.PickedUpBins >= s.TotalBins && s.TotalBins > 0),
                    TotalBinsCollected = g.Sum(s => s.PickedUpBins),
                    TotalBinsScheduled = g.Sum(s => s.TotalBins),
                    UniqueLocations = g.Select(s => s.l_ID).Distinct().Count(),
                    ActiveDays = g.Select(s => s.s_Date.Date).Distinct().Count(),
                    LastActivity = g.Max(s => s.s_Date)
                })
                .OrderByDescending(t => t.TotalBinsCollected)
                .ToListAsync();

            return truckSummaries;
        }

        private async Task<List<DriverPerformanceSummary>> GetDriverPerformanceSummary(DateTime start, DateTime end, string searchDriver = "")
        {
            var query = _context.Schedules
                .Include(s => s.AssignedUser)
                .Include(s => s.Truck)
                .Where(s => s.s_Date >= start && s.s_Date <= end && s.AssignedUser != null);

            if (!string.IsNullOrEmpty(searchDriver))
            {
                query = query.Where(s => s.AssignedUser.Name.Contains(searchDriver));
            }

            // First get the data from database
            var scheduleData = await query
                .Select(s => new
                {
                    s.AssignedUser_ID,
                    s.AssignedUser.Name,
                    s.PickedUpBins,
                    s.TotalBins,
                    s.l_ID,
                    s.t_ID,
                    s.s_Date,
                    PickupTimeMinutes = (s.s_PickupEnd.Hours * 60 + s.s_PickupEnd.Minutes) - (s.s_PickupTime.Hours * 60 + s.s_PickupTime.Minutes)
                })
                .ToListAsync();

            // Then group and calculate on client side
            var driverSummaries = scheduleData
                .GroupBy(s => new { s.AssignedUser_ID, s.Name })
                .Select(g => new DriverPerformanceSummary
                {
                    DriverId = g.Key.AssignedUser_ID,
                    DriverName = g.Key.Name,
                    TotalSchedules = g.Count(),
                    CompletedSchedules = g.Count(s => s.PickedUpBins >= s.TotalBins && s.TotalBins > 0),
                    TotalBinsCollected = g.Sum(s => s.PickedUpBins),
                    TotalBinsScheduled = g.Sum(s => s.TotalBins),
                    UniqueLocations = g.Select(s => s.l_ID).Distinct().Count(),
                    UniqueTrucks = g.Select(s => s.t_ID).Distinct().Count(),
                    ActiveDays = g.Select(s => s.s_Date.Date).Distinct().Count(),
                    LastActivity = g.Max(s => s.s_Date),
                    AverageCollectionTime = g.Average(s => s.PickupTimeMinutes)
                })
                .OrderByDescending(d => d.TotalBinsCollected)
                .ToList();

            return driverSummaries;
        }

        private async Task<List<(int Id, string PlateNumber)>> GetTruckList()
        {
            return await _context.Trucks
                .Select(t => new { t.t_ID, t.t_PlateNo })
                .OrderBy(t => t.t_PlateNo)
                .Select(t => new ValueTuple<int, string>(t.t_ID, t.t_PlateNo))
                .ToListAsync();
        }

        private async Task<List<(string Id, string Name)>> GetDriverList()
        {
            var drivers = await _context.Schedules
                .Include(s => s.AssignedUser)
                .Where(s => s.AssignedUser != null)
                .Select(s => new { s.AssignedUser.Id, s.AssignedUser.Name })
                .Distinct()
                .OrderBy(d => d.Name)
                .Select(d => new ValueTuple<string, string>(d.Id, d.Name))
                .ToListAsync();

            return drivers;
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            ViewData["Title"] = "Create New User";

            ViewBag.Roles = await _roleManager.Roles
                                             .Where(r => r.Name == Roles.Driver.ToString())
                                             .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                             .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserInputModel model)
        {
            ViewBag.Roles = _roleManager.Roles
                                         .Where(r => r.Name == Roles.Driver.ToString())
                                         .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                         .ToList();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("CreateUser: Model state invalid for user creation attempt. Errors: {Errors}",
                                   string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return View(model);
            }

            if (model.RoleName == Roles.Driver.ToString())
            {
                if (!Regex.IsMatch(model.Email, @"@gmail\.com$", RegexOptions.IgnoreCase))
                {
                    ModelState.AddModelError("Email", "Only Gmail accounts are allowed for Driver roles.");
                    _logger.LogWarning("CreateUser: Gmail validation failed for email '{Email}' with role '{RoleName}'.", model.Email, model.RoleName);
                    return View(model);
                }
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Name = model.Name,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.RoleName))
                {
                    await _userManager.AddToRoleAsync(user, model.RoleName);
                }

                if (model.RoleName == Roles.Driver.ToString())
                {
                    if (string.IsNullOrEmpty(model.Email))
                    {
                        _logger.LogError("CreateUser: Critical Error - model.Email was null or empty after ModelState.IsValid passed for user {UserId}. Role: {RoleName}", user.Id, model.RoleName);
                        TempData["ErrorMessage"] = "User created successfully, but an email could not be sent because the email address was unexpectedly missing. Please contact support.";
                        return RedirectToAction(nameof(Index));
                    }

                    try
                    {
                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", userId = user.Id, code = code },
                            protocol: Request.Scheme);

                        var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                        resetPasswordToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetPasswordToken));
                        var resetPasswordUrl = Url.Page(
                            "/Account/ResetPassword",
                            pageHandler: null,
                            values: new { area = "Identity", code = resetPasswordToken, email = user.Email },
                            protocol: Request.Scheme);

                        var subject = "Welcome to Kutip! Your New Account - Action Required";
                        var message = $"<p>Hello {model.Name},</p>" +
                                      $"<p>Your account for Kutip Waste Management has been created as a **{model.RoleName}**.</p>" +
                                      $"<p>Before you can log in, you need to confirm your email and change your temporary password:</p>" +
                                      $"<ol>" +
                                      $"<li>Please confirm your email address by clicking <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>this link</a>.</li>" +
                                      $"<li>Once your email is confirmed, you **must** change your password using the following link: <a href='{HtmlEncoder.Default.Encode(resetPasswordUrl)}'>Change Your Password</a></li>" +
                                      $"</ol>" +
                                      $"<p>Your current password is the one you entered during registration.</p>" +
                                      $"<p>If you have any questions, please contact our support.</p>" +
                                      $"<p>Thank you,</p>" +
                                      $"<p>The Kutip Team</p>";

                        await _emailSender.SendEmailAsync(model.Email, subject, message);

                        TempData["SuccessMessage"] = $"User '{model.Email}' ({model.RoleName}) created successfully. A confirmation and password setup email has been sent.";
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogError(ex, "Failed to send confirmation email for user '{Email}' ({RoleName}). Inner exception: {InnerMessage}", model.Email, model.RoleName, ex.InnerException?.Message);
                        TempData["ErrorMessage"] = $"User '{model.Email}' ({model.RoleName}) created successfully, but failed to send a confirmation email. Error: {ex.InnerException?.Message ?? ex.Message}";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An unexpected error occurred while processing email sending for user '{Email}' ({RoleName}).", model.Email, model.RoleName);
                        TempData["ErrorMessage"] = $"User '{model.Email}' ({model.RoleName}) created successfully, but an unexpected error occurred while sending the confirmation email. Please check logs.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                else
                {
                    var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    await _userManager.ConfirmEmailAsync(user, confirmToken);
                    TempData["SuccessMessage"] = $"User '{model.Email}' ({model.RoleName}) created successfully. No confirmation email sent for this role.";
                }

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        public async Task<IActionResult> UserList()
        {
            ViewData["Title"] = "Manage Users";

            var users = await _userManager.Users.ToListAsync();
            var userViewModels = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = roles.FirstOrDefault(),
                    EmailConfirmed = user.EmailConfirmed
                });
            }

            ViewBag.TotalUsersCount = userViewModels.Count;
            ViewBag.OperatorUsersCount = userViewModels.Count(u => u.Role == Roles.Operator.ToString());
            ViewBag.DriverUsersCount = userViewModels.Count(u => u.Role == Roles.Driver.ToString());
            ViewBag.TotalLocationsCount = await _context.Locations.CountAsync();

            return View(userViewModels);
        }

        [HttpGet]
        public async Task<IActionResult> DetailsUser(string id, DateTime? startDate, DateTime? endDate, bool isModal = false)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var currentRole = userRoles.FirstOrDefault();

            var model = new UserViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = currentRole,
                EmailConfirmed = user.EmailConfirmed
            };

            ViewBag.UserRole = currentRole;
            ViewBag.FilterStartDate = startDate;
            ViewBag.FilterEndDate = endDate;
            ViewBag.IsModal = isModal;

            if (currentRole == nameof(Roles.Driver))
            {
                var assignedTrucks = await _context.Schedules
                    .Include(s => s.Truck)
                    .Where(s => s.AssignedUser_ID == user.Id && s.Truck != null)
                    .Select(s => s.Truck)
                    .Distinct()
                    .ToListAsync();

                ViewBag.AssignedTrucks = assignedTrucks;

                var driverSchedulesQuery = _context.Schedules
                    .Include(s => s.Bin).ThenInclude(b => b.Location)
                    .Include(s => s.Truck)
                    .Where(s => s.AssignedUser_ID == user.Id);

                if (startDate.HasValue)
                {
                    driverSchedulesQuery = driverSchedulesQuery.Where(s => s.s_Date.Date >= startDate.Value.Date);
                }
                if (endDate.HasValue)
                {
                    driverSchedulesQuery = driverSchedulesQuery.Where(s => s.s_Date.Date <= endDate.Value.Date);
                }

                var driverSchedules = await driverSchedulesQuery
                    .OrderByDescending(s => s.s_Date)
                    .Take(20)
                    .ToListAsync();

                var groupedDriverSchedules = driverSchedules
                    .GroupBy(s => new { s.s_Date.Date, s.s_PickupTime, s.s_PickupEnd, s.Location.l_Address2, s.t_ID, s.AssignedUser_ID })
                    .Select(g => new GroupedScheduleSummaryViewModel
                    {
                        ScheduleDate = g.Key.Date,
                        PickupTime = g.Key.s_PickupTime,
                        PickupEnd = g.Key.s_PickupEnd,
                        LocationAddress2 = g.Key.l_Address2,
                        TruckPlateNo = g.First().Truck?.t_PlateNo,
                        TotalBinsScheduled = g.Sum(s => s.TotalBins),
                        TotalBinsCollected = g.Sum(s => s.PickedUpBins),
                        AssignedUserName = g.First().AssignedUser?.Name,
                        Status = GetScheduleGroupStatus(g.Key.Date, g.Sum(s => s.TotalBins), g.Sum(s => s.PickedUpBins))
                    })
                    .OrderByDescending(s => s.ScheduleDate)
                    .ThenByDescending(s => s.PickupTime)
                    .Take(5)
                    .ToList();

                ViewBag.RecentGroupedSchedules = groupedDriverSchedules;
            }

            ViewData["Title"] = $"User Details: {user.Name}";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var currentRole = userRoles.FirstOrDefault();

            var model = new EditUserInputModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                RoleName = currentRole
            };

            ViewBag.Roles = await _roleManager.Roles
                                         .Where(r => r.Name == Roles.Operator.ToString() || r.Name == Roles.Driver.ToString())
                                         .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                         .ToListAsync();

            ViewData["Title"] = $"Edit User: {user.Name}";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserInputModel model)
        {
            ViewBag.Roles = await _roleManager.Roles
                                         .Where(r => r.Name == Roles.Operator.ToString() || r.Name == Roles.Driver.ToString())
                                         .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                         .ToListAsync();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("EditUser: Model state invalid for user edit attempt for user ID {UserId}. Errors: {Errors}",
                                   model.Id, string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(UserList));
            }

            user.Name = model.Name;
            user.Email = model.Email;
            user.UserName = model.Email;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                _logger.LogError("EditUser: Failed to update user details for user ID {UserId}. Errors: {Errors}",
                                 model.Id, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                return View(model);
            }

            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                if (model.NewPassword != model.ConfirmNewPassword)
                {
                    ModelState.AddModelError("ConfirmNewPassword", "The new password and confirmation password do not match.");
                    return View(model);
                }

                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded && removePasswordResult.Errors.Any(e => e.Code != "PasswordNotSet"))
                {
                    foreach (var error in removePasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogError("EditUser: Failed to remove old password for user ID {UserId}. Errors: {Errors}",
                                     model.Id, string.Join("; ", removePasswordResult.Errors.Select(e => e.Description)));
                    return View(model);
                }

                var addPasswordResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
                if (!addPasswordResult.Succeeded)
                {
                    foreach (var error in addPasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogError("EditUser: Failed to add new password for user ID {UserId}. Errors: {Errors}",
                                     model.Id, string.Join("; ", addPasswordResult.Errors.Select(e => e.Description)));
                    return View(model);
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var newRole = model.RoleName;

            if (!currentRoles.Contains(newRole))
            {
                var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeRolesResult.Succeeded)
                {
                    foreach (var error in removeRolesResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogError("EditUser: Failed to remove old roles for user ID {UserId}. Errors: {Errors}",
                                     model.Id, string.Join("; ", removeRolesResult.Errors.Select(e => e.Description)));
                    return View(model);
                }

                var addRoleResult = await _userManager.AddToRoleAsync(user, newRole);
                if (!addRoleResult.Succeeded)
                {
                    foreach (var error in addRoleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogError("EditUser: Failed to add new role '{NewRole}' for user ID {UserId}. Errors: {Errors}",
                                     newRole, model.Id, string.Join("; ", addRoleResult.Errors.Select(e => e.Description)));
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = $"User '{user.Email}' updated successfully.";
            return RedirectToAction(nameof(UserList));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "User ID not provided for deletion.";
                return RedirectToAction(nameof(UserList));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(UserList));
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(UserList));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User '{user.Email}' deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Error deleting user '{user.Email}': {string.Join(", ", result.Errors.Select(e => e.Description))}";
                _logger.LogError("DeleteUser: Failed to delete user ID {UserId}. Errors: {Errors}",
                                 id, string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            return RedirectToAction(nameof(UserList));
        }


        private string GetScheduleGroupStatus(DateTime scheduleDate, int totalBins, int pickedUpBins)
        {
            if (totalBins > 0 && pickedUpBins >= totalBins)
                return "Completed";
            else if (scheduleDate.Date < DateTime.Today.Date)
                return "Past Due";
            else if (scheduleDate.Date == DateTime.Today.Date && pickedUpBins > 0 && pickedUpBins < totalBins)
                return "In Progress";
            else if (scheduleDate.Date == DateTime.Today.Date && pickedUpBins == 0)
                return "Scheduled (Today)";
            else if (scheduleDate.Date > DateTime.Today.Date)
                return "Scheduled (Future)";
            else
                return "Unknown";
        }

        private (DateTime start, DateTime end) GetDateRange(string period, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                var start = startDate.Value.Date;
                var end = endDate.Value.Date;

                if (end < start)
                {
                    end = start;
                }

                return (start, end.AddDays(1).AddTicks(-1));
            }

            // Default to today for both start and end dates
            var today = DateTime.Now.Date;
            return (today, today.AddDays(1).AddTicks(-1));
        }

        private async Task<List<ChartDataPoint>> GenerateCollectionTrendData(DateTime start, DateTime end)
        {
            var data = new List<ChartDataPoint>();
            var totalDays = (end - start).TotalDays;

            if (totalDays <= 7)
            {
                for (int i = 0; i <= totalDays; i++)
                {
                    var date = start.AddDays(i).Date;
                    var collected = await _context.Schedules
                        .Where(s => s.s_Date.Date == date)
                        .SumAsync(s => s.PickedUpBins);

                    data.Add(new ChartDataPoint
                    {
                        Label = date.ToString("MMM dd"),
                        Value = collected,
                        Date = date
                    });
                }
            }
            else if (totalDays <= 31)
            {
                var weeks = GetWeeksInRange(start, end);
                foreach (var week in weeks)
                {
                    var collected = await _context.Schedules
                        .Where(s => s.s_Date >= week.start && s.s_Date <= week.end)
                        .SumAsync(s => s.PickedUpBins);

                    data.Add(new ChartDataPoint
                    {
                        Label = $"Week {week.start:MMM dd}",
                        Value = collected,
                        Date = week.start
                    });
                }
            }
            else if (totalDays <= 366)
            {
                var months = GetMonthsInRange(start, end);
                foreach (var month in months)
                {
                    var collected = await _context.Schedules
                        .Where(s => s.s_Date >= month.start && s.s_Date <= month.end)
                        .SumAsync(s => s.PickedUpBins);

                    data.Add(new ChartDataPoint
                    {
                        Label = month.start.ToString("MMM yyyy"),
                        Value = collected,
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

                    var collected = await _context.Schedules
                        .Where(s => s.s_Date >= yearStart && s.s_Date <= yearEnd)
                        .SumAsync(s => s.PickedUpBins);

                    data.Add(new ChartDataPoint
                    {
                        Label = year.ToString(),
                        Value = collected,
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

        private async Task<List<ChartDataPoint>> GenerateEfficiencyTrendData(DateTime start, DateTime end)
        {
            var data = new List<ChartDataPoint>();
            var totalDays = (end - start).TotalDays;

            if (totalDays <= 7)
            {
                for (int i = 0; i <= totalDays; i++)
                {
                    var date = start.AddDays(i).Date;
                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date.Date == date)
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
                        .Where(s => s.s_Date >= week.start && s.s_Date <= week.end)
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
                        .Where(s => s.s_Date >= month.start && s.s_Date <= month.end)
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
                        .Where(s => s.s_Date >= yearStart && s.s_Date <= yearEnd)
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

        private async Task<List<ChartDataPoint>> GenerateMissedBinsTrendData(DateTime start, DateTime end)
        {
            var data = new List<ChartDataPoint>();
            var totalDays = (end - start).TotalDays;

            if (totalDays <= 7)
            {
                for (int i = 0; i <= totalDays; i++)
                {
                    var date = start.AddDays(i).Date;
                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date.Date == date)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var missed = totalScheduled - totalCollected;

                    data.Add(new ChartDataPoint
                    {
                        Label = date.ToString("MMM dd"),
                        Value = missed,
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
                        .Where(s => s.s_Date >= week.start && s.s_Date <= week.end)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var missed = totalScheduled - totalCollected;

                    data.Add(new ChartDataPoint
                    {
                        Label = $"Week {week.start:MMM dd}",
                        Value = missed,
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
                        .Where(s => s.s_Date >= month.start && s.s_Date <= month.end)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var missed = totalScheduled - totalCollected;

                    data.Add(new ChartDataPoint
                    {
                        Label = month.start.ToString("MMM yyyy"),
                        Value = missed,
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
                        .Where(s => s.s_Date >= yearStart && s.s_Date <= yearEnd)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var missed = totalScheduled - totalCollected;

                    data.Add(new ChartDataPoint
                    {
                        Label = year.ToString(),
                        Value = missed,
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

        private List<PieChartData> GenerateCollectionStatusData(int collected, int missed)
        {
            var data = new List<PieChartData>();

            if (collected > 0 || missed > 0)
            {
                data.Add(new PieChartData { Label = "Collected", Value = collected, Color = "#059669" });
                data.Add(new PieChartData { Label = "Missed", Value = missed, Color = "#dc2626" });
            }
            else
            {
                data.Add(new PieChartData { Label = "No Data", Value = 1, Color = "#6b7280" });
            }

            return data;
        }

        private async Task<List<PeriodSummary>> GeneratePeriodSummaries(string period, DateTime startDate, DateTime endDate)
        {
            var summaries = new List<PeriodSummary>();

            if (period == "weekly")
            {
                var weeks = GetWeeksInRange(startDate, endDate);
                foreach (var week in weeks)
                {
                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date >= week.start && s.s_Date <= week.end)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var efficiency = totalScheduled > 0 ? (double)totalCollected / totalScheduled * 100 : 0;

                    summaries.Add(new PeriodSummary
                    {
                        Period = $"Week {week.start:MMM dd}",
                        BinsCollected = totalCollected,
                        BinsMissed = totalScheduled - totalCollected,
                        TotalScheduled = totalScheduled,
                        Efficiency = Math.Round(efficiency, 2),
                        StartDate = week.start,
                        EndDate = week.end
                    });
                }
            }
            else if (period == "monthly")
            {
                var months = GetMonthsInRange(startDate, endDate);
                foreach (var month in months)
                {
                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date >= month.start && s.s_Date <= month.end)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var efficiency = totalScheduled > 0 ? (double)totalCollected / totalScheduled * 100 : 0;

                    summaries.Add(new PeriodSummary
                    {
                        Period = month.start.ToString("MMM yyyy"),
                        BinsCollected = totalCollected,
                        BinsMissed = totalScheduled - totalCollected,
                        TotalScheduled = totalScheduled,
                        Efficiency = Math.Round(efficiency, 2),
                        StartDate = month.start,
                        EndDate = month.end
                    });
                }
            }
            else if (period == "yearly")
            {
                var years = Enumerable.Range(startDate.Year, endDate.Year - startDate.Year + 1);
                foreach (var year in years)
                {
                    var yearStart = new DateTime(year, 1, 1);
                    var yearEnd = new DateTime(year, 12, 31);

                    var schedules = await _context.Schedules
                        .Where(s => s.s_Date >= yearStart && s.s_Date <= yearEnd)
                        .ToListAsync();

                    var totalScheduled = schedules.Sum(s => s.TotalBins);
                    var totalCollected = schedules.Sum(s => s.PickedUpBins);
                    var efficiency = totalScheduled > 0 ? (double)totalCollected / totalScheduled * 100 : 0;

                    summaries.Add(new PeriodSummary
                    {
                        Period = year.ToString(),
                        BinsCollected = totalCollected,
                        BinsMissed = totalScheduled - totalCollected,
                        TotalScheduled = totalScheduled,
                        Efficiency = Math.Round(efficiency, 2),
                        StartDate = yearStart,
                        EndDate = yearEnd
                    });
                }
            }

            return summaries;
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

        public async Task<IActionResult> DownloadReport(string format = "csv", string period = "daily", DateTime? startDate = null, DateTime? endDate = null, string searchTruck = "", string searchDriver = "")
        {
            var (filterStart, filterEnd) = GetDateRange(period, startDate, endDate);

            var totalCustomers = await _context.Customers.CountAsync();
            var totalBins = await _context.Bins.CountAsync();
            var totalLocations = await _context.Locations.CountAsync();
            var totalTrucks = await _context.Trucks.CountAsync();
            var totalSchedules = await _context.Schedules.CountAsync();
            var todaySchedules = await _context.Schedules.CountAsync(s => s.s_Date.Date == DateTime.Today);
            var completedToday = await _context.Schedules.CountAsync(s => s.s_Date.Date == DateTime.Today && s.PickedUpBins == s.TotalBins);

            var periodSchedules = await _context.Schedules
                .Where(s => s.s_Date >= filterStart && s.s_Date <= filterEnd)
                .Include(s => s.Bin).ThenInclude(b => b.Customer)
                .Include(s => s.Location)
                .Include(s => s.Truck)
                .Include(s => s.AssignedUser)
                .ToListAsync();

            var binsCollected = periodSchedules.Sum(s => s.PickedUpBins);
            var totalBinsScheduled = periodSchedules.Sum(s => s.TotalBins);
            var binsMissed = periodSchedules
                .Where(s => s.s_Date.Date < DateTime.Today && s.TotalBins > 0 && s.PickedUpBins == 0)
                .Sum(s => s.TotalBins);
            var collectionEfficiency = totalBinsScheduled > 0 ? (double)binsCollected / totalBinsScheduled * 100 : 0;

            var truckSummary = await GetTruckMovementSummary(filterStart, filterEnd, searchTruck);
            var driverSummary = await GetDriverPerformanceSummary(filterStart, filterEnd, searchDriver);
            var periodSummary = await GeneratePeriodSummaries(period, filterStart, filterEnd);

            var csv = new StringBuilder();

            // === Header Summary ===
            csv.AppendLine($"Report Period:,{filterStart:yyyy-MM-dd} to {filterEnd:yyyy-MM-dd}");
            csv.AppendLine($"Total Customers:,{totalCustomers}");
            csv.AppendLine($"Total Bins:,{totalBins}");
            csv.AppendLine($"Total Locations:,{totalLocations}");
            csv.AppendLine($"Total Trucks:,{totalTrucks}");
            csv.AppendLine($"Total Schedules:,{totalSchedules}");
            csv.AppendLine($"Today's Schedules:,{todaySchedules}");
            csv.AppendLine($"Completed Today:,{completedToday}");
            csv.AppendLine($"Bins Collected:,{binsCollected}");
            csv.AppendLine($"Bins Missed:,{binsMissed}");
            csv.AppendLine($"Total Scheduled Bins:,{totalBinsScheduled}");
            csv.AppendLine($"Collection Efficiency:,{collectionEfficiency:F2}%");
            csv.AppendLine(); // empty line

            // === Truck Summary ===
            csv.AppendLine("Truck Summary");
            csv.AppendLine("Plate Number,Total Schedules,Completed,Collected Bins,Scheduled Bins,Unique Locations,Active Days,Last Activity");
            foreach (var t in truckSummary)
            {
                csv.AppendLine($"{t.PlateNumber},{t.TotalSchedules},{t.CompletedSchedules},{t.TotalBinsCollected},{t.TotalBinsScheduled},{t.UniqueLocations},{t.ActiveDays},{t.LastActivity:yyyy-MM-dd}");
            }
            csv.AppendLine();

            // === Driver Summary ===
            csv.AppendLine("Driver Summary");
            csv.AppendLine("Driver Name,Total Schedules,Completed,Collected Bins,Scheduled Bins,Unique Locations,Trucks Used,Active Days,Last Activity,Avg Collection Time (min)");
            foreach (var d in driverSummary)
            {
                csv.AppendLine($"{d.DriverName},{d.TotalSchedules},{d.CompletedSchedules},{d.TotalBinsCollected},{d.TotalBinsScheduled},{d.UniqueLocations},{d.UniqueTrucks},{d.ActiveDays},{d.LastActivity:yyyy-MM-dd},{d.AverageCollectionTime:F1}");
            }
            csv.AppendLine();



            // === Detailed Schedules ===
            csv.AppendLine("Detailed Schedules");
            csv.AppendLine("Date,Time,Customer,Location,Truck,Driver,Scheduled Bins,Collected Bins,Status");

            foreach (var s in periodSchedules)
            {
                string status = GetScheduleStatus(s);

                csv.AppendLine($"{s.s_Date:yyyy-MM-dd}," +
                               $"{s.s_PickupTime}," +
                               $"\"{s.Bin?.Customer?.c_Name ?? "N/A"}\"," +
                               $"\"{s.l_Address1}, {s.l_Address2}\"," +
                               $"{s.Truck?.t_PlateNo ?? "N/A"}," +
                               $"{s.AssignedUser?.Name ?? "N/A"}," +
                               $"{s.TotalBins},{s.PickedUpBins},{status}");
            }

            var fileName = $"kutip-dashboard-report-{filterStart:yyyy-MM-dd}-to-{filterEnd:yyyy-MM-dd}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }


        private string GetScheduleStatus(Schedule schedule)
        {
            if (schedule.PickedUpBins == schedule.TotalBins && schedule.TotalBins > 0)
                return "Completed";
            else if (schedule.PickedUpBins > 0 && schedule.PickedUpBins < schedule.TotalBins)
                return "Partial";
            else if (schedule.PickedUpBins < schedule.TotalBins && schedule.s_Date.Date <= DateTime.Today)
                return "Missed";
            else
                return "Pending";
        }

    }
}
