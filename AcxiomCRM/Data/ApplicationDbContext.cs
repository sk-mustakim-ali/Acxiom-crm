using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<Opportunity> Opportunities { get; set; }
    public DbSet<FollowUp> FollowUps { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>().Property(c => c.Id).HasColumnName("CustomerId");
        builder.Entity<Lead>().Property(l => l.Id).HasColumnName("LeadId");
        builder.Entity<Opportunity>().Property(o => o.Id).HasColumnName("OpportunityId");
        builder.Entity<FollowUp>().Property(f => f.Id).HasColumnName("FollowUpId");
        builder.Entity<Activity>().Property(a => a.Id).HasColumnName("ActivityId");
        builder.Entity<AuditLog>().Property(a => a.Id).HasColumnName("AuditLogId");

        builder.Entity<Customer>().HasIndex(c => c.Email).IsUnique();
        builder.Entity<Customer>().HasIndex(c => c.Phone).IsUnique();
        builder.Entity<Lead>().Property(l => l.ExpectedValue).HasColumnType("decimal(18,2)");
        builder.Entity<Opportunity>().Property(o => o.Amount).HasColumnType("decimal(18,2)");
    }
}

