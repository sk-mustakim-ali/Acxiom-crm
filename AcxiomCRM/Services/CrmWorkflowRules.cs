using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public static class CrmWorkflowRules
{
    public static readonly string[] CustomerStatuses = ["Active", "Inactive", "Prospect"];
    public static readonly string[] LeadPriorities = ["Low", "Medium", "High"];
    public static readonly string[] LeadStatuses =
        ["New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"];

    public static readonly string[] OpportunityStages =
        ["Qualification", "Proposal", "Negotiation", "Won", "Lost"];

    public static readonly string[] OpportunityStatuses = ["Open", "Won", "Lost"];
    public static readonly string[] FollowUpStatuses = ["Planned", "Completed", "Missed", "Cancelled"];
    public static readonly string[] FollowUpTypes = ["Call", "Meeting", "Email", "Task"];
    public static readonly string[] ActivityStatuses = ["Planned", "In Progress", "Completed", "Cancelled"];

    public static bool IsValidLeadStatus(string? status) =>
        LeadStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static bool CanTransitionLead(string? current, string? next)
    {
        if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
        {
            return IsValidLeadStatus(next);
        }

        return current?.ToLowerInvariant() switch
        {
            "new" => IsOneOf(next, "Contacted", "Qualified", "Unqualified", "Lost"),
            "contacted" => IsOneOf(next, "Qualified", "Unqualified", "Lost"),
            "qualified" => IsOneOf(next, "Lost"),
            _ => false
        };
    }

    public static bool IsValidOpportunityStage(string? stage) =>
        OpportunityStages.Contains(stage, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidOpportunityStatus(string? status) =>
        OpportunityStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static bool OpportunityStageMatchesStatus(string? stage, string? status) =>
        stage?.Equals("Won", StringComparison.OrdinalIgnoreCase) == true
            ? status?.Equals("Won", StringComparison.OrdinalIgnoreCase) == true
            : stage?.Equals("Lost", StringComparison.OrdinalIgnoreCase) == true
                ? status?.Equals("Lost", StringComparison.OrdinalIgnoreCase) == true
                : status?.Equals("Open", StringComparison.OrdinalIgnoreCase) == true;

    public static bool CanTransitionOpportunityStage(string? current, string? next)
    {
        if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
        {
            return IsValidOpportunityStage(next);
        }

        return current?.ToLowerInvariant() switch
        {
            "qualification" => IsOneOf(next, "Proposal", "Negotiation", "Won", "Lost"),
            "proposal" => IsOneOf(next, "Negotiation", "Won", "Lost"),
            "negotiation" => IsOneOf(next, "Won", "Lost"),
            _ => false
        };
    }

    public static bool IsActiveOpportunityStatus(string? status) =>
        status?.Equals("Open", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsValidFollowUpStatus(string? status) =>
        FollowUpStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static bool CanTransitionFollowUp(string? current, string? next)
    {
        if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
        {
            return IsValidFollowUpStatus(next);
        }

        return current?.Equals("Planned", StringComparison.OrdinalIgnoreCase) == true
            && IsValidFollowUpStatus(next);
    }

    public static bool IsOverdueFollowUp(FollowUp followUp, DateTime today) =>
        followUp.Status.Equals("Planned", StringComparison.OrdinalIgnoreCase)
        && followUp.FollowUpDate.Date < today.Date;

    public static PipelineValues CalculatePipelineValues(IEnumerable<Opportunity> opportunities)
    {
        var open = opportunities.Where(opportunity => IsActiveOpportunityStatus(opportunity.Status)).ToList();
        return new PipelineValues(
            open.Sum(opportunity => opportunity.Amount),
            open.Sum(opportunity => opportunity.Amount * opportunity.Probability / 100m));
    }

    public static bool IsValidFollowUpType(string? type) =>
        FollowUpTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidActivityType(string? type) =>
        FollowUpTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidActivityStatus(string? status) =>
        ActivityStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    private static bool IsOneOf(string? value, params string[] choices) =>
        choices.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public readonly record struct PipelineValues(decimal OpenValue, decimal WeightedValue);
