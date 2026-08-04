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
using CreatorPay.Application.Organization;
using CreatorPay.Infrastructure.Organization;
using CreatorPay.Application.Qr;
using CreatorPay.Infrastructure.Qr;
using CreatorPay.Application.Commission;
using CreatorPay.Infrastructure.Commission;
using CreatorPay.Application.Wallet;
using CreatorPay.Infrastructure.Wallet;
using CreatorPay.Application.Earnings;
using CreatorPay.Infrastructure.Earnings;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Infrastructure.CustomerVerification;
using CreatorPay.Application.Notifications;
using CreatorPay.Infrastructure.Notifications;
using CreatorPay.Application.Operations;
using CreatorPay.Application.Risk;
using CreatorPay.Infrastructure.Risk;
using CreatorPay.Application.OfflineSync;
using CreatorPay.Infrastructure.OfflineSync;

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
        services.Configure<StaffInvitationOptions>(configuration.GetSection(StaffInvitationOptions.SectionName));
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<IUtcClock, UtcClock>(); services.AddSingleton<ITokenService, TokenService>(); services.AddSingleton<IPasswordResetNotifier, SafePasswordResetNotifier>();
        services.AddScoped<ICreatorStore, CreatorStore>(); services.AddSingleton<ICreatorVerificationProvider, DevelopmentCreatorVerificationProvider>();
        services.AddScoped<IMerchantStore, MerchantStore>(); services.AddSingleton<IMerchantVerificationProvider, DevelopmentMerchantVerificationProvider>(); services.AddSingleton<IMerchantDocumentStorage, MetadataOnlyMerchantDocumentStorage>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddSingleton<IQrTokenService, QrTokenService>();
        services.AddSingleton<IQrImageGenerator, QrImageGenerator>();
        services.AddScoped<ICreatorQrService, CreatorQrService>();
        services.AddScoped<ICommissionEngine, CommissionEngine>();
        services.Configure<WalletOptions>(configuration.GetSection(WalletOptions.SectionName));
        services.AddScoped<IWalletService, WalletService>();
        services.Configure<CreatorPayoutOptions>(configuration.GetSection(CreatorPayoutOptions.SectionName)); services.AddSingleton<IPayoutProvider,ManualPayoutProvider>(); services.AddScoped<ICreatorEarningsService,CreatorEarningsService>();
        services.Configure<CustomerVerificationOptions>(configuration.GetSection(CustomerVerificationOptions.SectionName)); services.AddSingleton<IPhoneNumberNormalizer,EthiopianPhoneNumberNormalizer>(); services.AddSingleton<IPhoneHashService,PhoneHashService>(); services.AddSingleton<IPhoneEncryptionService,PhoneEncryptionService>(); services.AddSingleton<ICustomerVerificationProvider,DevelopmentCustomerVerificationProvider>(); services.AddScoped<IRepeatUseApprovalService,RepeatUseApprovalService>();
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<ObservabilityOptions>(configuration.GetSection(ObservabilityOptions.SectionName));
        services.Configure<FeatureFlagOptions>(configuration.GetSection(FeatureFlagOptions.SectionName));
        services.AddSingleton<INotificationTemplateRenderer,SafeNotificationTemplateRenderer>();
        services.AddSingleton<DevelopmentNotificationProvider>();
        services.AddSingleton<IEmailNotificationProvider>(s=>s.GetRequiredService<DevelopmentNotificationProvider>()); services.AddSingleton<ISmsNotificationProvider>(s=>s.GetRequiredService<DevelopmentNotificationProvider>());
        services.AddSingleton<IPushNotificationProvider>(s=>s.GetRequiredService<DevelopmentNotificationProvider>()); services.AddSingleton<IInAppNotificationProvider>(s=>s.GetRequiredService<DevelopmentNotificationProvider>());
        services.AddSingleton<INotificationDispatcher,NotificationDispatcher>(); services.AddScoped<INotificationService,NotificationService>(); services.AddScoped<INotificationOutboxProcessor,NotificationOutboxProcessor>();
        services.AddScoped<IRiskOperationsService,RiskOperationsService>();
        services.Configure<OfflineSyncOptions>(configuration.GetSection(OfflineSyncOptions.SectionName)); services.AddScoped<IOfflineSyncService,OfflineSyncService>();
        return services;
    }
}
