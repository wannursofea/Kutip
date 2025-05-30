using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; 
using Kutip.Constants; 
using Kutip.Data;    
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Kutip.Controllers
{
    [Authorize(Roles = nameof(Roles.Operator))] // Only Operator role can access this controller
    public class OperatorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public OperatorController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // --- Operator Dashboard/Index (existing from previous steps) ---
        public IActionResult Index()
        {
            ViewData["Title"] = "Operator Dashboard";
            return View();
        }

        // --- Action to display the user creation form ---
        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            ViewData["Title"] = "Create New User";
            // Get all roles except Operator for assignment dropdown
            var roles = await _roleManager.Roles
                                         .Where(r => r.Name != nameof(Roles.Operator)) // Exclude Operator role
                                         .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                         .ToListAsync();
            ViewBag.Roles = roles;
            return View();
        }

        // --- Action to handle the user creation form submission ---
        [HttpPost]
        [ValidateAntiForgeryToken] // Important for security
        public async Task<IActionResult> CreateUser(CreateUserInputModel model)
        {
            ViewData["Title"] = "Create New User"; // Set title for redisplay

            // Re-populate roles for dropdown in case of ModelState errors
            var roles = await _roleManager.Roles
                                         .Where(r => r.Name != nameof(Roles.Operator))
                                         .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                         .ToListAsync();
            ViewBag.Roles = roles;

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, Name = model.Name };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Assign the selected role
                    if (await _roleManager.RoleExistsAsync(model.RoleName))
                    {
                        var roleResult = await _userManager.AddToRoleAsync(user, model.RoleName);
                        if (!roleResult.Succeeded)
                        {
                            // If role assignment fails, log errors and potentially delete user or flag
                            _userManager.DeleteAsync(user); // Clean up user if role assignment fails
                            ModelState.AddModelError(string.Empty, "Failed to assign role to user.");
                            foreach (var error in roleResult.Errors)
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }
                            return View(model);
                        }
                    }
                    else
                    {
                        // This case should ideally not happen if roles list is populated correctly
                        ModelState.AddModelError(string.Empty, $"Selected role '{model.RoleName}' does not exist.");
                        _userManager.DeleteAsync(user); // Clean up user if role is invalid
                        return View(model);
                    }

                    TempData["SuccessMessage"] = $"User '{model.Name}' created and assigned to role '{model.RoleName}' successfully!";
                    return RedirectToAction(nameof(Index)); // Redirect to Operator's main dashboard
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If ModelState is not valid, or user creation failed, return to view with errors
            return View(model);
        }

        // --- Input Model for User Creation Form ---
        public class CreateUserInputModel
        {
            [Required]
            [Display(Name = "Full Name")]
            public string Name { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [Display(Name = "Role")]
            public string RoleName { get; set; } // To hold the selected role (Driver/Collector)
        }
    }
}