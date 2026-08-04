using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Merchants;

public sealed class MerchantStore(ApplicationDbContext db) : IMerchantStore
{
    public Task<bool> EmailExistsAsync(string v, Guid? e, CancellationToken ct) => db.UserAccounts.AnyAsync(x => x.NormalizedEmail == v && (!e.HasValue || x.Id != e), ct); public Task<bool> PhoneExistsAsync(string v, Guid? e, CancellationToken ct) => db.Merchants.AnyAsync(x => x.NormalizedPhoneNumber == v && (!e.HasValue || x.Id != e), ct);
    public Task<Merchant?> FindMerchantAsync(Guid id, CancellationToken ct) => db.Merchants.SingleOrDefaultAsync(x => x.Id == id, ct); public Task<UserAccount?> FindUserByMerchantAsync(Guid id, CancellationToken ct) => db.UserAccounts.SingleOrDefaultAsync(x => x.MerchantId == id && x.Role == UserRole.MerchantAdmin, ct);
    public async Task<(UserAccount User, Merchant Merchant, IReadOnlyList<MerchantDocument> Documents)?> FindByUserAsync(Guid id, CancellationToken ct) { var row = await (from u in db.UserAccounts join m in db.Merchants on u.MerchantId equals m.Id where u.Id == id && u.Role == UserRole.MerchantAdmin select new { u, m }).SingleOrDefaultAsync(ct); if (row is null) return null; return (row.u, row.m, await FindDocumentsAsync(row.m.Id, ct)); }
    public async Task<(UserAccount User, Merchant Merchant)?> FindByTokenAsync(string hash, string purpose, CancellationToken ct) { var row = await (from t in db.MerchantVerificationTokens join u in db.UserAccounts on t.UserAccountId equals u.Id join m in db.Merchants on u.MerchantId equals m.Id where t.TokenHash == hash && t.Purpose == purpose select new { u, m }).SingleOrDefaultAsync(ct); return row is null ? null : (row.u, row.m); }
    public Task<MerchantVerificationToken?> FindTokenAsync(string h, string p, CancellationToken ct) => db.MerchantVerificationTokens.SingleOrDefaultAsync(x => x.TokenHash == h && x.Purpose == p, ct);
    public async Task<IReadOnlyList<Merchant>> FindByStatusAsync(MerchantStatus s, CancellationToken ct) => await db.Merchants.AsNoTracking().Where(x => x.Status == s).OrderBy(x => x.CreatedAtUtc).Take(200).ToListAsync(ct); public async Task<IReadOnlyList<MerchantDocument>> FindDocumentsAsync(Guid id, CancellationToken ct) => await db.MerchantDocuments.AsNoTracking().Where(x => x.MerchantId == id).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task InvalidateTokensAsync(Guid id, string purpose, DateTime used, CancellationToken ct) => db.MerchantVerificationTokens.Where(x => x.UserAccountId == id && x.Purpose == purpose && x.UsedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, used), ct);
    public void Add(UserAccount x) => db.Add(x); public void Add(Merchant x) => db.Add(x); public void Add(MerchantVerificationToken x) => db.Add(x); public void Add(MerchantDocument x) => db.Add(x); public void Add(MerchantAuditEvent x) => db.Add(x); public Task<int> SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct); public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) { if (!db.Database.IsRelational()) return await action(ct); await using var tx = await db.Database.BeginTransactionAsync(ct); var result = await action(ct); await tx.CommitAsync(ct); return result; }
}
public sealed class MetadataOnlyMerchantDocumentStorage : IMerchantDocumentStorage { public MerchantDocumentRegistration Register(MerchantDocumentMetadata metadata) => new("MetadataOnly", null); }
public sealed class DevelopmentMerchantVerificationProvider : IMerchantVerificationProvider
{
    public Task SendEmailVerificationAsync(UserAccount user, string token, CancellationToken ct) => Task.CompletedTask; public Task SendPhoneVerificationAsync(Merchant merchant, string token, CancellationToken ct) => Task.CompletedTask;
}
