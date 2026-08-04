using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications"; public int BatchSize { get; set; } = 25; public int PollIntervalSeconds { get; set; } = 10;
    public int MaximumAttempts { get; set; } = 5; public int InitialRetryDelaySeconds { get; set; } = 30; public int MaximumRetryDelayMinutes { get; set; } = 30;
    public int LockTimeoutMinutes { get; set; } = 5; public bool DevelopmentRevealVerificationCode { get; set; }
}
public sealed record NotificationRecipientRequest(Guid? UserAccountId, NotificationRecipientType RecipientType, NotificationChannel Channel, string? DestinationReference, string? MaskedDestination, string LanguageCode = "en");
public sealed record CreateNotificationRequest(NotificationType Type, string IdempotencyKey, IReadOnlyDictionary<string, string> Data, IReadOnlyList<NotificationRecipientRequest> Recipients, NotificationPriority Priority = NotificationPriority.Normal, string? CorrelationId = null, string? RelatedEntityType = null, string? RelatedEntityId = null, DateTime? ExpiresAtUtc = null);
public sealed record ProviderNotification(string PublicNotificationId, NotificationType Type, string? Subject, string Body, string? DestinationReference, string? MaskedDestination, IReadOnlyDictionary<string, string> Data);
public sealed record NotificationProviderResult(bool Accepted, string? ProviderReference, DeliveryAttemptStatus DeliveryStatus, string? ErrorCode = null, string? ErrorMessage = null, bool IsTransient = false);
public sealed record NotificationListItem(string NotificationId, NotificationType Type, string Title, string Body, NotificationPriority Priority, NotificationRecipientStatus Status, DateTime CreatedAtUtc, DateTime? ReadAtUtc, IReadOnlyDictionary<string, string> Data);
public sealed record NotificationPreferenceDto(NotificationType NotificationType, bool InAppEnabled, bool EmailEnabled, bool SmsEnabled, bool PushEnabled, string LanguageCode, bool IsMandatory);
public sealed record UpdateNotificationPreference(NotificationType NotificationType, bool InAppEnabled, bool EmailEnabled, bool SmsEnabled, bool PushEnabled, string LanguageCode);
public sealed record TemplateRequest(NotificationType NotificationType, NotificationChannel Channel, string LanguageCode, string? SubjectTemplate, string BodyTemplate, string[] AllowedPlaceholders, DateTime? EffectiveFromUtc = null, bool IsActive = true);

public interface IEmailNotificationProvider { Task<NotificationProviderResult> SendAsync(ProviderNotification notification, CancellationToken ct); }
public interface ISmsNotificationProvider { Task<NotificationProviderResult> SendAsync(ProviderNotification notification, CancellationToken ct); }
public interface IPushNotificationProvider { Task<NotificationProviderResult> SendAsync(ProviderNotification notification, CancellationToken ct); }
public interface IInAppNotificationProvider { Task<NotificationProviderResult> SendAsync(ProviderNotification notification, CancellationToken ct); }
public interface INotificationTemplateRenderer { string Render(string template, IReadOnlyDictionary<string, string> values, IReadOnlyCollection<string> allowedPlaceholders); void Validate(string template, IReadOnlyCollection<string> allowedPlaceholders); }
public interface INotificationDispatcher { Task<NotificationProviderResult> DispatchAsync(NotificationChannel channel, ProviderNotification notification, CancellationToken ct); }
public interface INotificationOutboxProcessor { Task<int> ProcessBatchAsync(string workerId, CancellationToken ct); }
public interface INotificationService
{
    Task<Guid> CreateAsync(CreateNotificationRequest request, CancellationToken ct);
    Task<(IReadOnlyList<NotificationListItem> Items, int Total)> ListAsync(Guid userId, int page, int pageSize, string? status, string? type, DateTime? from, DateTime? to, CancellationToken ct);
    Task<NotificationListItem?> GetAsync(Guid userId, string publicId, CancellationToken ct); Task<int> UnreadCountAsync(Guid userId, CancellationToken ct);
    Task<bool> MarkReadAsync(Guid userId, string publicId, CancellationToken ct); Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken ct); Task UpdatePreferencesAsync(Guid userId, IReadOnlyList<UpdateNotificationPreference> preferences, CancellationToken ct);
}
