namespace AcxiomCRM.Models;

public class AuditLogRowViewModel
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? IpAddress { get; set; }
}

public class ReportPageViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = Array.Empty<IReadOnlyList<string>>();
    public IReadOnlyList<ReportMetricViewModel> Metrics { get; set; } = Array.Empty<ReportMetricViewModel>();
}

public class ReportMetricViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
