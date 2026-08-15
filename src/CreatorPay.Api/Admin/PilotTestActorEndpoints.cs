using System.Security.Cryptography;
using System.Text.Json;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Api.Admin;

public static class PilotTestActorEndpoints
{
    public static IEndpointRouteBuilder MapPilotTestActorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin/pilot-test-actors")
            .WithTags("Pilot test actors")
            .RequireAuthorization("PlatformAdminOnly");
        admin.MapPost("", Create).RequireRateLimiting("auth-sensitive");
        admin.MapPost("/{batchId:guid}/rotate-credentials", RotateCredentials).RequireRateLimiting("auth-sensitive");
        admin.MapPost("/{batchId:guid}/deactivate", Deactivate);
        return endpoints;
    }

    private static async Task<IResult> RotateCredentials(
        Guid batchId,
        IWebHostEnvironment environment,
        ICurrentUserService current,
        IPasswordHasher passwords,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        if (!PilotTestActorPolicy.IsEnabled(environment.EnvironmentName, UserRole.PlatformAdmin)) return Results.NotFound();
        if (current.UserAccountId is null) return Results.Unauthorized();

        var audit = await db.OperationalAuditEvents.SingleOrDefaultAsync(
            x => x.EventType == "PilotTestActorsCreated" && x.CorrelationId == batchId.ToString("N"), ct);
        if (audit is null) return Results.NotFound();
        using var metadata = JsonDocument.Parse(audit.MetadataJson);
        var label = metadata.RootElement.GetProperty("label").GetString();
        if (string.IsNullOrWhiteSpace(label) || !PilotTestActorPolicy.IsDisposableLabel(label)) return Results.NotFound();

        var prefix = label.ToLowerInvariant();
        var accounts = await db.UserAccounts
            .Where(x => x.Email.StartsWith(prefix) && x.Role != UserRole.PlatformAdmin)
            .ToListAsync(ct);
        if (accounts.Count == 0) return Results.NotFound();

        var credentials = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            var secret = Secret();
            account.PasswordHash = passwords.Hash(account, secret);
            account.FailedLoginCount = 0;
            account.LockoutEndUtc = null;
            credentials[account.Email] = secret;
        }

        var accountIds = accounts.Select(x => x.Id).ToArray();
        await db.RefreshTokens
            .Where(x => accountIds.Contains(x.UserAccountId) && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(x => x.SetProperty(token => token.RevokedAtUtc, DateTime.UtcNow), ct);
        db.OperationalAuditEvents.Add(new OperationalAuditEvent
        {
            Id = Guid.NewGuid(), EventType = "PilotTestActorCredentialsRotated", ActorUserId = current.UserAccountId,
            MetadataJson = JsonSerializer.Serialize(new { batchId, label, count = accounts.Count }),
            CorrelationId = batchId.ToString("N"), CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { batchId, label, temporaryCredentials = credentials });
    }

    private static async Task<IResult> Create(
        IWebHostEnvironment environment,
        ICurrentUserService current,
        IPasswordHasher passwords,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        if (!PilotTestActorPolicy.IsEnabled(environment.EnvironmentName, UserRole.PlatformAdmin)) return Results.NotFound();
        if (current.UserAccountId is null) return Results.Unauthorized();

        var now = DateTime.UtcNow;
        var batchId = Guid.NewGuid();
        var label = $"PILOT-E2E-{now:yyyyMMddHHmmss}-{batchId:N}";
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        var actors = new List<object>();
        var batch = new List<(UserAccount User, string Kind, Guid EntityId)>();

        UserAccount Account(UserRole role, string email, string phone, Guid? customerId = null, Guid? creatorId = null, Guid? merchantId = null, Guid? cashierId = null)
        {
            var user = new UserAccount
            {
                Id = Guid.NewGuid(), Email = email, NormalizedEmail = email.ToUpperInvariant(),
                PhoneNumber = phone, NormalizedPhoneNumber = phone, Role = role, Status = AccountStatus.Active,
                CustomerId = customerId, CreatorId = creatorId, MerchantId = merchantId, CashierId = cashierId,
                IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now, CreatedBy = current.UserAccountId.Value.ToString()
            };
            var password = Secret();
            user.PasswordHash = passwords.Hash(user, password);
            secrets[email] = password;
            db.UserAccounts.Add(user);
            return user;
        }

        Customer MakeShopper(string suffix)
        {
            var id = Guid.NewGuid(); var phone = Phone(id); var email = $"{label.ToLowerInvariant()}-{suffix.ToLowerInvariant()}@invalid.weymela.test";
            var customer = new Customer { Id = id, PublicCustomerId = $"CUS-{id:N}"[..20], DisplayName = $"{label} Shopper {suffix}", PhoneNumber = phone, NormalizedPhoneNumber = phone, CreatedAtUtc = now };
            db.Customers.Add(customer); db.CustomerWallets.Add(new CustomerWallet { Id = Guid.NewGuid(), CustomerId = id, CurrencyCode = "ETB", CreatedAtUtc = now });
            var user = Account(UserRole.Customer, email, phone, customerId: id); batch.Add((user, "Shopper", id));
            actors.Add(new { role = "Shopper", name = customer.DisplayName, publicId = customer.PublicCustomerId }); return customer;
        }

        Merchant MakeBusiness(string suffix)
        {
            var id = Guid.NewGuid(); var phone = Phone(id); var email = $"{label.ToLowerInvariant()}-business-{suffix.ToLowerInvariant()}@invalid.weymela.test";
            var merchant = new Merchant { Id = id, PublicMerchantId = $"BUS-{id:N}"[..20], LegalBusinessName = $"{label} Business {suffix}", TradingName = $"{label} Business {suffix}", BusinessType = "Other", PrimaryContactName = "PILOT Test Owner", PhoneNumber = phone, NormalizedPhoneNumber = phone, Email = email, BusinessAddress = "PILOT Test Address", City = "Addis Ababa", Region = "Addis Ababa", Country = "Ethiopia", TimeZone = "Africa/Addis_Ababa", TermsAcceptedAtUtc = now, Status = MerchantStatus.Active, CreatedAtUtc = now };
            var location = new MerchantLocation { Id = Guid.NewGuid(), MerchantId = id, Name = "PILOT Test Location", AddressLine1 = merchant.BusinessAddress, City = merchant.City, Region = merchant.Region, CountryCode = "ET", TimeZoneId = merchant.TimeZone, IsActive = true, CreatedAtUtc = now };
            db.Merchants.Add(merchant); db.MerchantLocations.Add(location); db.MerchantWallets.Add(new MerchantWallet { Id = Guid.NewGuid(), MerchantId = id, CurrencyCode = "ETB", CreatedAtUtc = now });
            var user = Account(UserRole.MerchantAdmin, email, phone, merchantId: id); batch.Add((user, "Business", id));
            actors.Add(new { role = "Business", name = merchant.TradingName, publicId = merchant.PublicMerchantId }); return merchant;
        }

        Creator MakeCreator(string suffix)
        {
            var id = Guid.NewGuid(); var phone = Phone(id); var email = $"{label.ToLowerInvariant()}-creator-{suffix.ToLowerInvariant()}@invalid.weymela.test";
            var creator = new Creator { Id = id, PublicCreatorId = $"CRE-{id:N}"[..20], CreatorCode = Code(id), FirstName = "PILOT", LastName = $"Creator {suffix}", DisplayName = $"{label} Creator {suffix}", PhoneNumber = phone, NormalizedPhoneNumber = phone, Email = email, City = "Addis Ababa", Biography = "Disposable PILOT actor", ContentCategories = "Testing", TermsAcceptedAtUtc = now, Status = CreatorStatus.Active, CreatedAtUtc = now };
            db.Creators.Add(creator); db.CreatorBalanceAccounts.Add(new CreatorBalanceAccount { Id = Guid.NewGuid(), CreatorId = id, CurrencyCode = "ETB", CreatedAtUtc = now });
            var user = Account(UserRole.Creator, email, phone, creatorId: id); batch.Add((user, "Creator", id));
            actors.Add(new { role = "Creator", name = creator.DisplayName, publicId = creator.PublicCreatorId, creatorCode = creator.CreatorCode }); return creator;
        }

        var shopperA = MakeShopper("A"); MakeShopper("B");
        var businessA = MakeBusiness("A"); var businessB = MakeBusiness("B");
        MakeCreator("A"); MakeCreator("B");
        foreach (var (merchant, suffix) in new[] { (businessA, "A"), (businessB, "B") })
        {
            // The Business, owner, and location are intentionally persisted together at
            // the end of this batch. Resolve the newly added location from EF's local
            // change tracker rather than querying the database before SaveChangesAsync.
            var location = db.MerchantLocations.Local.Single(x => x.MerchantId == merchant.Id);
            var cashierId = Guid.NewGuid(); var phone = Phone(cashierId); var email = $"{label.ToLowerInvariant()}-cashier-{suffix.ToLowerInvariant()}@invalid.weymela.test";
            var cashier = new Cashier { Id = cashierId, MerchantId = merchant.Id, FirstName = "PILOT", LastName = $"Cashier {suffix}", Email = email, NormalizedEmail = email.ToUpperInvariant(), PhoneNumber = phone, NormalizedPhoneNumber = phone, IsActive = true, CreatedAtUtc = now };
            db.Cashiers.Add(cashier); db.CashierLocationAssignments.Add(new CashierLocationAssignment { Id = Guid.NewGuid(), CashierId = cashierId, MerchantLocationId = location.Id, IsPrimary = true, IsActive = true, CreatedAtUtc = now });
            var user = Account(UserRole.Cashier, email, phone, merchantId: merchant.Id, cashierId: cashierId); batch.Add((user, "Cashier", cashierId));
            actors.Add(new { role = "Cashier", name = $"{label} Cashier {suffix}", publicId = cashierId, businessId = merchant.PublicMerchantId });
        }

        db.OperationalAuditEvents.Add(new OperationalAuditEvent { Id = Guid.NewGuid(), EventType = "PilotTestActorsCreated", ActorUserId = current.UserAccountId, MetadataJson = JsonSerializer.Serialize(new { batchId, label, count = batch.Count }), CorrelationId = batchId.ToString("N"), CreatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { batchId, label, actors, temporaryCredentials = secrets });
    }

    private static async Task<IResult> Deactivate(Guid batchId, IWebHostEnvironment environment, ICurrentUserService current, ApplicationDbContext db, CancellationToken ct)
    {
        if (!PilotTestActorPolicy.IsEnabled(environment.EnvironmentName, UserRole.PlatformAdmin)) return Results.NotFound();
        if (current.UserAccountId is null) return Results.Unauthorized();
        var audit = await db.OperationalAuditEvents.SingleOrDefaultAsync(x => x.EventType == "PilotTestActorsCreated" && x.CorrelationId == batchId.ToString("N"), ct);
        if (audit is null) return Results.NotFound();
        var prefix = JsonDocument.Parse(audit.MetadataJson).RootElement.GetProperty("label").GetString()!;
        if (!PilotTestActorPolicy.IsDisposableLabel(prefix)) return Results.NotFound();
        await db.UserAccounts.Where(x => x.Email.StartsWith(prefix.ToLowerInvariant()) && x.Role != UserRole.PlatformAdmin).ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, AccountStatus.Suspended), ct);
        await db.Customers.Where(x => x.DisplayName.StartsWith(prefix)).ExecuteUpdateAsync(x => x.SetProperty(c => c.Status, CustomerStatus.Suspended), ct);
        await db.Creators.Where(x => x.DisplayName.StartsWith(prefix)).ExecuteUpdateAsync(x => x.SetProperty(c => c.Status, CreatorStatus.Suspended), ct);
        await db.Merchants.Where(x => x.TradingName.StartsWith(prefix)).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, MerchantStatus.Suspended), ct);
        await db.Cashiers.Where(x => x.Email.StartsWith(prefix.ToLowerInvariant())).ExecuteUpdateAsync(x => x.SetProperty(c => c.IsActive, false), ct);
        return Results.Ok(new { batchId, status = "Deactivated" });
    }

    private static string Phone(Guid id) => $"+2519{id.ToString("N")[..8]}";
    private static string Code(Guid id) => $"{int.Parse(id.ToString("N")[..4], System.Globalization.NumberStyles.HexNumber) % 9000 + 1000}";
    private static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
}
