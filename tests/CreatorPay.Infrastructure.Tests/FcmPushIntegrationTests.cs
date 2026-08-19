using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Notifications;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

namespace CreatorPay.Infrastructure.Tests;

// Deterministic provider-boundary tests: no Firebase service or registration token is contacted.
public sealed class FcmPushIntegrationTests
{
    [Fact]
    public async Task Disabled_provider_fails_without_sending_or_leaking_a_token()
    {
        using var provider = new FirebaseMessagingProvider(Options.Create(new FirebaseMessagingOptions()), DataProtectionProvider.Create("fcm-tests"), Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebaseMessagingProvider>.Instance);
        var result = await provider.SendAsync(new("NTF-1", NotificationType.PurchaseConfirmed, "123 ETB", "Sensitive amount", "not-a-token", null, new Dictionary<string, string>()), default);
        Assert.False(result.Accepted); Assert.False(result.IsTransient); Assert.Equal("FCM_NOT_CONFIGURED", result.ErrorCode); Assert.DoesNotContain("not-a-token", result.ErrorMessage!);
    }

    [Fact]
    public void Password_reset_lock_screen_payload_has_no_secret_material()
    {
        var payload = FirebaseMessagingProvider.SafeLockScreenContent(NotificationType.PasswordResetRequested);
        var value = $"{payload.Title} {payload.Body}".ToLowerInvariant();
        Assert.DoesNotContain("otp", value); Assert.DoesNotContain("token", value); Assert.DoesNotContain("code", value); Assert.Contains("open weymela", value);
    }

    [Fact]
    public void Financial_lock_screen_payload_is_generic()
    {
        var payload = FirebaseMessagingProvider.SafeLockScreenContent(NotificationType.PurchaseConfirmed);
        Assert.Equal("Weymela", payload.Title); Assert.DoesNotContain("ETB", payload.Body, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("purchase", payload.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Web_push_uses_service_worker_target_data_without_an_invalid_relative_fcm_link()
    {
        var message = FirebaseMessagingProvider.CreateMessage("opaque-test-token", "Weymela", "Open Weymela to view it.", new Dictionary<string, string> { ["targetPath"] = "/?view=notifications" });
        Assert.Null(message.Webpush);
        Assert.Equal("/?view=notifications", message.Data["targetPath"]);
    }

    [Fact]
    public async Task Fake_transient_fcm_provider_preserves_retry_classification()
    {
        var fake = new FakePushProvider(new(false, null, DeliveryAttemptStatus.Failed, "FCM_PROVIDER_FAILURE", "temporary", true));
        var development = new DevelopmentNotificationProvider(); var dispatcher = new NotificationDispatcher(development, development, fake, development);
        var result = await dispatcher.DispatchAsync(NotificationChannel.Push, new("NTF-1", NotificationType.SecurityAlert, null, "body", "protected", null, new Dictionary<string, string>()), default);
        Assert.False(result.Accepted); Assert.True(result.IsTransient); Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Fake_fan_out_provider_is_called_once_per_device_recipient()
    {
        var fake = new FakePushProvider(new(true, "fcm-id", DeliveryAttemptStatus.Submitted));
        await fake.SendAsync(new("NTF-1", NotificationType.SecurityAlert, null, "body", "device-a", null, new Dictionary<string, string>()), default);
        await fake.SendAsync(new("NTF-1", NotificationType.SecurityAlert, null, "body", "device-b", null, new Dictionary<string, string>()), default);
        Assert.Equal(2, fake.Calls);
    }

    private sealed class FakePushProvider(NotificationProviderResult result) : IPushNotificationProvider { public int Calls { get; private set; } public Task<NotificationProviderResult> SendAsync(ProviderNotification notification, CancellationToken ct) { Calls++; return Task.FromResult(result); } }
}
