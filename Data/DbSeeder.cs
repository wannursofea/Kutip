using Kutip.Constants; // Assuming your Roles enum is here
using Kutip.Data;
using Kutip.Models; // Assuming ApplicationUser is here
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq; // Added for .FirstOrDefault() or .SingleOrDefault()
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection; // For GetService

namespace Kutip.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            // Get managers from the service provider
            var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>(); // Use GetRequiredService for clarity and better error if not found
            var userManager = service.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed Roles
            foreach (var roleName in Enum.GetNames<Roles>())
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Admin/Operator User (as per your current code)
            string operatorEmail = "kutip.noreply@gmail.com";
            string operatorPassword = "KutipPass123!"; 
            string operatorName = "operatorKutip"; //QWERTY!@#123q
            // every new created user will use default initial password of QWERTY!@#123q

            var operatorUser = await userManager.FindByEmailAsync(operatorEmail);

            if (operatorUser == null) // User does not exist, so create it
            {
                operatorUser = new ApplicationUser // Assign to operatorUser variable
                {
                    UserName = operatorEmail,
                    Email = operatorEmail,
                    Name = operatorName,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true
                };

                var createResult = await userManager.CreateAsync(operatorUser, operatorPassword);

                if (createResult.Succeeded)
                {
                    // Assign role if user created successfully
                    if (!await userManager.IsInRoleAsync(operatorUser, Roles.Operator.ToString()))
                    {
                        await userManager.AddToRoleAsync(operatorUser, Roles.Operator.ToString());
                    }
                }
                else
                {
                    // Log errors if creation failed (e.g., password policy)
                    foreach (var error in createResult.Errors)
                    {
                        Console.WriteLine($"Error creating operator user: {error.Description}");
                    }
                }
            }
            else // User already exists
            {
                // Ensure the existing user is in the Operator role
                if (!await userManager.IsInRoleAsync(operatorUser, Roles.Operator.ToString()))
                {
                    await userManager.AddToRoleAsync(operatorUser, Roles.Operator.ToString());
                }
            }
        }
    }
}