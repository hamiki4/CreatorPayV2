using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPay.Infrastructure.Notifications;

public sealed class FirebaseMessagingOptions
{
    public const string SectionName = "FirebaseMessaging";
    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = "";
    public string ServiceAccountPath { get; set; } = "";
}

public sealed class FirebaseMessagingProvider : IPushNotificationProvider, IDisposable
{
    private readonly FirebaseApp? app;
    private readonly IDataProtector protector;
    private readonly ILogger<FirebaseMessagingProvider> logger;
    public FirebaseMessagingProvider(IOptions<FirebaseMessagingOptions> configured, IDataProtectionProvider protection, ILogger<FirebaseMessagingProvider> logger)
    {
        var options = configured.Value; this.logger = logger; protector = protection.CreateProtector("Weymela.PushDeviceToken.v1");
        if (!options.Enabled) return;
        if (string.IsNullOrWhiteSpace(options.ProjectId) || string.IsNullOrWhiteSpace(options.ServiceAccountPath)) throw new InvalidOperationException("FirebaseMessaging is enabled but ProjectId or ServiceAccountPath is missing.");
        app = FirebaseApp.Create(new AppOptions { Credential = CredentialFactory.FromFile<ServiceAccountCredential>(options.ServiceAccountPath).ToGoogleCredential(), ProjectId = options.ProjectId }, $"weymela-fcm-{Guid.NewGuid():N}");
    }
    public async Task<NotificationProviderResult> SendAsync(ProviderNotification notification, CancellationToken ct)
    {
        if (app is null) return new(false, null, DeliveryAttemptStatus.Failed, "FCM_NOT_CONFIGURED", "Firebase messaging is not configured.", false);
        if (string.IsNullOrWhiteSpace(notification.DestinationReference)) return new(false, null, DeliveryAttemptStatus.Failed, "FCM_DEVICE_MISSING", "Push device is unavailable.", false);
        try
        {
            var token = protector.Unprotect(notification.DestinationReference);
            var (title, body) = SafeLockScreenContent(notification.Type);
            var data = new Dictionary<string, string> { ["notificationId"] = notification.PublicNotificationId, ["type"] = notification.Type.ToString() };
            if (notification.Data.TryGetValue("TargetPath", out var target) && target.StartsWith('/')) data["targetPath"] = target;
#pragma warning disable CS0618 // Firebase Admin retains Token delivery for FCM registration tokens.
            var id = await FirebaseMessaging.GetMessaging(app).SendAsync(CreateMessage(token, title, body, data), ct);
#pragma warning restore CS0618
            return new(true, id, DeliveryAttemptStatus.Submitted);
        }
        catch (Exception ex)
        {
            var detail = ex.Message ?? "Firebase messaging failed.";
            var invalid = detail.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase) || detail.Contains("invalid registration token", StringComparison.OrdinalIgnoreCase) || detail.Contains("not registered", StringComparison.OrdinalIgnoreCase);
            logger.LogWarning(ex, "FCM delivery failed with classification {Classification}; notification token was not logged.", invalid ? "invalid-token" : "provider-failure");
            return new(false, null, DeliveryAttemptStatus.Failed, invalid ? "FCM_TOKEN_INVALID" : "FCM_PROVIDER_FAILURE", invalid ? "The push device is no longer registered." : "Firebase messaging delivery failed.", !invalid);
        }
    }
    internal static Message CreateMessage(string token, string title, string body, IReadOnlyDictionary<string, string> data)
    {
#pragma warning disable CS0618 // Firebase Admin retains Token delivery for FCM registration tokens.
        return new Message
        {
            Token = token,
            Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
            Data = new Dictionary<string, string>(data),
            Android = new AndroidConfig { Priority = Priority.High }
        };
#pragma warning restore CS0618
    }
    public static (string Title, string Body) SafeLockScreenContent(NotificationType type) => type == NotificationType.PasswordResetRequested
        ? ("Weymela security", "Password reset requested. Open Weymela to continue.")
        : ("Weymela", "You have a new notification. Open Weymela to view it.");
    public void Dispose() { app?.Delete(); }
}
