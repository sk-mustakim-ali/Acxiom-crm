using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class AuditLogController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuditLogController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? entityName,
        [FromQuery(Name = "action")] string? actionFilter,
        string? actorUserId,
        DateTime? from,
        DateTime? to)
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

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(log => log.EntityName == entityName);
        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(log => log.Action == actionFilter);
        if (from.HasValue)
            query = query.Where(log => log.CreatedDate >= from.Value.Date);
        if (to.HasValue)
        {
            var dayAfterTo = to.Value.Date.AddDays(1);
            query = query.Where(log => log.CreatedDate < dayAfterTo);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(log =>
                log.EntityName.Contains(term)
                || log.Action.Contains(term)
                || (log.RecordId != null && log.RecordId.Contains(term))
                || (isAdmin && ((log.OldValue != null && log.OldValue.Contains(term))
                    || (log.NewValue != null && log.NewValue.Contains(term)))));
        }

        var logs = await query.OrderByDescending(log => log.CreatedDate).Take(500).ToListAsync();
        var userIds = logs.Where(log => log.UserId != null).Select(log => log.UserId!).Distinct().ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName, user.Email })
            .ToDictionaryAsync(user => user.Id, user => string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName);

        ViewBag.Search = search;
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
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.IsAdmin = isAdmin;
        return View(logs.Select(log => new AuditLogRowViewModel
        {
            Id = log.Id,
            UserName = log.UserId != null && users.TryGetValue(log.UserId, out var name) ? name : null,
            Action = log.Action,
            EntityName = log.EntityName,
            RecordId = log.RecordId,
            CreatedDate = log.CreatedDate,
            OldValue = isAdmin ? log.OldValue : null,
            NewValue = isAdmin ? log.NewValue : null,
            IpAddress = isAdmin ? log.IpAddress : null
        }).ToList());
    }
}
