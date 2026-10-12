using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly string[] ManagedRoles = ["Admin", "Manager", "SalesExecutive"];

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? role,
        string? status,
        string? lockout)
    {
        var now = DateTimeOffset.UtcNow;
        var usersQuery = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            usersQuery = usersQuery.Where(user =>
                user.FullName.Contains(term) ||
                (user.Email != null && user.Email.Contains(term)));
        }

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(user => user.IsActive);
        }
        else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(user => !user.IsActive);
        }

        if (string.Equals(lockout, "LockedOut", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(user =>
                user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > now);
        }
        else if (string.Equals(lockout, "NotLockedOut", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(user =>
                !user.LockoutEnabled || !user.LockoutEnd.HasValue || user.LockoutEnd <= now);
        }

        if (ManagedRoles.Contains(role, StringComparer.Ordinal))
        {
            var roleUsers = await _userManager.GetUsersInRoleAsync(role!);
            var roleUserIds = roleUsers.Select(user => user.Id).ToHashSet();
            usersQuery = usersQuery.Where(user => roleUserIds.Contains(user.Id));
        }

        var users = await usersQuery
            .OrderBy(user => user.Email)
            .ToListAsync();
        var viewModel = new AdminUserListViewModel
        {
            Search = search,
            RoleFilter = role,
            StatusFilter = status,
            LockoutFilter = lockout
        };

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            viewModel.Users.Add(new AdminUserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Roles = string.Join(", ", roles),
                IsActive = user.IsActive,
                IsLockedOut = user.LockoutEnabled && user.LockoutEnd > now,
                LockoutEnd = user.LockoutEnd
            });
        }

        return View(viewModel);
    }

    public async Task<IActionResult> Roles()
    {
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Admin"] = "Full application administration, CRM records, users, role assignment, audit records, and reports.",
            ["Manager"] = "Team-wide CRM management, assignment, pipeline, activity, follow-up, and management reporting.",
            ["SalesExecutive"] = "Assigned customers, leads, opportunities, follow-ups, activities, and scoped dashboard/report access."
        };
        var roles = await _roleManager.Roles.OrderBy(role => role.Name).ToListAsync();
        var items = new List<AdminRoleListItemViewModel>();
        foreach (var role in roles)
        {
            if (role.Name == null || !descriptions.TryGetValue(role.Name, out var description))
            {
                continue;
            }

            var users = await _userManager.GetUsersInRoleAsync(role.Name);
            items.Add(new AdminRoleListItemViewModel
            {
                Name = role.Name,
                Scope = description,
                UserCount = users.Count
            });
        }

        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateAdminUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAdminUserViewModel model)
    {
        if (!IsManagedRole(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        }
        else if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "The selected role is not available.");
        }

        if (!ModelState.IsValid)
        {
            ClearPasswordFields(model);
            return View(model);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(createResult);
            ClearPasswordFields(model);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(roleResult);
            ClearPasswordFields(model);
            return View(model);
        }

        await AddAuditAsync("CreateUser", user.Id, null, $"role={model.Role}; active=true");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["StatusMessage"] = "The user was created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (!IsManagedRole(role) || !await _roleManager.RoleExistsAsync(role))
        {
            TempData["ErrorMessage"] = "Select a valid, available role.";
            return RedirectToAction(nameof(Index));
        }

        var existingRoles = await _userManager.GetRolesAsync(user);
        if (existingRoles.Count == 1 && string.Equals(existingRoles[0], role, StringComparison.Ordinal))
        {
            TempData["StatusMessage"] = "The user already has that role.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (user.IsActive &&
            existingRoles.Contains("Admin", StringComparer.Ordinal) &&
            !string.Equals(role, "Admin", StringComparison.Ordinal) &&
            await CountOtherActiveAdminsAsync(user.Id) == 0)
        {
            TempData["ErrorMessage"] = "The last active Admin cannot be demoted or removed from the Admin role.";
            return RedirectToAction(nameof(Index));
        }

        var oldRoleDetail = string.Join(",", existingRoles);
        if (existingRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(removeResult);
                return RedirectToAction(nameof(Index));
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(addResult);
            return RedirectToAction(nameof(Index));
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(stampResult);
            return RedirectToAction(nameof(Index));
        }

        await AddAuditAsync("ChangeUserRole", user.Id, $"roles={oldRoleDetail}", $"role={role}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["StatusMessage"] = "The user's role was updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool active)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (user.IsActive == active)
        {
            TempData["StatusMessage"] = active ? "The account is already active." : "The account is already inactive.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (!active &&
            user.IsActive &&
            (await _userManager.IsInRoleAsync(user, "Admin")) &&
            await CountOtherActiveAdminsAsync(user.Id) == 0)
        {
            TempData["ErrorMessage"] = "The last active Admin cannot be deactivated.";
            return RedirectToAction(nameof(Index));
        }

        var wasActive = user.IsActive;
        user.IsActive = active;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(updateResult);
            return RedirectToAction(nameof(Index));
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(stampResult);
            return RedirectToAction(nameof(Index));
        }

        await AddAuditAsync(
            active ? "ActivateUser" : "DeactivateUser",
            user.Id,
            $"active={wasActive.ToString().ToLowerInvariant()}",
            $"active={active.ToString().ToLowerInvariant()}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["StatusMessage"] = active ? "The account was activated." : "The account was deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsLockedOutAsync(user))
        {
            TempData["StatusMessage"] = "The account is not locked out.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!unlockResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(unlockResult);
            return RedirectToAction(nameof(Index));
        }

        var failedCountResult = await _userManager.ResetAccessFailedCountAsync(user);
        if (!failedCountResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(failedCountResult);
            return RedirectToAction(nameof(Index));
        }

        await AddAuditAsync("UnlockUser", user.Id, "lockout=locked", "lockout=unlocked");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["StatusMessage"] = "The account was unlocked.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<int> CountOtherActiveAdminsAsync(string excludedUserId)
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        return admins.Count(user => user.Id != excludedUserId && user.IsActive);
    }

    private async Task AddAuditAsync(string action, string userId, string? oldValue, string? newValue)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action,
            EntityName = "User",
            RecordId = userId,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await Task.CompletedTask;
    }

    private static bool IsManagedRole(string? role)
    {
        return role != null && ManagedRoles.Contains(role, StringComparer.Ordinal);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        var errors = string.Join(" ", result.Errors.Select(error => error.Description));
        TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(errors)
            ? "The requested account operation could not be completed."
            : errors;
    }

    private void ClearPasswordFields(CreateAdminUserViewModel model)
    {
        model.Password = string.Empty;
        model.ConfirmPassword = string.Empty;
        ModelState.Remove(nameof(model.Password));
        ModelState.Remove(nameof(model.ConfirmPassword));
    }
}
