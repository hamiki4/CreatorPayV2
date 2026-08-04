using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class Notification : Entity
{
    public string PublicNotificationId { get; set; } = ""; public string IdempotencyKey { get; set; } = "";
    public NotificationType NotificationType { get; set; }
    public string Title { get; set; } = ""; public string Body { get; set; } = "";
    public string DataJson { get; set; } = "{}"; public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public NotificationStatus Status { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? CorrelationId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public List<NotificationRecipient> Recipients { get; set; } = [];
}
public sealed class NotificationRecipient : Entity
{
    public Guid NotificationId { get; set; }
    public Guid? UserAccountId { get; set; }
    public NotificationRecipientType RecipientType { get; set; }
    public NotificationChannel Channel { get; set; }
    public string? DestinationReference { get; set; }
    public string? MaskedDestination { get; set; }
    public NotificationRecipientStatus Status { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string LanguageCode { get; set; } = "en";
    public Notification Notification { get; set; } = null!;
}
public sealed class NotificationTemplate : Entity
{
    public NotificationType NotificationType { get; set; }
    public NotificationChannel Channel { get; set; }
    public string LanguageCode { get; set; } = "en";
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = ""; public string AllowedPlaceholdersJson { get; set; } = "[]";
    public bool IsActive { get; set; }
    public int VersionNumber { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
public sealed class NotificationOutboxMessage : Entity
{
    public Guid NotificationId { get; set; }
    public NotificationOutboxStatus Status { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public string? LockedBy { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? CorrelationId { get; set; }
    public uint Version { get; set; }
    public Notification Notification { get; set; } = null!;
}
public sealed class NotificationDeliveryAttempt : Entity
{
    public Guid NotificationRecipientId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string ProviderName { get; set; } = "";
    public int AttemptNumber { get; set; }
    public DeliveryAttemptStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ProviderMessageReference { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsTransientFailure { get; set; }
}
public sealed class NotificationPreference : Entity
{
    public Guid UserAccountId { get; set; }
    public NotificationType NotificationType { get; set; }
    public bool InAppEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true; public bool SmsEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public string LanguageCode { get; set; } = "en";
}
public sealed class NotificationDeadLetter : Entity
{
    public Guid OutboxMessageId { get; set; }
    public NotificationDeadLetterStatus Status { get; set; }
    public string Reason { get; set; } = "";
    public DateTime DeadLetteredAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
}
public sealed class NotificationAuditEvent : Entity
{
    public Guid? NotificationId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string EventType { get; set; } = "";
    public string? SafeDetail { get; set; }
}
