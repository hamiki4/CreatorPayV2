using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessAddress",
                table: "merchants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "merchants",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "merchants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "merchants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoFileName",
                table: "merchants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LogoSizeBytes",
                table: "merchants",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "merchants",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "merchants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "merchant_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_audit_events_merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_merchant_audit_events_user_accounts_ActorUserAccountId",
                        column: x => x.ActorUserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "merchant_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_documents_merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merchant_verification_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_verification_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_verification_tokens_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_audit_events_ActorUserAccountId",
                table: "merchant_audit_events",
                column: "ActorUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_audit_events_MerchantId_CreatedAtUtc",
                table: "merchant_audit_events",
                columns: new[] { "MerchantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_documents_MerchantId_DocumentType",
                table: "merchant_documents",
                columns: new[] { "MerchantId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_verification_tokens_TokenHash_Purpose",
                table: "merchant_verification_tokens",
                columns: new[] { "TokenHash", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchant_verification_tokens_UserAccountId",
                table: "merchant_verification_tokens",
                column: "UserAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merchant_audit_events");

            migrationBuilder.DropTable(
                name: "merchant_documents");

            migrationBuilder.DropTable(
                name: "merchant_verification_tokens");

            migrationBuilder.DropColumn(
                name: "BusinessAddress",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "City",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "LogoFileName",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "LogoSizeBytes",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "merchants");
        }
    }
}
