using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations;

public partial class AddOfferReuseRule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ReuseRule",
            table: "CreatorMerchantCampaigns",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "OncePerOffer");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ReuseRule", table: "CreatorMerchantCampaigns");
    }
}
