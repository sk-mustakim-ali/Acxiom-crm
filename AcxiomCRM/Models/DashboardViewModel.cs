namespace AcxiomCRM.Models;

public class DashboardViewModel
{
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public int PendingFollowUps { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string Period { get; set; } = "all";

    public List<KeyValuePair<string, int>> LeadStatusCounts { get; set; } = new();
    public List<KeyValuePair<string, int>> OpportunityStageCounts { get; set; } = new();
    public List<KeyValuePair<string, decimal>> MonthlySales { get; set; } = new();
}
