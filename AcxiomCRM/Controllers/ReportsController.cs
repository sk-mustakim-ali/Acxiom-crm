using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _scope;

    public ReportsController(ApplicationDbContext context, CrmAccessScope scope)
    {
        _context = context;
        _scope = scope;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Customers()
    {
        var customers = await _scope.Customers().AsNoTracking().OrderByDescending(c => c.CreatedDate)
            .Select(c => new { c.CustomerName, c.CompanyName, c.Status, c.CreatedDate, c.OwnerId }).ToListAsync();
        return View("Report", BuildReportAsync("Customer Report", "Customer records in your permitted business scope.",
            new[] { "Customer", "Company", "Status", "Created", "Owner" },
            customers.Select(c => new[] { c.CustomerName, c.CompanyName, c.Status, c.CreatedDate.ToString("yyyy-MM-dd"), c.OwnerId ?? "Unassigned" }).ToList(),
            ("Customers", customers.Count.ToString())));
    }

    public async Task<IActionResult> Leads()
    {
        var leads = await _scope.Leads().AsNoTracking().OrderByDescending(l => l.CreatedDate)
            .Select(l => new { l.LeadName, l.CompanyName, l.Source, l.Status, l.ExpectedValue, l.AssignedToId, l.CreatedDate }).ToListAsync();
        return View("Report", BuildReportAsync("Lead Report", "Lead source, status, conversion, value, and assignment in your permitted business scope.",
            new[] { "Lead", "Company", "Source", "Status", "Conversion", "Expected value", "Assigned to", "Created" },
            leads.Select(l => new[] { l.LeadName, l.CompanyName, l.Source, l.Status,
                l.Status.Equals("Converted", StringComparison.OrdinalIgnoreCase) ? "Converted" : "Not converted",
                l.ExpectedValue.ToString("C"), l.AssignedToId ?? "Unassigned", l.CreatedDate.ToString("yyyy-MM-dd") }).ToList(),
            ("Leads", leads.Count.ToString()),
            ("Expected value", leads.Sum(l => l.ExpectedValue).ToString("C"))));
    }

    public async Task<IActionResult> FollowUps()
    {
        var followUps = await _scope.FollowUps().AsNoTracking().Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Opportunity)
            .OrderBy(f => f.FollowUpDate).ToListAsync();
        var today = DateTime.Today;
        var overdueCount = followUps.Count(followUp => CrmWorkflowRules.IsOverdueFollowUp(followUp, today));
        return View("Report", BuildReportAsync("Follow-up Report", "Scheduled follow-ups, planned state, and assignment in your permitted business scope.",
            new[] { "Subject", "Date", "Type", "Status", "Due state", "Customer", "Lead", "Opportunity", "Assigned to" },
            followUps.Select(f => new[] { f.Subject, f.FollowUpDate.ToString("yyyy-MM-dd"), f.FollowUpType, f.Status,
                CrmWorkflowRules.IsOverdueFollowUp(f, today) ? "Overdue" : f.Status == "Planned" ? "Upcoming" : "-",
                f.Customer?.CustomerName ?? "-", f.Lead?.LeadName ?? "-", f.Opportunity?.OpportunityName ?? "-", f.AssignedToId ?? "Unassigned" }).ToList(),
            ("Follow-ups", followUps.Count.ToString()),
            ("Overdue planned", overdueCount.ToString()),
            ("Completed", followUps.Count(followUp => followUp.Status == "Completed").ToString())));
    }

    public async Task<IActionResult> Opportunities()
    {
        var opportunities = await _scope.Opportunities().AsNoTracking().Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedDate).ToListAsync();
        return View("Report", BuildReportAsync("Opportunity Report", "Opportunity stage, amount, and expected close date in your permitted business scope.",
            new[] { "Opportunity", "Customer", "Source", "Stage", "Status", "Amount", "Probability", "Expected close" },
            opportunities.Select(o => new[] { o.OpportunityName, o.Customer?.CustomerName ?? "-", o.Source, o.Stage, o.Status,
                o.Amount.ToString("C"), o.Probability + "%", o.ExpectedCloseDate.ToString("yyyy-MM-dd") }).ToList(),
            ("Opportunities", opportunities.Count.ToString()),
            ("Total amount", opportunities.Sum(o => o.Amount).ToString("C"))));
    }

    public async Task<IActionResult> Pipeline()
    {
        var opportunities = await _scope.Opportunities().AsNoTracking()
            .Select(item => new { item.Stage, item.Status, item.Amount, item.Probability, item.OwnerId })
            .ToListAsync();
        var groups = CrmWorkflowRules.OpportunityStages.Select(stage =>
        {
            var stageItems = opportunities.Where(item => item.Stage.Equals(stage, StringComparison.OrdinalIgnoreCase)).ToList();
            var activeItems = stageItems.Where(item => item.Status.Equals("Open", StringComparison.OrdinalIgnoreCase)).ToList();
            return new
            {
                Stage = stage,
                Count = stageItems.Count,
                Amount = activeItems.Sum(item => item.Amount),
                WeightedAmount = activeItems.Sum(item => item.Amount * item.Probability / 100m)
            };
        }).ToList();
        var rows = groups.Select(group => new[] { group.Stage, group.Count.ToString(), group.Amount.ToString("C"), group.WeightedAmount.ToString("C") }).ToList();
        var openItems = opportunities.Where(item => item.Status == "Open").ToList();
        var ownerIds = openItems.Where(item => item.OwnerId != null).Select(item => item.OwnerId!).Distinct().ToList();
        var ownerNames = await _context.Users.AsNoTracking().Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName);
        var ownerGroups = openItems.GroupBy(item => item.OwnerId).OrderBy(group => group.Key);
        foreach (var owner in ownerGroups)
        {
            var ownerName = owner.Key != null && ownerNames.TryGetValue(owner.Key, out var fullName)
                ? fullName
                : "Unassigned";
            rows.Add(new[] { $"Owner: {ownerName}", owner.Count().ToString(), owner.Sum(item => item.Amount).ToString("C"),
                owner.Sum(item => item.Amount * item.Probability / 100m).ToString("C") });
        }
        return View("Report", BuildReportAsync("Pipeline Report", "Open opportunity amount and weighted value grouped by stage and owner.",
            new[] { "Stage", "Count", "Amount", "Weighted amount" }, rows,
            ("Open pipeline", openItems.Sum(item => item.Amount).ToString("C")),
            ("Weighted pipeline", openItems.Sum(item => item.Amount * item.Probability / 100m).ToString("C"))));
    }

    public async Task<IActionResult> Conversion()
    {
        var leads = _scope.Leads().AsNoTracking();
        var total = await leads.CountAsync();
        var converted = await leads.CountAsync(lead => lead.Status == "Converted");
        var groups = await leads.GroupBy(lead => lead.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(group => group.Status).ToListAsync();
        var rows = groups.Select(group => new[] { group.Status, group.Count.ToString(),
            (total == 0 ? 0m : group.Count * 100m / total).ToString("0.0") + "%" }).ToList();
        return View("Report", BuildReportAsync("Conversion Report", "Lead distribution by status and leads converted into customer records.",
            new[] { "Status", "Leads", "Share" }, rows,
            ("Total leads", total.ToString()),
            ("Converted leads", converted.ToString()),
            ("Conversion rate", (total == 0 ? 0m : converted * 100m / total).ToString("0.0") + "%")));
    }

    public async Task<IActionResult> UserActivity(DateTime? from, DateTime? to, string? assignee)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userQuery = _context.Users.AsNoTracking();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            userQuery = userQuery.Where(user => user.Id == currentUserId);
        if (!string.IsNullOrWhiteSpace(assignee) && (User.IsInRole("Admin") || User.IsInRole("Manager")))
            userQuery = userQuery.Where(user => user.Id == assignee);

        var users = await userQuery.Select(user => new { user.Id, user.FullName }).ToListAsync();
        ViewBag.Users = users.Select(user => new AssigneeOptionViewModel { Id = user.Id, Name = user.FullName }).ToList();
        var activityQuery = _scope.Activities().AsNoTracking();
        var followUpQuery = _scope.FollowUps().AsNoTracking();
        if (from.HasValue)
        {
            activityQuery = activityQuery.Where(item => item.ActivityDate >= from.Value.Date);
            followUpQuery = followUpQuery.Where(item => item.FollowUpDate >= from.Value.Date);
        }
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            activityQuery = activityQuery.Where(item => item.ActivityDate < end);
            followUpQuery = followUpQuery.Where(item => item.FollowUpDate < end);
        }

        var rows = new List<string[]>();
        foreach (var user in users)
        {
            var userActivities = activityQuery.Where(item => item.AssignedToId == user.Id);
            var userFollowUps = followUpQuery.Where(item => item.AssignedToId == user.Id);
            rows.Add(new[] { string.IsNullOrWhiteSpace(user.FullName) ? "User" : user.FullName,
                (await userActivities.CountAsync()).ToString(),
                (await userFollowUps.CountAsync()).ToString(),
                (await userActivities.CountAsync(item => item.Status == "Completed")).ToString() });
        }
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Assignee = assignee;
        return View("Report", new ReportPageViewModel
        {
            Title = "User Activity Report",
            Description = "Assigned CRM activities and follow-ups in the permitted business scope.",
            Columns = new[] { "User", "Activities", "Follow-ups", "Completed activities" },
            Rows = rows.Cast<IReadOnlyList<string>>().ToList()
        });
    }

    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Audit(
        DateTime? from,
        DateTime? to,
        string? entityName,
        [FromQuery(Name = "action")] string? actionFilter,
        string? actorUserId)
    {
        var isAdmin = User.IsInRole("Admin");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _context.AuditLogs.AsNoTracking();
        if (!isAdmin)
        {
            var businessEntities = new[] { "Customer", "Lead", "FollowUp", "Opportunity", "Activity" };
            query = query.Where(log => log.UserId == userId && businessEntities.Contains(log.EntityName));
        }
        else if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            query = query.Where(log => log.UserId == actorUserId);
        }
        if (from.HasValue)
            query = query.Where(log => log.CreatedDate >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(log => log.CreatedDate < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(log => log.EntityName == entityName);
        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(log => log.Action == actionFilter);

        var events = await query.OrderByDescending(log => log.CreatedDate).Take(500)
            .Select(log => new { log.CreatedDate, log.EntityName, log.Action, log.RecordId, log.UserId,
                OldValue = isAdmin ? log.OldValue : null, NewValue = isAdmin ? log.NewValue : null })
            .ToListAsync();
        var userIds = events.Where(item => item.UserId != null).Select(item => item.UserId!).Distinct().ToList();
        var userNames = await _context.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName }).ToDictionaryAsync(user => user.Id, user => user.FullName);
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.EntityName = entityName;
        ViewBag.ActionFilter = actionFilter;
        ViewBag.UserId = actorUserId;
        ViewBag.Users = isAdmin
            ? await _context.Users.AsNoTracking().OrderBy(user => user.FullName)
                .Select(user => new AssigneeOptionViewModel
                {
                    Id = user.Id,
                    Name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "User" : user.FullName
                }).ToListAsync()
            : new List<AssigneeOptionViewModel>();
        return View("Report", new ReportPageViewModel
        {
            Title = "Audit Report",
            Description = isAdmin ? "Recent audit events (maximum 500)." : "Your own business-activity events only; sensitive values and network details are hidden.",
            Columns = isAdmin
                ? new[] { "Date", "User", "Entity", "Action", "Record", "Old value", "New value" }
                : new[] { "Date", "Entity", "Action", "Record" },
            Rows = events.Select(item => isAdmin
                ? new[] { item.CreatedDate.ToString("yyyy-MM-dd HH:mm"), item.UserId != null && userNames.TryGetValue(item.UserId, out var name) ? name : "Unknown",
                    item.EntityName, item.Action, item.RecordId ?? "-", item.OldValue ?? "-", item.NewValue ?? "-" }
                : new[] { item.CreatedDate.ToString("yyyy-MM-dd HH:mm"), item.EntityName, item.Action, item.RecordId ?? "-" })
                .Cast<IReadOnlyList<string>>().ToList()
        });
    }

    private static ReportPageViewModel BuildReportAsync(string title, string description, IReadOnlyList<string> columns,
        IReadOnlyList<string[]> rows, params (string Label, string Value)[] metrics) =>
        new()
        {
            Title = title,
            Description = description,
            Columns = columns,
            Rows = rows.Cast<IReadOnlyList<string>>().ToList(),
            Metrics = metrics.Select(metric => new ReportMetricViewModel { Label = metric.Label, Value = metric.Value }).ToList()
        };
}
