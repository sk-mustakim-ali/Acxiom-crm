using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Services;

public sealed class AuditedSignInManager : SignInManager<ApplicationUser>
{
    private readonly ApplicationDbContext _context;

    public AuditedSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation,
        ApplicationDbContext context)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _context = context;
    }

    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.IsActive && await base.CanSignInAsync(user);

    public override async Task<SignInResult> PasswordSignInAsync(
        string userName,
        string password,
        bool isPersistent,
        bool lockoutOnFailure)
    {
        var normalizedUsername = userName.Trim();
        var user = await UserManager.FindByNameAsync(normalizedUsername)
            ?? await UserManager.FindByEmailAsync(normalizedUsername);
        var result = await base.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = user?.Id,
            Action = result.Succeeded ? "Login" : result.IsLockedOut ? "Lockout" : "FailedLogin",
            EntityName = "Authentication",
            RecordId = user?.Id,
            CreatedDate = DateTime.UtcNow,
            IpAddress = Context?.Connection.RemoteIpAddress?.ToString(),
            NewValue = result.Succeeded ? "Success" : result.IsLockedOut ? "Locked" : "Failed"
        });
        await _context.SaveChangesAsync();

        return result;
    }
}
