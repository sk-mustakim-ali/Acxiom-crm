using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AcxiomCRM.Tests;

public sealed class CrmWorkflowTests
{
    [Fact]
    public void CustomerValidation_RejectsInvalidEmailAndPhone()
    {
        var customer = new Customer
        {
            CustomerName = "Example",
            Email = "bad-email",
            Phone = "123",
            CompanyName = "Example Co",
            Status = "Active"
        };

        var errors = Validate(customer);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(Customer.Email)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(Customer.Phone)));
    }

    [Fact]
    public void LeadValidation_RejectsInvalidEmail()
    {
        var lead = new Lead { LeadName = "Example", CompanyName = "Example Co", Email = "invalid" };

        Assert.Contains(Validate(lead), error => error.MemberNames.Contains(nameof(Lead.Email)));
    }

    [Theory]
    [InlineData("New", "Contacted", true)]
    [InlineData("Qualified", "Lost", true)]
    [InlineData("Qualified", "Converted", false)]
    [InlineData("Converted", "New", false)]
    [InlineData("Lost", "Qualified", false)]
    public void LeadStatusTransitions_EnforceWorkflow(string current, string next, bool expected) =>
        Assert.Equal(expected, CrmWorkflowRules.CanTransitionLead(current, next));

    [Theory]
    [InlineData("Planned", "Completed", true)]
    [InlineData("Planned", "Missed", true)]
    [InlineData("Completed", "Planned", false)]
    [InlineData("Cancelled", "Completed", false)]
    public void FollowUpStatusTransitions_EnforceWorkflow(string current, string next, bool expected) =>
        Assert.Equal(expected, CrmWorkflowRules.CanTransitionFollowUp(current, next));

    [Fact]
    public void OverdueFollowUps_ExcludeCompletedAndToday()
    {
        var today = new DateTime(2026, 10, 12);
        Assert.True(CrmWorkflowRules.IsOverdueFollowUp(new FollowUp { Status = "Planned", FollowUpDate = today.AddDays(-1) }, today));
        Assert.False(CrmWorkflowRules.IsOverdueFollowUp(new FollowUp { Status = "Completed", FollowUpDate = today.AddDays(-1) }, today));
        Assert.False(CrmWorkflowRules.IsOverdueFollowUp(new FollowUp { Status = "Planned", FollowUpDate = today }, today));
    }

    [Fact]
    public void PipelineValues_KeepOpenAndWeightedTotalsSeparate()
    {
        var opportunities = new[]
        {
            new Opportunity { Amount = 1000m, Probability = 50, Status = "Open" },
            new Opportunity { Amount = 200m, Probability = 25, Status = "Open" },
            new Opportunity { Amount = 500m, Probability = 100, Status = "Won" }
        };

        var totals = CrmWorkflowRules.CalculatePipelineValues(opportunities);

        Assert.Equal(1200m, totals.OpenValue);
        Assert.Equal(550m, totals.WeightedValue);
    }

    [Fact]
    public void OpportunityValidation_RejectsZeroAmountAndProbabilityAboveOneHundred()
    {
        var opportunity = new Opportunity
        {
            OpportunityName = "Example",
            CustomerId = 1,
            Amount = 0,
            Probability = 101,
            ExpectedCloseDate = DateTime.Today.AddDays(1),
            Stage = "Qualification",
            Status = "Open"
        };

        var errors = Validate(opportunity);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(Opportunity.Amount)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(Opportunity.Probability)));
    }

    [Fact]
    public async Task SalesExecutiveScope_ExcludesRecordsOwnedByOtherUsers()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Customers.AddRange(
            new Customer { CustomerCode = "CUS-A", CustomerName = "Mine", Email = "mine@example.test", NormalizedEmail = "MINE@EXAMPLE.TEST", Phone = "11111111", NormalizedPhone = "11111111", CompanyName = "A", Status = "Active", OwnerId = "sales-a" },
            new Customer { CustomerCode = "CUS-B", CustomerName = "Other", Email = "other@example.test", NormalizedEmail = "OTHER@EXAMPLE.TEST", Phone = "22222222", NormalizedPhone = "22222222", CompanyName = "B", Status = "Active", OwnerId = "sales-b" });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "sales-a"),
            new Claim(ClaimTypes.Role, "SalesExecutive")
        ], "test"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var scope = new CrmAccessScope(db, accessor, userManager: null!);

        var visible = await scope.Customers().Select(customer => customer.CustomerName).ToListAsync();

        Assert.Equal(["Mine"], visible);
    }

    [Fact]
    public async Task CustomerDatabaseConstraints_RejectDuplicateEmailRegardlessOfCase()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Customers.Add(new Customer { CustomerCode = "CUS-A", CustomerName = "A", Email = "same@example.test", NormalizedEmail = "SAME@EXAMPLE.TEST", Phone = "11111111", NormalizedPhone = "11111111", CompanyName = "A", Status = "Active" });
        await db.SaveChangesAsync();
        db.Customers.Add(new Customer { CustomerCode = "CUS-B", CustomerName = "B", Email = "SAME@example.test", NormalizedEmail = "SAME@EXAMPLE.TEST", Phone = "22222222", NormalizedPhone = "22222222", CompanyName = "B", Status = "Active" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task LeadConversion_IsTransactionalAuditedAndCannotBeRepeated()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var lead = new Lead
        {
            LeadCode = "LEAD-A", LeadName = "Qualified", CompanyName = "Example Co", Status = "Qualified",
            Email = "person@example.test", NormalizedEmail = "PERSON@EXAMPLE.TEST", Phone = "+1 (555) 222-3333",
            NormalizedPhone = "15552223333", Source = "Website", Priority = "High"
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        var service = new LeadConversionService(db);

        var converted = await service.ConvertQualifiedLeadAsync(lead, "actor-1", "Admin", "127.0.0.1");
        var repeated = await service.ConvertQualifiedLeadAsync(lead, "actor-1", "Admin", "127.0.0.1");

        Assert.Equal(LeadConversionStatus.Converted, converted.Status);
        Assert.Equal(LeadConversionStatus.NotQualified, repeated.Status);
        Assert.Equal("Converted", lead.Status);
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(2, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task LeadConversion_DuplicateCustomerLeavesLeadUnchanged()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Customers.Add(new Customer
        {
            CustomerCode = "CUS-A", CustomerName = "Existing", CompanyName = "Example Co", Status = "Active",
            Email = "person@example.test", NormalizedEmail = "PERSON@EXAMPLE.TEST", Phone = "5552223333", NormalizedPhone = "5552223333"
        });
        var lead = new Lead
        {
            LeadCode = "LEAD-A", LeadName = "Qualified", CompanyName = "Example Co", Status = "Qualified",
            Email = "PERSON@example.test", NormalizedEmail = "PERSON@EXAMPLE.TEST", Phone = "5552223333",
            NormalizedPhone = "5552223333", Source = "Website", Priority = "High"
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var result = await new LeadConversionService(db).ConvertQualifiedLeadAsync(lead, "actor-1", "Admin", null);

        Assert.Equal(LeadConversionStatus.DuplicateCustomer, result.Status);
        Assert.Equal("Qualified", lead.Status);
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
