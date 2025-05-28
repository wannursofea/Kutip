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
            //Seed Roles
            var userManager = service.GetService<UserManager<ApplicationUser>>();
            var roleManager = service.GetService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole(Roles.Operator.ToString()));
            await roleManager.CreateAsync(new IdentityRole(Roles.Driver.ToString()));
            await roleManager.CreateAsync(new IdentityRole(Roles.Collector.ToString()));

            // creating admin/operator

            var user = new ApplicationUser
            {
                UserName = "operatorKutip@gmail.com",
                Email = "operatorKutip@gmail.com",
                Name = "operatorKutip",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };
            var userInDb = await userManager.FindByEmailAsync(user.Email);
            if (userInDb == null)
            {
                await userManager.CreateAsync(user, "Kutip1@"); //password
                await userManager.AddToRoleAsync(user, Roles.Operator.ToString());
            }
        }

        internal static async Task SeedRolesAndAdminAsync(object serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}