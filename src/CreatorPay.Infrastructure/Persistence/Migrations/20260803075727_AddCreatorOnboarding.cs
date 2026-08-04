using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileImageContentType",
                table: "creators",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageFileName",
                table: "creators",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProfileImageSizeBytes",
                table: "creators",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "creator_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_audit_events_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_creator_audit_events_user_accounts_ActorUserAccountId",
                        column: x => x.ActorUserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "creator_verification_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_verification_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_verification_tokens_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_creator_audit_events_ActorUserAccountId",
                table: "creator_audit_events",
                column: "ActorUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_creator_audit_events_CreatorId_CreatedAtUtc",
                table: "creator_audit_events",
                columns: new[] { "CreatorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_verification_tokens_TokenHash",
                table: "creator_verification_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_verification_tokens_UserAccountId_Purpose_ExpiresAt~",
                table: "creator_verification_tokens",
                columns: new[] { "UserAccountId", "Purpose", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_audit_events");

            migrationBuilder.DropTable(
                name: "creator_verification_tokens");

            migrationBuilder.DropColumn(
                name: "ProfileImageContentType",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "ProfileImageFileName",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "ProfileImageSizeBytes",
                table: "creators");
        }
    }
}
