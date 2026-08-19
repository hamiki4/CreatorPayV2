using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowPartnershipRequestHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_merchant_creator_partnerships_MerchantId_CreatorId",
                table: "merchant_creator_partnerships");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_active_or_pending_pair",
                table: "merchant_creator_partnerships",
                columns: new[] { "MerchantId", "CreatorId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Approved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_merchant_creator_partnerships_active_or_pending_pair",
                table: "merchant_creator_partnerships");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_MerchantId_CreatorId",
                table: "merchant_creator_partnerships",
                columns: new[] { "MerchantId", "CreatorId" },
                unique: true);
        }
    }
}
