using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class LeadController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _accessScope;

    public LeadController(ApplicationDbContext context, CrmAccessScope accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    public async Task<IActionResult> Index(string? status, string? search, string? assignedTo)
    {
        var query = _accessScope.Leads();

        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            query = query.Where(lead => lead.AssignedToId == assignedTo);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l => l.LeadName.Contains(term) || l.CompanyName.Contains(term)
                || (l.Email != null && l.Email.Contains(term)) || (l.Phone != null && l.Phone.Contains(term)));
        }

        var leads = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        var assignedIds = leads.Where(lead => lead.AssignedToId != null).Select(lead => lead.AssignedToId!).Distinct().ToList();
        ViewBag.AssigneeNames = await _context.Users
            .Where(user => assignedIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName);
        ViewBag.AssignedTo = assignedTo;
        if (CanManageAssignments)
        {
            ViewBag.AssignableOwners = await _accessScope.GetActiveSalesExecutivesAsync();
        }
        return View(leads);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAssignableOwnersAsync();
        return View(new Lead());
    }

    public async Task<IActionResult> Details(int id)
    {
        var lead = await _accessScope.Leads()
            .Include(item => item.Opportunities)
            .Include(item => item.FollowUps)
            .Include(item => item.Activities)
            .FirstOrDefaultAsync(item => item.Id == id);
        return lead == null ? NotFound() : View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        await PopulateAssignableOwnersAsync();
        lead.LeadCode = "LEAD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        ModelState.Remove(nameof(Lead.LeadCode));
        ModelState.Remove(nameof(Lead.Status));
        lead.Status = "New";

        if (!ModelState.IsValid)
        {
            return View(lead);
        }

        if (!CrmWorkflowRules.IsValidLeadStatus(lead.Status))
        {
            ModelState.AddModelError(nameof(Lead.Status), "Select a valid lead status.");
            return View(lead);
        }

        if (lead.ExpectedValue < 0 || lead.ExpectedValue > 1_000_000_000)
        {
            ModelState.AddModelError(nameof(Lead.ExpectedValue), "Expected value must be between 0 and 1,000,000,000.");
            return View(lead);
        }

        if (!CrmWorkflowRules.LeadPriorities.Contains(lead.Priority, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Lead.Priority), "Select a valid priority.");
            return View(lead);
        }

        lead.LeadName = lead.LeadName.Trim();
        lead.CompanyName = lead.CompanyName.Trim();
        lead.Email = string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email.Trim();
        lead.Phone = string.IsNullOrWhiteSpace(lead.Phone) ? null : lead.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(lead.Email) && await _context.Leads.AnyAsync(l => l.Email != null && l.Email.ToLower() == lead.Email.Trim().ToLower()))
        {
            ModelState.AddModelError(nameof(Lead.Email), "Lead email is already in use.");
            return View(lead);
        }

        lead.AssignedToId = CanManageAssignments
            ? lead.AssignedToId
            : User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await _accessScope.IsActiveSalesExecutiveAsync(lead.AssignedToId))
        {
            ModelState.AddModelError(nameof(Lead.AssignedToId), "Assign the lead to an active Sales Executive.");
            return View(lead);
        }

        lead.CreatedDate = DateTime.UtcNow;
        lead.LeadCode = "LEAD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        lead.Notes ??= string.Empty;

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Create", "Lead", lead.Id, null, lead.LeadName);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _accessScope.Leads().FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
        {
            return NotFound();
        }

        await PopulateAssignableOwnersAsync();
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.Id)
        {
            return BadRequest();
        }

        await PopulateAssignableOwnersAsync();
        ModelState.Remove(nameof(Lead.LeadCode));
        var existing = await _accessScope.Leads().AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        lead.AssignedToId = CanManageAssignments ? lead.AssignedToId : existing.AssignedToId;
        if (!ModelState.IsValid)
        {
            return View(lead);
        }

        if (!CrmWorkflowRules.IsValidLeadStatus(lead.Status))
        {
            ModelState.AddModelError(nameof(Lead.Status), "Select a valid lead status.");
            return View(lead);
        }

        if (lead.AssignedToId != existing.AssignedToId
            && !await _accessScope.IsActiveSalesExecutiveAsync(lead.AssignedToId))
        {
            ModelState.AddModelError(nameof(Lead.AssignedToId), "Assign the lead to an active Sales Executive.");
            return View(lead);
        }

        if (!CrmWorkflowRules.CanTransitionLead(existing.Status, lead.Status))
        {
            ModelState.AddModelError(nameof(Lead.Status), $"Lead status cannot transition from {existing.Status} to {lead.Status}.");
            return View(lead);
        }

        if (lead.ExpectedValue < 0 || lead.ExpectedValue > 1_000_000_000)
        {
            ModelState.AddModelError(nameof(Lead.ExpectedValue), "Expected value must be between 0 and 1,000,000,000.");
            return View(lead);
        }

        if (!CrmWorkflowRules.LeadPriorities.Contains(lead.Priority, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Lead.Priority), "Select a valid priority.");
            return View(lead);
        }

        lead.LeadName = lead.LeadName.Trim();
        lead.CompanyName = lead.CompanyName.Trim();
        lead.Email = string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email.Trim();
        lead.Phone = string.IsNullOrWhiteSpace(lead.Phone) ? null : lead.Phone.Trim();
        lead.LeadCode = existing.LeadCode;
        lead.CreatedDate = existing.CreatedDate;

        _context.Update(lead);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Update", "Lead", lead.Id, existing.LeadName, lead.LeadName);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _accessScope.Leads().FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
        {
            return NotFound();
        }

        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Delete", "Lead", id, lead.LeadName, null);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _accessScope.Leads().FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
        {
            return NotFound();
        }

        if (!lead.Status.Equals("Qualified", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only qualified leads can be converted.");
        }

        if (string.IsNullOrWhiteSpace(lead.Email) || string.IsNullOrWhiteSpace(lead.Phone))
        {
            TempData["ErrorMessage"] = "A lead must have a valid email and phone before conversion.";
            return RedirectToAction(nameof(Index));
        }

        var email = lead.Email.Trim();
        var phone = lead.Phone.Trim();
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == email.ToLower() || c.Phone == phone))
        {
            TempData["ErrorMessage"] = "A customer already exists with this lead's email or phone.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var customer = new Customer
        {
            CustomerCode = "CUS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            CustomerName = lead.LeadName,
            Email = email,
            Phone = phone,
            CompanyName = lead.CompanyName,
            Status = "Active",
            OwnerId = lead.AssignedToId,
            CreatedBy = User.Identity?.Name,
            CreatedDate = DateTime.UtcNow
        };
        _context.Customers.Add(customer);
        lead.Status = "Converted";
        await _context.SaveChangesAsync();
        await LogAuditAsync("Convert", "Lead", lead.Id, lead.LeadName, customer.CustomerName);
        await LogAuditAsync("Create", "Customer", customer.Id, null, customer.CustomerName);
        await transaction.CommitAsync();

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
            : new List<AcxiomCRM.Data.ApplicationUser>();
    }
}
