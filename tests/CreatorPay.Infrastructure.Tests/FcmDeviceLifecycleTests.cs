using CreatorPay.Application.Notifications;
using CreatorPay.Application.Wallet;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Notifications;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPay.Infrastructure.Tests;

// Database-backed deterministic lifecycle coverage. No Firebase transport is contacted.
public sealed class FcmDeviceLifecycleTests
{
    [Fact] public async Task One_notification_fans_out_once_per_active_device_and_not_revoked_device()
    {
        await using var db = Db(); var user = Guid.NewGuid(); await Seed(db, user, true, "a", "b");
        var service = Service(db); await service.CreateAsync(Request(user, "fanout"), default);
        Assert.Equal(2, await db.NotificationRecipients.CountAsync(x => x.Channel == NotificationChannel.Push));
        var device = await db.PushDeviceRegistrations.OrderBy(x => x.CreatedAtUtc).FirstAsync(); device.IsActive = false; device.RevokedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync();
        await service.CreateAsync(Request(user, "fanout-next"), default);
        Assert.Equal(1, await db.NotificationRecipients.CountAsync(x => x.Channel == NotificationChannel.Push && x.Notification.IdempotencyKey == "fanout-next"));
    }

    [Fact] public async Task Duplicate_idempotency_never_duplicates_per_device_delivery_identity()
    {
        await using var db = Db(); var user = Guid.NewGuid(); await Seed(db, user, true, "a", "b"); var service = Service(db);
        await service.CreateAsync(Request(user, "once"), default); await service.CreateAsync(Request(user, "once"), default);
        Assert.Equal(2, await db.NotificationRecipients.CountAsync(x => x.Channel == NotificationChannel.Push)); Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact] public async Task Push_opt_out_preserves_persisted_in_app_notification()
    {
        await using var db = Db(); var user = Guid.NewGuid(); await Seed(db, user, false, "a"); var service = Service(db);
        await service.CreateAsync(Request(user, "in-app"), default);
        Assert.Single(await db.NotificationRecipients.Where(x => x.Channel == NotificationChannel.InApp).ToListAsync()); Assert.Empty(await db.NotificationRecipients.Where(x => x.Channel == NotificationChannel.Push).ToListAsync());
    }

    [Fact] public async Task Shared_data_protection_key_ring_can_decrypt_api_protected_token_in_worker_scope()
    {
        var path = Path.Combine(Path.GetTempPath(), $"weymela-fcm-{Guid.NewGuid():N}"); Directory.CreateDirectory(path);
        try { var api = DataProtectionProvider.Create(new DirectoryInfo(path), b => b.SetApplicationName("Weymela.PushNotifications")); var worker = DataProtectionProvider.Create(new DirectoryInfo(path), b => b.SetApplicationName("Weymela.PushNotifications")); var protectedToken = api.CreateProtector("Weymela.PushDeviceToken.v1").Protect("test-token"); Assert.Equal("test-token", worker.CreateProtector("Weymela.PushDeviceToken.v1").Unprotect(protectedToken)); }
        finally { Directory.Delete(path, true); }
        await Task.CompletedTask;
    }

    static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    static NotificationService Service(ApplicationDbContext db) => new(db, Options.Create(new WalletOptions()));
    static CreateNotificationRequest Request(Guid user, string key) => new(NotificationType.SecurityAlert, key, new Dictionary<string,string>{{"Title","Security"},{"Body","Open Weymela"}}, [new(user, NotificationRecipientType.User, NotificationChannel.InApp, null, null)]);
    static async Task Seed(ApplicationDbContext db, Guid user, bool optedIn, params string[] devices)
    {
        db.NotificationPreferences.Add(new NotificationPreference { Id = Guid.NewGuid(), UserAccountId = user, NotificationType = NotificationType.SecurityAlert, PushEnabled = optedIn, InAppEnabled = true, CreatedAtUtc = DateTime.UtcNow });
        foreach (var token in devices) db.PushDeviceRegistrations.Add(new PushDeviceRegistration { Id = Guid.NewGuid(), UserAccountId = user, Platform = "web", TokenHash = token.PadRight(64, 'x'), ProtectedToken = "protected", IsActive = true, CreatedAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}
