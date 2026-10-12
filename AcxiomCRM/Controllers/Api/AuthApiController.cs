using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/auth")]
public sealed class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAntiforgery _antiforgery;

    public AuthApiController(SignInManager<ApplicationUser> signInManager, IAntiforgery antiforgery)
    {
        _signInManager = signInManager;
        _antiforgery = antiforgery;
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult GetAntiforgeryToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _signInManager.PasswordSignInAsync(request.Email.Trim(), request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Problem(statusCode: StatusCodes.Status423Locked, title: "The account is temporarily locked.");
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid credentials or inactive account." });
        }

        var user = await _signInManager.UserManager.FindByEmailAsync(request.Email.Trim());
        var roles = user == null ? Array.Empty<string>() : (await _signInManager.UserManager.GetRolesAsync(user)).ToArray();
        return Ok(new { message = "Signed in.", userId = user?.Id, roles });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    public sealed class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(256, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
