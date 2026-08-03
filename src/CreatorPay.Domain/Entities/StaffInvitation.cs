using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class StaffInvitation : Entity
{
    public Guid MerchantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public UserRole UserRole { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid InvitedByUserId { get; set; }
    public Guid? SupervisorId { get; set; }
    public Guid? CashierId { get; set; }
    public Merchant Merchant { get; set; } = null!;
}
