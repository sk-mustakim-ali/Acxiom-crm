using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _accessScope;

    public CustomerController(ApplicationDbContext context, CrmAccessScope accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    public async Task<IActionResult> Index(string? search, string? ownerId)
    {
        var query = _accessScope.Customers();
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            query = query.Where(customer => customer.OwnerId == ownerId);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.CustomerName.Contains(term) || c.Email.Contains(term) || c.Phone.Contains(term) || c.CompanyName.Contains(term));
        }

        var customers = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        ViewBag.OwnerId = ownerId;
        var ownerIds = customers.Where(customer => customer.OwnerId != null).Select(customer => customer.OwnerId!).Distinct().ToList();
        ViewBag.OwnerNames = await _context.Users
            .Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName);
        if (CanManageAssignments)
        {
            ViewBag.AssignableOwners = await _accessScope.GetActiveSalesExecutivesAsync();
        }
        return View(customers);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAssignableOwnersAsync();
        return View(new Customer { OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier) });
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _accessScope.Customers()
            .Include(item => item.Opportunities)
            .Include(item => item.FollowUps)
            .Include(item => item.Activities)
            .FirstOrDefaultAsync(item => item.Id == id);
        return customer == null ? NotFound() : View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        await PopulateAssignableOwnersAsync();
        customer.CustomerCode = "CUS-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        ModelState.Remove(nameof(Customer.CustomerCode));

        customer.OwnerId = CanManageAssignments
            ? customer.OwnerId
            : User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        customer.CustomerName = customer.CustomerName.Trim();
        customer.Email = customer.Email.Trim();
        customer.Phone = customer.Phone.Trim();
        customer.NormalizedEmail = CrmContactNormalization.NormalizeEmail(customer.Email);
        customer.NormalizedPhone = CrmContactNormalization.NormalizePhone(customer.Phone);

        if (!await _accessScope.IsActiveSalesExecutiveAsync(customer.OwnerId))
        {
            ModelState.AddModelError(nameof(Customer.OwnerId), "Assign the customer to an active Sales Executive.");
            return View(customer);
        }

        if (!CrmWorkflowRules.CustomerStatuses.Contains(customer.Status, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Customer.Status), "Select a valid customer status.");
            return View(customer);
        }

        if (await _context.Customers.AnyAsync(c => c.NormalizedEmail == customer.NormalizedEmail))
        {
            ModelState.AddModelError(nameof(Customer.Email), "Email is already in use.");
            return View(customer);
        }

        if (await _context.Customers.AnyAsync(c => c.NormalizedPhone == customer.NormalizedPhone))
        {
            ModelState.AddModelError(nameof(Customer.Phone), "Phone is already in use.");
            return View(customer);
        }

        customer.CompanyName = customer.CompanyName.Trim();
        customer.Status = CrmWorkflowRules.CustomerStatuses.First(status => status.Equals(customer.Status, StringComparison.OrdinalIgnoreCase));
        customer.CreatedBy = User.Identity?.Name;
        customer.CreatedDate = DateTime.UtcNow;

        await using var createTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            await LogAuditAsync("Create", "Customer", customer.Id, null, $"Name={customer.CustomerName}; Status={customer.Status}; Owner={customer.OwnerId}");
            await createTransaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (CrmContactNormalization.IsUniqueConstraintViolation(exception))
        {
            await createTransaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "A customer with this email or phone already exists.");
            return View(customer);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _accessScope.Customers().FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null)
        {
            return NotFound();
        }

        await PopulateAssignableOwnersAsync();
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.Id)
        {
            return BadRequest();
        }

        await PopulateAssignableOwnersAsync();
        ModelState.Remove(nameof(Customer.CustomerCode));
        var existing = await _accessScope.Customers().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        customer.OwnerId = CanManageAssignments ? customer.OwnerId : existing.OwnerId;
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        customer.CustomerName = customer.CustomerName.Trim();
        customer.Email = customer.Email.Trim();
        customer.Phone = customer.Phone.Trim();
        customer.NormalizedEmail = CrmContactNormalization.NormalizeEmail(customer.Email);
        customer.NormalizedPhone = CrmContactNormalization.NormalizePhone(customer.Phone);

        if (!CrmWorkflowRules.CustomerStatuses.Contains(customer.Status, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Customer.Status), "Select a valid customer status.");
            return View(customer);
        }

        if (customer.OwnerId != existing.OwnerId
            && !await _accessScope.IsActiveSalesExecutiveAsync(customer.OwnerId))
        {
            ModelState.AddModelError(nameof(Customer.OwnerId), "Assign the customer to an active Sales Executive.");
            return View(customer);
        }

        if (await _context.Customers.AnyAsync(c => c.Id != id && c.NormalizedEmail == customer.NormalizedEmail))
        {
            ModelState.AddModelError(nameof(Customer.Email), "Email is already in use.");
            return View(customer);
        }

        if (await _context.Customers.AnyAsync(c => c.Id != id && c.NormalizedPhone == customer.NormalizedPhone))
        {
            ModelState.AddModelError(nameof(Customer.Phone), "Phone is already in use.");
            return View(customer);
        }

        customer.CustomerCode = existing.CustomerCode;
        customer.CreatedBy = existing.CreatedBy;
        customer.CreatedDate = existing.CreatedDate;
        customer.CompanyName = customer.CompanyName.Trim();
        customer.ModifiedDate = DateTime.UtcNow;
        customer.Status = CrmWorkflowRules.CustomerStatuses.First(status => status.Equals(customer.Status, StringComparison.OrdinalIgnoreCase));

        await using var editTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Update(customer);
            await _context.SaveChangesAsync();
            await LogAuditAsync("Update", "Customer", customer.Id,
                $"Name={existing.CustomerName}; Email={existing.Email}; Phone={existing.Phone}; Status={existing.Status}; Owner={existing.OwnerId}",
                $"Name={customer.CustomerName}; Email={customer.Email}; Phone={customer.Phone}; Status={customer.Status}; Owner={customer.OwnerId}");
            await editTransaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (CrmContactNormalization.IsUniqueConstraintViolation(exception))
        {
            await editTransaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "A customer with this email or phone already exists.");
            return View(customer);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _accessScope.Customers().FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null)
        {
            return NotFound();
        }

        var oldStatus = customer.Status;
        customer.Status = "Inactive";
        customer.ModifiedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await LogAuditAsync("Deactivate", "Customer", id, oldStatus, customer.Status);
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
            : [];
    }
}
