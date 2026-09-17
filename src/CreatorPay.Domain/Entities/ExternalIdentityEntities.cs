using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class ExternalIdentity : Entity
{
    public string Issuer { get; set; } = "";
    public string Environment { get; set; } = "";
    public Guid ExternalUserId { get; set; }
    public Guid IdentityBindingId { get; set; }
    public long IdentityBindingVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastValidatedAtUtc { get; set; }
}

public sealed class ExternalProfileLink : Entity
{
    public Guid ExternalIdentityId { get; set; }
    public UserRole Role { get; set; }
    public Guid? ExternalProfileSubjectId { get; set; }
    public Guid? ExternalBusinessId { get; set; }
    public Guid? UserAccountId { get; set; }
    public ExternalProfileStatus Status { get; set; }
    public string ProvisioningKey { get; set; } = "";
}

public sealed class ExternalApplicationSession : Entity
{
    public Guid ExternalIdentityId { get; set; }
    public Guid? ExternalProfileLinkId { get; set; }
    public Guid? UserAccountId { get; set; }
    public Guid IdentityBindingId { get; set; }
    public long IdentityBindingVersion { get; set; }
    public UserRole Role { get; set; }
    public string Purpose { get; set; } = "";
    public DateTime LastValidatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
}
