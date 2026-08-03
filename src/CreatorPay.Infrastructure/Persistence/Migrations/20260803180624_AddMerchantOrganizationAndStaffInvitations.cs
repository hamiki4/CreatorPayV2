using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantOrganizationAndStaffInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_accounts_MerchantId",
                table: "user_accounts");

            migrationBuilder.DropIndex(
                name: "IX_supervisors_MerchantId",
                table: "supervisors");

            migrationBuilder.DropIndex(
                name: "IX_cashiers_MerchantId",
                table: "cashiers");

            migrationBuilder.CreateTable(
                name: "staff_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    UserRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_invitations_cashiers_CashierId",
                        column: x => x.CashierId,
                        principalTable: "cashiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_invitations_merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_invitations_supervisors_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_invitations_user_accounts_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_MerchantId",
                table: "user_accounts",
                column: "MerchantId",
                unique: true,
                filter: "\"MerchantId\" IS NOT NULL AND \"Role\" = 'MerchantAdmin'");

            migrationBuilder.CreateIndex(
                name: "IX_supervisors_MerchantId_IsActive",
                table: "supervisors",
                columns: new[] { "MerchantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_locations_MerchantId_IsActive",
                table: "merchant_locations",
                columns: new[] { "MerchantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_locations_MerchantId_Name",
                table: "merchant_locations",
                columns: new[] { "MerchantId", "Name" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_cashiers_MerchantId_IsActive",
                table: "cashiers",
                columns: new[] { "MerchantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_cashier_location_assignments_CashierId",
                table: "cashier_location_assignments",
                column: "CashierId",
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_CashierId",
                table: "staff_invitations",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_ExpiresAtUtc",
                table: "staff_invitations",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_InvitedByUserId",
                table: "staff_invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_MerchantId_NormalizedEmail",
                table: "staff_invitations",
                columns: new[] { "MerchantId", "NormalizedEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_SupervisorId",
                table: "staff_invitations",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_TokenHash",
                table: "staff_invitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_invitations");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_MerchantId",
                table: "user_accounts");

            migrationBuilder.DropIndex(
                name: "IX_supervisors_MerchantId_IsActive",
                table: "supervisors");

            migrationBuilder.DropIndex(
                name: "IX_merchant_locations_MerchantId_IsActive",
                table: "merchant_locations");

            migrationBuilder.DropIndex(
                name: "IX_merchant_locations_MerchantId_Name",
                table: "merchant_locations");

            migrationBuilder.DropIndex(
                name: "IX_cashiers_MerchantId_IsActive",
                table: "cashiers");

            migrationBuilder.DropIndex(
                name: "IX_cashier_location_assignments_CashierId",
                table: "cashier_location_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_MerchantId",
                table: "user_accounts",
                column: "MerchantId",
                unique: true,
                filter: "\"MerchantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_supervisors_MerchantId",
                table: "supervisors",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_cashiers_MerchantId",
                table: "cashiers",
                column: "MerchantId");
        }
    }
}
