using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class LoginAudit : Entity
{
    public Guid? UserAccountId { get; set; }
    public string? NormalizedEmail { get; set; }
    public bool WasSuccessful { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
    public string? CorrelationId { get; set; }
}
