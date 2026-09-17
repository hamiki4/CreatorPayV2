using Microsoft.Extensions.Configuration;

namespace CreatorPay.Infrastructure.Integration;

public sealed class V3IntegrationOptions
{
    public const string SectionName = "V3Integration";
    public bool Enabled { get; init; }
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Environment { get; init; } = "";
    public string V3ApiUrl { get; init; } = "";
    public string V3WebUrl { get; init; } = "";
    public string ProductWebUrl { get; init; } = "";
    public string CallbackId { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public Guid? PlatformAdminUserAccountId { get; init; }
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan RevalidationInterval { get; init; } = TimeSpan.FromMinutes(5);

    public static V3IntegrationOptions Load(IConfiguration configuration, string environment)
    {
        var section = configuration.GetSection(SectionName);
        var enabled = section.GetValue<bool>("Enabled");
        if (!enabled) return new();
        var result = new V3IntegrationOptions
        {
            Enabled = true,
            Issuer = section["Issuer"] ?? "",
            Audience = section["Audience"] ?? "",
            Environment = section["Environment"] ?? "",
            V3ApiUrl = section["V3ApiUrl"] ?? "",
            V3WebUrl = section["V3WebUrl"] ?? "",
            ProductWebUrl = section["ProductWebUrl"] ?? "",
            CallbackId = section["CallbackId"] ?? "",
            ClientId = section["ClientId"] ?? "",
            ClientSecret = section["ClientSecret"] ?? "",
            PlatformAdminUserAccountId = section.GetValue<Guid?>("PlatformAdminUserAccountId")
        };
        Require(result.Environment == environment, "V3 integration environment must match this runtime.");
        Require(result.Issuer.Length is >= 3 and <= 100 && result.Audience.Length is >= 3 and <= 100,
            "V3 integration issuer and audience are required.");
        Require(Origin(result.V3ApiUrl, environment) && Origin(result.V3WebUrl, environment)
            && Origin(result.ProductWebUrl, environment), "V3 integration origins are invalid.");
        Require(result.CallbackId.Length is >= 3 and <= 80 && result.ClientId.Length is >= 8 and <= 100
            && result.ClientSecret.Length >= 32, "Protected V3 integration credentials are required.");
        return result;
    }
    private static bool Origin(string value, string environment) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || environment is "Development" or "E2E" && uri.Scheme == "http" && uri.IsLoopback)
        && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

public sealed class V3ExternalAuthenticationPolicy(V3IntegrationOptions options)
    : CreatorPay.Application.Authentication.IExternalAuthenticationPolicy
{
    public bool LocalAuthenticationAllowed(CreatorPay.Domain.Entities.UserAccount user) =>
        user.AuthenticationSource == CreatorPay.Domain.Enums.AuthenticationSource.Local
        && (!options.Enabled || options.PlatformAdminUserAccountId != user.Id);
}
