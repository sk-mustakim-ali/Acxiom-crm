using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Opportunity
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(30)]
    public string Stage { get; set; } = "Qualification";

    [Required]
    [Range(0, 100)]
    public int Probability { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime ExpectedCloseDate { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Open";

    public string? OwnerId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public decimal WeightedPipeline => Amount * Probability / 100m;

    public Customer? Customer { get; set; }
    public Lead? Lead { get; set; }
}
