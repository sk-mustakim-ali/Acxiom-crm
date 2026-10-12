using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
