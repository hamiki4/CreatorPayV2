using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
namespace CreatorPay.Domain.Tests;

public sealed class CampaignLifecycleTests
{
    [Fact] public void Approval_issues_inactive_qr_and_start_uses_server_time() { var now = DateTime.SpecifyKind(new DateTime(2026, 8, 4, 12, 0, 0), DateTimeKind.Utc); var c = Campaign(); c.Approve(30, null, Guid.NewGuid(), Guid.NewGuid(), "SAFE42", null, now); c.QrCode = new() { Id = Guid.NewGuid(), CampaignId = c.Id, PublicQrId = "qr", TokenHash = new string('A', 64), IssuedAtUtc = now, CreatedAtUtc = now }; Assert.Equal(CampaignQrStatus.Inactive, c.QrCode.Status); c.Start(now.AddMinutes(2)); c.QrCode.Activate(c.StartsAtUtc!.Value, c.ExpiresAtUtc!.Value); Assert.Equal(now.AddMinutes(2), c.StartsAtUtc); Assert.Equal(CampaignStatus.Active, c.Status); Assert.Equal(CampaignQrStatus.Active, c.QrCode.Status); }
    [Fact] public void Future_allowed_start_schedules_and_expiration_is_time_authoritative() { var now = DateTime.SpecifyKind(new DateTime(2026, 8, 4), DateTimeKind.Utc); var c = Campaign(); c.Approve(7, now.AddDays(1), Guid.NewGuid(), Guid.NewGuid(), "CODE", null, now); c.QrCode = new() { Id = Guid.NewGuid(), CampaignId = c.Id, PublicQrId = "qr", TokenHash = new string('B', 64), IssuedAtUtc = now, CreatedAtUtc = now }; c.Start(now); Assert.Equal(CampaignStatus.Scheduled, c.Status); Assert.True(c.ActivateIfDue(now.AddDays(1))); Assert.True(c.ExpireIfDue(now.AddDays(8))); Assert.Equal(CampaignQrStatus.Expired, c.QrCode.Status); Assert.Throws<InvalidOperationException>(() => c.QrCode.Activate(now.AddDays(9), now.AddDays(10))); }
    [Fact] public void Start_happens_only_once() { var now = DateTime.UtcNow; var c = Campaign(); c.Approve(30, null, Guid.NewGuid(), Guid.NewGuid(), "CODE", null, now); c.Start(now); Assert.Throws<InvalidOperationException>(() => c.Start(now.AddMinutes(1))); }
    static CreatorMerchantCampaign Campaign() => new() { Id = Guid.NewGuid(), PublicCampaignId = "CMP-TEST", CreatorId = Guid.NewGuid(), MerchantId = Guid.NewGuid(), MerchantCreatorPartnershipId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow };
}
