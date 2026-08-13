using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Application.Operations;
using Npgsql;

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
            static bool Placeholder(string value)
            {
                var normalized = value.Replace("_", "-", StringComparison.Ordinal).Replace(" ", "-", StringComparison.Ordinal);
                return normalized.Contains("replace-with", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("replace-in", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("change-me", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("changeme", StringComparison.OrdinalIgnoreCase);
            }
            if (Placeholder(jwt.SigningKey)) errors.Add("Authentication:Jwt:SigningKey must not be a placeholder");
            var databaseConnection = configuration.GetConnectionString("CreatorPayDatabase")!;
            if (Placeholder(databaseConnection)) errors.Add("ConnectionStrings:CreatorPayDatabase must come from a secret source");
            try
            {
                var database = new NpgsqlConnectionStringBuilder(databaseConnection);
                if (string.IsNullOrEmpty(database.Password) || database.Password.Length < 24) errors.Add("ConnectionStrings:CreatorPayDatabase password (minimum 24 characters)");
            }
            catch (ArgumentException) { errors.Add("ConnectionStrings:CreatorPayDatabase must be a valid PostgreSQL connection string"); }
            var phone = configuration.GetSection(CustomerVerificationOptions.SectionName).Get<CustomerVerificationOptions>() ?? new();
            if (phone.HmacSecret.Length < 32) errors.Add("CustomerVerification:HmacSecret (minimum 32 characters)");
            if (phone.EncryptionKey.Length < 32) errors.Add("CustomerVerification:EncryptionKey (minimum 32 characters)");
            if (Placeholder(phone.HmacSecret) || Placeholder(phone.EncryptionKey)) errors.Add("CustomerVerification secrets must not be placeholders");
            var sms = configuration.GetSection(SmsOtpOptions.SectionName).Get<SmsOtpOptions>() ?? new();
            if (sms.HashSecret.Length < 32 || Placeholder(sms.HashSecret)) errors.Add("SmsOtp:HashSecret must be a non-placeholder secret of at least 32 characters");
            if (sms.OtpExpiryMinutes is < 1 or > 15 || sms.ResendCooldownSeconds < 30 || sms.MaxAttempts is < 1 or > 10) errors.Add("SmsOtp security limits are invalid");
            if (environment.IsProduction() && sms.SmsProvider == "PilotTest") errors.Add("SmsOtp:PilotTest is forbidden in Production");
            if (sms.PilotRegistrationAutoVerifyEnabled && !environment.IsEnvironment("Pilot") && !environment.IsEnvironment("E2E") && !environment.IsEnvironment("Test")) errors.Add("SmsOtp:PilotRegistrationAutoVerifyEnabled is forbidden outside Pilot/Test environments");
            if (sms.PilotRegistrationAutoVerifyEnabled && sms.SmsProvider != "PilotTest") errors.Add("SmsOtp:PilotRegistrationAutoVerifyEnabled requires PilotTest");
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
            var publicAppBaseUrl=configuration["PublicAppBaseUrl"];
            if(!Uri.TryCreate(publicAppBaseUrl,UriKind.Absolute,out var publicAppUri)||(!environment.IsEnvironment("E2E")&&publicAppUri.Scheme!=Uri.UriSchemeHttps)||(environment.IsEnvironment("E2E")&&publicAppUri.Scheme is not ("http" or "https"))||publicAppUri.UserInfo.Length>0||publicAppUri.Query.Length>0||publicAppUri.Fragment.Length>0)
                errors.Add("PublicAppBaseUrl must be an HTTPS URL without credentials, query, or fragment");
        }
        if (environment.IsEnvironment("Pilot"))
        {
            var pilot = configuration.GetSection(PilotOptions.SectionName).Get<PilotOptions>() ?? new();
            var flags = configuration.GetSection(FeatureFlagOptions.SectionName).Get<FeatureFlagOptions>() ?? new();
            if (!pilot.Enabled) errors.Add("Pilot:Enabled must be true");
            if (!pilot.RequireHttps) errors.Add("Pilot:RequireHttps must be true");
            if (!pilot.AuditLoggingEnabled) errors.Add("Pilot:AuditLoggingEnabled must be true");
            if (!pilot.HealthMonitoringEnabled) errors.Add("Pilot:HealthMonitoringEnabled must be true");
            if (!pilot.ManualWalletFundingOnly) errors.Add("Pilot:ManualWalletFundingOnly must be true");
            if (pilot.MaximumBusinesses <= 0 || pilot.MaximumCreators <= 0) errors.Add("Pilot participant limits must be positive");
            if (pilot.MaximumPurchaseAmount <= 0 || pilot.MaximumCommissionAmount <= 0 || pilot.DailyMerchantSpendingLimit <= 0 || pilot.ShopperCashbackLimit <= 0 || pilot.CreatorEarningLimit <= 0) errors.Add("Pilot financial limits must be positive");
            if (pilot.PayoutHoldDays < 1) errors.Add("Pilot:PayoutHoldDays must be at least one day");
            if (flags.AutomaticPayouts) errors.Add("FeatureFlags:AutomaticPayouts must be false in Pilot");
            if (flags.ExternalPaymentProvider) errors.Add("FeatureFlags:ExternalPaymentProvider must be false in Pilot unless separately reviewed");
            var supportEmail = configuration["Support:Email"];
            if (string.IsNullOrWhiteSpace(supportEmail) || !supportEmail.Contains('@')) errors.Add("Support:Email");
            var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new();
            if (string.IsNullOrWhiteSpace(storage.Provider)) errors.Add("Storage:Provider");
            var pilotCors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
            if (pilotCors.AllowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)) errors.Add("Cors:AllowedOrigins (Pilot origins must use HTTPS)");
            var sms = configuration.GetSection(SmsOtpOptions.SectionName).Get<SmsOtpOptions>() ?? new();
            if (sms.SmsProvider != "PilotTest" || sms.TestCode.Length != 6 || !sms.TestCode.All(char.IsAsciiDigit)) errors.Add("Pilot SmsOtp requires PilotTest with a six-digit environment-provided TestCode");
        }
        if (errors.Count > 0) throw new InvalidOperationException($"Invalid production configuration: {string.Join(", ", errors)}.");
    }
}
