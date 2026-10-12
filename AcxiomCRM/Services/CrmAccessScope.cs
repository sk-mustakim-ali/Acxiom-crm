using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public sealed class CrmAccessScope
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public CrmAccessScope(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool CanViewBusinessScope => User.IsInRole("Admin") || User.IsInRole("Manager");

    public IQueryable<Customer> Customers()
    {
        var query = _context.Customers.AsQueryable();
        return CanViewBusinessScope ? query : query.Where(customer => customer.OwnerId == UserId);
    }

    public IQueryable<Lead> Leads()
    {
        var query = _context.Leads.AsQueryable();
        return CanViewBusinessScope ? query : query.Where(lead => lead.AssignedToId == UserId);
    }

    public IQueryable<Opportunity> Opportunities()
    {
        var query = _context.Opportunities.AsQueryable();
        return CanViewBusinessScope
            ? query
            : query.Where(opportunity =>
                opportunity.OwnerId == UserId
                || _context.Customers.Any(customer =>
                    customer.Id == opportunity.CustomerId && customer.OwnerId == UserId));
    }

    public IQueryable<FollowUp> FollowUps()
    {
        var query = _context.FollowUps.AsQueryable();
        return CanViewBusinessScope
            ? query
            : query.Where(followUp =>
                followUp.AssignedToId == UserId
                || (followUp.CustomerId.HasValue
                    && _context.Customers.Any(customer =>
                        customer.Id == followUp.CustomerId && customer.OwnerId == UserId))
                || (followUp.LeadId.HasValue
                    && _context.Leads.Any(lead =>
                        lead.Id == followUp.LeadId && lead.AssignedToId == UserId))
                || (followUp.OpportunityId.HasValue
                    && _context.Opportunities.Any(opportunity =>
                        opportunity.Id == followUp.OpportunityId
                        && (opportunity.OwnerId == UserId
                            || _context.Customers.Any(customer =>
                                customer.Id == opportunity.CustomerId && customer.OwnerId == UserId)))));
    }

    public IQueryable<Activity> Activities()
    {
        var query = _context.Activities.AsQueryable();
        return CanViewBusinessScope
            ? query
            : query.Where(activity =>
                activity.AssignedToId == UserId
                || (activity.CustomerId.HasValue
                    && _context.Customers.Any(customer =>
                        customer.Id == activity.CustomerId && customer.OwnerId == UserId))
                || (activity.LeadId.HasValue
                    && _context.Leads.Any(lead =>
                        lead.Id == activity.LeadId && lead.AssignedToId == UserId)));
    }

    public bool CanAccessOwner(string? ownerId) =>
        CanViewBusinessScope || (!string.IsNullOrEmpty(UserId) && ownerId == UserId);

    public async Task<List<ApplicationUser>> GetActiveSalesExecutivesAsync()
    {
        var activeUsers = await _userManager.Users
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName)
            .ToListAsync();
        var salesUsers = new List<ApplicationUser>();
        foreach (var user in activeUsers)
        {
            if (await _userManager.IsInRoleAsync(user, "SalesExecutive"))
            {
                salesUsers.Add(user);
            }
        }

        return salesUsers;
    }

    public async Task<List<ApplicationUser>> GetActiveUsersAsync() =>
        await _userManager.Users
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName)
            .ToListAsync();

    public async Task<bool> IsActiveSalesExecutiveAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId);
        return user is { IsActive: true } && await _userManager.IsInRoleAsync(user, "SalesExecutive");
    }

    public async Task<bool> IsActiveUserAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId);
        return user is { IsActive: true };
    }

    public async Task<bool> CanAccessCustomerAsync(int customerId, CancellationToken cancellationToken = default) =>
        await Customers().AnyAsync(customer => customer.Id == customerId, cancellationToken);

    public async Task<bool> CanAccessLeadAsync(int leadId, CancellationToken cancellationToken = default) =>
        await Leads().AnyAsync(lead => lead.Id == leadId, cancellationToken);

    public async Task<bool> CanAccessOpportunityAsync(int opportunityId, CancellationToken cancellationToken = default) =>
        await Opportunities().AnyAsync(opportunity => opportunity.Id == opportunityId, cancellationToken);

    public async Task<bool> CanAccessFollowUpAsync(int followUpId, CancellationToken cancellationToken = default) =>
        await FollowUps().AnyAsync(followUp => followUp.Id == followUpId, cancellationToken);

    public async Task<bool> CanAccessActivityAsync(int activityId, CancellationToken cancellationToken = default) =>
        await Activities().AnyAsync(activity => activity.Id == activityId, cancellationToken);
}
