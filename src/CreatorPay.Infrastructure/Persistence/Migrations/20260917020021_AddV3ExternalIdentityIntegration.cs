using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddV3ExternalIdentityIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_NormalizedPhoneNumber",
                table: "Customers");

            migrationBuilder.AddColumn<string>(
                name: "AuthenticationSource",
                table: "user_accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.CreateTable(
                name: "external_identities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Environment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityBindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityBindingVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_identities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "external_profile_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalProfileSubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalBusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProvisioningKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_profile_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_profile_links_external_identities_ExternalIdentity~",
                        column: x => x.ExternalIdentityId,
                        principalTable: "external_identities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_profile_links_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_application_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalProfileLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentityBindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityBindingVersion = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_application_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_application_sessions_external_identities_ExternalI~",
                        column: x => x.ExternalIdentityId,
                        principalTable: "external_identities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_application_sessions_external_profile_links_Extern~",
                        column: x => x.ExternalProfileLinkId,
                        principalTable: "external_profile_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_application_sessions_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedPhoneNumber",
                table: "Customers",
                column: "NormalizedPhoneNumber",
                unique: true,
                filter: "\"NormalizedPhoneNumber\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_external_application_sessions_ExternalIdentityId_ExpiresAtU~",
                table: "external_application_sessions",
                columns: new[] { "ExternalIdentityId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_external_application_sessions_ExternalProfileLinkId",
                table: "external_application_sessions",
                column: "ExternalProfileLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_external_application_sessions_UserAccountId_RevokedAtUtc",
                table: "external_application_sessions",
                columns: new[] { "UserAccountId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_external_identities_IdentityBindingId_IdentityBindingVersion",
                table: "external_identities",
                columns: new[] { "IdentityBindingId", "IdentityBindingVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_external_identities_Issuer_Environment_ExternalUserId",
                table: "external_identities",
                columns: new[] { "Issuer", "Environment", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_profile_links_ExternalIdentityId_Role",
                table: "external_profile_links",
                columns: new[] { "ExternalIdentityId", "Role" },
                unique: true,
                filter: "\"Role\" <> 'MerchantAdmin'");

            migrationBuilder.CreateIndex(
                name: "IX_external_profile_links_ExternalIdentityId_Role_ExternalProf~",
                table: "external_profile_links",
                columns: new[] { "ExternalIdentityId", "Role", "ExternalProfileSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_profile_links_ExternalIdentityId_Role_Status",
                table: "external_profile_links",
                columns: new[] { "ExternalIdentityId", "Role", "Status" },
                unique: true,
                filter: "\"ExternalProfileSubjectId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_external_profile_links_ProvisioningKey",
                table: "external_profile_links",
                column: "ProvisioningKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_profile_links_UserAccountId",
                table: "external_profile_links",
                column: "UserAccountId",
                unique: true,
                filter: "\"UserAccountId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_application_sessions");

            migrationBuilder.DropTable(
                name: "external_profile_links");

            migrationBuilder.DropTable(
                name: "external_identities");

            migrationBuilder.DropIndex(
                name: "IX_Customers_NormalizedPhoneNumber",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AuthenticationSource",
                table: "user_accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedPhoneNumber",
                table: "Customers",
                column: "NormalizedPhoneNumber",
                unique: true);
        }
    }
}
