using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Customer
{
    public int Id { get; set; }

    [Required]
    [StringLength(30)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [RegularExpression(@"^(?=(?:\D*\d){7,15}\D*$)\+?[0-9][0-9(). -]*$", ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(80)]
    public string? State { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Active";

    public string? OwnerId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
