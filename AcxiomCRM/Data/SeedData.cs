using AcxiomCRM.Data;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["Admin", "Manager", "SalesExecutive"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@acxiomcrm.com", "System Administrator", "Admin", "Admin@123");
        await EnsureUserAsync(userManager, "manager@acxiomcrm.com", "Regional Manager", "Manager", "Manager@123");
        await EnsureUserAsync(userManager, "sales@acxiomcrm.com", "Sales Executive", "SalesExecutive", "Sales@123");
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string role, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
