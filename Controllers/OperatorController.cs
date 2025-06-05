using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Kutip.Data;
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

namespace Kutip.Controllers

{
    [Authorize(Roles = nameof(Roles.Operator))] 
    public class OperatorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        public OperatorController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }


        // --- Operator Dashboard/Index (existing from previous steps) ---
        public IActionResult Index()
        {
            ViewData["Title"] = "Operator Dashboard";
            return View();
        }

        // --- Inner Class: CreateUserInputModel ---
        public class CreateUserInputModel
        {
            [Required]
            [Display(Name = "Full Name")]
            public string Name { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email Address")]
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
            public string RoleName { get; set; } // Matches asp-for="RoleName" in your view
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            ViewData["Title"] = "Create New User";

            ViewBag.Roles = await _roleManager.Roles
                                         .Where(r => r.Name == Roles.Collector.ToString() || r.Name == Roles.Driver.ToString())
                                         .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                         .ToListAsync();
            
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserInputModel model)
        {            
            ViewBag.Roles = _roleManager.Roles
                                        .Where(r => r.Name == Roles.Collector.ToString() || r.Name == Roles.Driver.ToString())
                                        .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                        .ToList();

            if (ModelState.IsValid)
            {
                // Gmail Check
                if (model.RoleName == Roles.Collector.ToString() || model.RoleName == Roles.Driver.ToString())
                {
                    if (!Regex.IsMatch(model.Email, @"@gmail\.com$", RegexOptions.IgnoreCase))
                    {
                        ModelState.AddModelError("Email", "Only Gmail accounts are allowed for Collector and Driver roles.");
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

                // Create the user with the provided password
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Assign role
                    if (!string.IsNullOrEmpty(model.RoleName))
                    {
                        await _userManager.AddToRoleAsync(user, model.RoleName);
                    }

                    // --- Conditional Email Sending for Collector and Driver Roles ---
                    if (model.RoleName == Roles.Collector.ToString() || model.RoleName == Roles.Driver.ToString())
                    {
                        // Generate email confirmation token
                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", userId = user.Id, code = code },
                            protocol: Request.Scheme);

                        // Generate password reset token
                        var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                        resetPasswordToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetPasswordToken));
                        var resetPasswordUrl = Url.Page(
                            "/Account/ResetPassword",
                            pageHandler: null,
                            values: new { area = "Identity", code = resetPasswordToken, email = user.Email },
                            protocol: Request.Scheme);

                        // Construct email message
                        var subject = "Welcome to Kutip! Your New Account - Action Required";
                        var message = $"<p>Hello {model.Name},</p>" +
                                      $"<p>Your account for Kutip Waste Management has been created as a **{model.RoleName}**.</p>" +
                                      $"<p>Before you can log in, you need to confirm your email and change your temporary password:</p>" +
                                      $"<ol>" +
                                      $"<li>Please confirm your email address by clicking <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>this link</a>.</li>" +
                                      $"<li>Once your email is confirmed, you **must** change your password using the following link: <a href='{HtmlEncoder.Default.Encode(resetPasswordUrl)}'>Change Your Password</a></li>" +
                                      $"</ol>" +
                                      $"<p>Your current password is the one you entered during registration.</p>" + // Clarify
                                      $"<p>If you have any questions, please contact our support.</p>" +
                                      $"<p>Thank you,</p>" +
                                      $"<p>The Kutip Team</p>";

                        // Send the email
                        await _emailSender.SendEmailAsync(model.Email, subject, message);

                        TempData["SuccessMessage"] = $"User '{model.Email}' ({model.RoleName}) created successfully. A confirmation and password setup email has been sent.";
                    }
                    else 
                    {
                        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        await _userManager.ConfirmEmailAsync(user, confirmToken);
                        TempData["SuccessMessage"] = $"User '{model.Email}' ({model.RoleName}) created successfully. No confirmation email sent for this role.";
                    }

                    return RedirectToAction(nameof(Index));
                }

                // If user creation failed, add errors to ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            // If ModelState is invalid, return to view with validation errors
            return View(model);
        }

    }
}