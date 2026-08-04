using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantCreatorPartnershipWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntroductoryMessage",
                table: "merchant_creator_partnerships",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "merchant_creator_partnerships",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "partnership_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantCreatorPartnershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partnership_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_partnership_status_history_merchant_creator_partnerships_Me~",
                        column: x => x.MerchantCreatorPartnershipId,
                        principalTable: "merchant_creator_partnerships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_partnership_status_history_user_accounts_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_partnership_status_history_ChangedByUserId",
                table: "partnership_status_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_partnership_status_history_MerchantCreatorPartnershipId_Cha~",
                table: "partnership_status_history",
                columns: new[] { "MerchantCreatorPartnershipId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partnership_status_history");

            migrationBuilder.DropColumn(
                name: "IntroductoryMessage",
                table: "merchant_creator_partnerships");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "merchant_creator_partnerships");
        }
    }
}
