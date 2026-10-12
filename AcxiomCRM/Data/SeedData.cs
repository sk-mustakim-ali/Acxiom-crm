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
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create role {role}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
                }
            }
        }

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["BootstrapAdmin:Email"];
        var adminPassword = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(adminEmail) != string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException("Configure both BootstrapAdmin:Email and BootstrapAdmin:Password, or neither.");
        }

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            await EnsureAdminAsync(userManager, adminEmail.Trim(), adminPassword);
        }
    }

    private static async Task EnsureAdminAsync(UserManager<ApplicationUser> userManager, string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "System Administrator",
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
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to activate bootstrap administrator {email}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            var result = await userManager.AddToRoleAsync(user, "Admin");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign Admin role to {email}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }
    }
}
