using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineSyncFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfflineSyncBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicBatchId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRequiredCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeviceReferenceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineSyncBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfflineSyncItemResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfflineSyncBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientOperationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PurchaseTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepeatUseApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SafeMessage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineSyncItemResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineSyncItemResults_OfflineSyncBatches_OfflineSyncBatchId",
                        column: x => x.OfflineSyncBatchId,
                        principalTable: "OfflineSyncBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncBatches_PublicBatchId",
                table: "OfflineSyncBatches",
                column: "PublicBatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncBatches_ReceivedAtUtc",
                table: "OfflineSyncBatches",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncItemResults_CashierUserId_ClientOperationId",
                table: "OfflineSyncItemResults",
                columns: new[] { "CashierUserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncItemResults_OfflineSyncBatchId",
                table: "OfflineSyncItemResults",
                column: "OfflineSyncBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncItemResults_ResultStatus",
                table: "OfflineSyncItemResults",
                column: "ResultStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfflineSyncItemResults");

            migrationBuilder.DropTable(
                name: "OfflineSyncBatches");
        }
    }
}
