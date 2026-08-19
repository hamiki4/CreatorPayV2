using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPushDeviceRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationRecipients_NotificationId",
                table: "NotificationRecipients");

            migrationBuilder.AddColumn<Guid>(
                name: "PushDeviceRegistrationId",
                table: "NotificationRecipients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PushDeviceRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProtectedToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeviceRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushDeviceRegistrations_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_NotificationId_PushDeviceRegistratio~",
                table: "NotificationRecipients",
                columns: new[] { "NotificationId", "PushDeviceRegistrationId" },
                unique: true,
                filter: "\"PushDeviceRegistrationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceRegistrations_TokenHash",
                table: "PushDeviceRegistrations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceRegistrations_UserAccountId_IsActive",
                table: "PushDeviceRegistrations",
                columns: new[] { "UserAccountId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushDeviceRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_NotificationRecipients_NotificationId_PushDeviceRegistratio~",
                table: "NotificationRecipients");

            migrationBuilder.DropColumn(
                name: "PushDeviceRegistrationId",
                table: "NotificationRecipients");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_NotificationId",
                table: "NotificationRecipients",
                column: "NotificationId");
        }
    }
}
