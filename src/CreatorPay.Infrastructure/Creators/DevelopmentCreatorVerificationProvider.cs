using CreatorPay.Application.Creators;
using CreatorPay.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CreatorPay.Infrastructure.Creators;

public sealed class DevelopmentCreatorVerificationProvider(ILogger<DevelopmentCreatorVerificationProvider> logger) : ICreatorVerificationProvider
{
    public Task SendEmailVerificationAsync(UserAccount user, string token, CancellationToken ct) { logger.LogInformation("DEV email verification for user {UserId}: {Token}", user.Id, token); return Task.CompletedTask; }
    public Task SendPhoneVerificationAsync(Creator creator, string token, CancellationToken ct) { logger.LogInformation("DEV phone verification for creator {CreatorId}: {Token}", creator.Id, token); return Task.CompletedTask; }
}
