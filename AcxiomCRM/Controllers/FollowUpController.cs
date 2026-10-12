using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class FollowUpController : Controller
{
    private readonly ApplicationDbContext _context;

    public FollowUpController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? status, DateTime? date)
    {
        var query = _context.FollowUps.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (date.HasValue)
        {
            query = query.Where(f => f.FollowUpDate.Date == date.Value.Date);
        }

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        return View(followUps);
    }

    public IActionResult Create()
    {
        return View(new FollowUp());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        if (!ModelState.IsValid)
        {
            return View(followUp);
        }

        if (followUp.FollowUpDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(FollowUp.FollowUpDate), "Follow-up date cannot be earlier than today.");
            return View(followUp);
        }

        followUp.AssignedToId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        followUp.CreatedDate = DateTime.UtcNow;

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Create", "FollowUp", followUp.Id, null, followUp.Subject);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null)
        {
            return NotFound();
        }

        _context.FollowUps.Remove(followUp);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Delete", "FollowUp", id, followUp.Subject, null);
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
