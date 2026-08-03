using Microsoft.Extensions.DependencyInjection;
using CreatorPay.Application.Authentication;

namespace CreatorPay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<PasswordPolicyValidator>();
        return services;
    }
}
