using Microsoft.Extensions.DependencyInjection;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Creators;
using CreatorPay.Application.Merchants;
using CreatorPay.Application.Organization;

namespace CreatorPay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IExternalAuthenticationPolicy, LocalAuthenticationPolicy>();
        services.AddSingleton<PasswordPolicyValidator>();
        services.AddScoped<ICreatorService, CreatorService>();
        services.AddScoped<IMerchantService, MerchantService>();
        services.AddSingleton<IMerchantScopeAuthorizer, MerchantScopeAuthorizer>();
        return services;
    }
}
