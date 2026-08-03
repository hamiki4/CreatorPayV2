using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Application.Authentication;
using CreatorPay.Infrastructure.Authentication;
using CreatorPay.Application.Creators;
using CreatorPay.Infrastructure.Creators;
using CreatorPay.Application.Merchants;
using CreatorPay.Infrastructure.Merchants;

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
        services.Configure<CreatorVerificationOptions>(configuration.GetSection(CreatorVerificationOptions.SectionName));
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<IUtcClock, UtcClock>(); services.AddSingleton<ITokenService, TokenService>(); services.AddSingleton<IPasswordResetNotifier, SafePasswordResetNotifier>();
        services.AddScoped<ICreatorStore, CreatorStore>(); services.AddSingleton<ICreatorVerificationProvider, DevelopmentCreatorVerificationProvider>();
        services.AddScoped<IMerchantStore, MerchantStore>(); services.AddSingleton<IMerchantVerificationProvider, DevelopmentMerchantVerificationProvider>(); services.AddSingleton<IMerchantDocumentStorage, MetadataOnlyMerchantDocumentStorage>();
        return services;
    }
}
