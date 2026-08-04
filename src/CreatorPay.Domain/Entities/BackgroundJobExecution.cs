using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;
namespace CreatorPay.Domain.Entities;

public sealed class BackgroundJobExecution : Entity
{
    public string JobName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public BackgroundJobStatus Status { get; set; }
    public int AttemptNumber { get; set; }
    public int ItemsProcessed { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
