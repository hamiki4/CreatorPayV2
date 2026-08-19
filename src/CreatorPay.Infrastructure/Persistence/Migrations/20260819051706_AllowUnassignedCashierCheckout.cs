using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowUnassignedCashierCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(name: "MerchantLocationId", table: "PurchaseTransactions", type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(name: "MerchantLocationId", table: "PurchaseTransactions", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        }
    }
}
