using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeymelaCreatorPilotExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Biography",
                table: "creators",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "creators",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContentCategories",
                table: "creators",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GovernmentIdReference",
                table: "creators",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "creators",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredPayoutAccountIdentifier",
                table: "creators",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredPayoutChannel",
                table: "creators",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentificationNumber",
                table: "creators",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAtUtc",
                table: "creators",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Zone",
                table: "creators",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "creator_social_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Handle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProfileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FollowerCount = table.Column<long>(type: "bigint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_social_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_social_profiles_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_creator_social_profiles_CreatorId_Platform_Handle",
                table: "creator_social_profiles",
                columns: new[] { "CreatorId", "Platform", "Handle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_social_profiles");

            migrationBuilder.DropColumn(
                name: "Biography",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "City",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "ContentCategories",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "GovernmentIdReference",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "PreferredPayoutAccountIdentifier",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "PreferredPayoutChannel",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "TaxIdentificationNumber",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAtUtc",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "Zone",
                table: "creators");
        }
    }
}
