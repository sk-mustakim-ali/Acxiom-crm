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
}

public class CustomerCreateRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? Address { get; set; }
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
