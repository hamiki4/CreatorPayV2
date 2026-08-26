using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace CreatorPay.Api.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/v1").RequireAuthorization("AuthenticatedUser").WithTags("Notifications");
        users.MapGet("/notifications", async (int page, int pageSize, string? status, string? notificationType, DateTime? from, DateTime? to, ICurrentUserService u, INotificationService s, CancellationToken ct) => { var r = await s.ListAsync(u.UserAccountId!.Value, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, status, notificationType, from, to, ct); return Results.Ok(new { r.Items, r.Total, page = page == 0 ? 1 : page, pageSize = pageSize == 0 ? 20 : pageSize }); });
        users.MapGet("/notifications/unread-count", async (ICurrentUserService u, INotificationService s, CancellationToken ct) => Results.Ok(new { count = await s.UnreadCountAsync(u.UserAccountId!.Value, ct) }));
        users.MapGet("/notifications/{id}", async (string id, ICurrentUserService u, INotificationService s, CancellationToken ct) => await s.GetAsync(u.UserAccountId!.Value, id, ct) is { } x ? Results.Ok(x) : Results.NotFound());
        users.MapPost("/notifications/{id}/read", async (string id, ICurrentUserService u, INotificationService s, CancellationToken ct) => await s.MarkReadAsync(u.UserAccountId!.Value, id, ct) ? Results.NoContent() : Results.NotFound());
        users.MapPost("/notifications/read-all", async (ICurrentUserService u, INotificationService s, CancellationToken ct) => Results.Ok(new { count = await s.MarkAllReadAsync(u.UserAccountId!.Value, ct) }));
        users.MapGet("/notification-preferences", async (ICurrentUserService u, INotificationService s, CancellationToken ct) => Results.Ok(await s.GetPreferencesAsync(u.UserAccountId!.Value, ct)));
        users.MapPut("/notification-preferences", async (UpdateNotificationPreference[] p, ICurrentUserService u, INotificationService s, CancellationToken ct) => { await s.UpdatePreferencesAsync(u.UserAccountId!.Value, p, ct); return Results.NoContent(); });
        users.MapPost("/push-devices", RegisterPushDevice);
        users.MapDelete("/push-devices/{tokenHash}", RevokePushDevice);
        MapAdmin(app); return app;
    }
    static async Task<IResult> RegisterPushDevice(PushDeviceRequest request, ICurrentUserService user, ApplicationDbContext db, IDataProtectionProvider protection, CancellationToken ct)
    {
        if (request.Platform is not ("web" or "android") || string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 2048 || string.IsNullOrWhiteSpace(request.InstallationId) || request.InstallationId.Length > 128) return Results.BadRequest(new { message = "A valid platform, installation and device token are required." });
        var accountId = user.UserAccountId!.Value; var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token))).ToLowerInvariant(); var now = DateTime.UtcNow;
        var installationRows = await db.PushDeviceRegistrations.Where(x => x.Platform == request.Platform && x.InstallationId == request.InstallationId).ToListAsync(ct);
        if (installationRows.Any(x => x.UserAccountId != accountId)) return Results.Conflict(new { message = "This installation is already registered to another account." });
        var tokenOwner = await db.PushDeviceRegistrations.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (tokenOwner is not null && tokenOwner.UserAccountId != accountId) return Results.Conflict(new { message = "This device token is already registered to another account." });
        var active = installationRows.SingleOrDefault(x => x.IsActive);
        if (active is not null && active.TokenHash == hash) { active.LastSeenAtUtc = now; await db.SaveChangesAsync(ct); return Results.NoContent(); }
        if (active is not null) { active.IsActive = false; active.RevokedAtUtc = now; }
        var protectedToken = protection.CreateProtector("Weymela.PushDeviceToken.v1").Protect(request.Token);
        if (tokenOwner is not null) { tokenOwner.Platform = request.Platform; tokenOwner.InstallationId = request.InstallationId; tokenOwner.ProtectedToken = protectedToken; tokenOwner.IsActive = true; tokenOwner.RevokedAtUtc = null; tokenOwner.FailureCode = null; tokenOwner.FailureAtUtc = null; tokenOwner.LastSeenAtUtc = now; }
        else db.PushDeviceRegistrations.Add(new PushDeviceRegistration { Id = Guid.NewGuid(), UserAccountId = accountId, Platform = request.Platform, InstallationId = request.InstallationId, TokenHash = hash, ProtectedToken = protectedToken, IsActive = true, CreatedAtUtc = now, LastSeenAtUtc = now });
        await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    static async Task<IResult> RevokePushDevice(string tokenHash, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct)
    {
        if (tokenHash.Length != 64) return Results.NotFound(); var device = await db.PushDeviceRegistrations.SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.UserAccountId == user.UserAccountId, ct); if (device is null) return Results.NotFound(); device.IsActive = false; device.RevokedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    static void MapAdmin(IEndpointRouteBuilder app)
    {
        var a = app.MapGroup("/api/v1/admin").RequireAuthorization("AdminOperationsOnly").WithTags("Notification administration");
        a.MapGet("/notifications", async (int page, int pageSize, ApplicationDbContext db, CancellationToken ct) => Results.Ok(await db.Notifications.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100)).Take(Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100)).Select(x => new { x.PublicNotificationId, x.NotificationType, x.Priority, x.Status, x.CreatedAtUtc, x.CompletedAtUtc }).ToListAsync(ct)));
        a.MapGet("/notifications/{id}", async (string id, ApplicationDbContext db, CancellationToken ct) => await db.Notifications.AsNoTracking().Where(x => x.PublicNotificationId == id).Select(x => new { x.PublicNotificationId, x.NotificationType, x.Title, x.Body, x.Priority, x.Status, x.CreatedAtUtc, x.CompletedAtUtc, Recipients = x.Recipients.Select(r => new { r.Channel, r.MaskedDestination, r.Status, r.DeliveredAtUtc, r.FailedAtUtc, r.FailureReason }) }).SingleOrDefaultAsync(ct) is { } x ? Results.Ok(x) : Results.NotFound());
        a.MapGet("/notification-outbox", async (ApplicationDbContext db, CancellationToken ct) => Results.Ok(await db.NotificationOutboxMessages.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(200).Select(x => new { x.Id, x.NotificationId, x.Status, x.AvailableAtUtc, x.AttemptCount, x.LastAttemptAtUtc, x.LastError }).ToListAsync(ct)));
        a.MapGet("/notification-outbox/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken ct) => await db.NotificationOutboxMessages.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.NotificationId, x.Status, x.AvailableAtUtc, x.AttemptCount, x.LastAttemptAtUtc, x.ProcessedAtUtc, x.LastError, x.CorrelationId }).SingleOrDefaultAsync(ct) is { } x ? Results.Ok(x) : Results.NotFound());
        a.MapPost("/notification-outbox/{id:guid}/retry", (Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => RetryOutbox(id, u.UserAccountId!.Value, db, ct));
        a.MapPost("/notification-outbox/{id:guid}/cancel", async (Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => { var x = await db.NotificationOutboxMessages.FindAsync([id], ct); if (x is null) return Results.NotFound(); if (x.Status is NotificationOutboxStatus.Completed or NotificationOutboxStatus.Cancelled) return Results.Conflict(new { message = "Message can no longer be cancelled." }); x.Status = NotificationOutboxStatus.Cancelled; x.LockedAtUtc = null; x.LockedBy = null; db.NotificationAuditEvents.Add(Audit(x.NotificationId, u.UserAccountId, "MessageCancelled")); await db.SaveChangesAsync(ct); return Results.NoContent(); });
        a.MapGet("/notification-dead-letters", async (ApplicationDbContext db, CancellationToken ct) => Results.Ok(await db.NotificationDeadLetters.AsNoTracking().OrderByDescending(x => x.DeadLetteredAtUtc).Take(200).ToListAsync(ct)));
        a.MapPost("/notification-dead-letters/{id:guid}/retry", async (Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => { var d = await db.NotificationDeadLetters.FindAsync([id], ct); if (d is null) return Results.NotFound(); d.Status = NotificationDeadLetterStatus.Retrying; await db.SaveChangesAsync(ct); return await RetryOutbox(d.OutboxMessageId, u.UserAccountId!.Value, db, ct); });
        a.MapPost("/notification-dead-letters/{id:guid}/resolve", async (Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => { var d = await db.NotificationDeadLetters.FindAsync([id], ct); if (d is null) return Results.NotFound(); d.Status = NotificationDeadLetterStatus.Resolved; d.ResolvedAtUtc = DateTime.UtcNow; d.ResolvedByUserId = u.UserAccountId; db.NotificationAuditEvents.Add(new() { Id = Guid.NewGuid(), ActorUserId = u.UserAccountId, EventType = "DeadLetterResolved", SafeDetail = $"DeadLetterId={id}", CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); return Results.NoContent(); });
        a.MapGet("/notification-templates", async (ApplicationDbContext db, CancellationToken ct) => Results.Ok(await db.NotificationTemplates.AsNoTracking().OrderBy(x => x.NotificationType).ThenBy(x => x.Channel).ThenByDescending(x => x.VersionNumber).ToListAsync(ct)));
        a.MapGet("/notification-templates/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken ct) => await db.NotificationTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } x ? Results.Ok(x) : Results.NotFound());
        a.MapPost("/notification-templates", CreateTemplate); a.MapPost("/notification-templates/{id:guid}/versions", CreateVersion);
        a.MapPatch("/notification-templates/{id:guid}/status", async (Guid id, TemplateStatus body, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => { var x = await db.NotificationTemplates.FindAsync([id], ct); if (x is null) return Results.NotFound(); x.IsActive = body.IsActive; db.NotificationAuditEvents.Add(new() { Id = Guid.NewGuid(), ActorUserId = u.UserAccountId, EventType = body.IsActive ? "TemplateActivated" : "TemplateDeactivated", SafeDetail = $"TemplateId={id}", CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); return Results.NoContent(); });
    }
    static async Task<IResult> CreateTemplate(TemplateRequest r, ICurrentUserService u, INotificationTemplateRenderer renderer, ApplicationDbContext db, CancellationToken ct) { if (r.LanguageCode is not ("en" or "am")) return Results.BadRequest(new { message = "Language must be 'en' or 'am'." }); renderer.Validate(r.SubjectTemplate ?? "", r.AllowedPlaceholders); renderer.Validate(r.BodyTemplate, r.AllowedPlaceholders); var version = (await db.NotificationTemplates.Where(x => x.NotificationType == r.NotificationType && x.Channel == r.Channel && x.LanguageCode == r.LanguageCode).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1; var x = new NotificationTemplate { Id = Guid.NewGuid(), NotificationType = r.NotificationType, Channel = r.Channel, LanguageCode = r.LanguageCode, SubjectTemplate = r.SubjectTemplate, BodyTemplate = r.BodyTemplate, AllowedPlaceholdersJson = JsonSerializer.Serialize(r.AllowedPlaceholders.Distinct()), VersionNumber = version, IsActive = r.IsActive, EffectiveFromUtc = r.EffectiveFromUtc ?? DateTime.UtcNow, CreatedByUserId = u.UserAccountId, CreatedAtUtc = DateTime.UtcNow }; db.Add(x); db.NotificationAuditEvents.Add(new() { Id = Guid.NewGuid(), ActorUserId = u.UserAccountId, EventType = version == 1 ? "TemplateCreated" : "TemplateVersionCreated", SafeDetail = $"TemplateId={x.Id};Version={version}", CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/notification-templates/{x.Id}", x); }
    static async Task<IResult> CreateVersion(Guid id, TemplateRequest r, ICurrentUserService u, INotificationTemplateRenderer renderer, ApplicationDbContext db, CancellationToken ct) { var source = await db.NotificationTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return source is null ? Results.NotFound() : await CreateTemplate(r with { NotificationType = source.NotificationType, Channel = source.Channel, LanguageCode = source.LanguageCode }, u, renderer, db, ct); }
    static async Task<IResult> RetryOutbox(Guid id, Guid actor, ApplicationDbContext db, CancellationToken ct) { var x = await db.NotificationOutboxMessages.FindAsync([id], ct); if (x is null) return Results.NotFound(); x.Status = NotificationOutboxStatus.Pending; x.AvailableAtUtc = DateTime.UtcNow; x.LockedAtUtc = null; x.LockedBy = null; x.LastError = null; db.NotificationAuditEvents.Add(Audit(x.NotificationId, actor, "MessageManuallyRetried")); await db.SaveChangesAsync(ct); return Results.Accepted(); }
    static NotificationAuditEvent Audit(Guid id, Guid? actor, string type) => new() { Id = Guid.NewGuid(), NotificationId = id, ActorUserId = actor, EventType = type, CreatedAtUtc = DateTime.UtcNow };
    public sealed record TemplateStatus(bool IsActive);
    public sealed record PushDeviceRequest(string Platform, string Token, string InstallationId);
}
