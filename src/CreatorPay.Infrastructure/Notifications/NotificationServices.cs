using System.Text.Json;
using System.Text.RegularExpressions;
using CreatorPay.Application.Notifications;
using CreatorPay.Application.Wallet;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CreatorPay.Infrastructure.Eligibility;

namespace CreatorPay.Infrastructure.Notifications;

public sealed partial class SafeNotificationTemplateRenderer : INotificationTemplateRenderer
{
    [GeneratedRegex("\\{([A-Za-z][A-Za-z0-9]*)\\}")] private static partial Regex Placeholder();
    public void Validate(string template, IReadOnlyCollection<string> allowed) { if (template.Length > 4000) throw new ArgumentException("Template is too long."); foreach (Match m in Placeholder().Matches(template)) if (!allowed.Contains(m.Groups[1].Value, StringComparer.Ordinal)) throw new ArgumentException($"Unknown placeholder '{m.Groups[1].Value}'."); if (template.Contains("{{") || template.Contains("{%") || template.Contains("<script", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Unsafe template syntax is not allowed."); }
    public string Render(string template, IReadOnlyDictionary<string, string> values, IReadOnlyCollection<string> allowed) { Validate(template, allowed); return Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : throw new ArgumentException($"A value for '{m.Groups[1].Value}' is required.")); }
}
public sealed class DevelopmentNotificationProvider : IEmailNotificationProvider, ISmsNotificationProvider, IPushNotificationProvider, IInAppNotificationProvider
{
    public Task<NotificationProviderResult> SendAsync(ProviderNotification n, CancellationToken ct) => Task.FromResult(new NotificationProviderResult(true, $"dev-{Guid.NewGuid():N}", DeliveryAttemptStatus.Delivered));
}
public sealed class NotificationDispatcher(IEmailNotificationProvider email, ISmsNotificationProvider sms, IPushNotificationProvider push, IInAppNotificationProvider inApp) : INotificationDispatcher
{
    public Task<NotificationProviderResult> DispatchAsync(NotificationChannel c, ProviderNotification n, CancellationToken ct) => c switch { NotificationChannel.Email => email.SendAsync(n, ct), NotificationChannel.Sms => sms.SendAsync(n, ct), NotificationChannel.Push => push.SendAsync(n, ct), NotificationChannel.InApp => inApp.SendAsync(n, ct), _ => Task.FromResult(new NotificationProviderResult(false, null, DeliveryAttemptStatus.Failed, "PROVIDER_NOT_IMPLEMENTED", "This channel is reserved for future use.", false)) };
}
public sealed class NotificationService(ApplicationDbContext db, IOptions<WalletOptions> walletConfigured) : INotificationService
{
    readonly WalletOptions walletOptions = walletConfigured.Value;
    static readonly HashSet<NotificationType> Mandatory = [NotificationType.SecurityAlert, NotificationType.PasswordResetRequested, NotificationType.CustomerVerificationCode, NotificationType.PurchaseConfirmed, NotificationType.PayoutPaid, NotificationType.PayoutFailed, NotificationType.SystemOperationalAlert];
    public async Task<Guid> CreateAsync(CreateNotificationRequest r, CancellationToken ct)
    {
        var prior = await db.Notifications.Where(x => x.IdempotencyKey == r.IdempotencyKey).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct); if (prior.HasValue) return prior.Value; var now = DateTime.UtcNow; var data = JsonSerializer.Serialize(r.Data); var n = new Notification { Id = Guid.NewGuid(), PublicNotificationId = $"NTF-{Guid.NewGuid():N}", IdempotencyKey = r.IdempotencyKey, NotificationType = r.Type, Title = r.Data.GetValueOrDefault("Title", Humanize(r.Type)), Body = r.Data.GetValueOrDefault("Body", Humanize(r.Type)), DataJson = data, Priority = r.Priority, Status = NotificationStatus.Pending, ScheduledAtUtc = now, ExpiresAtUtc = r.ExpiresAtUtc, CorrelationId = r.CorrelationId, RelatedEntityType = r.RelatedEntityType, RelatedEntityId = r.RelatedEntityId, CreatedAtUtc = now };
        foreach (var recipient in r.Recipients.DistinctBy(x => new { x.UserAccountId, x.Channel, x.DestinationReference })) n.Recipients.Add(new() { Id = Guid.NewGuid(), UserAccountId = recipient.UserAccountId, RecipientType = recipient.RecipientType, Channel = recipient.Channel, DestinationReference = recipient.DestinationReference, MaskedDestination = recipient.MaskedDestination, LanguageCode = recipient.LanguageCode, Status = NotificationRecipientStatus.Pending, CreatedAtUtc = now });
        await AddPushRecipientsAsync(n, now, ct);
        db.Notifications.Add(n); db.NotificationOutboxMessages.Add(new() { Id = Guid.NewGuid(), NotificationId = n.Id, Status = NotificationOutboxStatus.Pending, AvailableAtUtc = now, CorrelationId = r.CorrelationId, CreatedAtUtc = now }); Audit(n.Id, "NotificationCreated", null); Audit(n.Id, "OutboxMessageCreated", null);
        if (r.Type == NotificationType.CustomerCashbackEarned && Guid.TryParse(r.RelatedEntityId, out var purchaseId)) await AddLowBalanceNotificationAsync(purchaseId, now, ct);
        await db.SaveChangesAsync(ct); return n.Id;
    }
    async Task AddLowBalanceNotificationAsync(Guid purchaseId, DateTime now, CancellationToken ct)
    {
        var entry = await db.MerchantWalletEntries.AsNoTracking().Where(x => x.RelatedTransactionId == purchaseId && x.EntryType == MerchantWalletEntryType.CommissionDebit).SingleOrDefaultAsync(ct);
        var minimum = await RewardEligibilityQueries.CurrentMinimumAsync(db, walletOptions.CurrencyCode, ct);
        if (entry is null || minimum <= 0m || entry.BalanceBefore < minimum || entry.BalanceAfter >= minimum) return;
        var key = $"merchant-low-balance:{entry.Id}";
        if (await db.Notifications.AnyAsync(x => x.IdempotencyKey == key, ct)) return;
        var recipients = await db.UserAccounts.AsNoTracking().Where(x => x.MerchantId == entry.MerchantId && x.Role == UserRole.MerchantAdmin && x.Status == AccountStatus.Active).Select(x => x.Id).ToListAsync(ct);
        if (recipients.Count == 0) return;
        var n = new Notification { Id = Guid.NewGuid(), PublicNotificationId = $"NTF-{Guid.NewGuid():N}", IdempotencyKey = key, NotificationType = NotificationType.MerchantWalletLowBalance, Title = "Business balance is low", Body = $"Your available balance is {entry.BalanceAfter:0.00} ETB. Please add funds.", DataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["TargetPath"] = "/?view=wallet" }), Priority = NotificationPriority.High, Status = NotificationStatus.Pending, ScheduledAtUtc = now, RelatedEntityType = "MerchantWallet", RelatedEntityId = entry.MerchantId.ToString(), CreatedAtUtc = now };
        foreach (var id in recipients) n.Recipients.Add(new NotificationRecipient { Id = Guid.NewGuid(), UserAccountId = id, RecipientType = NotificationRecipientType.User, Channel = NotificationChannel.InApp, Status = NotificationRecipientStatus.Pending, CreatedAtUtc = now });
        await AddPushRecipientsAsync(n, now, ct); db.Notifications.Add(n); db.NotificationOutboxMessages.Add(new NotificationOutboxMessage { Id = Guid.NewGuid(), NotificationId = n.Id, Status = NotificationOutboxStatus.Pending, AvailableAtUtc = now, CreatedAtUtc = now }); Audit(n.Id, "NotificationCreated", null);
    }
    async Task AddPushRecipientsAsync(Notification notification, DateTime now, CancellationToken ct)
    {
        var userIds = notification.Recipients.Where(x => x.Channel == NotificationChannel.InApp && x.UserAccountId.HasValue).Select(x => x.UserAccountId!.Value).Distinct().ToArray();
        if (userIds.Length == 0) return;
        var optedIn = await db.NotificationPreferences.AsNoTracking().Where(x => userIds.Contains(x.UserAccountId) && x.NotificationType == notification.NotificationType && x.PushEnabled).Select(x => x.UserAccountId).ToListAsync(ct);
        if (optedIn.Count == 0) return;
        var devices = await db.PushDeviceRegistrations.AsNoTracking().Where(x => optedIn.Contains(x.UserAccountId) && x.IsActive).ToListAsync(ct);
        foreach (var device in devices)
            notification.Recipients.Add(new NotificationRecipient { Id = Guid.NewGuid(), UserAccountId = device.UserAccountId, RecipientType = NotificationRecipientType.User, Channel = NotificationChannel.Push, DestinationReference = device.ProtectedToken, MaskedDestination = $"{device.Platform} device", PushDeviceRegistrationId = device.Id, Status = NotificationRecipientStatus.Pending, CreatedAtUtc = now });
    }
    public async Task<(IReadOnlyList<NotificationListItem> Items, int Total)> ListAsync(Guid uid, int page, int size, string? status, string? type, DateTime? from, DateTime? to, CancellationToken ct) { page = Math.Max(1, page); size = Math.Clamp(size, 1, 100); var q = db.NotificationRecipients.AsNoTracking().Include(x => x.Notification).Where(x => x.UserAccountId == uid && x.Channel == NotificationChannel.InApp); if (Enum.TryParse<NotificationRecipientStatus>(status, true, out var s)) q = q.Where(x => x.Status == s); if (Enum.TryParse<NotificationType>(type, true, out var t)) q = q.Where(x => x.Notification.NotificationType == t); if (from.HasValue) q = q.Where(x => x.CreatedAtUtc >= from); if (to.HasValue) q = q.Where(x => x.CreatedAtUtc <= to); var total = await q.CountAsync(ct); var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * size).Take(size).ToListAsync(ct); return (rows.Select(Map).ToList(), total); }
    public async Task<NotificationListItem?> GetAsync(Guid uid, string id, CancellationToken ct) { var x = await db.NotificationRecipients.AsNoTracking().Include(x => x.Notification).SingleOrDefaultAsync(x => x.UserAccountId == uid && x.Channel == NotificationChannel.InApp && x.Notification.PublicNotificationId == id, ct); return x is null ? null : Map(x); }
    public Task<int> UnreadCountAsync(Guid uid, CancellationToken ct) => db.NotificationRecipients.CountAsync(x => x.UserAccountId == uid && x.Channel == NotificationChannel.InApp && !x.ReadAtUtc.HasValue && x.Status != NotificationRecipientStatus.Cancelled, ct);
    public async Task<bool> MarkReadAsync(Guid uid, string id, CancellationToken ct) { var x = await db.NotificationRecipients.Include(x => x.Notification).SingleOrDefaultAsync(x => x.UserAccountId == uid && x.Channel == NotificationChannel.InApp && x.Notification.PublicNotificationId == id, ct); if (x is null) return false; if (!x.ReadAtUtc.HasValue) { x.ReadAtUtc = DateTime.UtcNow; x.Status = NotificationRecipientStatus.Read; Audit(x.NotificationId, "NotificationMarkedRead", uid); await db.SaveChangesAsync(ct); } return true; }
    public async Task<int> MarkAllReadAsync(Guid uid, CancellationToken ct) { var rows = await db.NotificationRecipients.Where(x => x.UserAccountId == uid && x.Channel == NotificationChannel.InApp && !x.ReadAtUtc.HasValue).ToListAsync(ct); foreach (var x in rows) { x.ReadAtUtc = DateTime.UtcNow; x.Status = NotificationRecipientStatus.Read; } if (rows.Count > 0) { db.NotificationAuditEvents.Add(new() { Id = Guid.NewGuid(), ActorUserId = uid, EventType = "NotificationsMarkedRead", SafeDetail = $"Count={rows.Count}", CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); } return rows.Count; }
    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(Guid uid, CancellationToken ct) { var saved = await db.NotificationPreferences.Where(x => x.UserAccountId == uid).ToDictionaryAsync(x => x.NotificationType, ct); return Enum.GetValues<NotificationType>().Select(t => saved.TryGetValue(t, out var p) ? new(t, p.InAppEnabled, p.EmailEnabled, p.SmsEnabled, false, p.LanguageCode, Mandatory.Contains(t)) : new NotificationPreferenceDto(t, true, true, true, false, "en", Mandatory.Contains(t))).ToList(); }
    public async Task UpdatePreferencesAsync(Guid uid, IReadOnlyList<UpdateNotificationPreference> input, CancellationToken ct) { foreach (var p in input) { if (p.LanguageCode is not ("en" or "am")) throw new ArgumentException("Language must be 'en' or 'am'."); var mandatory = Mandatory.Contains(p.NotificationType); var x = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserAccountId == uid && x.NotificationType == p.NotificationType, ct); if (x is null) { x = new() { Id = Guid.NewGuid(), UserAccountId = uid, NotificationType = p.NotificationType, CreatedAtUtc = DateTime.UtcNow }; db.Add(x); } x.InAppEnabled = mandatory || p.InAppEnabled; x.EmailEnabled = mandatory || p.EmailEnabled; x.SmsEnabled = p.SmsEnabled; x.PushEnabled = p.PushEnabled; x.LanguageCode = p.LanguageCode; x.UpdatedAtUtc = DateTime.UtcNow; } db.NotificationAuditEvents.Add(new() { Id = Guid.NewGuid(), ActorUserId = uid, EventType = "PreferenceUpdated", SafeDetail = $"Count={input.Count}", CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); }
    void Audit(Guid id, string type, Guid? actor) => db.NotificationAuditEvents.Add(new() { Id = Guid.NewGuid(), NotificationId = id, ActorUserId = actor, EventType = type, CreatedAtUtc = DateTime.UtcNow });
    static NotificationListItem Map(NotificationRecipient x) => new(x.Notification.PublicNotificationId, x.Notification.NotificationType, x.Notification.Title, x.Notification.Body, x.Notification.Priority, x.Status, x.Notification.CreatedAtUtc, x.ReadAtUtc, JsonSerializer.Deserialize<Dictionary<string, string>>(x.Notification.DataJson) ?? []);
    static string Humanize(NotificationType t) => Regex.Replace(t.ToString(), "([a-z])([A-Z])", "$1 $2");
}
