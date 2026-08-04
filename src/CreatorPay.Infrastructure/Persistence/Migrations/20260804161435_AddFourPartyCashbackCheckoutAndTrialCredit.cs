using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFourPartyCashbackCheckoutAndTrialCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "user_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatorQrCodeId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignCommissionRuleVersionId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CampaignExpiresAtUtc",
                table: "PurchaseTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignQrCodeId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CampaignStartsAtUtc",
                table: "PurchaseTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutSessionId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerCashbackAmount",
                table: "PurchaseTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerCashbackSharePercent",
                table: "CommissionRuleVersions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerCashbackAmount",
                table: "CommissionCalculationSnapshots",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerCashbackSharePercent",
                table: "CommissionCalculationSnapshots",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CreatorMerchantCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicCampaignId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantCreatorPartnershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewedFromCampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignCode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    MerchantAllowedStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByMerchantUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Conditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SuspendedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationSevenDayReminderAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationOneDayReminderAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorMerchantCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorMerchantCampaigns_CommissionRuleVersions_CommissionR~",
                        column: x => x.CommissionRuleVersionId,
                        principalTable: "CommissionRuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorMerchantCampaigns_CreatorMerchantCampaigns_RenewedFr~",
                        column: x => x.RenewedFromCampaignId,
                        principalTable: "CreatorMerchantCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorMerchantCampaigns_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorMerchantCampaigns_merchant_creator_partnerships_Merc~",
                        column: x => x.MerchantCreatorPartnershipId,
                        principalTable: "merchant_creator_partnerships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorMerchantCampaigns_merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPayoutRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicPayoutId = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalMethod = table.Column<string>(type: "text", nullable: true),
                    ExternalReference = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    PaymentConfirmationIdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayoutRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerRecoveryBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionReversalId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRecoveryBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicCustomerId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    NormalizedPhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MerchantPromotionProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeaturedCreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ZoneCode = table.Column<string>(type: "text", nullable: true),
                    SocialLinksJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantPromotionProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MerchantStoreQrs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicQrId = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantStoreQrs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MerchantTrialCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ConfirmedTransactionCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCommissionFunded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumTransactions = table.Column<int>(type: "integer", nullable: false),
                    MaximumCommission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantTrialCredits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRevenueEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRevenueEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignQrCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicQrId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignQrCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignQrCodes_CreatorMerchantCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CreatorMerchantCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignRenewalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiredCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NewCampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignRenewalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignRenewalRequests_CreatorMerchantCampaigns_ExpiredCam~",
                        column: x => x.ExpiredCampaignId,
                        principalTable: "CreatorMerchantCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicCheckoutId = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PresentedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: true),
                    MerchantLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedCreatorAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedCashbackAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CustomerApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PurchaseTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreateIdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    PresentIdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    ApprovalIdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckoutSessions_CreatorMerchantCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CreatorMerchantCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckoutSessions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    AvailableCashback = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReservedCashback = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidLifetime = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecoveryBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerWallets_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedPromotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedPromotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedPromotions_CreatorMerchantCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CreatorMerchantCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedPromotions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerCashbackEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    PurchaseTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerPayoutRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCashbackEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerCashbackEntries_CustomerWallets_CustomerWalletId",
                        column: x => x.CustomerWalletId,
                        principalTable: "CustomerWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_CustomerId",
                table: "user_accounts",
                column: "CustomerId",
                unique: true,
                filter: "\"CustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignQrCodes_CampaignId",
                table: "CampaignQrCodes",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignQrCodes_PublicQrId",
                table: "CampaignQrCodes",
                column: "PublicQrId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignQrCodes_TokenHash",
                table: "CampaignQrCodes",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRenewalRequests_ExpiredCampaignId_Status",
                table: "CampaignRenewalRequests",
                columns: new[] { "ExpiredCampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_CampaignId",
                table: "CheckoutSessions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_CustomerId_CreateIdempotencyKey",
                table: "CheckoutSessions",
                columns: new[] { "CustomerId", "CreateIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_PublicCheckoutId",
                table: "CheckoutSessions",
                column: "PublicCheckoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_Status_ExpiresAtUtc",
                table: "CheckoutSessions",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_TokenHash",
                table: "CheckoutSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_CommissionRuleVersionId",
                table: "CreatorMerchantCampaigns",
                column: "CommissionRuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_CreatorId",
                table: "CreatorMerchantCampaigns",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_ExpiresAtUtc",
                table: "CreatorMerchantCampaigns",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_MerchantCreatorPartnershipId_Status",
                table: "CreatorMerchantCampaigns",
                columns: new[] { "MerchantCreatorPartnershipId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_MerchantId",
                table: "CreatorMerchantCampaigns",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_PublicCampaignId",
                table: "CreatorMerchantCampaigns",
                column: "PublicCampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_RenewedFromCampaignId",
                table: "CreatorMerchantCampaigns",
                column: "RenewedFromCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorMerchantCampaigns_Status",
                table: "CreatorMerchantCampaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCashbackEntries_CustomerId_CreatedAtUtc",
                table: "CustomerCashbackEntries",
                columns: new[] { "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCashbackEntries_CustomerWalletId",
                table: "CustomerCashbackEntries",
                column: "CustomerWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCashbackEntries_IdempotencyKey",
                table: "CustomerCashbackEntries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayoutRequests_IdempotencyKey",
                table: "CustomerPayoutRequests",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayoutRequests_PaymentConfirmationIdempotencyKey",
                table: "CustomerPayoutRequests",
                column: "PaymentConfirmationIdempotencyKey",
                unique: true,
                filter: "\"PaymentConfirmationIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayoutRequests_PublicPayoutId",
                table: "CustomerPayoutRequests",
                column: "PublicPayoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayoutRequests_Status_RequestedAtUtc",
                table: "CustomerPayoutRequests",
                columns: new[] { "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRecoveryBalances_CustomerId_Status",
                table: "CustomerRecoveryBalances",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRecoveryBalances_TransactionReversalId",
                table: "CustomerRecoveryBalances",
                column: "TransactionReversalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedPhoneNumber",
                table: "Customers",
                column: "NormalizedPhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PublicCustomerId",
                table: "Customers",
                column: "PublicCustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_CustomerId_CurrencyCode",
                table: "CustomerWallets",
                columns: new[] { "CustomerId", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantPromotionProfiles_MerchantId",
                table: "MerchantPromotionProfiles",
                column: "MerchantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantStoreQrs_MerchantId",
                table: "MerchantStoreQrs",
                column: "MerchantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantStoreQrs_PublicQrId",
                table: "MerchantStoreQrs",
                column: "PublicQrId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantStoreQrs_TokenHash",
                table: "MerchantStoreQrs",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantTrialCredits_MerchantId",
                table: "MerchantTrialCredits",
                column: "MerchantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRevenueEntries_IdempotencyKey",
                table: "PlatformRevenueEntries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRevenueEntries_PurchaseTransactionId",
                table: "PlatformRevenueEntries",
                column: "PurchaseTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedPromotions_CampaignId",
                table: "SavedPromotions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedPromotions_CustomerId_CampaignId",
                table: "SavedPromotions",
                columns: new[] { "CustomerId", "CampaignId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_user_accounts_Customers_CustomerId",
                table: "user_accounts",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_accounts_Customers_CustomerId",
                table: "user_accounts");

            migrationBuilder.DropTable(
                name: "CampaignQrCodes");

            migrationBuilder.DropTable(
                name: "CampaignRenewalRequests");

            migrationBuilder.DropTable(
                name: "CheckoutSessions");

            migrationBuilder.DropTable(
                name: "CustomerCashbackEntries");

            migrationBuilder.DropTable(
                name: "CustomerPayoutRequests");

            migrationBuilder.DropTable(
                name: "CustomerRecoveryBalances");

            migrationBuilder.DropTable(
                name: "MerchantPromotionProfiles");

            migrationBuilder.DropTable(
                name: "MerchantStoreQrs");

            migrationBuilder.DropTable(
                name: "MerchantTrialCredits");

            migrationBuilder.DropTable(
                name: "PlatformRevenueEntries");

            migrationBuilder.DropTable(
                name: "SavedPromotions");

            migrationBuilder.DropTable(
                name: "CustomerWallets");

            migrationBuilder.DropTable(
                name: "CreatorMerchantCampaigns");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_CustomerId",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "CampaignCommissionRuleVersionId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CampaignExpiresAtUtc",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CampaignQrCodeId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CampaignStartsAtUtc",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CheckoutSessionId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerCashbackAmount",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerCashbackSharePercent",
                table: "CommissionRuleVersions");

            migrationBuilder.DropColumn(
                name: "CustomerCashbackAmount",
                table: "CommissionCalculationSnapshots");

            migrationBuilder.DropColumn(
                name: "CustomerCashbackSharePercent",
                table: "CommissionCalculationSnapshots");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatorQrCodeId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
