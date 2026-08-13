using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Organization;

public sealed record LocationRequest(string Name, string AddressLine1, string? AddressLine2, string City, string? Region, string CountryCode, string TimeZoneId);
public sealed record StatusRequest(bool IsActive);
public sealed record StaffInvitationRequest(string FirstName, string LastName, string Email, string PhoneNumber, IReadOnlyList<Guid>? LocationIds, string? LocationName = null);
public sealed record CreateCashierRequest(string FirstName, string LastName, string PhoneNumber, string TemporaryPassword, string Confirmation, string? LocationName = null, string? Email = null, DateOnly? BirthDate = null);
public sealed record StaffProfileRequest(string FirstName, string LastName, string PhoneNumber);
public sealed record SupervisorLocationsRequest(IReadOnlyList<Guid> LocationIds);
public sealed record CashierLocationsRequest(IReadOnlyList<Guid> LocationIds, Guid? PrimaryLocationId);
public sealed record AcceptStaffInvitationRequest(string Token, string Password, string Confirmation);
public sealed record LocationResponse(Guid Id, string Name, string AddressLine1, string? AddressLine2, string City, string? Region, string CountryCode, string TimeZoneId, bool IsActive);
public sealed record StaffResponse(Guid Id, string FirstName, string LastName, string Email, string Username, string PhoneNumber, bool IsActive, IReadOnlyList<StaffLocationResponse> Locations, string BusinessName);
public sealed record StaffLocationResponse(Guid Id, string Name, bool IsActive, bool IsPrimary);
public sealed record InvitationResponse(Guid InvitationId, string Email, UserRole Role, DateTime ExpiresAtUtc, string InvitationUrl, string? DevelopmentToken);
public sealed record InvitationPreviewResponse(string BusinessName, string Role, string LocationName, DateTime ExpiresAtUtc);
public sealed record OrganizationResult<T>(bool Succeeded, T? Value, string? Error, int StatusCode)
{ public static OrganizationResult<T> Ok(T value, int status = 200) => new(true, value, null, status); public static OrganizationResult<T> Fail(string error, int status = 400) => new(false, default, error, status); }
public sealed record OrganizationResult(bool Succeeded, string? Error, int StatusCode)
{ public static OrganizationResult Ok(int status = 200) => new(true, null, status); public static OrganizationResult Fail(string error, int status = 400) => new(false, error, status); }

public interface IOrganizationService
{
    Task<OrganizationResult<IReadOnlyList<LocationResponse>>> GetLocationsAsync(Guid merchantId, bool activeOnly, CancellationToken ct);
    Task<OrganizationResult<LocationResponse>> GetLocationAsync(Guid merchantId, Guid id, CancellationToken ct);
    Task<OrganizationResult<LocationResponse>> CreateLocationAsync(Guid merchantId, Guid actor, LocationRequest request, CancellationToken ct);
    Task<OrganizationResult<LocationResponse>> UpdateLocationAsync(Guid merchantId, Guid actor, Guid id, LocationRequest request, CancellationToken ct);
    Task<OrganizationResult> SetLocationStatusAsync(Guid merchantId, Guid actor, Guid id, bool active, CancellationToken ct);
    Task<OrganizationResult<IReadOnlyList<StaffResponse>>> GetStaffAsync(Guid merchantId, UserRole role, CancellationToken ct);
    Task<OrganizationResult<StaffResponse>> GetStaffAsync(Guid merchantId, UserRole role, Guid id, CancellationToken ct);
    Task<OrganizationResult<InvitationResponse>> InviteAsync(Guid merchantId, Guid actor, UserRole role, StaffInvitationRequest request, CancellationToken ct);
    Task<OrganizationResult<StaffResponse>> CreateCashierAsync(Guid merchantId, Guid actor, CreateCashierRequest request, CancellationToken ct);
    Task<OrganizationResult<StaffResponse>> UpdateStaffAsync(Guid merchantId, Guid actor, UserRole role, Guid id, StaffProfileRequest request, CancellationToken ct);
    Task<OrganizationResult> AssignSupervisorAsync(Guid merchantId, Guid actor, Guid id, SupervisorLocationsRequest request, CancellationToken ct);
    Task<OrganizationResult> AssignCashierAsync(Guid merchantId, Guid actor, Guid id, CashierLocationsRequest request, CancellationToken ct);
    Task<OrganizationResult> SetStaffStatusAsync(Guid merchantId, Guid actor, UserRole role, Guid id, bool active, CancellationToken ct);
    Task<OrganizationResult> RevokeInvitationAsync(Guid merchantId, Guid actor, Guid id, CancellationToken ct);
    Task<OrganizationResult> AcceptInvitationAsync(AcceptStaffInvitationRequest request, CancellationToken ct);
    Task<OrganizationResult<InvitationPreviewResponse>> GetInvitationAsync(string token, CancellationToken ct);
    Task<OrganizationResult<StaffResponse>> GetCurrentStaffAsync(Guid merchantId, UserRole role, Guid staffId, CancellationToken ct);
}

public interface IMerchantScopeAuthorizer
{ OrganizationResult EnsureMerchant(Guid? claimMerchantId, Guid resourceMerchantId); OrganizationResult EnsureLocation(Guid merchantId, Guid locationMerchantId); }
