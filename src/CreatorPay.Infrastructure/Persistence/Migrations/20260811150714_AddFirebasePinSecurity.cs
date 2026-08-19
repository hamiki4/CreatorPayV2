using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFirebasePinSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirebaseUid",
                table: "user_accounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecoveryEmailVerified",
                table: "user_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedRecoveryEmail",
                table: "user_accounts",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinChangedAtUtc",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinEnrolledAtUtc",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PinFailedAttemptCount",
                table: "user_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "user_accounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinLockedAtUtc",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinRetryNotBeforeUtc",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PinVersion",
                table: "user_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryEmail",
                table: "user_accounts",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pin_reset_authorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirebaseUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedByIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UsedByIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pin_reset_authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pin_reset_authorizations_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_FirebaseUid",
                table: "user_accounts",
                column: "FirebaseUid",
                unique: true,
                filter: "\"FirebaseUid\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_NormalizedRecoveryEmail",
                table: "user_accounts",
                column: "NormalizedRecoveryEmail",
                unique: true,
                filter: "\"NormalizedRecoveryEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_pin_reset_authorizations_TokenHash",
                table: "pin_reset_authorizations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pin_reset_authorizations_UserAccountId_ExpiresAtUtc",
                table: "pin_reset_authorizations",
                columns: new[] { "UserAccountId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pin_reset_authorizations");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_FirebaseUid",
                table: "user_accounts");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_NormalizedRecoveryEmail",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "FirebaseUid",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "IsRecoveryEmailVerified",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "NormalizedRecoveryEmail",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PinChangedAtUtc",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PinEnrolledAtUtc",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PinFailedAttemptCount",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PinLockedAtUtc",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PinRetryNotBeforeUtc",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PinVersion",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "RecoveryEmail",
                table: "user_accounts");
        }
    }
}
