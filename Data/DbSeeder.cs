using Kutip.Constants;
using Kutip.Data;
using Microsoft.AspNetCore.Identity;
using System;

namespace Kutip.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            // Seed Roles
            var roleManager = service.GetService<RoleManager<IdentityRole>>();

            if (roleManager == null)
            {
                throw new InvalidOperationException("RoleManager service is not available.");
            }

            // Iterate through all roles in your enum and create if they don't exist
            foreach (var roleName in Enum.GetNames(typeof(Roles)))
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            //    // creating admin/operator

            //    var user = new ApplicationUser
            //    {
            //        UserName = "operatorKutip@gmail.com",
            //        Email = "operatorKutip@gmail.com",
            //        Name = "operatorKutip",
            //        EmailConfirmed = true,
            //        PhoneNumberConfirmed = true
            //    };
            //    var userInDb = await userManager.FindByEmailAsync(user.Email);
            //    if (userInDb == null)
            //    {
            //        await userManager.CreateAsync(user, "Kutip1@"); //password
            //        await userManager.AddToRoleAsync(user, Roles.Operator.ToString());
            //    }
        }
    }
}