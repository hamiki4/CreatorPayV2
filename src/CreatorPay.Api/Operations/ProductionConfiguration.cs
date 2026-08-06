using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Application.Operations;

namespace CreatorPay.Api.Operations;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var errors = new List<string>();
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(jwt.Issuer)) errors.Add("Authentication:Jwt:Issuer");
        if (string.IsNullOrWhiteSpace(jwt.Audience)) errors.Add("Authentication:Jwt:Audience");
        if (jwt.SigningKey.Length < 32) errors.Add("Authentication:Jwt:SigningKey (minimum 32 characters)");
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("CreatorPayDatabase"))) errors.Add("ConnectionStrings:CreatorPayDatabase");
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test"))
        {
            static bool Placeholder(string value) => value.Contains("replace-with", StringComparison.OrdinalIgnoreCase) || value.Contains("changeme", StringComparison.OrdinalIgnoreCase);
            if (Placeholder(jwt.SigningKey)) errors.Add("Authentication:Jwt:SigningKey must not be a placeholder");
            if (Placeholder(configuration.GetConnectionString("CreatorPayDatabase")!)) errors.Add("ConnectionStrings:CreatorPayDatabase must come from a secret source");
            var phone = configuration.GetSection(CustomerVerificationOptions.SectionName).Get<CustomerVerificationOptions>() ?? new();
            if (phone.HmacSecret.Length < 32) errors.Add("CustomerVerification:HmacSecret (minimum 32 characters)");
            if (phone.EncryptionKey.Length < 32) errors.Add("CustomerVerification:EncryptionKey (minimum 32 characters)");
            if (Placeholder(phone.HmacSecret) || Placeholder(phone.EncryptionKey)) errors.Add("CustomerVerification secrets must not be placeholders");
            var flags = configuration.GetSection(FeatureFlagOptions.SectionName).Get<FeatureFlagOptions>() ?? new();
            if (flags.DevelopmentOtpReveal || flags.DevelopmentInvitationTokenReveal) errors.Add("development reveal feature flags must be false");
            var cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
            if (cors.AllowedOrigins.Length == 0) errors.Add("Cors:AllowedOrigins (at least one explicit origin)");
            if (environment.IsProduction() && cors.AllowedOrigins.Any(origin =>
                    !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                errors.Add("Cors:AllowedOrigins (Production origins must use HTTPS)");
            var rateLimits = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new();
            if (rateLimits.AuthPermitLimit <= 0 || rateLimits.FinancialPermitLimit <= 0 || rateLimits.WindowSeconds <= 0)
                errors.Add("RateLimiting limits and window must be positive");
            var health = configuration.GetSection(HealthOptions.SectionName).Get<HealthOptions>() ?? new();
            if (health.TimeoutSeconds is < 1 or > 30) errors.Add("HealthChecks:TimeoutSeconds must be between 1 and 30");
            var monitoring = configuration.GetSection(ErrorMonitoringOptions.SectionName).Get<ErrorMonitoringOptions>() ?? new();
            if (monitoring.Enabled && (!Uri.TryCreate(monitoring.Endpoint, UriKind.Absolute, out var monitorUri) || monitorUri.Scheme != Uri.UriSchemeHttps)) errors.Add("ErrorMonitoring:Endpoint must be an HTTPS URI when enabled");
            var proxy = configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new();
            if (proxy.KnownProxies.Any(value => !System.Net.IPAddress.TryParse(value, out _)))
                errors.Add("ReverseProxy:KnownProxies (IP addresses only)");
        }
        if (errors.Count > 0) throw new InvalidOperationException($"Invalid production configuration: {string.Join(", ", errors)}.");
    }
}
