using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPhoneVerificationAndRepeatUseApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseTransactions_CreatorId_TransactionDateUtc",
                table: "PurchaseTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseTransactions_MerchantId_TransactionDateUtc",
                table: "PurchaseTransactions");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerConfirmationId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhoneHash",
                table: "PurchaseTransactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPhoneReferenceId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MerchantLocalDate",
                table: "PurchaseTransactions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepeatUseApprovalRequestId",
                table: "PurchaseTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasRepeatUseApproved",
                table: "PurchaseTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CustomerConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepeatUseApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerConfirmations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPhoneReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncryptedPhoneNumber = table.Column<byte[]>(type: "bytea", nullable: false),
                    PhoneNumberHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MaskedPhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPhoneReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepeatUseApprovalHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepeatUseApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "text", nullable: true),
                    NewStatus = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepeatUseApprovalHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepeatUseApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicApprovalRequestId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantCreatorPartnershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhoneReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhoneHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MaskedPhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MerchantLocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    QrReference = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupervisorDecisionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupervisorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RelatedPurchaseTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    RequiresCustomerConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresSupervisorApproval = table.Column<bool>(type: "boolean", nullable: false),
                    SupervisorApproved = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerConfirmationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepeatUseApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseTransactions_MerchantId_CreatorId_CustomerPhoneHash~",
                table: "PurchaseTransactions",
                columns: new[] { "MerchantId", "CreatorId", "CustomerPhoneHash", "MerchantLocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerConfirmations_RepeatUseApprovalRequestId",
                table: "CustomerConfirmations",
                column: "RepeatUseApprovalRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneReferences_PhoneNumberHash",
                table: "CustomerPhoneReferences",
                column: "PhoneNumberHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepeatUseApprovalHistories_RepeatUseApprovalRequestId_Occur~",
                table: "RepeatUseApprovalHistories",
                columns: new[] { "RepeatUseApprovalRequestId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepeatUseApprovalRequests_MerchantId_CreatorId_CustomerPhon~",
                table: "RepeatUseApprovalRequests",
                columns: new[] { "MerchantId", "CreatorId", "CustomerPhoneHash", "MerchantLocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RepeatUseApprovalRequests_MerchantId_IdempotencyKey",
                table: "RepeatUseApprovalRequests",
                columns: new[] { "MerchantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepeatUseApprovalRequests_PublicApprovalRequestId",
                table: "RepeatUseApprovalRequests",
                column: "PublicApprovalRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerConfirmations");

            migrationBuilder.DropTable(
                name: "CustomerPhoneReferences");

            migrationBuilder.DropTable(
                name: "RepeatUseApprovalHistories");

            migrationBuilder.DropTable(
                name: "RepeatUseApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseTransactions_MerchantId_CreatorId_CustomerPhoneHash~",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerConfirmationId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerPhoneHash",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerPhoneReferenceId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "MerchantLocalDate",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "RepeatUseApprovalRequestId",
                table: "PurchaseTransactions");

            migrationBuilder.DropColumn(
                name: "WasRepeatUseApproved",
                table: "PurchaseTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseTransactions_CreatorId_TransactionDateUtc",
                table: "PurchaseTransactions",
                columns: new[] { "CreatorId", "TransactionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseTransactions_MerchantId_TransactionDateUtc",
                table: "PurchaseTransactions",
                columns: new[] { "MerchantId", "TransactionDateUtc" });
        }
    }
}
