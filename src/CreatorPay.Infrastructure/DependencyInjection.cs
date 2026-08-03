using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Application.Authentication;
using CreatorPay.Infrastructure.Authentication;

namespace CreatorPay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString("CreatorPayDatabase")
            ?? throw new InvalidOperationException("Connection string 'CreatorPayDatabase' is not configured.");
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PasswordOptions>(configuration.GetSection(PasswordOptions.SectionName));
        services.Configure<LockoutOptions>(configuration.GetSection(LockoutOptions.SectionName));
        services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<IUtcClock, UtcClock>(); services.AddSingleton<ITokenService, TokenService>(); services.AddSingleton<IPasswordResetNotifier, SafePasswordResetNotifier>();
        return services;
    }
}
