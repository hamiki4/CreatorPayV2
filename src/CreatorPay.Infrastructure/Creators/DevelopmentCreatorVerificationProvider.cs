using CreatorPay.Application.Creators;
using CreatorPay.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace CreatorPay.Infrastructure.Creators;

public sealed class DevelopmentCreatorVerificationProvider(ILogger<DevelopmentCreatorVerificationProvider> logger, IHostEnvironment environment) : ICreatorVerificationProvider
{
    public Task SendEmailVerificationAsync(UserAccount user, string token, CancellationToken ct) { if (environment.IsDevelopment()) logger.LogInformation("DEV email verification for user {UserId}: {Token}", user.Id, token); else logger.LogInformation("Email verification delivery requested for user {UserId}; provider is not configured.", user.Id); return Task.CompletedTask; }
    public Task SendPhoneVerificationAsync(Creator creator, string token, CancellationToken ct) { if (environment.IsDevelopment()) logger.LogInformation("DEV phone verification for creator {CreatorId}: {Token}", creator.Id, token); else logger.LogInformation("Phone verification delivery requested for creator {CreatorId}; provider is not configured.", creator.Id); return Task.CompletedTask; }
}
