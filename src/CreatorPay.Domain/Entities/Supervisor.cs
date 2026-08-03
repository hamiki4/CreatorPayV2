using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class Supervisor : Entity
{
    public Guid MerchantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public ICollection<SupervisorLocationAssignment> LocationAssignments { get; } = [];
}
