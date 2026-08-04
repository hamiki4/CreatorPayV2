using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class OperationalAlert : Entity
{
    public string AlertType { get; set; } = "";
    public string Severity { get; set; } = "Warning";
    public string Status { get; set; } = "Open";
    public string Title { get; set; } = "";
    public string SafeDescription { get; set; } = "";
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string CorrelationId { get; set; } = "";
    public string CooldownKey { get; set; } = "";
    public ICollection<OperationalAlertHistory> History { get; } = [];
}

public sealed class OperationalAlertHistory : Entity
{
    public Guid OperationalAlertId { get; set; }
    public string PreviousStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public Guid ActorUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public OperationalAlert Alert { get; set; } = null!;
}
