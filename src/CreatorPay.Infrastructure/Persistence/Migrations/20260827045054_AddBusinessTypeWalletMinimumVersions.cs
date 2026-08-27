using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTypeWalletMinimumVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_type_wallet_minimum_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BusinessType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    MinimumBusinessWalletBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_type_wallet_minimum_versions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_type_wallet_minimum_versions_CurrencyCode_Busines~1",
                table: "business_type_wallet_minimum_versions",
                columns: new[] { "CurrencyCode", "BusinessType", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_type_wallet_minimum_versions_CurrencyCode_Business~",
                table: "business_type_wallet_minimum_versions",
                columns: new[] { "CurrencyCode", "BusinessType", "EffectiveFromUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_type_wallet_minimum_versions");
        }
    }
}
