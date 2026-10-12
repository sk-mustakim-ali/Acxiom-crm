using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class FollowUp
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime FollowUpDate { get; set; }

    [Required]
    [StringLength(100)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string FollowUpType { get; set; } = "Call";

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Planned";

    public string? AssignedToId { get; set; }
    public string? Notes { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }
    public Lead? Lead { get; set; }
    public Opportunity? Opportunity { get; set; }
}
