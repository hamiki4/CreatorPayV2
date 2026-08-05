using System.Text.Json;
using CreatorPay.Application.Creators;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Tests;

public sealed class CreatorRegistrationSerializationTests
{
    [Fact]
    public void Reduced_creator_request_accepts_public_social_platform_name()
    {
        const string json = """
            {"firstName":"Abebe","lastName":"Kebede","displayName":"Abebe","phoneNumber":"0911000022","email":"creator01@example.com","password":"Welcome1!","preferredLanguage":"en","city":"Addis Ababa","biography":"","contentCategories":"","termsAccepted":true,"socialProfiles":[{"platform":"TikTok","handle":"","profileUrl":"https://www.tiktok.com/@abebe","followerCount":50000,"isPrimary":true}]}
            """;

        var request = JsonSerializer.Deserialize<RegisterCreatorRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        var social = Assert.Single(request.SocialProfiles!);
        Assert.Equal(SocialPlatform.TikTok, social.Platform);
        Assert.Equal("https://www.tiktok.com/@abebe", social.ProfileUrl);
        Assert.Equal(50000, social.FollowerCount);
        Assert.True(social.IsPrimary);
    }

    [Fact]
    public void Unknown_social_platform_is_rejected_during_request_binding()
    {
        const string json = """
            {"platform":"NotAPlatform","handle":"","profileUrl":"https://example.com/abebe","followerCount":1,"isPrimary":true}
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SocialProfileRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
