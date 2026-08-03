using Microsoft.Extensions.DependencyInjection;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Creators;
using CreatorPay.Application.Merchants;

namespace CreatorPay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<PasswordPolicyValidator>();
        services.AddScoped<ICreatorService, CreatorService>();
        services.AddScoped<IMerchantService, MerchantService>();
        return services;
    }
}
