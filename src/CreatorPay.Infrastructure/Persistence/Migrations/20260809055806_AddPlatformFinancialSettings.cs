using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFinancialSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_financial_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MinimumBusinessWalletBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_financial_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_financial_settings_CurrencyCode",
                table: "platform_financial_settings",
                column: "CurrencyCode",
                unique: true);

            migrationBuilder.InsertData(
                table: "platform_financial_settings",
                columns: new[] { "Id", "CurrencyCode", "MinimumBusinessWalletBalance", "ChangedByUserId", "ChangedAtUtc", "CreatedAtUtc", "CreatedBy" },
                values: new object[] { new Guid("8ad188f9-35da-498a-9ffd-9efb30238954"), "ETB", 1000m, Guid.Empty, new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc), "Migration default" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_financial_settings");
        }
    }
}
