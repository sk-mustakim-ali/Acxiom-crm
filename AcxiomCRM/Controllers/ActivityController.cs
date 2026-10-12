using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class ActivityController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _scope;

    public ActivityController(ApplicationDbContext context, CrmAccessScope scope)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<IActionResult> Index(string? search, string? type, DateTime? from, DateTime? to, string? status, string? assignee)
    {
        IQueryable<Activity> query = _scope.Activities().AsNoTracking().Include(activity => activity.Customer).Include(activity => activity.Lead);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(activity => activity.Subject.Contains(term)
                || (activity.Description != null && activity.Description.Contains(term))
                || (activity.Customer != null && activity.Customer.CustomerName.Contains(term))
                || (activity.Lead != null && activity.Lead.LeadName.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(activity => activity.ActivityType == type);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(activity => activity.Status == status);
        if (from.HasValue)
            query = query.Where(activity => activity.ActivityDate >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(activity => activity.ActivityDate < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(assignee))
            query = query.Where(activity => activity.AssignedToId == assignee);

        var activities = await query.OrderByDescending(activity => activity.ActivityDate).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.Assignee = assignee;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Users = await UserOptionsAsync();
        var assignedIds = activities.Where(activity => activity.AssignedToId != null)
            .Select(activity => activity.AssignedToId!).Distinct().ToList();
        ViewBag.AssigneeNames = await _context.Users.AsNoTracking()
            .Where(user => assignedIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName);
        return View(activities);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateChoicesAsync();
        return View(new Activity { ActivityDate = DateTime.Now });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        await PopulateChoicesAsync();
        if (!ModelState.IsValid || !await ValidateLinksAsync(activity) || !ValidateWorkflow(activity))
            return View(activity);

        activity.Id = 0;
        activity.AssignedToId = CanManageAssignments && !string.IsNullOrWhiteSpace(activity.AssignedToId)
            ? activity.AssignedToId
            : User.FindFirstValue(ClaimTypes.NameIdentifier);
        activity.Customer = null;
        activity.Lead = null;
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Create", activity.Id, null, activity.Subject);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var activity = await _scope.Activities().AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (activity == null)
            return NotFound();
        await PopulateChoicesAsync();
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Activity activity)
    {
        if (id != activity.Id)
            return BadRequest();

        var existing = await _scope.Activities().FirstOrDefaultAsync(item => item.Id == id);
        if (existing == null)
            return NotFound();
        await PopulateChoicesAsync();
        if (!ModelState.IsValid || !await ValidateLinksAsync(activity) || !ValidateWorkflow(activity))
            return View(activity);

        if (!CanTransitionActivity(existing.Status, activity.Status))
        {
            ModelState.AddModelError(nameof(Activity.Status), $"Activity status cannot transition from {existing.Status} to {activity.Status}.");
            return View(activity);
        }

        var oldSubject = existing.Subject;
        existing.ActivityType = activity.ActivityType;
        existing.Subject = activity.Subject;
        existing.Description = activity.Description;
        existing.ActivityDate = activity.ActivityDate;
        existing.CustomerId = activity.CustomerId;
        existing.LeadId = activity.LeadId;
        existing.Status = activity.Status;
        if (CanManageAssignments)
            existing.AssignedToId = activity.AssignedToId;
        existing.Customer = null;
        existing.Lead = null;
        await _context.SaveChangesAsync();
        await LogAuditAsync("Update", id, oldSubject, existing.Subject);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _scope.Activities().FirstOrDefaultAsync(item => item.Id == id);
        if (activity == null)
            return NotFound();
        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Delete", id, activity.Subject, null);
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ValidateLinksAsync(Activity activity)
    {
        if (activity.CustomerId.HasValue == activity.LeadId.HasValue)
            ModelState.AddModelError(string.Empty, "Select exactly one customer or lead for this activity.");
        if (activity.CustomerId.HasValue && !await _scope.CanAccessCustomerAsync(activity.CustomerId.Value))
            ModelState.AddModelError(nameof(Activity.CustomerId), "The selected customer is unavailable.");
        if (activity.LeadId.HasValue && !await _scope.CanAccessLeadAsync(activity.LeadId.Value))
            ModelState.AddModelError(nameof(Activity.LeadId), "The selected lead is unavailable.");
        if (CanManageAssignments && !string.IsNullOrWhiteSpace(activity.AssignedToId)
            && !await _scope.IsActiveUserAsync(activity.AssignedToId))
            ModelState.AddModelError(nameof(Activity.AssignedToId), "The selected assignee is unavailable.");
        return ModelState.IsValid;
    }

    private bool ValidateWorkflow(Activity activity)
    {
        if (!CrmWorkflowRules.IsValidActivityType(activity.ActivityType))
            ModelState.AddModelError(nameof(Activity.ActivityType), "Select a valid activity type.");
        else
            activity.ActivityType = CrmWorkflowRules.FollowUpTypes.First(type =>
                type.Equals(activity.ActivityType, StringComparison.OrdinalIgnoreCase));
        if (!CrmWorkflowRules.IsValidActivityStatus(activity.Status))
            ModelState.AddModelError(nameof(Activity.Status), "Select a valid activity status.");
        else
            activity.Status = CrmWorkflowRules.ActivityStatuses.First(status =>
                status.Equals(activity.Status, StringComparison.OrdinalIgnoreCase));
        return ModelState.IsValid;
    }

    private async Task PopulateChoicesAsync()
    {
        ViewBag.Customers = await _scope.Customers().AsNoTracking().OrderBy(customer => customer.CustomerName).ToListAsync();
        ViewBag.Leads = await _scope.Leads().AsNoTracking().OrderBy(lead => lead.LeadName).ToListAsync();
        ViewBag.Users = CanManageAssignments ? await UserOptionsAsync() : new List<AssigneeOptionViewModel>();
    }

    private async Task<List<AssigneeOptionViewModel>> UserOptionsAsync()
    {
        var users = await _scope.GetActiveUsersAsync();
        return users.Select(user => new AssigneeOptionViewModel { Id = user.Id, Name = user.FullName }).ToList();
    }

    private static bool CanTransitionActivity(string current, string next)
    {
        if (current.Equals(next, StringComparison.OrdinalIgnoreCase))
            return true;
        return current.Equals("Planned", StringComparison.OrdinalIgnoreCase)
            ? next is "In Progress" or "Completed" or "Cancelled"
            : current.Equals("In Progress", StringComparison.OrdinalIgnoreCase)
                && next is "Completed" or "Cancelled";
    }

    private bool CanManageAssignments => User.IsInRole("Admin") || User.IsInRole("Manager");

    private async Task LogAuditAsync(string action, int id, string? oldValue, string? newValue)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action,
            EntityName = "Activity",
            RecordId = id.ToString(),
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _context.SaveChangesAsync();
    }
}

public class AssigneeOptionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
