using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AdminUserListViewModel
{
    public string? Search { get; set; }
    public string? RoleFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string? LockoutFilter { get; set; }
    public List<AdminUserListItemViewModel> Users { get; set; } = [];
}

public class AdminUserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}

public class CreateAdminUserViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Role { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AdminRoleListItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int UserCount { get; set; }
}
