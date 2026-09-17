using System.IdentityModel.Tokens.Jwt;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Authentication;
using CreatorPay.Infrastructure.Integration;
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
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "a@example.com", Role = UserRole.MerchantAdmin, Status = AccountStatus.PendingApproval, MerchantId = Guid.NewGuid(), IsEmailVerified = true };
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Service().CreateAccessToken(user).Token);
        Assert.Equal(user.Id.ToString(), jwt.Subject); Assert.Contains(jwt.Claims, x => x.Type == "merchant_id" && x.Value == user.MerchantId.ToString()); Assert.Contains(jwt.Claims, x => x.Type == AuthenticationClaimTypes.AccountStatus && x.Value == AccountStatus.PendingApproval.ToString()); Assert.Contains(jwt.Claims, x => x.Type == AuthenticationClaimTypes.EmailVerified && x.Value == "true"); Assert.DoesNotContain(jwt.Claims, x => x.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void V3_external_accounts_can_never_be_local_sign_in_authority()
    {
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            AuthenticationSource = AuthenticationSource.V3External,
            Role = UserRole.Customer,
            Status = AccountStatus.Active,
            PasswordHash = "not-an-authority",
            PinHash = "not-an-authority",
            FirebaseUid = "not-an-authority"
        };
        Assert.False(AuthenticationService.CanSignIn(user));
    }
    [Fact]
    public void Configured_platform_admin_is_external_only_before_first_handoff()
    {
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            AuthenticationSource = AuthenticationSource.Local,
            Role = UserRole.PlatformAdmin,
            Status = AccountStatus.Active
        };
        var policy = new V3ExternalAuthenticationPolicy(new V3IntegrationOptions
        {
            Enabled = true,
            PlatformAdminUserAccountId = user.Id
        });
        Assert.False(policy.LocalAuthenticationAllowed(user));
        Assert.True(policy.LocalAuthenticationAllowed(new UserAccount
        {
            Id = Guid.NewGuid(),
            AuthenticationSource = AuthenticationSource.Local,
            Role = UserRole.Customer,
            Status = AccountStatus.Active
        }));
    }
    private static TokenService Service() => new(Options.Create(new JwtOptions { Issuer = "tests", Audience = "tests", SigningKey = new string('j', 64) }), new FakeClock());
    private sealed class FakeClock : IUtcClock { public DateTime UtcNow => new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc); }
}
