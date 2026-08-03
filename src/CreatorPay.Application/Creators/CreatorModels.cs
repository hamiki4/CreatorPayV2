using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Creators;

public sealed record RegisterCreatorRequest(string FirstName, string LastName, string DisplayName, string PhoneNumber, string Email, string Password);
public sealed record VerifyCreatorRequest(string Token);
public sealed record UpdateCreatorProfileRequest(string FirstName, string LastName, string DisplayName, string PhoneNumber, string Email, ProfileImageMetadata? ProfileImage);
public sealed record ProfileImageMetadata(string FileName, string ContentType, long SizeBytes);
public sealed record CreatorDecisionRequest(Guid CreatorId, string? Reason);
public sealed record CreatorRegistrationResponse(Guid CreatorId, string PublicCreatorId, string Message);
public sealed record CreatorProfileResponse(Guid CreatorId, string PublicCreatorId, string FirstName, string LastName, string DisplayName, string PhoneNumber, string Email, bool IsEmailVerified, bool IsPhoneVerified, AccountStatus AccountStatus, CreatorStatus CreatorStatus, ProfileImageMetadata? ProfileImage, string NextStep);
public sealed record PendingCreatorResponse(Guid CreatorId, string PublicCreatorId, string DisplayName, string Email, DateTime RegisteredAtUtc);

public interface ICreatorService
{
    Task<CreatorResult<CreatorRegistrationResponse>> RegisterAsync(RegisterCreatorRequest request, CancellationToken ct);
    Task<CreatorResult> VerifyEmailAsync(string token, CancellationToken ct);
    Task<CreatorResult> VerifyPhoneAsync(string token, CancellationToken ct);
    Task<CreatorResult<CreatorProfileResponse>> GetMeAsync(Guid userId, CancellationToken ct);
    Task<CreatorResult<CreatorProfileResponse>> UpdateMeAsync(Guid userId, UpdateCreatorProfileRequest request, CancellationToken ct);
    Task<IReadOnlyList<PendingCreatorResponse>> GetPendingAsync(CancellationToken ct);
    Task<CreatorResult<CreatorProfileResponse>> GetAsync(Guid creatorId, CancellationToken ct);
    Task<CreatorResult> ApproveAsync(Guid creatorId, Guid adminId, CancellationToken ct);
    Task<CreatorResult> RejectAsync(Guid creatorId, Guid adminId, string? reason, CancellationToken ct);
    Task<CreatorResult> SuspendAsync(Guid creatorId, Guid adminId, string? reason, CancellationToken ct);
    Task<CreatorResult> ReactivateAsync(Guid creatorId, Guid adminId, string? reason, CancellationToken ct);
}

public record CreatorResult(bool Succeeded, string? Error = null)
{
    public static CreatorResult Success() => new(true);
    public static CreatorResult Failure(string error) => new(false, error);
}
public sealed record CreatorResult<T>(T? Value, string? Error = null)
{
    public bool Succeeded => Error is null;
    public static CreatorResult<T> Success(T value) => new(value);
    public static CreatorResult<T> Failure(string error) => new(default, error);
}
