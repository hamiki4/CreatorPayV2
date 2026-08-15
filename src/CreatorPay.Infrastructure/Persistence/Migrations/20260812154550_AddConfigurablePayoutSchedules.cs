using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurablePayoutSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatorCutoffDay",
                table: "platform_financial_settings",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CreatorCutoffTime",
                table: "platform_financial_settings",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "CreatorPayoutDay",
                table: "platform_financial_settings",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutScheduleEffectiveFromUtc",
                table: "platform_financial_settings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<int>(
                name: "ShopperCutoffDay",
                table: "platform_financial_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ShopperCutoffTime",
                table: "platform_financial_settings",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "ShopperPayoutDay",
                table: "platform_financial_settings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "payout_schedule_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatorCutoffDay = table.Column<int>(type: "integer", nullable: false),
                    CreatorCutoffTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CreatorPayoutDay = table.Column<int>(type: "integer", nullable: false),
                    ShopperCutoffDay = table.Column<int>(type: "integer", nullable: false),
                    ShopperCutoffTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ShopperPayoutDay = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payout_schedule_versions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payout_schedule_versions_CurrencyCode_EffectiveFromUtc",
                table: "payout_schedule_versions",
                columns: new[] { "CurrencyCode", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payout_schedule_versions_CurrencyCode_VersionNumber",
                table: "payout_schedule_versions",
                columns: new[] { "CurrencyCode", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payout_schedule_versions");

            migrationBuilder.DropColumn(
                name: "CreatorCutoffDay",
                table: "platform_financial_settings");

            migrationBuilder.DropColumn(
                name: "CreatorCutoffTime",
                table: "platform_financial_settings");

            migrationBuilder.DropColumn(
                name: "CreatorPayoutDay",
                table: "platform_financial_settings");

            migrationBuilder.DropColumn(
                name: "PayoutScheduleEffectiveFromUtc",
                table: "platform_financial_settings");

            migrationBuilder.DropColumn(
                name: "ShopperCutoffDay",
                table: "platform_financial_settings");

            migrationBuilder.DropColumn(
                name: "ShopperCutoffTime",
                table: "platform_financial_settings");

            migrationBuilder.DropColumn(
                name: "ShopperPayoutDay",
                table: "platform_financial_settings");
        }
    }
}
