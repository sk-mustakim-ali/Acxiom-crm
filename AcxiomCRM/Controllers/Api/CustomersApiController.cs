using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CustomersApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers()
    {
        var customers = await _context.Customers
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Status = c.Status,
                OwnerId = c.OwnerId,
                CreatedDate = c.CreatedDate
            })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("customers/{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
    {
        var customer = await _context.Customers
            .Where(c => c.Id == id)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Status = c.Status,
                OwnerId = c.OwnerId,
                CreatedDate = c.CreatedDate
            })
            .SingleOrDefaultAsync();

        if (customer == null)
        {
            return NotFound();
        }

        return Ok(customer);
    }

    [HttpPost("customers")]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(CustomerCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var email = request.Email.Trim();
        var phone = request.Phone.Trim();
        var customerName = request.CustomerName.Trim();
        var companyName = request.CompanyName.Trim();

        if (string.IsNullOrWhiteSpace(customerName))
        {
            return BadRequest(new { message = "Customer name is required." });
        }

        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { message = "Phone is required." });
        }

        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == email.ToLower()))
        {
            return Conflict(new { message = "Customer email already exists." });
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == phone))
        {
            return Conflict(new { message = "Customer phone already exists." });
        }

        var customer = new Customer
        {
            CustomerCode = "CUS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            CustomerName = customerName,
            Email = email,
            Phone = phone,
            CompanyName = companyName,
            Address = request.Address,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status,
            OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedBy = User.Identity?.Name,
            CreatedDate = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var dto = new CustomerDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Status = customer.Status,
            OwnerId = customer.OwnerId,
            CreatedDate = customer.CreatedDate
        };

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, dto);
    }

    [HttpGet("leads")]
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads()
    {
        var leads = await _context.Leads
            .OrderBy(l => l.LeadName)
            .Select(l => new LeadDto
            {
                Id = l.Id,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                CompanyName = l.CompanyName,
                Email = l.Email,
                Phone = l.Phone,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedToId = l.AssignedToId,
                CreatedDate = l.CreatedDate
            })
            .ToListAsync();

        return Ok(leads);
    }

    [HttpPost("leads")]
    public async Task<ActionResult<LeadDto>> CreateLead(Lead lead)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        lead.LeadCode = "LEAD-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        lead.AssignedToId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        lead.CreatedDate = DateTime.UtcNow;

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLeads), new { id = lead.Id }, new LeadDto
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
        });
    }

    [HttpGet("opportunities")]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities()
    {
        var opportunities = await _context.Opportunities
            .Include(o => o.Customer)
            .OrderBy(o => o.OpportunityName)
            .Select(o => new OpportunityDto
            {
                Id = o.Id,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                Stage = o.Stage,
                Amount = o.Amount,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                OwnerId = o.OwnerId,
                CreatedDate = o.CreatedDate
            })
            .ToListAsync();

        return Ok(opportunities);
    }

    [HttpPost("opportunities")]
    public async Task<ActionResult<OpportunityDto>> CreateOpportunity(Opportunity opportunity)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (opportunity.Amount <= 0)
        {
            return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            return BadRequest(new { message = "Probability must be between 0 and 100." });
        }

        opportunity.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        opportunity.CreatedDate = DateTime.UtcNow;

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOpportunities), new { id = opportunity.Id }, new OpportunityDto
        {
            Id = opportunity.Id,
            OpportunityName = opportunity.OpportunityName,
            CustomerId = opportunity.CustomerId,
            Stage = opportunity.Stage,
            Amount = opportunity.Amount,
            Probability = opportunity.Probability,
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            Status = opportunity.Status,
            OwnerId = opportunity.OwnerId,
            CreatedDate = opportunity.CreatedDate
        });
    }
}
