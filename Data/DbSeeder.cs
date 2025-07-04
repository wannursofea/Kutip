using Kutip.Constants;
using Microsoft.AspNetCore.Identity;

namespace Kutip.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = service.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var roleName in Enum.GetNames<Roles>())
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            string operatorEmail = "kutip.noreply@gmail.com";
            string operatorPassword = "KutipPass123!";
            string operatorName = "operatorKutip";
            // every new created user will use default initial password of QWERTY!@#123q

            var operatorUser = await userManager.FindByEmailAsync(operatorEmail);

            if (operatorUser == null)
            {
                operatorUser = new ApplicationUser
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
                    if (!await userManager.IsInRoleAsync(operatorUser, Roles.Operator.ToString()))
                    {
                        await userManager.AddToRoleAsync(operatorUser, Roles.Operator.ToString());
                    }
                }
                else
                {
                    foreach (var error in createResult.Errors)
                    {
                        Console.WriteLine($"Error creating operator user: {error.Description}");
                    }
                }
            }
            else
            {
                if (!await userManager.IsInRoleAsync(operatorUser, Roles.Operator.ToString()))
                {
                    await userManager.AddToRoleAsync(operatorUser, Roles.Operator.ToString());
                }
            }
        }
    }
}