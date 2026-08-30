using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class MerchantLocation : Entity
{
    public Guid MerchantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public ICollection<CashierLocationAssignment> CashierAssignments { get; } = [];
    public ICollection<SupervisorLocationAssignment> SupervisorAssignments { get; } = [];
    public ICollection<PartnershipLocation> PartnershipLocations { get; } = [];
}
