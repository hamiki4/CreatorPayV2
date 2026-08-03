using System.ComponentModel.DataAnnotations;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Creators;

public sealed class CreatorService(ICreatorStore store, IPasswordHasher passwords, ITokenService tokens, IUtcClock clock,
    ICreatorVerificationProvider provider, PasswordPolicyValidator passwordPolicy, IOptions<CreatorVerificationOptions> options) : ICreatorService
{
    public Task<CreatorResult<CreatorRegistrationResponse>> RegisterAsync(RegisterCreatorRequest request, CancellationToken ct)
    {
        var error = ValidateIdentity(request.FirstName, request.LastName, request.DisplayName, request.Email, request.PhoneNumber);
        if (error is not null) return Task.FromResult(CreatorResult<CreatorRegistrationResponse>.Failure(error));
        var passwordErrors = passwordPolicy.Validate(request.Password);
        if (passwordErrors.Count > 0) return Task.FromResult(CreatorResult<CreatorRegistrationResponse>.Failure(string.Join(" ", passwordErrors)));
        return RegisterValidatedAsync(request, ct);
    }

    private Task<CreatorResult<CreatorRegistrationResponse>> RegisterValidatedAsync(RegisterCreatorRequest request, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var normalizedEmail = NormalizeEmail(request.Email); var normalizedPhone = NormalizePhone(request.PhoneNumber);
        if (await store.EmailExistsAsync(normalizedEmail, null, innerCt)) return CreatorResult<CreatorRegistrationResponse>.Failure("Email is already registered.");
        if (await store.PhoneExistsAsync(normalizedPhone, null, innerCt)) return CreatorResult<CreatorRegistrationResponse>.Failure("Phone number is already registered.");
        var now = clock.UtcNow; var creatorId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var creator = new Creator { Id = creatorId, PublicCreatorId = $"CR-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DisplayName = request.DisplayName.Trim(), PhoneNumber = FormatPhone(normalizedPhone), NormalizedPhoneNumber = normalizedPhone, Email = request.Email.Trim(), Status = CreatorStatus.Draft, CreatedAtUtc = now, CreatedBy = userId.ToString() };
        var user = new UserAccount { Id = userId, Email = request.Email.Trim(), NormalizedEmail = normalizedEmail, Role = UserRole.Creator, Status = AccountStatus.PendingVerification, CreatorId = creatorId, CreatedAtUtc = now, CreatedBy = userId.ToString() };
        user.PasswordHash = passwords.Hash(user, request.Password); store.Add(creator); store.Add(user);
        var emailToken = Issue(userId, "Email", now); var phoneToken = Issue(userId, "Phone", now); store.Add(emailToken.Item); store.Add(phoneToken.Item);
        Audit(creatorId, userId, "Registration", null, now); await store.SaveAsync(innerCt);
        await provider.SendEmailVerificationAsync(user, emailToken.Raw, innerCt); await provider.SendPhoneVerificationAsync(creator, phoneToken.Raw, innerCt);
        return CreatorResult<CreatorRegistrationResponse>.Success(new(creatorId, creator.PublicCreatorId, "Registration received. Complete email and phone verification before platform review."));
    }, ct);

    public Task<CreatorResult> VerifyEmailAsync(string token, CancellationToken ct) => VerifyAsync(token, "Email", ct);
    public Task<CreatorResult> VerifyPhoneAsync(string token, CancellationToken ct) => VerifyAsync(token, "Phone", ct);

    private Task<CreatorResult> VerifyAsync(string raw, string purpose, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        if (string.IsNullOrWhiteSpace(raw)) return CreatorResult.Failure("Invalid or expired verification token.");
        var hash = tokens.HashToken(raw); var item = await store.FindTokenAsync(hash, purpose, innerCt); var now = clock.UtcNow;
        if (item is null || item.UsedAtUtc is not null || item.ExpiresAtUtc <= now) return CreatorResult.Failure("Invalid or expired verification token.");
        var pair = await store.FindByTokenAsync(hash, purpose, innerCt);
        if (pair is null) return CreatorResult.Failure("Invalid or expired verification token.");
        item.UsedAtUtc = now;
        if (purpose == "Email") pair.Value.User.IsEmailVerified = true; else pair.Value.User.IsPhoneVerified = true;
        if (pair.Value.Creator.Status == CreatorStatus.Draft) pair.Value.Creator.Status = CreatorStatus.PendingVerification;
        if (pair.Value.User.IsEmailVerified && pair.Value.User.IsPhoneVerified)
        {
            pair.Value.User.Status = AccountStatus.PendingApproval; pair.Value.Creator.Status = CreatorStatus.PendingApproval;
        }
        Audit(pair.Value.Creator.Id, pair.Value.User.Id, $"{purpose}Verification", null, now); await store.SaveAsync(innerCt);
        return CreatorResult.Success();
    }, ct);

    public async Task<CreatorResult<CreatorProfileResponse>> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var pair = await store.FindByUserAsync(userId, ct);
        return pair is null ? CreatorResult<CreatorProfileResponse>.Failure("Creator not found.") : CreatorResult<CreatorProfileResponse>.Success(ToProfile(pair.Value.User, pair.Value.Creator));
    }

    public Task<CreatorResult<CreatorProfileResponse>> UpdateMeAsync(Guid userId, UpdateCreatorProfileRequest request, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var pair = await store.FindByUserAsync(userId, innerCt); if (pair is null) return CreatorResult<CreatorProfileResponse>.Failure("Creator not found.");
        var error = ValidateIdentity(request.FirstName, request.LastName, request.DisplayName, request.Email, request.PhoneNumber);
        if (error is not null) return CreatorResult<CreatorProfileResponse>.Failure(error);
        if (request.ProfileImage is { } image && (image.SizeBytes is <= 0 or > 5_000_000 || image.FileName.Trim().Length is 0 or > 255 || image.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))) return CreatorResult<CreatorProfileResponse>.Failure("Profile image metadata is invalid.");
        var normalizedEmail = NormalizeEmail(request.Email); var normalizedPhone = NormalizePhone(request.PhoneNumber);
        if (await store.EmailExistsAsync(normalizedEmail, userId, innerCt)) return CreatorResult<CreatorProfileResponse>.Failure("Email is already registered.");
        if (await store.PhoneExistsAsync(normalizedPhone, pair.Value.Creator.Id, innerCt)) return CreatorResult<CreatorProfileResponse>.Failure("Phone number is already registered.");
        var now = clock.UtcNow; var emailChanged = pair.Value.User.NormalizedEmail != normalizedEmail; var phoneChanged = pair.Value.Creator.NormalizedPhoneNumber != normalizedPhone;
        pair.Value.Creator.FirstName = request.FirstName.Trim(); pair.Value.Creator.LastName = request.LastName.Trim(); pair.Value.Creator.DisplayName = request.DisplayName.Trim(); pair.Value.Creator.Email = request.Email.Trim(); pair.Value.Creator.PhoneNumber = FormatPhone(normalizedPhone); pair.Value.Creator.NormalizedPhoneNumber = normalizedPhone;
        pair.Value.User.Email = request.Email.Trim(); pair.Value.User.NormalizedEmail = normalizedEmail; pair.Value.Creator.UpdatedAtUtc = now; pair.Value.Creator.UpdatedBy = userId.ToString(); pair.Value.User.UpdatedAtUtc = now; pair.Value.User.UpdatedBy = userId.ToString();
        if (request.ProfileImage is { } metadata) { pair.Value.Creator.ProfileImageFileName = metadata.FileName.Trim(); pair.Value.Creator.ProfileImageContentType = metadata.ContentType; pair.Value.Creator.ProfileImageSizeBytes = metadata.SizeBytes; }
        if (emailChanged) { pair.Value.User.IsEmailVerified = false; await store.InvalidateTokensAsync(userId, "Email", now, innerCt); var issued = Issue(userId, "Email", now); store.Add(issued.Item); await provider.SendEmailVerificationAsync(pair.Value.User, issued.Raw, innerCt); }
        if (phoneChanged) { pair.Value.User.IsPhoneVerified = false; await store.InvalidateTokensAsync(userId, "Phone", now, innerCt); var issued = Issue(userId, "Phone", now); store.Add(issued.Item); await provider.SendPhoneVerificationAsync(pair.Value.Creator, issued.Raw, innerCt); }
        if (emailChanged || phoneChanged) { pair.Value.User.Status = AccountStatus.PendingVerification; pair.Value.Creator.Status = CreatorStatus.PendingVerification; }
        Audit(pair.Value.Creator.Id, userId, "ProfileUpdate", $"EmailChanged={emailChanged};PhoneChanged={phoneChanged}", now); await store.SaveAsync(innerCt);
        return CreatorResult<CreatorProfileResponse>.Success(ToProfile(pair.Value.User, pair.Value.Creator));
    }, ct);

    public async Task<IReadOnlyList<PendingCreatorResponse>> GetPendingAsync(CancellationToken ct) => (await store.FindByStatusAsync(CreatorStatus.PendingApproval, ct)).Select(x => new PendingCreatorResponse(x.Id, x.PublicCreatorId, x.DisplayName, x.Email, x.CreatedAtUtc)).ToArray();
    public async Task<CreatorResult<CreatorProfileResponse>> GetAsync(Guid creatorId, CancellationToken ct) { var c = await store.FindCreatorAsync(creatorId, ct); if (c is null) return CreatorResult<CreatorProfileResponse>.Failure("Creator not found."); var u = await FindCreatorUser(c, ct); return u is null ? CreatorResult<CreatorProfileResponse>.Failure("Creator not found.") : CreatorResult<CreatorProfileResponse>.Success(ToProfile(u, c)); }
    public Task<CreatorResult> ApproveAsync(Guid id, Guid admin, CancellationToken ct) => DecideAsync(id, admin, null, "Approval", (c, now, actor) => c.Approve(now, actor), CreatorStatus.PendingApproval, AccountStatus.Active, ct);
    public Task<CreatorResult> RejectAsync(Guid id, Guid admin, string? reason, CancellationToken ct) => DecideAsync(id, admin, reason, "Rejection", (c, now, actor) => c.Reject(now, actor.ToString()), CreatorStatus.PendingApproval, AccountStatus.Rejected, ct, true);
    public Task<CreatorResult> SuspendAsync(Guid id, Guid admin, string? reason, CancellationToken ct) => DecideAsync(id, admin, reason, "Suspension", (c, now, actor) => c.Suspend(now, actor.ToString()), CreatorStatus.Active, AccountStatus.Suspended, ct, true);
    public Task<CreatorResult> ReactivateAsync(Guid id, Guid admin, string? reason, CancellationToken ct) => DecideAsync(id, admin, reason, "Reactivation", (c, now, actor) => c.Reactivate(now, actor.ToString()), CreatorStatus.Suspended, AccountStatus.Active, ct);

    private Task<CreatorResult> DecideAsync(Guid id, Guid admin, string? reason, string eventType, Action<Creator, DateTime, Guid> transition, CreatorStatus required, AccountStatus accountStatus, CancellationToken ct, bool reasonRequired = false) => store.InTransactionAsync(async innerCt =>
    {
        if (reasonRequired && string.IsNullOrWhiteSpace(reason)) return CreatorResult.Failure("A reason is required.");
        var creator = await store.FindCreatorAsync(id, innerCt); if (creator is null) return CreatorResult.Failure("Creator not found.");
        if (creator.Status != required) return CreatorResult.Failure($"Creator must be {required} for this action.");
        var user = await FindCreatorUser(creator, innerCt); if (user is null) return CreatorResult.Failure("Creator account not found.");
        if (eventType == "Approval" && (!user.IsEmailVerified || !user.IsPhoneVerified)) return CreatorResult.Failure("Email and phone must be verified before approval.");
        var now = clock.UtcNow; transition(creator, now, admin); user.Status = accountStatus; user.UpdatedAtUtc = now; user.UpdatedBy = admin.ToString(); Audit(id, admin, eventType, reason?.Trim(), now); await store.SaveAsync(innerCt); return CreatorResult.Success();
    }, ct);

    private Task<UserAccount?> FindCreatorUser(Creator creator, CancellationToken ct) => store.FindUserByCreatorAsync(creator.Id, ct);
    private (CreatorVerificationToken Item, string Raw) Issue(Guid userId, string purpose, DateTime now) { var raw = tokens.CreateOpaqueToken(); return (new CreatorVerificationToken { Id = Guid.NewGuid(), UserAccountId = userId, Purpose = purpose, TokenHash = tokens.HashToken(raw), ExpiresAtUtc = now.AddMinutes(options.Value.TokenLifetimeMinutes), CreatedAtUtc = now }, raw); }
    private void Audit(Guid creatorId, Guid? actor, string type, string? detail, DateTime now) => store.Add(new CreatorAuditEvent { Id = Guid.NewGuid(), CreatorId = creatorId, ActorUserAccountId = actor, EventType = type, Detail = detail, CreatedAtUtc = now, CreatedBy = actor?.ToString() });
    private static string? ValidateIdentity(string first, string last, string display, string email, string phone) { if (string.IsNullOrWhiteSpace(first) || first.Trim().Length > 100) return "First name is required and must not exceed 100 characters."; if (string.IsNullOrWhiteSpace(last) || last.Trim().Length > 100) return "Last name is required and must not exceed 100 characters."; if (string.IsNullOrWhiteSpace(display) || display.Trim().Length is < 2 or > 200) return "Display name must be between 2 and 200 characters."; if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email.Trim()) || email.Trim().Length > 320) return "Email is invalid."; if (string.IsNullOrWhiteSpace(phone)) return "Phone number must contain 8 to 15 digits."; var normalized = NormalizePhone(phone); if (normalized.Length is < 8 or > 15) return "Phone number must contain 8 to 15 digits."; return null; }
    internal static string NormalizeEmail(string value) => value.Trim().ToUpperInvariant();
    internal static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string FormatPhone(string normalized) => $"+{normalized}";
    private static CreatorProfileResponse ToProfile(UserAccount user, Creator c) => new(c.Id, c.PublicCreatorId, c.FirstName, c.LastName, c.DisplayName, c.PhoneNumber, user.Email, user.IsEmailVerified, user.IsPhoneVerified, user.Status, c.Status, c.ProfileImageFileName is null ? null : new(c.ProfileImageFileName, c.ProfileImageContentType!, c.ProfileImageSizeBytes!.Value), Next(user, c));
    private static string Next(UserAccount u, Creator c) => !u.IsEmailVerified ? "Verify your email address." : !u.IsPhoneVerified ? "Verify your phone number." : c.Status == CreatorStatus.PendingApproval ? "Your profile is awaiting platform approval." : c.Status == CreatorStatus.Active ? "Your creator account is active." : c.Status == CreatorStatus.Rejected ? "Your application was rejected. Contact platform support." : c.Status == CreatorStatus.Suspended ? "Your creator account is suspended. Contact platform support." : "Complete creator onboarding.";
}
