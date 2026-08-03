using System.Data;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Authentication;

public sealed class AuthenticationStore(ApplicationDbContext db) : IAuthenticationStore
{
    public Task<UserAccount?> FindUserByEmailAsync(string email, CancellationToken ct) => db.UserAccounts.SingleOrDefaultAsync(x => x.NormalizedEmail == email, ct);
    public Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct) => db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<RefreshToken?> FindRefreshAsync(string hash, CancellationToken ct) => db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public Task<PasswordResetToken?> FindResetAsync(string hash, CancellationToken ct) => db.PasswordResetTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public void AddRefresh(RefreshToken token) => db.RefreshTokens.Add(token); public void AddReset(PasswordResetToken token) => db.PasswordResetTokens.Add(token); public void AddAudit(LoginAudit audit) => db.LoginAudits.Add(audit);
    public Task RevokeFamilyAsync(string family, DateTime now, string reason, string? ip, CancellationToken ct) => db.RefreshTokens.Where(x => x.TokenFamily == family && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now).SetProperty(x => x.RevokedReason, reason).SetProperty(x => x.RevokedByIp, ip), ct);
    public Task RevokeAllAsync(Guid userId, DateTime now, string reason, string? ip, CancellationToken ct) => db.RefreshTokens.Where(x => x.UserAccountId == userId && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now).SetProperty(x => x.RevokedReason, reason).SetProperty(x => x.RevokedByIp, ip), ct);
    public Task<int> SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) { await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var result = await action(ct); await transaction.CommitAsync(ct); return result; }
}
