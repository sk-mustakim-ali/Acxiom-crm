using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Data;

public class ApplicationUser : IdentityUser
{
    [PersonalData]
    [ProtectedPersonalData]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Whether this account is allowed to sign in. PDF §4.7 §6.1</summary>
    public bool IsActive { get; set; } = true;
}
