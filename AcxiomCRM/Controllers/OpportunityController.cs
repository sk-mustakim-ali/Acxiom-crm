using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class OpportunityController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _accessScope;

    public OpportunityController(ApplicationDbContext context, CrmAccessScope accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    public async Task<IActionResult> Index(string? stage, string? status, string? source, string? search, string? ownerId)
    {
        var query = _accessScope.Opportunities()
            .Include(o => o.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            query = query.Where(opportunity => opportunity.OwnerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(o => o.Source == source);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.OpportunityName.Contains(term) || o.Customer!.CustomerName.Contains(term));
        }

        var opportunities = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();
        ViewBag.OwnerId = ownerId;
        ViewBag.Source = source;
        ViewBag.Sources = await _accessScope.Opportunities().AsNoTracking()
            .Where(item => item.Source != null && item.Source != "")
            .Select(item => item.Source).Distinct().OrderBy(item => item).ToListAsync();
        if (CanManageAssignments)
        {
            ViewBag.AssignableOwners = await _accessScope.GetActiveSalesExecutivesAsync();
        }
        return View(opportunities);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateChoicesAsync();
        await PopulateAssignableOwnersAsync();
        return View(new Opportunity { OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier) });
    }

    public async Task<IActionResult> Details(int id)
    {
        var opportunity = await _accessScope.Opportunities()
            .Include(item => item.Customer)
            .Include(item => item.Lead)
            .Include(item => item.FollowUps)
            .FirstOrDefaultAsync(item => item.Id == id);
        return opportunity == null ? NotFound() : View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        await PopulateChoicesAsync();
        await PopulateAssignableOwnersAsync();

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

        if (!CrmWorkflowRules.IsValidOpportunityStage(opportunity.Stage)
            || !CrmWorkflowRules.IsValidOpportunityStatus(opportunity.Status)
            || !CrmWorkflowRules.OpportunityStageMatchesStatus(opportunity.Stage, opportunity.Status))
        {
            ModelState.AddModelError(nameof(Opportunity.Stage), "Select a valid pipeline stage and matching status.");
            return View(opportunity);
        }

        if (CrmWorkflowRules.IsActiveOpportunityStatus(opportunity.Status) && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(Opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
            return View(opportunity);
        }

        if (!await _accessScope.CanAccessCustomerAsync(opportunity.CustomerId))
        {
            ModelState.AddModelError(nameof(Opportunity.CustomerId), "Select a customer you are authorized to manage.");
            return View(opportunity);
        }

        if (opportunity.LeadId.HasValue && !await _accessScope.CanAccessLeadAsync(opportunity.LeadId.Value))
        {
            ModelState.AddModelError(nameof(Opportunity.LeadId), "Select a lead you are authorized to manage.");
            return View(opportunity);
        }

        opportunity.OwnerId = CanManageAssignments
            ? opportunity.OwnerId
            : User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await _accessScope.IsActiveSalesExecutiveAsync(opportunity.OwnerId))
        {
            ModelState.AddModelError(nameof(Opportunity.OwnerId), "Assign the opportunity to an active Sales Executive.");
            return View(opportunity);
        }

        opportunity.CreatedDate = DateTime.UtcNow;
        opportunity.Source = opportunity.Source.Trim();
        opportunity.ClosedDate = CrmWorkflowRules.IsActiveOpportunityStatus(opportunity.Status) ? null : DateTime.UtcNow;

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Create", "Opportunity", opportunity.Id, null, opportunity.OpportunityName);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var opportunity = await _accessScope.Opportunities().FirstOrDefaultAsync(o => o.Id == id);
        if (opportunity == null)
        {
            return NotFound();
        }

        await PopulateChoicesAsync();
        await PopulateAssignableOwnersAsync();
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

        await PopulateChoicesAsync();
        await PopulateAssignableOwnersAsync();

        if (!ModelState.IsValid)
        {
            return View(opportunity);
        }

        var existing = await _accessScope.Opportunities().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
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

        if (!CrmWorkflowRules.IsValidOpportunityStage(opportunity.Stage)
            || !CrmWorkflowRules.IsValidOpportunityStatus(opportunity.Status)
            || !CrmWorkflowRules.OpportunityStageMatchesStatus(opportunity.Stage, opportunity.Status))
        {
            ModelState.AddModelError(nameof(Opportunity.Stage), "Select a valid pipeline stage and matching status.");
            return View(opportunity);
        }

        if (!CrmWorkflowRules.CanTransitionOpportunityStage(existing.Stage, opportunity.Stage))
        {
            ModelState.AddModelError(nameof(Opportunity.Stage), $"Opportunity stage cannot transition from {existing.Stage} to {opportunity.Stage}.");
            return View(opportunity);
        }

        if (CrmWorkflowRules.IsActiveOpportunityStatus(opportunity.Status) && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(Opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
            return View(opportunity);
        }

        if (!await _accessScope.CanAccessCustomerAsync(opportunity.CustomerId))
        {
            ModelState.AddModelError(nameof(Opportunity.CustomerId), "Select a customer you are authorized to manage.");
            return View(opportunity);
        }

        if (opportunity.LeadId.HasValue && !await _accessScope.CanAccessLeadAsync(opportunity.LeadId.Value))
        {
            ModelState.AddModelError(nameof(Opportunity.LeadId), "Select a lead you are authorized to manage.");
            return View(opportunity);
        }

        opportunity.OwnerId = CanManageAssignments ? opportunity.OwnerId : existing.OwnerId;
        if (opportunity.OwnerId != existing.OwnerId
            && !await _accessScope.IsActiveSalesExecutiveAsync(opportunity.OwnerId))
        {
            ModelState.AddModelError(nameof(Opportunity.OwnerId), "Assign the opportunity to an active Sales Executive.");
            return View(opportunity);
        }

        opportunity.CreatedDate = existing.CreatedDate;
        opportunity.Source = opportunity.Source.Trim();
        opportunity.ClosedDate = opportunity.Status.Equals("Open", StringComparison.OrdinalIgnoreCase)
            ? null
            : existing.ClosedDate ?? DateTime.UtcNow;

        _context.Update(opportunity);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Update", "Opportunity", opportunity.Id, existing.OpportunityName, opportunity.OpportunityName);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var opportunity = await _accessScope.Opportunities().FirstOrDefaultAsync(o => o.Id == id);
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

    private bool CanManageAssignments => User.IsInRole("Admin") || User.IsInRole("Manager");

    private async Task PopulateAssignableOwnersAsync()
    {
        ViewBag.AssignableOwners = CanManageAssignments
            ? await _accessScope.GetActiveSalesExecutivesAsync()
            : new List<ApplicationUser>();
    }

    private async Task PopulateChoicesAsync()
    {
        ViewBag.Customers = await _accessScope.Customers().OrderBy(customer => customer.CustomerName).ToListAsync();
        ViewBag.Leads = await _accessScope.Leads().OrderBy(lead => lead.LeadName).ToListAsync();
        await PopulateAssignableOwnersAsync();
    }
}
