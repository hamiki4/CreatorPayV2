using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations;

public partial class AddMerchantBusinessTypeConstraint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "CK_merchants_BusinessType_Valid",
            table: "merchants",
            sql: "\"BusinessType\" IN ('Restaurant / Café','Grocery / Mini-market','Clothing / Boutique','Beauty / Salon','Furniture','Electronics','Hotel / Travel','Professional Services','Other')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_merchants_BusinessType_Valid",
            table: "merchants");
    }
}
