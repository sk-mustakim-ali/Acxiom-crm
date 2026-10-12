using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class OpportunityController : Controller
{
    private readonly ApplicationDbContext _context;

    public OpportunityController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? stage, string? search)
    {
        var query = _context.Opportunities
            .Include(o => o.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.OpportunityName.Contains(term) || o.Customer!.CustomerName.Contains(term));
        }

        var opportunities = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();
        return View(opportunities);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        return View(new Opportunity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        ViewBag.Customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();

        if (!ModelState.IsValid)
        {
            return View(opportunity);
        }

        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError(nameof(Opportunity.Amount), "Opportunity Amount must be greater than 0.");
            return View(opportunity);
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError(nameof(Opportunity.Probability), "Probability must be between 0 and 100.");
            return View(opportunity);
        }

        if (IsActiveOpportunityStatus(opportunity.Status) && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(Opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
            return View(opportunity);
        }

        opportunity.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        opportunity.CreatedDate = DateTime.UtcNow;

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Create", "Opportunity", opportunity.Id, null, opportunity.OpportunityName);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity == null)
        {
            return NotFound();
        }

        ViewBag.Customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        return View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.Id)
        {
            return BadRequest();
        }

        ViewBag.Customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();

        if (!ModelState.IsValid)
        {
            return View(opportunity);
        }

        var existing = await _context.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError(nameof(Opportunity.Amount), "Opportunity Amount must be greater than 0.");
            return View(opportunity);
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError(nameof(Opportunity.Probability), "Probability must be between 0 and 100.");
            return View(opportunity);
        }

        if (IsActiveOpportunityStatus(opportunity.Status) && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(Opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
            return View(opportunity);
        }

        opportunity.OwnerId = existing.OwnerId;
        opportunity.CreatedDate = existing.CreatedDate;

        _context.Update(opportunity);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Update", "Opportunity", opportunity.Id, existing.OpportunityName, opportunity.OpportunityName);

        return RedirectToAction(nameof(Index));
    }

    private static bool IsActiveOpportunityStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Equals("Open", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Qualification", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Proposal", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Negotiation", StringComparison.OrdinalIgnoreCase);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity == null)
        {
            return NotFound();
        }

        _context.Opportunities.Remove(opportunity);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Delete", "Opportunity", id, opportunity.OpportunityName, null);
        return RedirectToAction(nameof(Index));
    }

    private async Task LogAuditAsync(string action, string entityName, int recordId, string? oldValue, string? newValue)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action,
            EntityName = entityName,
            RecordId = recordId.ToString(),
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _context.SaveChangesAsync();
    }
}
