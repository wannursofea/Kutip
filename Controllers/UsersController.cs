using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Kutip.Data; 
using Kutip.ViewModels; 
using Kutip.Constants; 
using Microsoft.AspNetCore.WebUtilities; 
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity.UI.Services;

public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailSender _emailSender;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _emailSender = emailSender;
    }

    // GET: Users/Create
    public IActionResult Create()
    {
        // Populate roles for dropdown, filter out Admin/Operator if needed
        ViewBag.Roles = _roleManager.Roles
                                    .Where(r => r.Name == Roles.Collector.ToString() || r.Name == Roles.Driver.ToString())
                                    .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                    .ToList();
        return View();
    }

    // POST: Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        // Repopulate roles for dropdown if ModelState is invalid and view is returned
        // Do this at the beginning to ensure it's always set before returning View(model)
        ViewBag.Roles = _roleManager.Roles
                                    .Where(r => r.Name == Roles.Collector.ToString() || r.Name == Roles.Driver.ToString())
                                    .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                    .ToList();

        if (ModelState.IsValid)
        {
            // --- Gmail Only Check ---
            if (model.Role == Roles.Collector.ToString() || model.Role == Roles.Driver.ToString())
            {
                // Simple regex check for @gmail.com. More robust checks could be added if needed.
                if (!Regex.IsMatch(model.Email, @"@gmail\.com$", RegexOptions.IgnoreCase))
                {
                    ModelState.AddModelError("Email", "Only Gmail accounts are allowed for Collector and Driver roles.");
                    return View(model);
                }
            }
            // --- End Gmail Only Check ---

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Name = model.Name,
                EmailConfirmed = false // Keep false for confirmation flow
            };

            // Option 1: Force password reset after confirmation (more secure)
            // Do NOT set a temporary password here. User sets it via the reset link.
            var result = await _userManager.CreateAsync(user); // Create without password initially

            // If you still want to set a temporary password and send it:
            // var temporaryPassword = "TempPassword123!"; // Make this secure/random
            // var result = await _userManager.CreateAsync(user, temporaryPassword);


            if (result.Succeeded)
            {
                // Assign role
                if (!string.IsNullOrEmpty(model.Role))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                // --- Conditional Email Sending for Collector and Driver Roles ---
                if (model.Role == Roles.Collector.ToString() || model.Role == Roles.Driver.ToString())
                {
                    // Generate email confirmation token
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    // Build confirmation link
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code },
                        protocol: Request.Scheme);

                    // Build password reset token (for the "change password" message)
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
                                  $"<p>Your account for Kutip Waste Management has been created as a **{model.Role}**.</p>" +
                                  $"<p>Before you can log in, you need to confirm your email and set your password:</p>" +
                                  $"<ol>" +
                                  $"<li>Please confirm your email address by clicking <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>this link</a>.</li>" +
                                  $"<li>Once your email is confirmed, you **must** set your password. Click this link to create your new password: <a href='{HtmlEncoder.Default.Encode(resetPasswordUrl)}'>Set Your Password</a></li>" +
                                  $"</ol>" +
                                  $"<p>Please note: Your initial login will require you to set a password using the link provided above.</p>" +
                                  $"<p>If you have any questions, please contact our support.</p>" +
                                  $"<p>Thank you,</p>" +
                                  $"<p>The Kutip Team</p>";

                    // Send the email
                    await _emailSender.SendEmailAsync(model.Email, subject, message);

                    TempData["SuccessMessage"] = $"User '{model.Email}' ({model.Role}) created successfully. A confirmation and password setup email has been sent.";
                }
                else // For other roles (e.g., Admin, Operator) - no email sent
                {
                    // If you want to confirm admin/operator email immediately or set a default password
                    // you would do it here. For simplicity, we are skipping email confirmation for them.
                    await _userManager.ConfirmEmailAsync(user, await _userManager.GenerateEmailConfirmationTokenAsync(user));
                    // Optionally set a temporary password for operators/admins if not handled by seeder
                    // await _userManager.AddPasswordAsync(user, "DefaultAdminPass123!");
                    TempData["SuccessMessage"] = $"User '{model.Email}' ({model.Role}) created successfully. No confirmation email sent for this role.";
                }

                return RedirectToAction(nameof(Index)); // Or wherever you list users
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        // If ModelState is invalid, the ViewBag.Roles was already set at the top
        return View(model);
    }
}