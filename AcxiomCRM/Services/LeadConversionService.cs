using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public enum LeadConversionStatus
{
    Converted,
    NotQualified,
    MissingContactDetails,
    DuplicateCustomer
}

public sealed record LeadConversionResult(LeadConversionStatus Status, Customer? Customer = null);

public sealed class LeadConversionService
{
    private readonly ApplicationDbContext _context;

    public LeadConversionService(ApplicationDbContext context) => _context = context;

    public async Task<LeadConversionResult> ConvertQualifiedLeadAsync(
        Lead lead,
        string? actorId,
        string? actorName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var persistedStatus = await _context.Leads.AsNoTracking()
            .Where(item => item.Id == lead.Id)
            .Select(item => item.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.Equals(persistedStatus, "Qualified", StringComparison.OrdinalIgnoreCase))
        {
            return new LeadConversionResult(LeadConversionStatus.NotQualified);
        }

        if (string.IsNullOrWhiteSpace(lead.Email) || string.IsNullOrWhiteSpace(lead.Phone))
        {
            return new LeadConversionResult(LeadConversionStatus.MissingContactDetails);
        }

        var email = lead.Email.Trim();
        var phone = lead.Phone.Trim();
        var normalizedEmail = CrmContactNormalization.NormalizeEmail(email);
        var normalizedPhone = CrmContactNormalization.NormalizePhone(phone);
        if (await _context.Customers.AnyAsync(customer =>
                customer.NormalizedEmail == normalizedEmail || customer.NormalizedPhone == normalizedPhone,
                cancellationToken))
        {
            return new LeadConversionResult(LeadConversionStatus.DuplicateCustomer);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var customer = new Customer
        {
            CustomerCode = CreateCode("CUS-"),
            CustomerName = lead.LeadName,
            Email = email,
            NormalizedEmail = normalizedEmail,
            Phone = phone,
            NormalizedPhone = normalizedPhone,
            CompanyName = lead.CompanyName,
            Status = "Active",
            OwnerId = lead.AssignedToId,
            CreatedBy = actorName,
            CreatedDate = DateTime.UtcNow
        };
        _context.Customers.Add(customer);
        lead.Status = "Converted";

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _context.AuditLogs.AddRange(
                CreateAudit(actorId, ipAddress, "Convert", "Lead", lead.Id.ToString(), "Status=Qualified", "Status=Converted; CustomerId=" + customer.Id),
                CreateAudit(actorId, ipAddress, "Create", "Customer", customer.Id.ToString(), null, "CustomerCode=" + customer.CustomerCode));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new LeadConversionResult(LeadConversionStatus.Converted, customer);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new LeadConversionResult(LeadConversionStatus.DuplicateCustomer);
        }
    }

    private static AuditLog CreateAudit(string? actorId, string? ipAddress, string action,
        string entity, string recordId, string? oldValue, string? newValue) => new()
    {
        UserId = actorId,
        Action = action,
        EntityName = entity,
        RecordId = recordId,
        OldValue = oldValue,
        NewValue = newValue,
        CreatedDate = DateTime.UtcNow,
        IpAddress = ipAddress
    };

    private static string CreateCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 };
}
