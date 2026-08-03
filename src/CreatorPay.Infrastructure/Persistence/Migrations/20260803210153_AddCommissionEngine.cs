using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BeforeValues = table.Column<string>(type: "jsonb", nullable: true),
                    AfterValues = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionRules_CommissionPlans_CommissionPlanId",
                        column: x => x.CommissionPlanId,
                        principalTable: "CommissionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignCommissionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantCreatorPartnershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignCommissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignCommissionAssignments_CommissionRules_CommissionRul~",
                        column: x => x.CommissionRuleId,
                        principalTable: "CommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionRuleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    MerchantCommissionRatePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    CreatorSharePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    PlatformSharePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    MinimumPurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumPurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RoundingMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionRuleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionRuleVersions_CommissionRules_CommissionRuleId",
                        column: x => x.CommissionRuleId,
                        principalTable: "CommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MerchantCommissionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantCommissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantCommissionAssignments_CommissionRules_CommissionRul~",
                        column: x => x.CommissionRuleId,
                        principalTable: "CommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartnershipCommissionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantCreatorPartnershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnershipCommissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnershipCommissionAssignments_CommissionRules_Commission~",
                        column: x => x.CommissionRuleId,
                        principalTable: "CommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformCommissionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformCommissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformCommissionAssignments_CommissionRules_CommissionRul~",
                        column: x => x.CommissionRuleId,
                        principalTable: "CommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionCalculationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MerchantCommissionRatePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    TotalCommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatorSharePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    CreatorCommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformSharePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    PlatformCommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoundingMode = table.Column<string>(type: "text", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RuleSourceType = table.Column<string>(type: "text", nullable: false),
                    RuleSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculationVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionCalculationSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionCalculationSnapshots_CommissionRuleVersions_Commi~",
                        column: x => x.CommissionRuleVersionId,
                        principalTable: "CommissionRuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignCommissionAssignments_CampaignId_MerchantCreatorPar~",
                table: "CampaignCommissionAssignments",
                columns: new[] { "CampaignId", "MerchantCreatorPartnershipId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignCommissionAssignments_CommissionRuleId",
                table: "CampaignCommissionAssignments",
                column: "CommissionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignCommissionAssignments_IsActive",
                table: "CampaignCommissionAssignments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAuditEvents_EventType",
                table: "CommissionAuditEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionCalculationSnapshots_CommissionRuleVersionId",
                table: "CommissionCalculationSnapshots",
                column: "CommissionRuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPlans_IsActive",
                table: "CommissionPlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_CommissionPlanId",
                table: "CommissionRules",
                column: "CommissionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_CurrencyCode",
                table: "CommissionRules",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_IsActive",
                table: "CommissionRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRuleVersions_CommissionRuleId_EffectiveFromUtc_Ef~",
                table: "CommissionRuleVersions",
                columns: new[] { "CommissionRuleId", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRuleVersions_CommissionRuleId_VersionNumber",
                table: "CommissionRuleVersions",
                columns: new[] { "CommissionRuleId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRuleVersions_IsActive",
                table: "CommissionRuleVersions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantCommissionAssignments_CommissionRuleId",
                table: "MerchantCommissionAssignments",
                column: "CommissionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantCommissionAssignments_IsActive",
                table: "MerchantCommissionAssignments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantCommissionAssignments_MerchantId_EffectiveFromUtc_E~",
                table: "MerchantCommissionAssignments",
                columns: new[] { "MerchantId", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnershipCommissionAssignments_CommissionRuleId",
                table: "PartnershipCommissionAssignments",
                column: "CommissionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnershipCommissionAssignments_IsActive",
                table: "PartnershipCommissionAssignments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PartnershipCommissionAssignments_MerchantCreatorPartnership~",
                table: "PartnershipCommissionAssignments",
                columns: new[] { "MerchantCreatorPartnershipId", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommissionAssignments_CommissionRuleId",
                table: "PlatformCommissionAssignments",
                column: "CommissionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommissionAssignments_CurrencyCode_EffectiveFromUtc~",
                table: "PlatformCommissionAssignments",
                columns: new[] { "CurrencyCode", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommissionAssignments_IsActive",
                table: "PlatformCommissionAssignments",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignCommissionAssignments");

            migrationBuilder.DropTable(
                name: "CommissionAuditEvents");

            migrationBuilder.DropTable(
                name: "CommissionCalculationSnapshots");

            migrationBuilder.DropTable(
                name: "MerchantCommissionAssignments");

            migrationBuilder.DropTable(
                name: "PartnershipCommissionAssignments");

            migrationBuilder.DropTable(
                name: "PlatformCommissionAssignments");

            migrationBuilder.DropTable(
                name: "CommissionRuleVersions");

            migrationBuilder.DropTable(
                name: "CommissionRules");

            migrationBuilder.DropTable(
                name: "CommissionPlans");
        }
    }
}
