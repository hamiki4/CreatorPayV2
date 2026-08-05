namespace CreatorPay.Application.Authentication;

public sealed class JwtOptions { public const string SectionName = "Authentication:Jwt"; public string Issuer { get; set; } = string.Empty; public string Audience { get; set; } = string.Empty; public string SigningKey { get; set; } = string.Empty; public int AccessTokenMinutes { get; set; } = 15; public int RefreshTokenDays { get; set; } = 30; }
public sealed class PasswordOptions { public const string SectionName = "Authentication:Password"; public int MinimumLength { get; set; } = 8; public int MaximumLength { get; set; } = 128; public bool RequireUppercase { get; set; } = true; public bool RequireLowercase { get; set; } = true; public bool RequireDigit { get; set; } = true; public bool RequireNonAlphanumeric { get; set; } = true; }
public sealed class LockoutOptions { public const string SectionName = "Authentication:Lockout"; public int MaxFailedAttempts { get; set; } = 5; public int LockoutMinutes { get; set; } = 15; }
public sealed class PasswordResetOptions { public const string SectionName = "Authentication:PasswordReset"; public int TokenLifetimeMinutes { get; set; } = 30; }
