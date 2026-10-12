using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Activity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string ActivityType { get; set; } = "Call";

    [Required]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public string? AssignedToId { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Planned";

    public Customer? Customer { get; set; }
    public Lead? Lead { get; set; }
}
