using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api")]
[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public sealed class CrmApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _accessScope;

    public CrmApiController(ApplicationDbContext context, CrmAccessScope accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers()
    {
        var customers = await _accessScope.Customers()
            .OrderBy(customer => customer.CustomerName)
            .ToListAsync();
        return Ok(customers.Select(ToDto));
    }

    [HttpGet("customers/{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
    {
        var customer = await _accessScope.Customers().FirstOrDefaultAsync(item => item.Id == id);
        return customer == null ? NotFound() : Ok(ToDto(customer));
    }

    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer(CustomerCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var customer = new Customer
        {
            CustomerCode = CreateCode("CUS-"),
            CustomerName = request.CustomerName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            CompanyName = request.CompanyName.Trim(),
            Status = NormalizeChoice(request.Status, CrmWorkflowRules.CustomerStatuses),
            Address = request.Address?.Trim(),
            OwnerId = CanManageAssignments ? request.OwnerId : CurrentUserId,
            CreatedBy = User.Identity?.Name,
            CreatedDate = DateTime.UtcNow
        };
        if (!await _accessScope.IsActiveSalesExecutiveAsync(customer.OwnerId))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Assign the customer to an active Sales Executive.");
        }

        var validation = await ValidateCustomerAsync(customer);
        if (validation != null)
        {
            return validation;
        }

        await SaveWithAuditAsync(customer, "Create", "Customer", null, customer.CustomerName);
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, ToDto(customer));
    }

    [HttpPut("customers/{id:int}")]
    public async Task<IActionResult> UpdateCustomer(int id, CustomerCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var customer = await _accessScope.Customers().FirstOrDefaultAsync(item => item.Id == id);
        if (customer == null)
        {
            return NotFound();
        }

        var oldName = customer.CustomerName;
        customer.CustomerName = request.CustomerName.Trim();
        customer.Email = request.Email.Trim();
        customer.Phone = request.Phone.Trim();
        customer.CompanyName = request.CompanyName.Trim();
        customer.Status = NormalizeChoice(request.Status, CrmWorkflowRules.CustomerStatuses);
        if (CanManageAssignments && request.OwnerId != customer.OwnerId)
        {
            if (!await _accessScope.IsActiveSalesExecutiveAsync(request.OwnerId))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Assign the customer to an active Sales Executive.");
            }

            customer.OwnerId = request.OwnerId;
        }
        customer.ModifiedDate = DateTime.UtcNow;
        customer.Address = request.Address?.Trim();

        var validation = await ValidateCustomerAsync(customer, id);
        if (validation != null)
        {
            return validation;
        }

        await SaveWithAuditAsync(customer, "Update", "Customer", oldName, customer.CustomerName);
        return Ok(ToDto(customer));
    }

    [HttpDelete("customers/{id:int}")]
    public async Task<IActionResult> DeactivateCustomer(int id)
    {
        var customer = await _accessScope.Customers().FirstOrDefaultAsync(item => item.Id == id);
        if (customer == null)
        {
            return NotFound();
        }

        customer.Status = "Inactive";
        await SaveWithAuditAsync(customer, "Deactivate", "Customer", "Active", "Inactive");
        return NoContent();
    }

    [HttpGet("leads")]
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads()
    {
        var leads = await _accessScope.Leads()
            .OrderBy(lead => lead.LeadName)
            .ToListAsync();
        return Ok(leads.Select(ToDto));
    }

    [HttpGet("leads/{id:int}")]
    public async Task<ActionResult<LeadDto>> GetLead(int id)
    {
        var lead = await _accessScope.Leads().FirstOrDefaultAsync(item => item.Id == id);
        return lead == null ? NotFound() : Ok(ToDto(lead));
    }

    [HttpPost("leads")]
    public async Task<ActionResult<LeadDto>> CreateLead(LeadCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!CrmWorkflowRules.IsValidLeadStatus(request.Status)
            || !CrmWorkflowRules.LeadPriorities.Contains(request.Priority, StringComparer.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select a valid initial lead status and priority.");
        }

        var lead = new Lead
        {
            LeadCode = CreateCode("LEAD-"),
            LeadName = request.LeadName.Trim(),
            CompanyName = request.CompanyName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Source = request.Source.Trim(),
            Status = "New",
            Priority = request.Priority.Trim(),
            ExpectedValue = request.ExpectedValue,
            Notes = request.Notes?.Trim(),
            AssignedToId = CanManageAssignments ? request.AssignedToId : CurrentUserId,
            CreatedDate = DateTime.UtcNow
        };
        if (!await _accessScope.IsActiveSalesExecutiveAsync(lead.AssignedToId))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Assign the lead to an active Sales Executive.");
        }

        _context.Leads.Add(lead);
        await SaveWithAuditAsync(lead, "Create", "Lead", null, lead.LeadName);
        return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, ToDto(lead));
    }

    [HttpPut("leads/{id:int}")]
    public async Task<ActionResult<LeadDto>> UpdateLead(int id, LeadCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var lead = await _accessScope.Leads().FirstOrDefaultAsync(item => item.Id == id);
        if (lead == null)
        {
            return NotFound();
        }

        if (!CrmWorkflowRules.IsValidLeadStatus(request.Status)
            || !CrmWorkflowRules.LeadPriorities.Contains(request.Priority, StringComparer.OrdinalIgnoreCase)
            || !CrmWorkflowRules.CanTransitionLead(lead.Status, request.Status))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid lead status transition.");
        }

        var oldName = lead.LeadName;
        lead.LeadName = request.LeadName.Trim();
        lead.CompanyName = request.CompanyName.Trim();
        lead.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        lead.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        lead.Source = request.Source.Trim();
        lead.Status = request.Status;
        lead.Priority = request.Priority.Trim();
        lead.ExpectedValue = request.ExpectedValue;
        lead.Notes = request.Notes?.Trim();
        if (CanManageAssignments && request.AssignedToId != lead.AssignedToId)
        {
            if (!await _accessScope.IsActiveSalesExecutiveAsync(request.AssignedToId))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Assign the lead to an active Sales Executive.");
            }

            lead.AssignedToId = request.AssignedToId;
        }

        await SaveWithAuditAsync(lead, "Update", "Lead", oldName, lead.LeadName);
        return Ok(ToDto(lead));
    }

    [HttpDelete("leads/{id:int}")]
    public async Task<IActionResult> DeleteLead(int id)
    {
        var lead = await _accessScope.Leads().FirstOrDefaultAsync(item => item.Id == id);
        if (lead == null)
        {
            return NotFound();
        }

        _context.Leads.Remove(lead);
        await SaveWithAuditAsync(null, "Delete", "Lead", lead.LeadName, null, lead.Id.ToString());
        return NoContent();
    }

    [HttpGet("opportunities")]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities()
    {
        var results = await _accessScope.Opportunities()
            .OrderBy(opportunity => opportunity.OpportunityName)
            .Select(opportunity => new OpportunityDto
            {
                Id = opportunity.Id,
                OpportunityName = opportunity.OpportunityName,
                CustomerId = opportunity.CustomerId,
                CustomerName = opportunity.Customer == null ? null : opportunity.Customer.CustomerName,
                Stage = opportunity.Stage,
                Amount = opportunity.Amount,
                Probability = opportunity.Probability,
                ExpectedCloseDate = opportunity.ExpectedCloseDate,
                Status = opportunity.Status,
                OwnerId = opportunity.OwnerId,
                CreatedDate = opportunity.CreatedDate
            })
            .ToListAsync();
        return Ok(results);
    }

    [HttpGet("opportunities/{id:int}")]
    public async Task<ActionResult<OpportunityDto>> GetOpportunity(int id)
    {
        var opportunity = await _accessScope.Opportunities()
            .Include(item => item.Customer)
            .FirstOrDefaultAsync(item => item.Id == id);
        return opportunity == null ? NotFound() : Ok(ToDto(opportunity));
    }

    [HttpPost("opportunities")]
    public async Task<IActionResult> CreateOpportunity(OpportunityCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await ValidateOpportunityRequestAsync(request);
        if (result != null)
        {
            return result;
        }

        var opportunity = new Opportunity
        {
            OpportunityName = request.OpportunityName.Trim(),
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
            Amount = request.Amount,
            Stage = request.Stage,
            Probability = request.Probability,
            ExpectedCloseDate = request.ExpectedCloseDate.Date,
            Status = request.Status,
            OwnerId = CanManageAssignments ? request.OwnerId : CurrentUserId,
            Notes = request.Notes?.Trim(),
            CreatedDate = DateTime.UtcNow,
            ClosedDate = CrmWorkflowRules.IsActiveOpportunityStatus(request.Status) ? null : DateTime.UtcNow
        };
        if (!await _accessScope.IsActiveSalesExecutiveAsync(opportunity.OwnerId))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Assign the opportunity to an active Sales Executive.");
        }
        await SaveWithAuditAsync(opportunity, "Create", "Opportunity", null, opportunity.OpportunityName);
        var customer = await _context.Customers.FindAsync(opportunity.CustomerId);
        return CreatedAtAction(nameof(GetOpportunity), new { id = opportunity.Id }, ToDto(opportunity, customer?.CustomerName));
    }

    [HttpPut("opportunities/{id:int}")]
    public async Task<IActionResult> UpdateOpportunity(int id, OpportunityCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var opportunity = await _accessScope.Opportunities().FirstOrDefaultAsync(item => item.Id == id);
        if (opportunity == null)
        {
            return NotFound();
        }

        var result = await ValidateOpportunityRequestAsync(request);
        if (result != null)
        {
            return result;
        }

        if (!CrmWorkflowRules.CanTransitionOpportunityStage(opportunity.Stage, request.Stage))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid opportunity stage transition.");
        }

        var oldName = opportunity.OpportunityName;
        opportunity.OpportunityName = request.OpportunityName.Trim();
        opportunity.CustomerId = request.CustomerId;
        opportunity.LeadId = request.LeadId;
        opportunity.Amount = request.Amount;
        opportunity.Stage = request.Stage;
        opportunity.Probability = request.Probability;
        opportunity.ExpectedCloseDate = request.ExpectedCloseDate.Date;
        opportunity.Status = request.Status;
        opportunity.Notes = request.Notes?.Trim();
        if (CanManageAssignments && request.OwnerId != opportunity.OwnerId)
        {
            if (!await _accessScope.IsActiveSalesExecutiveAsync(request.OwnerId))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Assign the opportunity to an active Sales Executive.");
            }

            opportunity.OwnerId = request.OwnerId;
        }
        opportunity.ClosedDate = CrmWorkflowRules.IsActiveOpportunityStatus(request.Status)
            ? null
            : opportunity.ClosedDate ?? DateTime.UtcNow;

        await SaveWithAuditAsync(opportunity, "Update", "Opportunity", oldName, opportunity.OpportunityName);
        var customer = await _context.Customers.FindAsync(opportunity.CustomerId);
        return Ok(ToDto(opportunity, customer?.CustomerName));
    }

    [HttpDelete("opportunities/{id:int}")]
    public async Task<IActionResult> DeleteOpportunity(int id)
    {
        var opportunity = await _accessScope.Opportunities().FirstOrDefaultAsync(item => item.Id == id);
        if (opportunity == null)
        {
            return NotFound();
        }

        _context.Opportunities.Remove(opportunity);
        await SaveWithAuditAsync(null, "Delete", "Opportunity", opportunity.OpportunityName, null, opportunity.Id.ToString());
        return NoContent();
    }

    [HttpGet("followups")]
    public async Task<ActionResult<IEnumerable<FollowUpDto>>> GetFollowUps()
    {
        var results = await _accessScope.FollowUps()
            .Include(followUp => followUp.Customer)
            .Include(followUp => followUp.Lead)
            .Include(followUp => followUp.Opportunity)
            .OrderBy(followUp => followUp.FollowUpDate)
            .Select(followUp => new FollowUpDto
            {
                Id = followUp.Id,
                CustomerId = followUp.CustomerId,
                CustomerName = followUp.Customer == null ? null : followUp.Customer.CustomerName,
                LeadId = followUp.LeadId,
                LeadName = followUp.Lead == null ? null : followUp.Lead.LeadName,
                OpportunityId = followUp.OpportunityId,
                OpportunityName = followUp.Opportunity == null ? null : followUp.Opportunity.OpportunityName,
                FollowUpDate = followUp.FollowUpDate,
                Subject = followUp.Subject,
                FollowUpType = followUp.FollowUpType,
                Status = followUp.Status,
                AssignedToId = followUp.AssignedToId
            })
            .ToListAsync();
        return Ok(results);
    }

    [HttpPost("followups")]
    public async Task<ActionResult<FollowUpDto>> CreateFollowUp(FollowUpCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var relatedCount = (request.CustomerId.HasValue ? 1 : 0)
            + (request.LeadId.HasValue ? 1 : 0)
            + (request.OpportunityId.HasValue ? 1 : 0);
        if (relatedCount != 1
            || (request.CustomerId.HasValue && !await _accessScope.CanAccessCustomerAsync(request.CustomerId.Value))
            || (request.LeadId.HasValue && !await _accessScope.CanAccessLeadAsync(request.LeadId.Value))
            || (request.OpportunityId.HasValue && !await _accessScope.CanAccessOpportunityAsync(request.OpportunityId.Value)))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Choose exactly one related record within your authorized scope.");
        }

        if (!CrmWorkflowRules.IsValidFollowUpType(request.FollowUpType)
            || !CrmWorkflowRules.IsValidFollowUpStatus(request.Status))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select a valid follow-up type and status.");
        }

        if (request.Status.Equals("Planned", StringComparison.OrdinalIgnoreCase)
            && request.FollowUpDate.Date < DateTime.Today)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Follow-up date cannot be earlier than today.");
        }

        var followUp = new FollowUp
        {
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
            OpportunityId = request.OpportunityId,
            FollowUpDate = request.FollowUpDate.Date,
            Subject = request.Subject.Trim(),
            FollowUpType = request.FollowUpType,
            Status = request.Status,
            Notes = request.Notes?.Trim(),
            AssignedToId = CanManageAssignments ? request.AssignedToId : CurrentUserId,
            CreatedDate = DateTime.UtcNow
        };
        if (!await _accessScope.IsActiveUserAsync(followUp.AssignedToId))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Assign the follow-up to an active user.");
        }
        await SaveWithAuditAsync(followUp, "Create", "FollowUp", null, followUp.Subject);
        return CreatedAtAction(nameof(GetFollowUps), new { id = followUp.Id }, new FollowUpDto
        {
            Id = followUp.Id,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            OpportunityId = followUp.OpportunityId,
            FollowUpDate = followUp.FollowUpDate,
            Subject = followUp.Subject,
            FollowUpType = followUp.FollowUpType,
            Status = followUp.Status,
            AssignedToId = followUp.AssignedToId
        });
    }

    [HttpGet("reports/pipeline")]
    public async Task<IActionResult> GetPipelineReport()
    {
        var opportunityData = await _accessScope.Opportunities()
            .Select(opportunity => new { opportunity.Stage, opportunity.Amount, opportunity.Probability, opportunity.Status })
            .ToListAsync();
        var pipeline = CrmWorkflowRules.OpportunityStages.Select(stage =>
        {
            var stageItems = opportunityData.Where(item => item.Stage.Equals(stage, StringComparison.OrdinalIgnoreCase)).ToList();
            var active = stageItems.Where(item => item.Status.Equals("Open", StringComparison.OrdinalIgnoreCase)).ToList();
            return new
            {
                Stage = stage,
                Count = stageItems.Count,
                Amount = active.Sum(item => item.Amount),
                WeightedAmount = active.Sum(item => item.Amount * item.Probability / 100m)
            };
        });
        return Ok(pipeline);
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool CanManageAssignments => User.IsInRole("Admin") || User.IsInRole("Manager");

    private async Task<IActionResult?> ValidateCustomerAsync(Customer customer, int? excludeId = null)
    {
        if (!CrmWorkflowRules.CustomerStatuses.Contains(customer.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select a valid customer status.");
        }

        if (!CrmWorkflowRules.CustomerStatuses.Contains(customer.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select a valid customer status.");
        }

        if (await _context.Customers.AnyAsync(item => item.Id != excludeId
            && item.Email.ToLower() == customer.Email.ToLower()))
        {
            return Conflict(new { message = "Customer email already exists." });
        }

        if (await _context.Customers.AnyAsync(item => item.Id != excludeId && item.Phone == customer.Phone))
        {
            return Conflict(new { message = "Customer phone already exists." });
        }

        return null;
    }

    private async Task<IActionResult?> ValidateOpportunityRequestAsync(OpportunityCreateRequest request)
    {
        if (request.Amount <= 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Opportunity Amount must be greater than 0.");
        }

        if (request.Probability is < 0 or > 100)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Probability must be between 0 and 100.");
        }

        if (!CrmWorkflowRules.IsValidOpportunityStage(request.Stage)
            || !CrmWorkflowRules.IsValidOpportunityStatus(request.Status)
            || !CrmWorkflowRules.OpportunityStageMatchesStatus(request.Stage, request.Status))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select a valid pipeline stage and matching status.");
        }

        if (CrmWorkflowRules.IsActiveOpportunityStatus(request.Status)
            && request.ExpectedCloseDate.Date < DateTime.Today)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Expected Close Date cannot be in the past.");
        }

        if (!await _accessScope.CanAccessCustomerAsync(request.CustomerId))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select a customer you are authorized to manage.");
        }

        if (request.LeadId.HasValue && !await _accessScope.CanAccessLeadAsync(request.LeadId.Value))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select a lead you are authorized to manage.");
        }

        return null;
    }

    private async Task SaveWithAuditAsync(object? entity, string action, string entityName, string? oldValue, string? newValue, string? recordId = null)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (entity != null)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _context.Add(entity);
            }

            await _context.SaveChangesAsync();
            recordId ??= entity switch
            {
                Customer customer => customer.Id.ToString(),
                Lead lead => lead.Id.ToString(),
                Opportunity opportunity => opportunity.Id.ToString(),
                FollowUp followUp => followUp.Id.ToString(),
                _ => null
            };
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static string CreateCode(string prefix) =>
        prefix + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private static string NormalizeChoice(string? value, IReadOnlyCollection<string> choices) =>
        choices.FirstOrDefault(choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

    private static CustomerDto ToDto(Customer customer) => new()
    {
        Id = customer.Id,
        CustomerCode = customer.CustomerCode,
        CustomerName = customer.CustomerName,
        Email = customer.Email,
        Phone = customer.Phone,
        CompanyName = customer.CompanyName,
        Status = customer.Status,
        OwnerId = customer.OwnerId,
        CreatedDate = customer.CreatedDate,
        ModifiedDate = customer.ModifiedDate
    };

    private static LeadDto ToDto(Lead lead) => new()
    {
        Id = lead.Id,
        LeadCode = lead.LeadCode,
        LeadName = lead.LeadName,
        CompanyName = lead.CompanyName,
        Email = lead.Email,
        Phone = lead.Phone,
        Status = lead.Status,
        Priority = lead.Priority,
        ExpectedValue = lead.ExpectedValue,
        AssignedToId = lead.AssignedToId,
        CreatedDate = lead.CreatedDate
    };

    private static OpportunityDto ToDto(Opportunity opportunity, string? customerName = null) => new()
    {
        Id = opportunity.Id,
        OpportunityName = opportunity.OpportunityName,
        CustomerId = opportunity.CustomerId,
        CustomerName = customerName ?? opportunity.Customer?.CustomerName,
        Stage = opportunity.Stage,
        Amount = opportunity.Amount,
        Probability = opportunity.Probability,
        ExpectedCloseDate = opportunity.ExpectedCloseDate,
        Status = opportunity.Status,
        OwnerId = opportunity.OwnerId,
        CreatedDate = opportunity.CreatedDate
    };
}
