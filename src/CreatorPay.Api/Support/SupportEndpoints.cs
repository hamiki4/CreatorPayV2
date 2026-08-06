using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;

namespace CreatorPay.Api.Support;

public sealed class SupportOptions { public const string SectionName = "Support"; public string Email { get; set; } = "support@example.com"; }
public sealed record PublicSupportRequest(string? Name, string? Contact, string? UserType, string? Subject, string? Message, string? PreferredLanguage, bool ConsentAcknowledged);

public static partial class SupportEndpoints
{
    static readonly HashSet<string> UserTypes = ["Shopper", "Creator", "Business", "Cashier/Supervisor", "Other"];
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.IgnoreCase)] private static partial Regex Email();
    [GeneratedRegex(@"^\+?[0-9][0-9 ()-]{7,19}$")] private static partial Regex Phone();
    [GeneratedRegex(@"[<>]|javascript:|&lt;|&#", RegexOptions.IgnoreCase)] private static partial Regex UnsafeMarkup();

    public static IEndpointRouteBuilder MapSupportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/support/requests", Create).AllowAnonymous().RequireRateLimiting("public-support").WithTags("Public support");
        return endpoints;
    }

    static async Task<IResult> Create(PublicSupportRequest r, IConfiguration config, ApplicationDbContext db, INotificationService notifications, HttpContext http, CancellationToken ct)
    {
        static string Clean(string? value) => (value ?? "").Trim();
        var name = Clean(r.Name); var contact = Clean(r.Contact); var type = Clean(r.UserType); var subject = Clean(r.Subject); var message = Clean(r.Message); var language = Clean(r.PreferredLanguage).ToLowerInvariant();
        var values = new[] { name, contact, type, subject, message };
        if (name.Length is < 2 or > 120 || contact.Length > 254 || (!Email().IsMatch(contact) && !Phone().IsMatch(contact)) || !UserTypes.Contains(type) || subject.Length is < 3 or > 160 || message.Length is < 10 or > 4000 || language is not ("en" or "am") || !r.ConsentAcknowledged || values.Any(x => UnsafeMarkup().IsMatch(x)))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Please check all fields. Use a valid email or phone number and plain text only."] });
        var reference = $"SUP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
        db.SupportRequests.Add(new SupportRequest { Id = Guid.NewGuid(), PublicReference = reference, Name = name, Contact = contact, UserType = type, Subject = subject, Message = message, PreferredLanguage = language, ConsentAcknowledged = true, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        var supportEmail = config[$"{SupportOptions.SectionName}:Email"] ?? "support@example.com";
        await notifications.CreateAsync(new(NotificationType.SupportRequestReceived, $"support:{reference}", new Dictionary<string, string> { ["Title"] = $"New support request {reference}", ["Body"] = $"{type}: {subject}", ["SupportReference"] = reference }, [new(null, NotificationRecipientType.PlatformOperations, NotificationChannel.Email, supportEmail, Mask(supportEmail), language)], NotificationPriority.Normal, http.TraceIdentifier, nameof(SupportRequest), reference), ct);
        return Results.Ok(new { referenceNumber = reference, message = "Your request was received. Keep this reference for follow-up." });
    }
    static string Mask(string value) { var at = value.IndexOf('@'); return at > 0 ? $"{value[0]}***{value[at..]}" : "configured support channel"; }
}
