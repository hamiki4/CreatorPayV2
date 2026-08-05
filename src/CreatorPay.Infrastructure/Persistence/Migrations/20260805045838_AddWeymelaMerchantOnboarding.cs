using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeymelaMerchantOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_merchants_Email",
                table: "merchants");

            migrationBuilder.AddColumn<string>(
                name: "BusinessRegistrationNumber",
                table: "merchants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "merchants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpeningHours",
                table: "merchants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "merchants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryContactName",
                table: "merchants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublicDescription",
                table: "merchants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAtUtc",
                table: "merchants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_merchants_Email",
                table: "merchants",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchants_TradingName",
                table: "merchants",
                column: "TradingName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_merchants_Email",
                table: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_merchants_TradingName",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "BusinessRegistrationNumber",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "OpeningHours",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "PrimaryContactName",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "PublicDescription",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAtUtc",
                table: "merchants");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_Email",
                table: "merchants",
                column: "Email");
        }
    }
}
