using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessScope _accessScope;

    public DashboardController(ApplicationDbContext context, CrmAccessScope accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    public async Task<IActionResult> Index(string? period, DateTime? fromDate, DateTime? toDate)
    {
        period = string.IsNullOrWhiteSpace(period) ? "all" : period.ToLowerInvariant();
        var today = DateTime.Today;
        switch (period)
        {
            case "today":
                fromDate = today;
                toDate = today;
                break;
            case "week":
                fromDate = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                toDate = today;
                break;
            case "month":
                fromDate = new DateTime(today.Year, today.Month, 1);
                toDate = today;
                break;
            case "custom":
            case "all":
                break;
            default:
                ModelState.AddModelError(nameof(period), "Select a valid reporting period.");
                period = "all";
                fromDate = null;
                toDate = null;
                break;
        }

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
        {
            ModelState.AddModelError(nameof(toDate), "The end date must be on or after the start date.");
            return View(new DashboardViewModel { FromDate = fromDate, ToDate = toDate, Period = period });
        }

        var customers = _accessScope.Customers();
        var leads = _accessScope.Leads();
        var scopedOpportunities = _accessScope.Opportunities();
        var opportunities = scopedOpportunities;
        var followUps = _accessScope.FollowUps();

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            customers = customers.Where(customer => customer.CreatedDate >= start);
            leads = leads.Where(lead => lead.CreatedDate >= start);
            opportunities = opportunities.Where(opportunity => opportunity.CreatedDate >= start);
            followUps = followUps.Where(followUp => followUp.FollowUpDate >= start);
        }

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.Date.AddDays(1);
            customers = customers.Where(customer => customer.CreatedDate < endExclusive);
            leads = leads.Where(lead => lead.CreatedDate < endExclusive);
            opportunities = opportunities.Where(opportunity => opportunity.CreatedDate < endExclusive);
            followUps = followUps.Where(followUp => followUp.FollowUpDate < endExclusive);
        }

        var totalCustomers = await customers.CountAsync();
        var totalLeads = await leads.CountAsync();
        var openLeads = await leads.CountAsync(lead =>
            lead.Status != "Unqualified" && lead.Status != "Lost" && lead.Status != "Converted");
        var totalOpportunities = await opportunities.CountAsync();
        var openOpportunities = await opportunities.CountAsync(opportunity => opportunity.Status == "Open");
        var wonOpportunities = await opportunities.CountAsync(opportunity => opportunity.Status == "Won");
        var lostOpportunities = await opportunities.CountAsync(opportunity => opportunity.Status == "Lost");
        var pipelineValues = await opportunities
            .Where(opportunity => opportunity.Status == "Open")
            .Select(opportunity => new { opportunity.Amount, opportunity.Probability })
            .ToListAsync();
        var totalPipelineValue = pipelineValues.Sum(opportunity => opportunity.Amount * opportunity.Probability / 100m);
        var pendingFollowUps = await followUps.CountAsync(followUp =>
            followUp.Status == "Planned" && followUp.FollowUpDate >= DateTime.Today);

        var leadCounts = await leads
            .GroupBy(lead => lead.Status)
            .Select(group => new { Label = group.Key, Count = group.Count() })
            .ToListAsync();
        var opportunityCounts = await opportunities
            .GroupBy(opportunity => opportunity.Stage)
            .Select(group => new { Label = group.Key, Count = group.Count() })
            .ToListAsync();

        var chartEnd = toDate?.Date ?? DateTime.Today;
        var chartStart = fromDate?.Date ?? new DateTime(chartEnd.Year, chartEnd.Month, 1).AddMonths(-5);
        var salesResults = await scopedOpportunities
            .Where(opportunity => opportunity.Status == "Won"
                && opportunity.ClosedDate.HasValue
                && opportunity.ClosedDate.Value.Date >= chartStart
                && opportunity.ClosedDate.Value.Date <= chartEnd)
            .Select(opportunity => new { opportunity.ClosedDate, opportunity.Amount })
            .ToListAsync();
        var salesByMonth = salesResults
            .GroupBy(opportunity => new { opportunity.ClosedDate!.Value.Year, opportunity.ClosedDate.Value.Month })
            .ToDictionary(
                group => $"{group.Key.Year}-{group.Key.Month:00}",
                group => group.Sum(opportunity => opportunity.Amount));
        var monthlySales = new List<KeyValuePair<string, decimal>>();
        var monthCursor = new DateTime(chartStart.Year, chartStart.Month, 1);
        var lastMonth = new DateTime(chartEnd.Year, chartEnd.Month, 1);
        while (monthCursor <= lastMonth)
        {
            var label = $"{monthCursor.Year}-{monthCursor.Month:00}";
            monthlySales.Add(new KeyValuePair<string, decimal>(label, salesByMonth.GetValueOrDefault(label)));
            monthCursor = monthCursor.AddMonths(1);
        }

        var model = new DashboardViewModel
        {
            TotalCustomers = totalCustomers,
            TotalLeads = totalLeads,
            OpenLeads = openLeads,
            TotalOpportunities = totalOpportunities,
            OpenOpportunities = openOpportunities,
            WonOpportunities = wonOpportunities,
            LostOpportunities = lostOpportunities,
            PendingFollowUps = pendingFollowUps,
            TotalPipelineValue = totalPipelineValue,
            FromDate = fromDate,
            ToDate = toDate,
            Period = period,
            LeadStatusCounts = CrmWorkflowRules.LeadStatuses
                .Select(status => new KeyValuePair<string, int>(
                    status,
                    leadCounts.FirstOrDefault(item => item.Label.Equals(status, StringComparison.OrdinalIgnoreCase))?.Count ?? 0))
                .ToList(),
            OpportunityStageCounts = CrmWorkflowRules.OpportunityStages
                .Select(stage => new KeyValuePair<string, int>(
                    stage,
                    opportunityCounts.FirstOrDefault(item => item.Label.Equals(stage, StringComparison.OrdinalIgnoreCase))?.Count ?? 0))
                .ToList(),
            MonthlySales = monthlySales
        };

        return View(model);
    }
}
