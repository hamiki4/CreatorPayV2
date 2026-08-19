using System.Security.Cryptography;
using System.Text;
using CreatorPay.Application.Qr;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Infrastructure.Eligibility;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Microsoft.Extensions.Configuration;

namespace CreatorPay.Infrastructure.Qr;

public sealed class CreatorQrUrlBuilder
{
    private readonly string publicAppBaseUrl;
    public CreatorQrUrlBuilder(IConfiguration configuration)
    {
        var configured = configuration["PublicAppBaseUrl"]?.Trim();
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("PublicAppBaseUrl must be an absolute HTTP(S) URL without credentials, query, or fragment.");
        publicAppBaseUrl = configured!.TrimEnd('/');
    }
    public string Create(string publicQrId, int version, string token) => $"{publicAppBaseUrl}/c/{Uri.EscapeDataString(publicQrId)}?t={Uri.EscapeDataString(token)}&v={version}";
}

public sealed class QrTokenService(IConfiguration configuration) : IQrTokenService
{
    public string CreateToken(string publicQrId, int version)
    {
        var key = configuration["Authentication:Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is required for QR token signing.");
        using var hmac = new HMACSHA256(SHA256.HashData(Encoding.UTF8.GetBytes($"CreatorQr:{key}")));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{publicQrId}:{version}"))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public bool FixedTimeEquals(string token, string expectedHash) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(expectedHash));
}

public sealed class QrImageGenerator : IQrImageGenerator
{
    public byte[] GeneratePng(string payload)
    {
        using var data = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(12, [0, 0, 0, 255], [255, 255, 255, 255]);
    }
}

public sealed class CreatorQrService(ApplicationDbContext db, IQrTokenService tokens, CreatorQrUrlBuilder urls) : ICreatorQrService
{
    public async Task<CreatorQrDto?> GetCurrentAsync(Guid creatorId, Guid actor, CancellationToken ct)
    {
        var qr = await db.CreatorQrCodes.AsNoTracking().SingleOrDefaultAsync(x => x.CreatorId == creatorId && x.IsActive, ct);
        if (qr is not null) AddCreatorAudit(creatorId, actor, "CreatorQrViewed", null);
        if (qr is not null) await db.SaveChangesAsync(ct);
        return qr is null ? null : ToDto(qr, null);
    }

    public async Task<IReadOnlyList<CreatorQrDto>> GetHistoryAsync(Guid creatorId, CancellationToken ct) =>
        await db.CreatorQrCodes.AsNoTracking().Where(x => x.CreatorId == creatorId).OrderByDescending(x => x.IssuedAtUtc).Select(x => new CreatorQrDto(x.Id, x.PublicQrId, "", x.Version, x.IssuedAtUtc, x.RevokedAtUtc, x.RevocationReason, x.IsActive)).ToListAsync(ct);

    public async Task<CreatorQrDto> IssueOrGetAsync(Guid creatorId, Guid actor, CancellationToken ct)
    {
        var creator = await db.Creators.FindAsync([creatorId], ct) ?? throw new KeyNotFoundException();
        if (creator.Status != CreatorStatus.Active) throw new InvalidOperationException("Only an active platform-approved creator may receive a QR code.");
        var current = await db.CreatorQrCodes.SingleOrDefaultAsync(x => x.CreatorId == creatorId && x.IsActive, ct);
        if (current is not null) return ToDto(current, null);
        return await IssueAsync(creatorId, actor, "CreatorQrIssued", ct);
    }

    public async Task<CreatorQrDto> RegenerateAsync(Guid creatorId, Guid actor, bool confirmed, CancellationToken ct)
    {
        if (!confirmed) throw new ArgumentException("Regeneration confirmation is required.");
        var creator = await db.Creators.FindAsync([creatorId], ct);
        if (creator?.Status != CreatorStatus.Active) throw new InvalidOperationException("Only an active platform-approved creator may regenerate a QR code.");
        var now = DateTime.UtcNow;
        var old = await db.CreatorQrCodes.SingleOrDefaultAsync(x => x.CreatorId == creatorId && x.IsActive, ct);
        old?.Revoke(now, actor, "Regenerated");
        return await IssueAsync(creatorId, actor, "CreatorQrRegenerated", ct);
    }

    public async Task RevokeAsync(Guid creatorId, Guid actor, string? reason, CancellationToken ct)
    {
        var qr = await db.CreatorQrCodes.SingleOrDefaultAsync(x => x.CreatorId == creatorId && x.IsActive, ct) ?? throw new KeyNotFoundException();
        qr.Revoke(DateTime.UtcNow, actor, reason ?? "Revoked by creator"); AddCreatorAudit(creatorId, actor, "CreatorQrRevoked", reason); await db.SaveChangesAsync(ct);
    }

