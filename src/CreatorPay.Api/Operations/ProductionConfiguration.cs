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
            var phone = configuration.GetSection(CustomerVerificationOptions.SectionName).Get<CustomerVerificationOptions>() ?? new();
            if (phone.HmacSecret.Length < 32) errors.Add("CustomerVerification:HmacSecret (minimum 32 characters)");
            if (phone.EncryptionKey.Length < 32) errors.Add("CustomerVerification:EncryptionKey (minimum 32 characters)");
            var flags = configuration.GetSection(FeatureFlagOptions.SectionName).Get<FeatureFlagOptions>() ?? new();
            if (flags.DevelopmentOtpReveal || flags.DevelopmentInvitationTokenReveal) errors.Add("development reveal feature flags must be false");
        }
        if (errors.Count > 0) throw new InvalidOperationException($"Invalid production configuration: {string.Join(", ", errors)}.");
    }
}
