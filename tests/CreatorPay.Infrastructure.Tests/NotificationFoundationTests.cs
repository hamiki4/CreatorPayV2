using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Notifications;

namespace CreatorPay.Infrastructure.Tests;

public sealed class NotificationFoundationTests
{
    [Fact] public void Renderer_replaces_only_explicitly_allowed_placeholders() { var renderer = new SafeNotificationTemplateRenderer(); var result = renderer.Render("Hello {CreatorDisplayName}", new Dictionary<string, string> { { "CreatorDisplayName", "Aster" } }, ["CreatorDisplayName"]); Assert.Equal("Hello Aster", result); }
    [Fact] public void Renderer_rejects_unknown_placeholder() { var renderer = new SafeNotificationTemplateRenderer(); Assert.Throws<ArgumentException>(() => renderer.Validate("Code: {Unknown}", ["VerificationCode"])); }
    [Theory]
    [InlineData("{{danger}}")]
    [InlineData("{% execute %}")]
    [InlineData("<script>alert(1)</script>")]
    public void Renderer_rejects_unsafe_syntax(string value) => Assert.Throws<ArgumentException>(() => new SafeNotificationTemplateRenderer().Validate(value, []));
    [Fact] public async Task Development_provider_returns_safe_metadata() { var provider = new DevelopmentNotificationProvider(); var result = await ((IEmailNotificationProvider)provider).SendAsync(new("NTF-test", NotificationType.SecurityAlert, "Security alert", "Review your account.", "user:1", "a***@example.com", new Dictionary<string, string>()), default); Assert.True(result.Accepted); Assert.StartsWith("dev-", result.ProviderReference); Assert.DoesNotContain("Review", result.ProviderReference); }
    [Fact] public async Task Unsupported_future_channel_is_permanent_failure() { var provider = new DevelopmentNotificationProvider(); var dispatcher = new NotificationDispatcher(provider, provider, provider, provider); var result = await dispatcher.DispatchAsync(NotificationChannel.WhatsApp, new("NTF-test", NotificationType.SystemOperationalAlert, null, "test", null, null, new Dictionary<string, string>()), default); Assert.False(result.Accepted); Assert.False(result.IsTransient); Assert.Equal("PROVIDER_NOT_IMPLEMENTED", result.ErrorCode); }
}
