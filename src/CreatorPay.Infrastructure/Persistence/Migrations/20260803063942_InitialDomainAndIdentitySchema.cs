using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDomainAndIdentitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cashier_location_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashier_location_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cashiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedPhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicCreatorId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedPhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "merchant_creator_partnerships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SuspendedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedCommissionRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedCampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_creator_partnerships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_creator_partnerships_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchant_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "partnership_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantCreatorPartnershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partnership_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_partnership_locations_merchant_creator_partnerships_Merchan~",
                        column: x => x.MerchantCreatorPartnershipId,
                        principalTable: "merchant_creator_partnerships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_partnership_locations_merchant_locations_MerchantLocationId",
                        column: x => x.MerchantLocationId,
                        principalTable: "merchant_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "merchants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicMerchantId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LegalBusinessName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    TradingName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    BusinessType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedPhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TaxRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "supervisors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedPhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supervisors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supervisors_merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supervisor_location_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supervisor_location_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supervisor_location_assignments_merchant_locations_Merchant~",
                        column: x => x.MerchantLocationId,
                        principalTable: "merchant_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supervisor_location_assignments_supervisors_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsPhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_accounts_cashiers_CashierId",
                        column: x => x.CashierId,
                        principalTable: "cashiers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_accounts_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "creators",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_accounts_merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_accounts_supervisors_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "supervisors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_cashier_location_assignments_CashierId_MerchantLocationId",
                table: "cashier_location_assignments",
                columns: new[] { "CashierId", "MerchantLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cashier_location_assignments_MerchantLocationId",
                table: "cashier_location_assignments",
                column: "MerchantLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_cashiers_MerchantId",
                table: "cashiers",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_cashiers_NormalizedEmail",
                table: "cashiers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_cashiers_NormalizedPhoneNumber",
                table: "cashiers",
                column: "NormalizedPhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_creators_ApprovedByUserId",
                table: "creators",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_creators_Email",
                table: "creators",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_creators_NormalizedPhoneNumber",
                table: "creators",
                column: "NormalizedPhoneNumber",
                unique: true,
                filter: "\"NormalizedPhoneNumber\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_creators_PublicCreatorId",
                table: "creators",
                column: "PublicCreatorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creators_Status",
                table: "creators",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_ApprovedByUserId",
                table: "merchant_creator_partnerships",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_CreatorId_Status",
                table: "merchant_creator_partnerships",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_EndDateUtc",
                table: "merchant_creator_partnerships",
                column: "EndDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_MerchantId_CreatorId",
                table: "merchant_creator_partnerships",
                columns: new[] { "MerchantId", "CreatorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_MerchantId_Status",
                table: "merchant_creator_partnerships",
                columns: new[] { "MerchantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_RejectedByUserId",
                table: "merchant_creator_partnerships",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_RequestedByUserId",
                table: "merchant_creator_partnerships",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_creator_partnerships_SuspendedByUserId",
                table: "merchant_creator_partnerships",
                column: "SuspendedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_locations_MerchantId",
                table: "merchant_locations",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_ApprovedByUserId",
                table: "merchants",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_Email",
                table: "merchants",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_NormalizedPhoneNumber",
                table: "merchants",
                column: "NormalizedPhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_PublicMerchantId",
                table: "merchants",
                column: "PublicMerchantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchants_Status",
                table: "merchants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_partnership_locations_MerchantCreatorPartnershipId_Merchant~",
                table: "partnership_locations",
                columns: new[] { "MerchantCreatorPartnershipId", "MerchantLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_partnership_locations_MerchantLocationId",
                table: "partnership_locations",
                column: "MerchantLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_supervisor_location_assignments_MerchantLocationId",
                table: "supervisor_location_assignments",
                column: "MerchantLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_supervisor_location_assignments_SupervisorId_MerchantLocati~",
                table: "supervisor_location_assignments",
                columns: new[] { "SupervisorId", "MerchantLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supervisors_MerchantId",
                table: "supervisors",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_supervisors_NormalizedEmail",
                table: "supervisors",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_supervisors_NormalizedPhoneNumber",
                table: "supervisors",
                column: "NormalizedPhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_CashierId",
                table: "user_accounts",
                column: "CashierId",
                unique: true,
                filter: "\"CashierId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_CreatorId",
                table: "user_accounts",
                column: "CreatorId",
                unique: true,
                filter: "\"CreatorId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_MerchantId",
                table: "user_accounts",
                column: "MerchantId",
                unique: true,
                filter: "\"MerchantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_NormalizedEmail",
                table: "user_accounts",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_SupervisorId",
                table: "user_accounts",
                column: "SupervisorId",
                unique: true,
                filter: "\"SupervisorId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_location_assignments_cashiers_CashierId",
                table: "cashier_location_assignments",
                column: "CashierId",
                principalTable: "cashiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_location_assignments_merchant_locations_MerchantLoc~",
                table: "cashier_location_assignments",
                column: "MerchantLocationId",
                principalTable: "merchant_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashiers_merchants_MerchantId",
                table: "cashiers",
                column: "MerchantId",
                principalTable: "merchants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_creators_user_accounts_ApprovedByUserId",
                table: "creators",
                column: "ApprovedByUserId",
                principalTable: "user_accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_merchant_creator_partnerships_merchants_MerchantId",
                table: "merchant_creator_partnerships",
                column: "MerchantId",
                principalTable: "merchants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_merchant_creator_partnerships_user_accounts_ApprovedByUserId",
                table: "merchant_creator_partnerships",
                column: "ApprovedByUserId",
                principalTable: "user_accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_merchant_creator_partnerships_user_accounts_RejectedByUserId",
                table: "merchant_creator_partnerships",
                column: "RejectedByUserId",
                principalTable: "user_accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_merchant_creator_partnerships_user_accounts_RequestedByUser~",
                table: "merchant_creator_partnerships",
                column: "RequestedByUserId",
                principalTable: "user_accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_merchant_creator_partnerships_user_accounts_SuspendedByUser~",
                table: "merchant_creator_partnerships",
                column: "SuspendedByUserId",
                principalTable: "user_accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_merchant_locations_merchants_MerchantId",
                table: "merchant_locations",
                column: "MerchantId",
                principalTable: "merchants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_merchants_user_accounts_ApprovedByUserId",
                table: "merchants",
                column: "ApprovedByUserId",
                principalTable: "user_accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_accounts_cashiers_CashierId",
                table: "user_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_supervisors_merchants_MerchantId",
                table: "supervisors");

            migrationBuilder.DropForeignKey(
                name: "FK_user_accounts_merchants_MerchantId",
                table: "user_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_creators_user_accounts_ApprovedByUserId",
                table: "creators");

            migrationBuilder.DropTable(
                name: "cashier_location_assignments");

            migrationBuilder.DropTable(
                name: "partnership_locations");

            migrationBuilder.DropTable(
                name: "supervisor_location_assignments");

            migrationBuilder.DropTable(
                name: "merchant_creator_partnerships");

            migrationBuilder.DropTable(
                name: "merchant_locations");

            migrationBuilder.DropTable(
                name: "cashiers");

            migrationBuilder.DropTable(
                name: "merchants");

            migrationBuilder.DropTable(
                name: "user_accounts");

            migrationBuilder.DropTable(
                name: "creators");

            migrationBuilder.DropTable(
                name: "supervisors");
        }
    }
}