    public async Task<MerchantQrValidationResult> ValidateAsync(string payload, Guid locationId, Guid merchantId, Guid actor, string role, Guid? cashierId, Guid? supervisorId, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var failure = await ValidateScope(locationId, merchantId, role, cashierId, supervisorId, ct);
        if (failure is not null) return await Fail(failure.Value.Code, failure.Value.Message, merchantId, actor, locationId, now, ct);
        if (!TryParse(payload, out var publicQrId, out var token)) return await Fail("InvalidQr", "The QR code is invalid.", merchantId, actor, locationId, now, ct);
        var qr = await db.CreatorQrCodes.Include(x => x.Creator).SingleOrDefaultAsync(x => x.PublicQrId == publicQrId, ct);
        if (qr is null) return await Fail("InvalidQr", "The QR code is invalid.", merchantId, actor, locationId, now, ct);
        if (!tokens.FixedTimeEquals(token!, qr.TokenHash)) return await Fail("TamperedQr", "The QR code could not be authenticated.", merchantId, actor, locationId, now, ct);
        if (qr.Version != 1) return await Fail("InvalidQr", "This QR version is not supported.", merchantId, actor, locationId, now, ct);
        if (!qr.IsActive || qr.RevokedAtUtc.HasValue) return await Fail("RevokedQr", "This QR code is no longer active.", merchantId, actor, locationId, now, ct);
        if (qr.Creator.Status != CreatorStatus.Active) return await Fail("InactiveCreator", "This creator is not currently active.", merchantId, actor, locationId, now, ct);
        if (!await db.UserAccounts.AnyAsync(x => x.CreatorId == qr.CreatorId && x.Role == UserRole.Creator && x.Status == AccountStatus.Active, ct)) return await Fail("InactiveCreator", "Creator account is not active.", merchantId, actor, locationId, now, ct);
        var merchant = await db.Merchants.AsNoTracking().SingleAsync(x => x.Id == merchantId, ct);
        if (merchant.Status != MerchantStatus.Active || !await db.UserAccounts.AnyAsync(x => x.MerchantId == merchantId && x.Role == UserRole.MerchantAdmin && x.Status == AccountStatus.Active, ct)) return await Fail("InactiveMerchant", "Business account is not active.", merchantId, actor, locationId, now, ct);
        var p = await db.MerchantCreatorPartnerships.Include(x => x.Locations).SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.CreatorId == qr.CreatorId, ct);
        if (p is null) return await Fail("NoApprovedPartnership", "Advertising relationship is no longer active.", merchantId, actor, locationId, now, ct);
        if (p.Status == PartnershipStatus.Suspended) return await Fail("PartnershipSuspended", "The partnership is suspended.", merchantId, actor, locationId, now, ct);
        if (p.Status == PartnershipStatus.Blocked) return await Fail("PartnershipBlocked", "The partnership is blocked.", merchantId, actor, locationId, now, ct);
        if (p.Status != PartnershipStatus.Approved) return await Fail("NoApprovedPartnership", "No approved partnership exists.", merchantId, actor, locationId, now, ct);
        if (!p.StartDateUtc.HasValue || !p.EndDateUtc.HasValue)
            return await Fail("PartnershipExpired", "The advertising relationship is not active.", merchantId, actor, locationId, now, ct);
        if (p.StartDateUtc.Value > now) return await Fail("PartnershipNotStarted", "The advertising relationship has not started.", merchantId, actor, locationId, now, ct);
        if (p.EndDateUtc.Value <= now) return await Fail("PartnershipExpired", "The advertising relationship has expired.", merchantId, actor, locationId, now, ct);
        if (!await RewardEligibilityQueries.HasRequiredFundingAsync(db, merchantId, "ETB", 0m, ct)) return await Fail("BusinessFundingRequired", "Business account requires funding.", merchantId, actor, locationId, now, ct);
        if (p.Locations.Any(x => x.IsActive) && !p.Locations.Any(x => x.IsActive && x.MerchantLocationId == locationId)) return await Fail("LocationNotAllowed", "The partnership is not approved at this location.", merchantId, actor, locationId, now, ct);
        AddMerchantAudit(merchantId, actor, "CreatorQrValidationSucceeded", $"CreatorPublicId={qr.Creator.PublicCreatorId};LocationId={locationId}"); await db.SaveChangesAsync(ct);
        return new(true, "Valid", "Creator QR is valid for this merchant and location.", qr.Creator.PublicCreatorId, qr.Creator.DisplayName, p.Id, merchantId, locationId, now);
    }

