using CreatorPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPay.Infrastructure.Persistence.Configurations;

public sealed class MerchantDocumentConfiguration : IEntityTypeConfiguration<MerchantDocument>
{
    public void Configure(EntityTypeBuilder<MerchantDocument> b) { b.ToTable("merchant_documents"); b.ConfigureEntity(); b.Property(x => x.DocumentType).HasMaxLength(50).IsRequired(); b.Property(x => x.FileName).HasMaxLength(255).IsRequired(); b.Property(x => x.ContentType).HasMaxLength(100).IsRequired(); b.Property(x => x.StorageProvider).HasMaxLength(50).IsRequired(); b.Property(x => x.StorageKey).HasMaxLength(500); b.HasOne<Merchant>().WithMany(x => x.Documents).HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x => new { x.MerchantId, x.DocumentType }); }
}
public sealed class MerchantVerificationTokenConfiguration : IEntityTypeConfiguration<MerchantVerificationToken>
{
    public void Configure(EntityTypeBuilder<MerchantVerificationToken> b) { b.ToTable("merchant_verification_tokens"); b.ConfigureEntity(); b.Property(x => x.Purpose).HasMaxLength(20).IsRequired(); b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired(); b.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.UsedAtUtc).HasColumnType("timestamp with time zone"); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x => new { x.TokenHash, x.Purpose }).IsUnique(); }
}
public sealed class MerchantAuditEventConfiguration : IEntityTypeConfiguration<MerchantAuditEvent>
{
    public void Configure(EntityTypeBuilder<MerchantAuditEvent> b) { b.ToTable("merchant_audit_events"); b.ConfigureEntity(); b.Property(x => x.EventType).HasMaxLength(80).IsRequired(); b.Property(x => x.Detail).HasMaxLength(2000); b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserAccountId).OnDelete(DeleteBehavior.NoAction); b.HasIndex(x => new { x.MerchantId, x.CreatedAtUtc }); }
}
