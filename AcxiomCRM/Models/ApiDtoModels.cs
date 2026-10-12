using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class CustomerDto
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? OwnerId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class CustomerCreateRequest
{
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

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Active";

    [StringLength(250)]
    public string? Address { get; set; }
    public string? OwnerId { get; set; }
}

public class LeadCreateRequest
{
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

    [StringLength(30)]
    public string Status { get; set; } = "New";

    [Required]
    [StringLength(30)]
    public string Priority { get; set; } = "Medium";

    [Range(0, 1_000_000_000)]
    public decimal ExpectedValue { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
    public string? AssignedToId { get; set; }
}

public class OpportunityCreateRequest
{
    [Required]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(30)]
    public string Stage { get; set; } = "Qualification";

    [Range(0, 100)]
    public int Probability { get; set; }

    [DataType(DataType.Date)]
    public DateTime ExpectedCloseDate { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Open";

    [StringLength(2000)]
    public string? Notes { get; set; }
    public string? OwnerId { get; set; }
}

public class FollowUpCreateRequest
{
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

    [StringLength(30)]
    public string Status { get; set; } = "Planned";

    [StringLength(2000)]
    public string? Notes { get; set; }
    public string? AssignedToId { get; set; }
}

public class LeadDto
{
    public int Id { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal ExpectedValue { get; set; }
    public string? AssignedToId { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class OpportunityDto
{
    public int Id { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string Stage { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OwnerId { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class FollowUpDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public int? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string FollowUpType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssignedToId { get; set; }
}
