using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Lead
{
    public int Id { get; set; }

    [Required]
    [StringLength(30)]
    public string LeadCode { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [Phone]
    [RegularExpression(@"^(?=(?:\D*\d){7,15}\D*$)\+?[0-9][0-9(). -]*$", ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Required]
    [StringLength(150)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Source { get; set; } = "Website";

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "New";

    [Required]
    [StringLength(30)]
    public string Priority { get; set; } = "Medium";

    [Range(0, 1000000000)]
    public decimal ExpectedValue { get; set; }

    public string? AssignedToId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
