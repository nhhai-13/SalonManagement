using Microsoft.AspNetCore.Identity;

namespace SalonManagement.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roles =
            {
                "Admin",
                "Owner",
                "Receptionist",
                "Stylist"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}