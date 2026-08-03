namespace CreatorPay.Application.Qr;

public sealed record CreatorQrDto(Guid Id, string PublicQrId, string Payload, int Version, DateTime IssuedAtUtc, DateTime? RevokedAtUtc, string? RevocationReason, bool IsActive);
public sealed record MerchantQrValidationRequest(string Payload, Guid LocationId);
public sealed record MerchantQrValidationResult(bool IsValid, string Code, string Message, string? CreatorPublicId, string? CreatorDisplayName, Guid? PartnershipId, Guid? MerchantId, Guid? LocationId, DateTime ValidatedAtUtc);

public interface IQrTokenService
{
    string CreateToken(string publicQrId, int version);
    string Hash(string token);
    bool FixedTimeEquals(string token, string expectedHash);
}

public interface IQrImageGenerator { byte[] GeneratePng(string payload); }

public interface ICreatorQrService
{
    Task<CreatorQrDto?> GetCurrentAsync(Guid creatorId, Guid actorUserId, CancellationToken ct);
    Task<IReadOnlyList<CreatorQrDto>> GetHistoryAsync(Guid creatorId, CancellationToken ct);
    Task<CreatorQrDto> IssueOrGetAsync(Guid creatorId, Guid actorUserId, CancellationToken ct);
    Task<CreatorQrDto> RegenerateAsync(Guid creatorId, Guid actorUserId, bool confirmed, CancellationToken ct);
    Task RevokeAsync(Guid creatorId, Guid actorUserId, string? reason, CancellationToken ct);
    Task<MerchantQrValidationResult> ValidateAsync(string payload, Guid locationId, Guid merchantId, Guid actorUserId, string role, Guid? cashierId, Guid? supervisorId, CancellationToken ct);
}
