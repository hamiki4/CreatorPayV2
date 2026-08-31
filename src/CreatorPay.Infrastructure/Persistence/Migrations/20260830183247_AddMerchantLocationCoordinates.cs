using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantLocationCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "merchant_locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "merchant_locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_merchant_locations_latitude",
                table: "merchant_locations",
                sql: "\"Latitude\" IS NULL OR (\"Latitude\" >= -90 AND \"Latitude\" <= 90)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_merchant_locations_longitude",
                table: "merchant_locations",
                sql: "\"Longitude\" IS NULL OR (\"Longitude\" >= -180 AND \"Longitude\" <= 180)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_merchant_locations_latitude",
                table: "merchant_locations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_merchant_locations_longitude",
                table: "merchant_locations");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "merchant_locations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "merchant_locations");
        }
    }
}
