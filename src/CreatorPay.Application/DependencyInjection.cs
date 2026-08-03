using Microsoft.Extensions.DependencyInjection;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Creators;

namespace CreatorPay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<PasswordPolicyValidator>();
        services.AddScoped<ICreatorService, CreatorService>();
        return services;
    }
}
