using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class FollowUpController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _accessScope;

    public FollowUpController(ApplicationDbContext context, CrmAccessScope accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    public async Task<IActionResult> Index(string? status, DateTime? date, string? search, string? assignedTo)
    {
        var query = _accessScope.FollowUps()
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (date.HasValue)
        {
            query = query.Where(f => f.FollowUpDate.Date == date.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            query = query.Where(followUp => followUp.AssignedToId == assignedTo);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(f => f.Subject.Contains(term)
                || (f.Customer != null && f.Customer.CustomerName.Contains(term))
                || (f.Lead != null && f.Lead.LeadName.Contains(term))
                || (f.Opportunity != null && f.Opportunity.OpportunityName.Contains(term)));
        }

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        var userIds = followUps.Where(followUp => followUp.AssignedToId != null)
            .Select(followUp => followUp.AssignedToId!).Distinct().ToList();
        ViewBag.AssigneeNames = await _context.Users
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName);
        ViewBag.AssignedTo = assignedTo;
        if (CanManageAssignments)
        {
            ViewBag.Assignees = await _accessScope.GetActiveUsersAsync();
        }
        return View(followUps);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateRelatedRecordsAsync();
        return View(new FollowUp { AssignedToId = User.FindFirstValue(ClaimTypes.NameIdentifier) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        await PopulateRelatedRecordsAsync();
        if (!ModelState.IsValid)
        {
            return View(followUp);
        }

        if (!CrmWorkflowRules.IsValidFollowUpStatus(followUp.Status)
            || !CrmWorkflowRules.IsValidFollowUpType(followUp.FollowUpType))
        {
            ModelState.AddModelError(string.Empty, "Select a valid follow-up type and status.");
            return View(followUp);
        }

        if (CrmWorkflowRules.IsValidFollowUpStatus(followUp.Status)
            && followUp.Status.Equals("Planned", StringComparison.OrdinalIgnoreCase)
            && followUp.FollowUpDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(FollowUp.FollowUpDate), "Follow-up date cannot be earlier than today.");
            return View(followUp);
        }

        followUp.Status = CrmWorkflowRules.FollowUpStatuses.First(status =>
            status.Equals(followUp.Status, StringComparison.OrdinalIgnoreCase));
        followUp.FollowUpType = CrmWorkflowRules.FollowUpTypes.First(type =>
            type.Equals(followUp.FollowUpType, StringComparison.OrdinalIgnoreCase));

        var relatedCount = (followUp.CustomerId.HasValue ? 1 : 0)
            + (followUp.LeadId.HasValue ? 1 : 0)
            + (followUp.OpportunityId.HasValue ? 1 : 0);
        if (relatedCount != 1
            || (followUp.CustomerId.HasValue && !await _accessScope.CanAccessCustomerAsync(followUp.CustomerId.Value))
            || (followUp.LeadId.HasValue && !await _accessScope.CanAccessLeadAsync(followUp.LeadId.Value))
            || (followUp.OpportunityId.HasValue && !await _accessScope.CanAccessOpportunityAsync(followUp.OpportunityId.Value)))
        {
            ModelState.AddModelError(string.Empty, "Select exactly one related customer, lead, or opportunity you are authorized to manage.");
            return View(followUp);
        }

        followUp.AssignedToId = CanManageAssignments
            ? followUp.AssignedToId
            : User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await _accessScope.IsActiveUserAsync(followUp.AssignedToId))
        {
            ModelState.AddModelError(nameof(FollowUp.AssignedToId), "Assign the follow-up to an active user.");
            return View(followUp);
        }

        followUp.CreatedDate = DateTime.UtcNow;

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Create", "FollowUp", followUp.Id, null, followUp.Subject);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var followUp = await _accessScope.FollowUps().FirstOrDefaultAsync(f => f.Id == id);
        if (followUp == null)
        {
            return NotFound();
        }

        await PopulateRelatedRecordsAsync();
        return View(followUp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUp followUp)
    {
        if (id != followUp.Id)
        {
            return BadRequest();
        }

        var existing = await _accessScope.FollowUps().FirstOrDefaultAsync(f => f.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        await PopulateRelatedRecordsAsync();
        if (!ModelState.IsValid)
        {
            return View(followUp);
        }

        if (!CrmWorkflowRules.IsValidFollowUpStatus(followUp.Status)
            || !CrmWorkflowRules.IsValidFollowUpType(followUp.FollowUpType))
        {
            ModelState.AddModelError(string.Empty, "Select a valid follow-up type and status.");
            return View(followUp);
        }

        if (followUp.Status.Equals("Planned", StringComparison.OrdinalIgnoreCase)
            && followUp.FollowUpDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(FollowUp.FollowUpDate), "Follow-up date cannot be earlier than today.");
            return View(followUp);
        }

        if (!CanTransitionFollowUp(existing.Status, followUp.Status))
        {
            ModelState.AddModelError(nameof(FollowUp.Status), $"Follow-up status cannot transition from {existing.Status} to {followUp.Status}.");
            return View(followUp);
        }

        if (!CanManageAssignments)
        {
            followUp.AssignedToId = existing.AssignedToId;
        }

        if (followUp.AssignedToId != existing.AssignedToId
            && !await _accessScope.IsActiveUserAsync(followUp.AssignedToId))
        {
            ModelState.AddModelError(nameof(FollowUp.AssignedToId), "Assign the follow-up to an active user.");
            return View(followUp);
        }

        followUp.Status = CrmWorkflowRules.FollowUpStatuses.First(status =>
            status.Equals(followUp.Status, StringComparison.OrdinalIgnoreCase));
        followUp.FollowUpType = CrmWorkflowRules.FollowUpTypes.First(type =>
            type.Equals(followUp.FollowUpType, StringComparison.OrdinalIgnoreCase));

        var relatedCount = (followUp.CustomerId.HasValue ? 1 : 0)
            + (followUp.LeadId.HasValue ? 1 : 0)
            + (followUp.OpportunityId.HasValue ? 1 : 0);
        if (relatedCount != 1
            || (followUp.CustomerId.HasValue && !await _accessScope.CanAccessCustomerAsync(followUp.CustomerId.Value))
            || (followUp.LeadId.HasValue && !await _accessScope.CanAccessLeadAsync(followUp.LeadId.Value))
            || (followUp.OpportunityId.HasValue && !await _accessScope.CanAccessOpportunityAsync(followUp.OpportunityId.Value)))
        {
            ModelState.AddModelError(string.Empty, "Select exactly one related customer, lead, or opportunity you are authorized to manage.");
            return View(followUp);
        }

        var previousDate = existing.FollowUpDate;
        var previousStatus = existing.Status;
        var previousSubject = existing.Subject;
        existing.CustomerId = followUp.CustomerId;
        existing.LeadId = followUp.LeadId;
        existing.OpportunityId = followUp.OpportunityId;
        existing.FollowUpDate = followUp.FollowUpDate;
        existing.Subject = followUp.Subject.Trim();
        existing.FollowUpType = followUp.FollowUpType;
        existing.Status = followUp.Status;
        existing.Notes = followUp.Notes;
        existing.Remarks = followUp.Remarks;
        existing.AssignedToId = followUp.AssignedToId;
        if (previousDate.Date != existing.FollowUpDate.Date)
        {
            await LogAuditAsync("Reschedule", "FollowUp", existing.Id, previousDate.ToString("yyyy-MM-dd"), existing.FollowUpDate.ToString("yyyy-MM-dd"));
        }

        await _context.SaveChangesAsync();
        await LogAuditAsync(existing.Status == "Completed" ? "Complete" : "Update", "FollowUp", existing.Id,
            $"Subject={previousSubject}; Status={previousStatus}",
            $"Subject={existing.Subject}; Status={existing.Status}");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await _accessScope.FollowUps().FirstOrDefaultAsync(f => f.Id == id);
        if (followUp == null)
        {
            return NotFound();
        }

        _context.FollowUps.Remove(followUp);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Delete", "FollowUp", id, followUp.Subject, null);
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateRelatedRecordsAsync()
    {
        ViewBag.Customers = await _accessScope.Customers().OrderBy(c => c.CustomerName).ToListAsync();
        ViewBag.Leads = await _accessScope.Leads().OrderBy(l => l.LeadName).ToListAsync();
        ViewBag.Opportunities = await _accessScope.Opportunities().OrderBy(o => o.OpportunityName).ToListAsync();
        ViewBag.FollowUpTypes = CrmWorkflowRules.FollowUpTypes;
        ViewBag.FollowUpStatuses = CrmWorkflowRules.FollowUpStatuses;
        ViewBag.Assignees = CanManageAssignments
            ? await _accessScope.GetActiveUsersAsync()
            : new List<ApplicationUser>();
    }

    private bool CanManageAssignments => User.IsInRole("Admin") || User.IsInRole("Manager");

    private static bool CanTransitionFollowUp(string current, string next)
    {
        if (current.Equals(next, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return current.Equals("Planned", StringComparison.OrdinalIgnoreCase)
            && CrmWorkflowRules.IsValidFollowUpStatus(next);
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
