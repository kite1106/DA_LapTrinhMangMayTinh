using Microsoft.AspNetCore.Identity;
using SecurityMonitor.Models;

namespace SecurityMonitor.Data
{
    public static class RoleInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Tạo admin mặc định nếu chưa có
            var adminEmail = "admin@securitymonitor.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, "Admin@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }

            // Tạo user mẫu
            var testUserEmail = "user@securitymonitor.com";
            var testUser = await userManager.FindByEmailAsync(testUserEmail);

            if (testUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = testUserEmail,
                    Email = testUserEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, "User@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "User");
                }
            }
        }
    }
}