    private async Task<(string Code, string Message)?> ValidateScope(Guid locationId, Guid merchantId, string role, Guid? cashierId, Guid? supervisorId, CancellationToken ct)
    {
        var location = await db.MerchantLocations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == locationId, ct);
        if (location is null || location.MerchantId != merchantId) return ("UnauthorizedMerchantAccess", "The selected location is outside your merchant scope.");
        if (!location.IsActive) return ("InactiveLocation", "The selected location is inactive.");
        if (role == nameof(UserRole.Cashier)) { var c = await db.Cashiers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cashierId && x.MerchantId == merchantId, ct); if (c?.IsActive != true) return ("InactiveCashier", "Cashier access is inactive."); if (!await db.CashierLocationAssignments.AnyAsync(x => x.CashierId == cashierId && x.MerchantLocationId == locationId && x.IsActive, ct)) return ("UnauthorizedMerchantAccess", "You are not assigned to this location."); }
        if (role == nameof(UserRole.Supervisor)) { var s = await db.Supervisors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == supervisorId && x.MerchantId == merchantId, ct); if (s?.IsActive != true) return ("InactiveCashier", "Supervisor access is inactive."); if (!await db.SupervisorLocationAssignments.AnyAsync(x => x.SupervisorId == supervisorId && x.MerchantLocationId == locationId && x.IsActive, ct)) return ("UnauthorizedMerchantAccess", "You are not assigned to this location."); }
        return null;
    }

    private async Task<CreatorQrDto> IssueAsync(Guid creatorId, Guid actor, string eventType, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var publicId = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(); var raw = tokens.CreateToken(publicId, 1); var qr = new CreatorQrCode { Id = Guid.NewGuid(), CreatorId = creatorId, PublicQrId = publicId, TokenHash = tokens.Hash(raw), Version = 1, IssuedAtUtc = now, CreatedAtUtc = now, CreatedBy = actor.ToString() };
        db.CreatorQrCodes.Add(qr); AddCreatorAudit(creatorId, actor, eventType, $"PublicQrId={qr.PublicQrId}"); await db.SaveChangesAsync(ct); return ToDto(qr, raw);
    }
    private CreatorQrDto ToDto(CreatorQrCode q, string? raw) { raw ??= tokens.CreateToken(q.PublicQrId, q.Version); return new(q.Id, q.PublicQrId, urls.Create(q.PublicQrId, q.Version, raw), q.Version, q.IssuedAtUtc, q.RevokedAtUtc, q.RevocationReason, q.IsActive); }
    private static bool TryParse(string value, out string? id, out string? token) { id = token = null; if (!Uri.TryCreate(value, UriKind.Absolute, out var u)) return false; var parts = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries); if (parts.Length != 2 || parts[0] != "c") return false; id = parts[1]; token = System.Web.HttpUtility.ParseQueryString(u.Query)["t"]; return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(token); }
    private async Task<MerchantQrValidationResult> Fail(string code, string message, Guid merchant, Guid actor, Guid location, DateTime now, CancellationToken ct) { var eventType = code switch { "TamperedQr" => "CreatorQrTampered", "UnauthorizedMerchantAccess" => "CreatorQrCrossMerchantAttempt", "LocationNotAllowed" or "InactiveLocation" => "CreatorQrInvalidLocationAttempt", _ => "CreatorQrValidationFailed" }; AddMerchantAudit(merchant, actor, eventType, $"Code={code};LocationId={location}"); await db.SaveChangesAsync(ct); return new(false, code, message, null, null, null, merchant, location, now); }
    private void AddCreatorAudit(Guid creator, Guid actor, string type, string? detail) => db.CreatorAuditEvents.Add(new() { Id = Guid.NewGuid(), CreatorId = creator, ActorUserAccountId = actor, EventType = type, Detail = detail, CreatedAtUtc = DateTime.UtcNow, CreatedBy = actor.ToString() });
    private void AddMerchantAudit(Guid merchant, Guid actor, string type, string? detail) => db.MerchantAuditEvents.Add(new() { Id = Guid.NewGuid(), MerchantId = merchant, ActorUserAccountId = actor, EventType = type, Detail = detail, CreatedAtUtc = DateTime.UtcNow, CreatedBy = actor.ToString() });
}
