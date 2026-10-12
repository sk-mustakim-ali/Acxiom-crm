using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomerController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.CustomerName.Contains(term) || c.Email.Contains(term) || c.Phone.Contains(term) || c.CompanyName.Contains(term));
        }

        var customers = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        return View(customers);
    }

    public IActionResult Create()
    {
        return View(new Customer());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        customer.CustomerCode = "CUS-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        ModelState.Remove(nameof(Customer.CustomerCode));

        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.Trim().ToLower()))
        {
            ModelState.AddModelError(nameof(Customer.Email), "Email is already in use.");
            return View(customer);
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone.Trim()))
        {
            ModelState.AddModelError(nameof(Customer.Phone), "Phone is already in use.");
            return View(customer);
        }

        customer.Email = customer.Email.Trim();
        customer.Phone = customer.Phone.Trim();
        customer.CompanyName = customer.CompanyName.Trim();
        customer.CustomerName = customer.CustomerName.Trim();
        customer.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        customer.CreatedBy = User.Identity?.Name;
        customer.CreatedDate = DateTime.UtcNow;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Create", "Customer", customer.Id, null, customer.CustomerName);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

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

        ModelState.Remove(nameof(Customer.CustomerCode));
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        var existing = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        if (await _context.Customers.AnyAsync(c => c.Id != id && c.Email.ToLower() == customer.Email.Trim().ToLower()))
        {
            ModelState.AddModelError(nameof(Customer.Email), "Email is already in use.");
            return View(customer);
        }

        if (await _context.Customers.AnyAsync(c => c.Id != id && c.Phone == customer.Phone.Trim()))
        {
            ModelState.AddModelError(nameof(Customer.Phone), "Phone is already in use.");
            return View(customer);
        }

        customer.CustomerCode = existing.CustomerCode;
        customer.OwnerId = existing.OwnerId;
        customer.CreatedBy = existing.CreatedBy;
        customer.CreatedDate = existing.CreatedDate;

        _context.Update(customer);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Update", "Customer", customer.Id, existing.CustomerName, customer.CustomerName);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        await LogAuditAsync("Delete", "Customer", id, customer.CustomerName, null);
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
