using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CreatorPay.Infrastructure.Authentication;

public sealed class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<UserAccount> _hasher = new();
    public string Hash(UserAccount user, string password) => _hasher.HashPassword(user, password);
    public PasswordVerification Verify(UserAccount user, string hash, string password) => _hasher.VerifyHashedPassword(user, hash, password) switch { PasswordVerificationResult.Success => PasswordVerification.Success, PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded, _ => PasswordVerification.Failed };
}
public sealed class UtcClock : IUtcClock { public DateTime UtcNow => DateTime.UtcNow; }
public sealed class TokenService(IOptions<JwtOptions> options, IUtcClock clock) : ITokenService
{
    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(UserAccount user)
    {
        var o = options.Value; var now = clock.UtcNow; var expires = now.AddMinutes(o.AccessTokenMinutes);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, user.Role.ToString()), new(JwtRegisteredClaimNames.Email, user.Email), new(AuthenticationClaimTypes.AccountStatus, user.Status.ToString()), new(AuthenticationClaimTypes.EmailVerified, user.IsEmailVerified.ToString().ToLowerInvariant()), new(AuthenticationClaimTypes.PhoneVerified, user.IsPhoneVerified.ToString().ToLowerInvariant()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")), new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64) };
        Add(claims, "creator_id", user.CreatorId); Add(claims, "customer_id", user.CustomerId); Add(claims, "merchant_id", user.MerchantId); Add(claims, "supervisor_id", user.SupervisorId); Add(claims, "cashier_id", user.CashierId);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(o.Issuer, o.Audience, claims, now, expires, credentials); return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
    public string CreateOpaqueToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static void Add(List<Claim> claims, string type, Guid? value) { if (value.HasValue) claims.Add(new(type, value.Value.ToString())); }
}
public sealed class SafePasswordResetNotifier(ILogger<SafePasswordResetNotifier> logger) : IPasswordResetNotifier
{
    public Task NotifyAsync(UserAccount user, string rawToken, CancellationToken cancellationToken) { logger.LogInformation("Password reset notification requested for user {UserId}; delivery provider is not configured.", user.Id); return Task.CompletedTask; }
}
