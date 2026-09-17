using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class UserAccount : Entity
{
    public string? DisplayName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? NormalizedPhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public AuthenticationSource AuthenticationSource { get; set; } = AuthenticationSource.Local;
    public UserRole Role { get; set; }
    public AccountStatus Status { get; set; }
    public Guid? CreatorId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? MerchantId { get; set; }
    public Guid? SupervisorId { get; set; }
    public Guid? CashierId { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastFailedLoginAtUtc { get; set; }
    public string? FirebaseUid { get; set; }
    public string? RecoveryEmail { get; set; }
    public string? NormalizedRecoveryEmail { get; set; }
    public bool IsRecoveryEmailVerified { get; set; }
    public string? PinHash { get; set; }
    public int PinFailedAttemptCount { get; set; }
    public DateTime? PinLockedAtUtc { get; set; }
    public DateTime? PinRetryNotBeforeUtc { get; set; }
    public DateTime? PinEnrolledAtUtc { get; set; }
    public DateTime? PinChangedAtUtc { get; set; }
    public int PinVersion { get; set; }
}
