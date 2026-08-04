using System.IdentityModel.Tokens.Jwt;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace CreatorPay.Infrastructure.Tests;

public sealed class AuthenticationSecurityTests
{
    [Fact]
    public void Password_hash_verifies_and_does_not_contain_password()
    {
        var service = new PasswordHasherService(); var user = new UserAccount(); var hash = service.Hash(user, "SecurePass1!");
        Assert.DoesNotContain("SecurePass1!", hash); Assert.NotEqual(PasswordVerification.Failed, service.Verify(user, hash, "SecurePass1!")); Assert.Equal(PasswordVerification.Failed, service.Verify(user, hash, "wrong"));
    }
    [Fact]
    public void Opaque_tokens_are_random_and_hash_is_stable()
    {
        var service = Service(); var first = service.CreateOpaqueToken(); var second = service.CreateOpaqueToken();
        Assert.NotEqual(first, second); Assert.Equal(service.HashToken(first), service.HashToken(first)); Assert.DoesNotContain(first, service.HashToken(first));
    }
    [Fact]
    public void Jwt_contains_required_and_scoping_claims_only()
    {
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "a@example.com", Role = UserRole.MerchantAdmin, MerchantId = Guid.NewGuid() };
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Service().CreateAccessToken(user).Token);
        Assert.Equal(user.Id.ToString(), jwt.Subject); Assert.Contains(jwt.Claims, x => x.Type == "merchant_id" && x.Value == user.MerchantId.ToString()); Assert.DoesNotContain(jwt.Claims, x => x.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }
    private static TokenService Service() => new(Options.Create(new JwtOptions { Issuer = "tests", Audience = "tests", SigningKey = "a-test-signing-key-that-is-at-least-32-characters" }), new FakeClock());
    private sealed class FakeClock : IUtcClock { public DateTime UtcNow => new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc); }
}
