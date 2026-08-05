using CreatorPay.Application.Creators;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Creators;

public sealed class CreatorStore(ApplicationDbContext db) : ICreatorStore
{
    public Task<bool> EmailExistsAsync(string value, Guid? exclude, CancellationToken ct) => db.UserAccounts.AnyAsync(x => x.NormalizedEmail == value && (!exclude.HasValue || x.Id != exclude), ct);
    public Task<bool> PhoneExistsAsync(string value, Guid? exclude, CancellationToken ct) => db.Creators.AnyAsync(x => x.NormalizedPhoneNumber == value && (!exclude.HasValue || x.Id != exclude), ct);
    public Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct) => db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<UserAccount?> FindUserByCreatorAsync(Guid creatorId, CancellationToken ct) => db.UserAccounts.SingleOrDefaultAsync(x => x.CreatorId == creatorId, ct);
    public Task<Creator?> FindCreatorAsync(Guid id, CancellationToken ct) => db.Creators.Include(x => x.SocialProfiles).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<(UserAccount User, Creator Creator)?> FindByUserAsync(Guid id, CancellationToken ct)
    {
        var row = await (from u in db.UserAccounts join c in db.Creators.Include(x => x.SocialProfiles) on u.CreatorId equals c.Id where u.Id == id select new { u, c }).SingleOrDefaultAsync(ct);
        return row is null ? null : (row.u, row.c);
    }
    public async Task<(UserAccount User, Creator Creator)?> FindByTokenAsync(string hash, string purpose, CancellationToken ct)
    {
        var row = await (from t in db.CreatorVerificationTokens join u in db.UserAccounts on t.UserAccountId equals u.Id join c in db.Creators on u.CreatorId equals c.Id where t.TokenHash == hash && t.Purpose == purpose select new { u, c }).SingleOrDefaultAsync(ct);
        return row is null ? null : (row.u, row.c);
    }
    public Task<CreatorVerificationToken?> FindTokenAsync(string hash, string purpose, CancellationToken ct) => db.CreatorVerificationTokens.SingleOrDefaultAsync(x => x.TokenHash == hash && x.Purpose == purpose, ct);
    public async Task<IReadOnlyList<Creator>> FindByStatusAsync(CreatorStatus status, CancellationToken ct) => await db.Creators.AsNoTracking().Where(x => x.Status == status).OrderBy(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
    public Task InvalidateTokensAsync(Guid userId, string purpose, DateTime usedAt, CancellationToken ct) => db.CreatorVerificationTokens.Where(x => x.UserAccountId == userId && x.Purpose == purpose && x.UsedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, usedAt), ct);
    public void Add(UserAccount item) => db.UserAccounts.Add(item); public void Add(Creator item) => db.Creators.Add(item); public void Add(CreatorVerificationToken item) => db.CreatorVerificationTokens.Add(item); public void Add(CreatorAuditEvent item) => db.CreatorAuditEvents.Add(item);
    public void Add(CreatorSocialProfile item) => db.CreatorSocialProfiles.Add(item);
    public void RemoveSocialProfiles(IEnumerable<CreatorSocialProfile> items) => db.CreatorSocialProfiles.RemoveRange(items);
    public Task<int> SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) { if (!db.Database.IsRelational()) return await action(ct); await using var transaction = await db.Database.BeginTransactionAsync(ct); var result = await action(ct); await transaction.CommitAsync(ct); return result; }
}
