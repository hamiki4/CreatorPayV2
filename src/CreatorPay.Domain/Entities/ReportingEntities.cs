using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class SavedReportView : Entity
{
    public Guid OwnerUserId { get; set; }
    public string OwnerRole { get; set; } = "";
    public string Name { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string FiltersJson { get; set; } = "{}";
    public bool IsDefault { get; set; }
}

public sealed class ReportExportAudit : Entity
{
    public Guid RequestedByUserId { get; set; }
    public string RequesterRole { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string Format { get; set; } = "";
    public string FiltersJson { get; set; } = "{}";
    public int RowCount { get; set; }
    public string PolicyDecision { get; set; } = "Allowed";
    public string CorrelationId { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; }
}

public sealed class AlertThresholdPolicy : Entity
{
    public string AlertType { get; set; } = "";
    public int Version { get; set; }
    public decimal Threshold { get; set; }
    public int WindowMinutes { get; set; }
    public int CooldownMinutes { get; set; }
    public string Severity { get; set; } = "Warning";
    public string Status { get; set; } = "Draft";
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ReviewReason { get; set; }
}
