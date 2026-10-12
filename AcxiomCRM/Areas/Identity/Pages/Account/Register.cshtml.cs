using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcxiomCRM.Areas.Identity.Pages.Account;

public sealed class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email.Trim(),
            Email = Input.Email.Trim(),
            FullName = Input.FullName.Trim(),
            EmailConfirmed = true,
            IsActive = true
        };
        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "SalesExecutive");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "Register",
            EntityName = "User",
            RecordId = user.Id,
            CreatedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            NewValue = "SalesExecutive"
        });
        await _context.SaveChangesAsync();
        await _signInManager.SignInAsync(user, isPersistent: false);

        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Dashboard");
        return LocalRedirect(returnUrl ?? "/Dashboard");
    }

    public sealed class InputModel
    {
        [Required]
        [StringLength(120)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(256, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
