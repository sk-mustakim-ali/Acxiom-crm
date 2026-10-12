using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");
        var isManager = User.IsInRole("Manager");

        var customersQuery = _context.Customers.AsQueryable();
        var leadsQuery = _context.Leads.AsQueryable();
        var opportunitiesQuery = _context.Opportunities.AsQueryable();

        if (!isAdmin && !isManager)
        {
            customersQuery = customersQuery.Where(c => c.OwnerId == userId);
            leadsQuery = leadsQuery.Where(l => l.AssignedToId == userId || l.AssignedToId == null);
            opportunitiesQuery = opportunitiesQuery.Where(o => o.OwnerId == userId);
        }

        var totalCustomers = await customersQuery.CountAsync();
        var totalLeads = await leadsQuery.CountAsync();
        var openLeads = await leadsQuery.CountAsync(l => l.Status != "Closed" && l.Status != "Lost" && l.Status != "Converted");
        var totalOpportunities = await opportunitiesQuery.CountAsync();
        var openOpportunities = await opportunitiesQuery.CountAsync(o => o.Status == "Open");
        var wonOpportunities = await opportunitiesQuery.CountAsync(o => o.Status == "Won");
        var lostOpportunities = await opportunitiesQuery.CountAsync(o => o.Status == "Lost");
        var pipelineAmounts = await opportunitiesQuery
            .Where(o => o.Status != "Won" && o.Status != "Lost")
            .Select(o => o.Amount)
            .ToListAsync();
        var totalPipelineValue = pipelineAmounts.Sum();

        var leadStatusCounts = await leadsQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderBy(x => x.Label)
            .ToListAsync();

        var opportunityStageCounts = await opportunitiesQuery
            .GroupBy(o => o.Stage)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderBy(x => x.Label)
            .ToListAsync();

        var wonSales = await opportunitiesQuery
            .Where(o => o.Status == "Won")
            .Select(o => new { o.CreatedDate, o.Amount })
            .ToListAsync();
        var monthlySales = wonSales
            .GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month })
            .Select(g => new
            {
                Label = $"{g.Key.Year}-{g.Key.Month:00}",
                Amount = g.Sum(o => o.Amount)
            })
            .OrderBy(x => x.Label)
            .Take(6)
            .ToList();

        var viewModel = new DashboardViewModel
        {
            TotalCustomers = totalCustomers,
            TotalLeads = totalLeads,
            OpenLeads = openLeads,
            TotalOpportunities = totalOpportunities,
            OpenOpportunities = openOpportunities,
            WonOpportunities = wonOpportunities,
            LostOpportunities = lostOpportunities,
            TotalPipelineValue = totalPipelineValue,
            LeadStatusCounts = leadStatusCounts.Select(x => new KeyValuePair<string, int>(x.Label, x.Count)).ToList(),
            OpportunityStageCounts = opportunityStageCounts.Select(x => new KeyValuePair<string, int>(x.Label, x.Count)).ToList(),
            MonthlySales = monthlySales.Select(x => new KeyValuePair<string, decimal>(x.Label, x.Amount)).ToList()
        };

        return View(viewModel);
    }
}
