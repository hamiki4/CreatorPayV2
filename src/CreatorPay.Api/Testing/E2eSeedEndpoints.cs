using CreatorPay.Application.Authentication;
using CreatorPay.Application.Qr;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Api.Testing;

public sealed record E2eSeedRequest(string Password);

public static class E2eSeedEndpoints
{
    public static IEndpointRouteBuilder MapE2eSeedEndpoints(this IEndpointRouteBuilder endpoints, IWebHostEnvironment environment)
    {
        if (!environment.IsEnvironment("E2E") && !environment.IsEnvironment("Test")) return endpoints;
        endpoints.MapPost("/api/v1/e2e/seed", SeedAsync);
        return endpoints;
    }

    private static async Task<IResult> SeedAsync(E2eSeedRequest request, ApplicationDbContext db, IPasswordHasher passwords, IQrTokenService qrTokens, CancellationToken ct)
    {
        if (request.Password.Length < 12) return Results.BadRequest();
        if (await db.UserAccounts.AnyAsync(x => x.Email == "shopper@e2e.invalid", ct)) return Results.Conflict();
        var now=DateTime.UtcNow; Guid Id(string value)=>Guid.Parse(value);
        var shopper=new Customer{Id=Id("10000000-0000-0000-0000-000000000001"),PublicCustomerId="CUS-E2E",DisplayName="E2E Shopper",PhoneNumber="+251911000001",NormalizedPhoneNumber="+251911000001",CreatedAtUtc=now};
        var user=new UserAccount{Id=Id("10000000-0000-0000-0000-000000000002"),Email="shopper@e2e.invalid",NormalizedEmail="SHOPPER@E2E.INVALID",Role=UserRole.Customer,Status=AccountStatus.Active,CustomerId=shopper.Id,IsEmailVerified=true,IsPhoneVerified=true,CreatedAtUtc=now}; user.PasswordHash=passwords.Hash(user,request.Password);
        var merchant=Merchant(Id("20000000-0000-0000-0000-000000000001"),"Active E2E Business",MerchantStatus.Active,now);
        var ownerUser=new UserAccount{Id=Id("20000000-0000-0000-0000-000000000008"),Email="owner@e2e.invalid",NormalizedEmail="OWNER@E2E.INVALID",Role=UserRole.MerchantAdmin,Status=AccountStatus.Active,MerchantId=merchant.Id,IsEmailVerified=true,IsPhoneVerified=true,CreatedAtUtc=now};ownerUser.PasswordHash=passwords.Hash(ownerUser,request.Password);
        var inactiveMerchant=Merchant(Id("20000000-0000-0000-0000-000000000002"),"Inactive E2E Business",MerchantStatus.Suspended,now);
        var location=new MerchantLocation{Id=Id("20000000-0000-0000-0000-000000000003"),MerchantId=merchant.Id,Name="E2E Checkout",AddressLine1="Synthetic",City="Addis Ababa",CountryCode="ET",TimeZoneId="Africa/Addis_Ababa",IsActive=true,CreatedAtUtc=now};
        var cashier=new Cashier{Id=Id("20000000-0000-0000-0000-000000000005"),MerchantId=merchant.Id,FirstName="E2E",LastName="Cashier",Email="cashier@e2e.invalid",NormalizedEmail="CASHIER@E2E.INVALID",PhoneNumber="+251911000005",NormalizedPhoneNumber="+251911000005",IsActive=true,CreatedAtUtc=now};
        var cashierUser=new UserAccount{Id=Id("20000000-0000-0000-0000-000000000006"),Email=cashier.Email,NormalizedEmail=cashier.NormalizedEmail,Role=UserRole.Cashier,Status=AccountStatus.Active,MerchantId=merchant.Id,CashierId=cashier.Id,IsEmailVerified=true,IsPhoneVerified=true,CreatedAtUtc=now};cashierUser.PasswordHash=passwords.Hash(cashierUser,request.Password);
        var cashierAssignment=new CashierLocationAssignment{Id=Id("20000000-0000-0000-0000-000000000007"),CashierId=cashier.Id,MerchantLocationId=location.Id,IsActive=true,IsPrimary=true,CreatedAtUtc=now};
        var active=Creator(Id("30000000-0000-0000-0000-000000000001"),"Selam Active",CreatorStatus.Active,now);
        var activeCreatorUser=new UserAccount{Id=Id("30000000-0000-0000-0000-000000000004"),Email="creator@e2e.invalid",NormalizedEmail="CREATOR@E2E.INVALID",Role=UserRole.Creator,Status=AccountStatus.Active,CreatorId=active.Id,IsEmailVerified=true,IsPhoneVerified=true,CreatedAtUtc=now};activeCreatorUser.PasswordHash=passwords.Hash(activeCreatorUser,request.Password);
        var noOffer=Creator(Id("30000000-0000-0000-0000-000000000002"),"No Offer Creator",CreatorStatus.Active,now);
        var suspended=Creator(Id("30000000-0000-0000-0000-000000000003"),"Suspended Creator",CreatorStatus.Suspended,now);
        active.SocialProfiles.Add(new(){Id=Id("30000000-0000-0000-0000-000000000011"),CreatorId=active.Id,Platform=SocialPlatform.TikTok,ProfileUrl="https://www.tiktok.com/@weymela-e2e",Handle="@weymela-e2e",CreatedAtUtc=now});
        active.SocialProfiles.Add(new(){Id=Id("30000000-0000-0000-0000-000000000012"),CreatorId=active.Id,Platform=SocialPlatform.YouTube,ProfileUrl="https://www.youtube.com/@weymela-e2e",Handle="@weymela-e2e",CreatedAtUtc=now});
        active.SocialProfiles.Add(new(){Id=Id("30000000-0000-0000-0000-000000000013"),CreatorId=active.Id,Platform=SocialPlatform.Other,ProfileUrl="javascript:alert(1)",Handle="unsafe",CreatedAtUtc=now});
        var partnership=new MerchantCreatorPartnership{Id=Id("40000000-0000-0000-0000-000000000001"),MerchantId=merchant.Id,CreatorId=active.Id,RequestedAtUtc=now,CreatedAtUtc=now}; partnership.Approve(now,user.Id);
        var plan=new CommissionPlan{Id=Id("50000000-0000-0000-0000-000000000001"),Name="E2E",CreatedAtUtc=now};
        var rule=new CommissionRule{Id=Id("50000000-0000-0000-0000-000000000002"),CommissionPlanId=plan.Id,Name="E2E 10%",ScopeType=CommissionScopeType.CampaignOverride,CurrencyCode="ETB",IsActive=true,CreatedAtUtc=now};
        var version=new CommissionRuleVersion{Id=Id("50000000-0000-0000-0000-000000000003"),CommissionRuleId=rule.Id,VersionNumber=1,MerchantCommissionRatePercent=10,CreatorSharePercent=40,CustomerCashbackSharePercent=30,PlatformSharePercent=30,EffectiveFromUtc=now.AddDays(-10),RoundingMode=CommissionRoundingMode.AwayFromZero,IsActive=true,CreatedByUserId=user.Id,CreatedAtUtc=now};
        var offer=Campaign(Id("60000000-0000-0000-0000-000000000001"),"CMP-E2E-ACTIVE",active.Id,merchant.Id,partnership.Id,version.Id,"E2EACTIVE",now.AddMinutes(-1),30,user.Id,qrTokens);
        var expired=Campaign(Id("60000000-0000-0000-0000-000000000002"),"CMP-E2E-EXPIRED",active.Id,merchant.Id,partnership.Id,version.Id,"E2EEXPIRED",now.AddDays(-2),1,user.Id,qrTokens); expired.ExpireIfDue(now);
        var offerQrId=offer.QrCode!.PublicQrId;
        const string expiredRaw="E2E-EXPIRED-CHECKOUT";var expiredCheckout=new CheckoutSession{Id=Id("70000000-0000-0000-0000-000000000001"),PublicCheckoutId="CHK-E2E-EXPIRED",CustomerId=shopper.Id,CampaignId=offer.Id,MerchantId=merchant.Id,CreatorId=active.Id,TokenHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expiredRaw))),ExpiresAtUtc=now.AddMinutes(-1),CreateIdempotencyKey="e2e-expired",CreatedAtUtc=now.AddMinutes(-5)};
        var wallet=new MerchantWallet{Id=Id("20000000-0000-0000-0000-000000000009"),MerchantId=merchant.Id,CurrencyCode="ETB",CreatedAtUtc=now};wallet.Credit(1000m,100m,now);
        var commissionAssignment=new CampaignCommissionAssignment{Id=Id("50000000-0000-0000-0000-000000000004"),CampaignId=offer.Id,MerchantCreatorPartnershipId=partnership.Id,CommissionRuleId=rule.Id,EffectiveFromUtc=now.AddDays(-10),IsActive=true,CreatedAtUtc=now};
        db.AddRange(shopper,user,new CustomerWallet{Id=Id("10000000-0000-0000-0000-000000000003"),CustomerId=shopper.Id,CurrencyCode="ETB",CreatedAtUtc=now},merchant,ownerUser,inactiveMerchant,location,cashier,cashierUser,cashierAssignment,wallet,active,activeCreatorUser,new CreatorBalanceAccount{Id=Id("30000000-0000-0000-0000-000000000014"),CreatorId=active.Id,CurrencyCode="ETB",CreatedAtUtc=now},noOffer,suspended,partnership,plan,rule,version,offer,commissionAssignment,expired,expiredCheckout,new MerchantTrialCredit{Id=Id("20000000-0000-0000-0000-000000000004"),MerchantId=merchant.Id,CreatedAtUtc=now});
        await db.SaveChangesAsync(ct);
        return Results.Ok(new{shopperEmail=user.Email,cashierEmail=cashier.Email,locationId=location.Id,expiredCheckoutQr=$"creatorpay:checkout:{expiredCheckout.PublicCheckoutId}:{expiredRaw}",creatorName=active.DisplayName,activeOfferCode=offer.CampaignCode,expiredOfferCode=expired.CampaignCode,offerQrId,offerQrPayload=$"creatorpay:offer:{offer.QrCode.PublicQrId}:{qrTokens.CreateToken(offer.QrCode.PublicQrId,1)}",expiredOfferQrPayload=$"creatorpay:offer:{expired.QrCode!.PublicQrId}:{qrTokens.CreateToken(expired.QrCode.PublicQrId,1)}"});
    }

    private static Merchant Merchant(Guid id,string name,MerchantStatus status,DateTime now)=>new(){Id=id,PublicMerchantId=$"MER-{id.ToString("N")[^16..]}",LegalBusinessName=name,TradingName=name,BusinessType="Synthetic",PrimaryContactName="E2E",PhoneNumber=$"+2519{id.ToString("N")[^8..]}",NormalizedPhoneNumber=$"+2519{id.ToString("N")[^8..]}",Email=$"{id:N}@e2e.invalid",TermsAcceptedAtUtc=now,PublicDescription="Synthetic browser-test Offer.",BusinessAddress="Synthetic",City="Addis Ababa",Region="Addis Ababa",Country="ET",TimeZone="Africa/Addis_Ababa",Status=status,CreatedAtUtc=now};
    private static Creator Creator(Guid id,string name,CreatorStatus status,DateTime now)=>new(){Id=id,PublicCreatorId=$"CRE-{id.ToString("N")[^16..]}",FirstName="E2E",LastName="Creator",DisplayName=name,PhoneNumber=$"+2519{id.ToString("N")[^8..]}",NormalizedPhoneNumber=$"+2519{id.ToString("N")[^8..]}",Email=$"{id:N}@e2e.invalid",City="Addis Ababa",Biography="Synthetic",ContentCategories="Testing",TermsAcceptedAtUtc=now,Status=status,CreatedAtUtc=now};
    private static CreatorMerchantCampaign Campaign(Guid id,string publicId,Guid creator,Guid merchant,Guid partnership,Guid version,string code,DateTime start,int days,Guid actor,IQrTokenService qrTokens){var x=new CreatorMerchantCampaign{Id=id,PublicCampaignId=publicId,CreatorId=creator,MerchantId=merchant,MerchantCreatorPartnershipId=partnership,CreatedAtUtc=start};x.Approve(days,null,version,actor,code,"E2E Active Offer\nSynthetic public description",start);var publicQrId=$"CQR-{code}";var token=qrTokens.CreateToken(publicQrId,1);x.QrCode=new(){Id=Guid.NewGuid(),CampaignId=id,PublicQrId=publicQrId,TokenHash=qrTokens.Hash(token),IssuedAtUtc=start,CreatedAtUtc=start};x.Start(start);x.QrCode.Activate(start,x.ExpiresAtUtc!.Value);return x;}
}
