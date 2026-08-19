using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Infrastructure.Notifications;
using CreatorPay.Application.Notifications;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class PushDeviceLifecycleApiTests : IAsyncLifetime
{
    readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_push_tests").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    WebApplicationFactory<Program>? factory;
    int userNumber;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        var values=new Dictionary<string,string?> { ["ConnectionStrings:CreatorPayDatabase"] = database.GetConnectionString(), ["Authentication:Jwt:Issuer"]="CreatorPay", ["Authentication:Jwt:Audience"]="CreatorPay.Web", ["Authentication:Jwt:SigningKey"]="development-only-replace-this-signing-key-000000", ["CustomerVerification:HmacSecret"]=new string('h',32), ["CustomerVerification:EncryptionKey"]=new string('e',32), ["SmsOtp:SmsProvider"]="PilotTest", ["SmsOtp:HashSecret"]=new string('o',32), ["SmsOtp:TestCode"]="654321", ["Support:Email"]="tests@example.invalid", ["Storage:Provider"]="MetadataOnly" };
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Test"); foreach(var pair in values) builder.UseSetting(pair.Key,pair.Value); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(values)); });
        await using var db = Db(); await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync() { if(factory is not null) await factory.DisposeAsync(); await database.DisposeAsync(); }

    [DockerFact]
    public async Task Authenticated_registration_persists_protected_owned_active_device()
    {
        var (client,user) = await User(); const string token="fcm-token-a"; var response=await Register(client,token,"install-a"); Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
        await using var db=Db(); var row=await db.PushDeviceRegistrations.SingleAsync(); Assert.Equal(user,row.UserAccountId); Assert.True(row.IsActive); Assert.Equal("install-a",row.InstallationId); Assert.NotEmpty(row.TokenHash); Assert.NotEqual(token,row.ProtectedToken); Assert.NotEqual(token,row.TokenHash); Assert.True(row.LastSeenAtUtc>=row.CreatedAtUtc);
    }
    [DockerFact]
    public async Task Cross_user_token_and_installation_takeover_are_rejected()
    {
        var (a,_) = await User(); await Register(a,"token-x","install-x"); var (b,_) = await User(); Assert.Equal(HttpStatusCode.Conflict,(await Register(b,"token-x","install-y")).StatusCode); Assert.Equal(HttpStatusCode.Conflict,(await Register(b,"token-y","install-x")).StatusCode);
        await using var db=Db(); Assert.Single(await db.PushDeviceRegistrations.ToListAsync());
    }
    [DockerFact]
    public async Task Multiple_devices_replacement_and_owner_revocation_preserve_correct_active_state()
    {
        var (client,user)=await User(); await Register(client,"token-a","install-a"); await Register(client,"token-b","install-b"); await Register(client,"token-a2","install-a");
        await using(var db=Db()) { var rows=await db.PushDeviceRegistrations.Where(x=>x.UserAccountId==user).ToListAsync(); Assert.Equal(3,rows.Count); var activeA=Assert.Single(rows,x=>x.InstallationId=="install-a"&&x.IsActive); Assert.Single(rows,x=>x.InstallationId=="install-b"&&x.IsActive); Assert.NotNull(Assert.Single(rows,x=>x.TokenHash!=activeA.TokenHash&&x.InstallationId=="install-a").RevokedAtUtc); }
        var hash=await Hash("token-a2"); Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/v1/push-devices/{hash}")).StatusCode);
        await using var after=Db(); Assert.False((await after.PushDeviceRegistrations.SingleAsync(x=>x.TokenHash==hash)).IsActive); Assert.True((await after.PushDeviceRegistrations.SingleAsync(x=>x.InstallationId=="install-b")).IsActive);
    }
    [DockerFact]
    public async Task Permanent_invalid_push_deactivates_device_without_replaying_in_app_notification()
    {
        var (_,user)=await User(); var device=new PushDeviceRegistration{Id=Guid.NewGuid(),UserAccountId=user,Platform="web",InstallationId="delivery",TokenHash="delivery-hash",ProtectedToken="protected",IsActive=true,CreatedAtUtc=DateTime.UtcNow,LastSeenAtUtc=DateTime.UtcNow};
        await using(var db=Db()) { db.PushDeviceRegistrations.Add(device); db.Add(Message(user,device.Id,"invalid")); await db.SaveChangesAsync(); var invalid=new FakePush(new NotificationProviderResult(false,null,DeliveryAttemptStatus.Failed,"FCM_TOKEN_INVALID","The push device is no longer registered.",false)); await Processor(db,invalid).ProcessBatchAsync("test",default); }
        await using(var db=Db()) { var after=await db.PushDeviceRegistrations.SingleAsync(x=>x.Id==device.Id); Assert.False(after.IsActive); Assert.NotNull(after.RevokedAtUtc); Assert.Equal("FCM_TOKEN_INVALID",after.FailureCode); Assert.NotNull(after.FailureAtUtc); Assert.Single(await db.Notifications.ToListAsync()); Assert.Single(await db.NotificationRecipients.Where(x=>x.Channel==NotificationChannel.InApp).ToListAsync()); Assert.Single(await db.NotificationDeadLetters.ToListAsync()); Assert.Equal(NotificationOutboxStatus.DeadLettered,(await db.NotificationOutboxMessages.SingleAsync()).Status); Assert.Single(await db.NotificationDeliveryAttempts.Where(x=>x.Channel==NotificationChannel.Push).ToListAsync()); }
    }
    [DockerFact]
    public async Task Transient_push_retries_once_without_duplicate_recipient_or_notification()
    {
        var (_,user)=await User(); var device=new PushDeviceRegistration{Id=Guid.NewGuid(),UserAccountId=user,Platform="web",InstallationId="retry",TokenHash="retry-hash",ProtectedToken="protected",IsActive=true,CreatedAtUtc=DateTime.UtcNow,LastSeenAtUtc=DateTime.UtcNow};
        await using(var db=Db()) { db.PushDeviceRegistrations.Add(device); db.Add(Message(user,device.Id,"retry")); await db.SaveChangesAsync(); var push=new FakePush(new NotificationProviderResult(false,null,DeliveryAttemptStatus.Failed,"FCM_PROVIDER_FAILURE","temporary",true),new NotificationProviderResult(true,"accepted",DeliveryAttemptStatus.Submitted)); var processor=Processor(db,push); await processor.ProcessBatchAsync("test",default); Assert.Equal(NotificationOutboxStatus.Failed,(await db.NotificationOutboxMessages.SingleAsync()).Status); Assert.Single(await db.NotificationRecipients.Where(x=>x.Channel==NotificationChannel.Push).ToListAsync()); Assert.Single(await db.Notifications.ToListAsync()); await processor.ProcessBatchAsync("test",default); }
        await using(var db=Db()) { Assert.Equal(NotificationOutboxStatus.Completed,(await db.NotificationOutboxMessages.SingleAsync()).Status); Assert.Single(await db.Notifications.ToListAsync()); Assert.Single(await db.NotificationRecipients.Where(x=>x.Channel==NotificationChannel.Push).ToListAsync()); var attempts=await db.NotificationDeliveryAttempts.Where(x=>x.Channel==NotificationChannel.Push).OrderBy(x=>x.AttemptNumber).ToListAsync(); Assert.Equal(2,attempts.Count); Assert.Equal(1,attempts[0].AttemptNumber); Assert.Equal(2,attempts[1].AttemptNumber); Assert.True(attempts[0].IsTransientFailure); Assert.Equal(DeliveryAttemptStatus.Submitted,attempts[1].Status); }
    }
    static NotificationOutboxProcessor Processor(ApplicationDbContext db, IPushNotificationProvider push) { var dev=new DevelopmentNotificationProvider(); return new NotificationOutboxProcessor(db,new SafeNotificationTemplateRenderer(),new NotificationDispatcher(dev,dev,push,dev),Options.Create(new NotificationOptions{MaximumAttempts=2,InitialRetryDelaySeconds=0})); }
    static NotificationOutboxMessage Message(Guid user,Guid device,string key) { var n=new Notification{Id=Guid.NewGuid(),PublicNotificationId="NTF-"+Guid.NewGuid().ToString("N"),IdempotencyKey=key,NotificationType=NotificationType.SecurityAlert,Title="Security",Body="Open Weymela",DataJson="{}",Status=NotificationStatus.Pending,CreatedAtUtc=DateTime.UtcNow}; n.Recipients.Add(new NotificationRecipient{Id=Guid.NewGuid(),NotificationId=n.Id,UserAccountId=user,RecipientType=NotificationRecipientType.User,Channel=NotificationChannel.InApp,Status=NotificationRecipientStatus.Pending,CreatedAtUtc=DateTime.UtcNow}); n.Recipients.Add(new NotificationRecipient{Id=Guid.NewGuid(),NotificationId=n.Id,UserAccountId=user,RecipientType=NotificationRecipientType.User,Channel=NotificationChannel.Push,PushDeviceRegistrationId=device,DestinationReference="protected",Status=NotificationRecipientStatus.Pending,CreatedAtUtc=DateTime.UtcNow}); return new NotificationOutboxMessage{Id=Guid.NewGuid(),NotificationId=n.Id,Notification=n,Status=NotificationOutboxStatus.Pending,AvailableAtUtc=DateTime.UtcNow,CreatedAtUtc=DateTime.UtcNow}; }
    sealed class FakePush(params NotificationProviderResult[] results):IPushNotificationProvider { int index; public Task<NotificationProviderResult> SendAsync(ProviderNotification n,CancellationToken ct)=>Task.FromResult(results[Math.Min(index++,results.Length-1)]); }
    async Task<(HttpClient Client,Guid User)> User()
    {
        var n=Interlocked.Increment(ref userNumber); var phone=$"097700{n:0000}"; var normalized="+251"+phone.Substring(1); const string password="Lifecycle-test-password-1!"; var client=factory!.CreateClient(); var registration=await client.PostAsJsonAsync("/api/v1/customers/register",new { displayName=$"Push {n}",email=(string?)null,phoneNumber=phone,password,confirmation=password }); Assert.Equal(HttpStatusCode.Created,registration.StatusCode); var login=await client.PostAsJsonAsync("/api/v1/auth/login",new { email=phone,password }); Assert.Equal(HttpStatusCode.OK,login.StatusCode); var json=await login.Content.ReadFromJsonAsync<JsonElement>(); client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",json.GetProperty("accessToken").GetString()); await using var db=Db(); var user=await db.UserAccounts.SingleAsync(x=>x.NormalizedPhoneNumber==normalized); return(client,user.Id);
    }
    static Task<string> Hash(string token) => Task.FromResult(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant());
    static Task<HttpResponseMessage> Register(HttpClient c,string token,string installationId)=>c.PostAsJsonAsync("/api/v1/push-devices",new { platform="web",token,installationId });
    ApplicationDbContext Db()=>new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);
}
