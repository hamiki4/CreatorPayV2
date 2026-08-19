using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPushDeviceInstallationIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstallationId",
                table: "PushDeviceRegistrations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceRegistrations_UserAccountId_Platform_Installation~",
                table: "PushDeviceRegistrations",
                columns: new[] { "UserAccountId", "Platform", "InstallationId", "IsActive" },
                unique: true,
                filter: "\"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PushDeviceRegistrations_UserAccountId_Platform_Installation~",
                table: "PushDeviceRegistrations");

            migrationBuilder.DropColumn(
                name: "InstallationId",
                table: "PushDeviceRegistrations");
        }
    }
}
