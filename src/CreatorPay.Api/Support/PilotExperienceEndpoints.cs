using System.Text.RegularExpressions;
using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Api.Support;

public sealed record PilotFeedbackRequest(string? Category, string? Message, string? PreferredLanguage, string? Context);
public sealed record CheckoutSurveyRequest(string? CheckoutId, int Rating, string? PreferredLanguage);

public static partial class PilotExperienceEndpoints
{
    private static readonly HashSet<string> Categories = ["Usability", "Accessibility", "Translation", "Checkout", "Other"];
    [GeneratedRegex(@"(?:[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|(?:\+?251|0)?9\d{8}|\b(?:\d[ -]*?){13,19}\b|[<>])", RegexOptions.IgnoreCase)]
    private static partial Regex PersonalOrUnsafeData();

    public static IEndpointRouteBuilder MapPilotExperienceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pilot = endpoints.MapGroup("/api/v1/pilot").WithTags("Pilot experience").RequireAuthorization("AuthenticatedUser").RequireRateLimiting("public-support");
        pilot.MapPost("/feedback", CreateFeedback);
        pilot.MapPost("/checkout-survey", CreateCheckoutSurvey).RequireAuthorization("CustomerOnly");
        return endpoints;
    }

    private static async Task<IResult> CreateFeedback(PilotFeedbackRequest request, ICurrentUserService user, ApplicationDbContext db, INotificationService notifications, HttpContext http, CancellationToken ct)
    {
        var category = (request.Category ?? "").Trim(); var message = (request.Message ?? "").Trim(); var language = NormalizeLanguage(request.PreferredLanguage); var context = NormalizeContext(request.Context);
        if (!Categories.Contains(category) || message.Length is < 10 or > 2000 || PersonalOrUnsafeData().IsMatch(message))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["message"] = ["Use 10 to 2,000 plain-text characters and remove email, phone, card, or other personal information."] });
        var item = NewRequest($"Pilot feedback: {category}", message, RoleLabel(user.Role), language);
        db.SupportRequests.Add(item);
        db.OperationalAuditEvents.Add(new OperationalAuditEvent { Id = Guid.NewGuid(), EventType = "PilotFeedbackSubmitted", ActorUserId = user.UserAccountId, SubjectId = item.Id, MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { category, context, language }), CorrelationId = http.TraceIdentifier, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await Notify(item, category, language, notifications, http.TraceIdentifier, ct);
        return Results.Ok(new { referenceNumber = item.PublicReference, message = "Feedback received." });
    }

    private static async Task<IResult> CreateCheckoutSurvey(CheckoutSurveyRequest request, ICurrentUserService user, ApplicationDbContext db, INotificationService notifications, HttpContext http, CancellationToken ct)
    {
        if (request.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(request.CheckoutId)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["rating"] = ["Choose a rating from 1 to 5."] });
        var checkout = await db.CheckoutSessions.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == user.CustomerId && x.PublicCheckoutId == request.CheckoutId && x.Status == CheckoutSessionStatus.Completed, ct);
        if (checkout is null) return Results.NotFound(new { detail = "Completed checkout not found." });
        var surveySubject = $"Checkout satisfaction: {checkout.PublicCheckoutId}";
        if (await db.SupportRequests.AnyAsync(x => x.Subject == surveySubject && x.UserType == "Shopper", ct)) return Results.Conflict(new { detail = "This checkout was already rated." });
        var language = NormalizeLanguage(request.PreferredLanguage); var item = NewRequest(surveySubject, $"Rating: {request.Rating}/5", "Shopper", language);
        db.SupportRequests.Add(item);
        db.OperationalAuditEvents.Add(new OperationalAuditEvent { Id = Guid.NewGuid(), EventType = "CheckoutSatisfactionSubmitted", ActorUserId = user.UserAccountId, SubjectId = checkout.Id, MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { rating = request.Rating, language }), CorrelationId = http.TraceIdentifier, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        if (request.Rating <= 2) await Notify(item, "Checkout rating requires review", language, notifications, http.TraceIdentifier, ct);
        return Results.Ok(new { message = "Rating received." });
    }

    private static SupportRequest NewRequest(string subject, string message, string userType, string language) => new()
    {
        Id = Guid.NewGuid(), PublicReference = $"PIL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant(), Name = "Authenticated pilot user", Contact = "Not collected", UserType = userType,
        Subject = subject, Message = message, PreferredLanguage = language, ConsentAcknowledged = true, Status = "Open", CreatedAtUtc = DateTime.UtcNow
    };
    private static string NormalizeLanguage(string? value) => string.Equals(value?.Trim(), "am", StringComparison.OrdinalIgnoreCase) ? "am" : "en";
    private static string NormalizeContext(string? value) { var context = (value ?? "").Trim(); return context.StartsWith('/') && context.Length <= 120 && !context.Contains('?') ? context : "/"; }
    private static string RoleLabel(string? role) => role switch { "Customer" => "Shopper", "MerchantAdmin" => "Business", "Cashier" or "Supervisor" => "Cashier/Supervisor", "Creator" => "Creator", _ => "Other" };
    private static async Task Notify(SupportRequest item, string category, string language, INotificationService notifications, string correlation, CancellationToken ct) =>
        await notifications.CreateAsync(new(NotificationType.SupportRequestReceived, $"pilot:{item.PublicReference}", new Dictionary<string, string> { ["Title"] = $"Pilot experience item {item.PublicReference}", ["Body"] = $"{item.UserType}: {category}", ["SupportReference"] = item.PublicReference }, [new(null, NotificationRecipientType.PlatformOperations, NotificationChannel.InApp, "platform-operations", "platform operations", language)], NotificationPriority.Normal, correlation, nameof(SupportRequest), item.PublicReference), ct);
}
