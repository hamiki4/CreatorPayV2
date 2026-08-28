using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class CreatorProfilePhotoTests : IAsyncLifetime
{
    private const string SigningKey = "development-only-replace-this-signing-key-000000";
    private static readonly Guid MerchantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid MerchantUserId = Guid.Parse("20000000-0000-0000-0000-000000000008");
    private static readonly Guid CustomerUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CustomerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_creator_profile_photo").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    private readonly string photoRoot = Path.Combine(Path.GetTempPath(), $"creatorpay-profile-photos-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program>? factory;
    private int sequence;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        Directory.CreateDirectory(photoRoot);
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CreatorPayDatabase"] = database.GetConnectionString(),
            ["Authentication:Jwt:Issuer"] = "CreatorPay",
            ["Authentication:Jwt:Audience"] = "CreatorPay.Web",
            ["Authentication:Jwt:SigningKey"] = SigningKey,
            ["CustomerVerification:HmacSecret"] = new string('h', 32),
            ["CustomerVerification:EncryptionKey"] = new string('e', 32),
            ["SmsOtp:SmsProvider"] = "PilotTest",
            ["SmsOtp:HashSecret"] = new string('o', 32),
            ["SmsOtp:TestCode"] = "654321",
            ["Support:Email"] = "tests@example.invalid",
            ["Storage:Provider"] = "MetadataOnly",
            ["CreatorProfilePhotos:RootPath"] = photoRoot
        };
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            foreach (var value in values) builder.UseSetting(value.Key, value.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        });
        await using var db = Db();
        await db.Database.MigrateAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        await database.DisposeAsync();
        if (Directory.Exists(photoRoot)) Directory.Delete(photoRoot, true);
    }

    [DockerFact]
    public async Task Creator_profile_photo_upload_replace_remove_and_public_route_work()
    {
        var creator = await AddCreator("Profile Photo Creator", "https://www.tiktok.com/@photo-creators", 12345);
        using var client = Client(creator.UserId, creator.CreatorId);
        using var merchant = Client(MerchantUserId, null, MerchantId);
        var png = PngBytes();
        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 2000 WHERE \"MerchantId\" = {MerchantId}");
            await db.SaveChangesAsync();
        }

        var first = await Upload(client, png, "photo.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var firstFile = firstBody.GetProperty("profileImage").GetProperty("fileName").GetString();
        Assert.False(string.IsNullOrWhiteSpace(firstFile));
        Assert.True(firstBody.GetProperty("profileImage").GetProperty("sizeBytes").GetInt64() > 0);

        var requested = await Post(client, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Please approve" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" })).StatusCode);

        var me = await Get(client, "/api/v1/creators/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(firstFile, meBody!.GetProperty("profileImage").GetProperty("fileName").GetString());

        var publicProfile = await GetAnon($"/api/v1/discovery/creators/{Uri.EscapeDataString(creator.PublicCreatorId)}");
        Assert.Equal(HttpStatusCode.OK, publicProfile.StatusCode);
        var publicBody = await publicProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains("/photo?v=", publicBody!.GetProperty("profileImageUrl").GetString());

        var publicPhoto = await GetAnon($"/api/v1/discovery/creators/{Uri.EscapeDataString(creator.PublicCreatorId)}/photo?v={Uri.EscapeDataString(firstFile!)}");
        Assert.Equal(HttpStatusCode.OK, publicPhoto.StatusCode);
        Assert.StartsWith("image/png", publicPhoto.Content.Headers.ContentType?.MediaType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("cross-origin", publicPhoto.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        var photoBytes = await publicPhoto.Content.ReadAsByteArrayAsync();
        Assert.True(photoBytes.AsSpan(0, PngSignature().Length).SequenceEqual(PngSignature()));

        var second = await Upload(client, png, "photo-2.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var secondFile = secondBody.GetProperty("profileImage").GetProperty("fileName").GetString();
        Assert.NotEqual(firstFile, secondFile);

        var removed = await Delete(client, "/api/v1/creators/me/profile-photo");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var removedBody = await removed.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(System.Text.Json.JsonValueKind.Null, removedBody!.GetProperty("profileImage").ValueKind);

        var missing = await GetAnon($"/api/v1/discovery/creators/{Uri.EscapeDataString(creator.PublicCreatorId)}/photo?v={Uri.EscapeDataString(secondFile!)}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [DockerFact]
    public async Task Creator_photo_urls_are_reused_in_business_and_customer_discovery()
    {
        var merchant = Client(MerchantUserId, null, MerchantId);
        var searchCreator = await AddCreator("Search Photo Creator", "https://www.instagram.com/search-photo", 3001);
        var activeCreator = await AddCreator("Active Photo Creator", "https://www.youtube.com/@active-photo", 7500);
        var pendingCreator = await AddCreator("Pending Photo Creator", "https://www.facebook.com/pending-photo", 1500);
        var customer = CustomerClient(CustomerUserId, CustomerId);
        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 2000 WHERE \"MerchantId\" = {MerchantId}");
            await db.SaveChangesAsync();
        }
        using var activeClient = Client(activeCreator.UserId, activeCreator.CreatorId);
        var png = PngBytes();
        Assert.Equal(HttpStatusCode.OK, (await Upload(activeClient, png, "active.png", "image/png")).StatusCode);
        using var searchClient = Client(searchCreator.UserId, searchCreator.CreatorId);
        Assert.Equal(HttpStatusCode.OK, (await Upload(searchClient, png, "search.png", "image/png")).StatusCode);
        using var pendingClient = Client(pendingCreator.UserId, pendingCreator.CreatorId);
        Assert.Equal(HttpStatusCode.OK, (await Upload(pendingClient, png, "pending.png", "image/png")).StatusCode);
        var requested = await Post(activeClient, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Please approve" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" })).StatusCode);
        const string videoUrl = "https://www.tiktok.com/@active-photo/video/1234567890123456789";
        var promoVideo = await Post(activeClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl });
        Assert.Equal(HttpStatusCode.Created, promoVideo.StatusCode);
        var promoVideoId = (await promoVideo.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/promotion-videos/{promoVideoId}/approve", new { reason = (string?)null })).StatusCode);

        var invited = await Post(merchant, "/api/v1/merchant/partnerships/invitations", new { creatorId = pendingCreator.CreatorId, introductoryMessage = "Join us" });
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);

        var merchantSearch = await Get(merchant, "/api/v1/merchant/creators/search?q=Search");
        Assert.Equal(HttpStatusCode.OK, merchantSearch.StatusCode);
        var merchantRows = await merchantSearch.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var searchRow = Assert.Single(merchantRows!.EnumerateArray(), x => x.GetProperty("publicCreatorId").GetString() == searchCreator.PublicCreatorId);
        Assert.Contains("/photo?v=", searchRow.GetProperty("profileImageUrl").GetString());

        var creatorAds = await Get(activeClient, "/api/v1/creator/partnerships");
        Assert.Equal(HttpStatusCode.OK, creatorAds.StatusCode);
        var creatorRows = await creatorAds.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var activeRow = Assert.Single(creatorRows!.EnumerateArray(), x => x.GetProperty("merchantId").GetGuid() == MerchantId);
        Assert.Contains("/photo?v=", activeRow.GetProperty("creatorProfileImageUrl").GetString());

        var merchantRequests = await Get(merchant, "/api/v1/merchant/partnerships?status=Pending");
        Assert.Equal(HttpStatusCode.OK, merchantRequests.StatusCode);
        var requestRows = await merchantRequests.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var pendingRow = Assert.Single(requestRows!.EnumerateArray(), x => x.GetProperty("creatorId").GetGuid() == pendingCreator.CreatorId);
        Assert.Contains("/photo?v=", pendingRow.GetProperty("creatorProfileImageUrl").GetString());

        var advertising = await Get(customer, "/api/v1/customer/discovery/advertising");
        Assert.Equal(HttpStatusCode.OK, advertising.StatusCode);
        var advertisingRows = await advertising.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var advertisingRow = Assert.Single(advertisingRows!.EnumerateArray(), x => x.GetProperty("creatorId").GetGuid() == activeCreator.CreatorId);
        Assert.Contains("/photo?v=", advertisingRow.GetProperty("creatorProfileImageUrl").GetString());
        Assert.Equal(videoUrl, advertisingRow.GetProperty("promotionVideoUrl").GetString());
        Assert.Equal("Live", advertisingRow.GetProperty("promotionVideoStatus").GetString());

        var businessDetail = await Get(customer, $"/api/v1/customer/discovery/businesses/{MerchantId}");
        Assert.Equal(HttpStatusCode.OK, businessDetail.StatusCode);
        var businessBody = await businessDetail.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var creatorDetail = Assert.Single(businessBody!.GetProperty("creators").EnumerateArray(), x => x.GetProperty("creatorId").GetGuid() == activeCreator.CreatorId);
        Assert.Contains("/photo?v=", creatorDetail.GetProperty("creatorProfileImageUrl").GetString());
    }

    [DockerFact]
    public async Task Invalid_uploads_are_rejected()
    {
        var creator = await AddCreator("Invalid Upload Creator");
        using var client = Client(creator.UserId, creator.CreatorId);
        var invalid = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes("not an image"));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        invalid.Add(bytes, "photo", "photo.txt");
        var response = await client.PostAsync("/api/v1/creators/me/profile-photo", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(Guid CreatorId, Guid UserId, string PublicCreatorId)> AddCreator(string name, string socialProfileUrl = "https://www.tiktok.com/@photo", long followers = 0)
    {
        var n = Interlocked.Increment(ref sequence);
        var creatorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var phone = $"+251988{n:000000}";
        var publicCreatorId = $"CR-TEST-{n:N0}";
        await using var db = Db();
        db.Creators.Add(new Creator
        {
            Id = creatorId,
            PublicCreatorId = publicCreatorId,
            CreatorCode = (4000 + n).ToString(),
            DisplayName = name,
            PhoneNumber = phone,
            NormalizedPhoneNumber = phone,
            City = "Addis Ababa",
            Status = CreatorStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            PhoneNumber = phone,
            NormalizedPhoneNumber = phone,
            Role = UserRole.Creator,
            Status = AccountStatus.Active,
            CreatorId = creatorId,
            IsPhoneVerified = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        db.CreatorSocialProfiles.Add(new CreatorSocialProfile
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Platform = SocialPlatform.TikTok,
            Handle = socialProfileUrl,
            ProfileUrl = socialProfileUrl,
            FollowerCount = followers,
            IsPrimary = true,
            VerificationStatus = SocialProfileVerificationStatus.Unverified,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (creatorId, userId, publicCreatorId);
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);
    private HttpClient Client(Guid userId, Guid? creatorId, Guid? merchantId = null)
    {
        var client = factory!.CreateClient(); var role = merchantId.HasValue ? UserRole.MerchantAdmin : UserRole.Creator;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, role.ToString()), new(AuthenticationClaimTypes.AccountStatus, AccountStatus.Active.ToString()), new("test_token", "true") };
        if (creatorId.HasValue) claims.Add(new("creator_id", creatorId.Value.ToString())); if (merchantId.HasValue) claims.Add(new("merchant_id", merchantId.Value.ToString()));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client;
    }
    private HttpClient CustomerClient(Guid userId, Guid customerId)
    {
        var client = factory!.CreateClient();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, UserRole.Customer.ToString()), new(AuthenticationClaimTypes.AccountStatus, AccountStatus.Active.ToString()), new("test_token", "true"), new("customer_id", customerId.ToString()) };
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client;
    }
    private static async Task<HttpResponseMessage> Upload(HttpClient client, byte[] bytes, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "photo", fileName);
        return await client.PostAsync("/api/v1/creators/me/profile-photo", form);
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) => client.PostAsJsonAsync(path, body);
    private static Task<HttpResponseMessage> Delete(HttpClient client, string path) => client.DeleteAsync(path);
    private static Task<HttpResponseMessage> Get(HttpClient client, string path) => client.GetAsync(path);
    private HttpClient AnonClient() => factory!.CreateClient();
    private Task<HttpResponseMessage> GetAnon(string path) => AnonClient().GetAsync(path);
    private static byte[] PngBytes()
    {
        using var image = new Image<Rgba32>(1, 1);
        image[0, 0] = new Rgba32(255, 0, 0);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
    private static byte[] PngSignature() => [137, 80, 78, 71, 13, 10, 26, 10];
}
