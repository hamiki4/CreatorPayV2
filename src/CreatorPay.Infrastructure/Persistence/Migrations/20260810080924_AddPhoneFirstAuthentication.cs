using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneFirstAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_merchants_Email",
                table: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_NormalizedEmail",
                table: "user_accounts");

            migrationBuilder.Sql("UPDATE merchants SET \"Email\" = NULL WHERE btrim(\"Email\") = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "merchants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhoneNumber",
                table: "user_accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "user_accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE user_accounts u
                SET "PhoneNumber" = source.phone,
                    "NormalizedPhoneNumber" = CASE
                        WHEN source.compact ~ '^0[79][0-9]{8}$' THEN '+251' || substring(source.compact from 2)
                        WHEN source.compact ~ '^\+251[79][0-9]{8}$' THEN source.compact
                        WHEN source.compact ~ '^251[79][0-9]{8}$' THEN '+' || source.compact
                        ELSE NULL
                    END
                FROM (
                    SELECT u2."Id" AS user_id,
                           COALESCE(c."PhoneNumber", cr."PhoneNumber", m."PhoneNumber", ca."PhoneNumber") AS phone,
                           regexp_replace(COALESCE(c."PhoneNumber", cr."PhoneNumber", m."PhoneNumber", ca."PhoneNumber", ''), '[[:space:]-]', '', 'g') AS compact
                    FROM user_accounts u2
                    LEFT JOIN "Customers" c ON c."Id" = u2."CustomerId"
                    LEFT JOIN creators cr ON cr."Id" = u2."CreatorId"
                    LEFT JOIN merchants m ON m."Id" = u2."MerchantId" AND u2."Role" = 'MerchantAdmin'
                    LEFT JOIN cashiers ca ON ca."Id" = u2."CashierId"
                ) source
                WHERE u."Id" = source.user_id;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM user_accounts
                        WHERE "NormalizedPhoneNumber" IS NOT NULL
                        GROUP BY "NormalizedPhoneNumber" HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Phone-first migration blocked: duplicate normalized phone identities require manual resolution';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "phone_otp_challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedByIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phone_otp_challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_phone_otp_challenges_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_merchants_Email",
                table: "merchants",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL AND \"Email\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_NormalizedEmail",
                table: "user_accounts",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_NormalizedPhoneNumber",
                table: "user_accounts",
                column: "NormalizedPhoneNumber",
                unique: true,
                filter: "\"NormalizedPhoneNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_phone_otp_challenges_UserAccountId_Purpose_CreatedAtUtc",
                table: "phone_otp_challenges",
                columns: new[] { "UserAccountId", "Purpose", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "phone_otp_challenges");

            migrationBuilder.DropIndex(
                name: "IX_merchants_Email",
                table: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_NormalizedEmail",
                table: "user_accounts");

            migrationBuilder.Sql("UPDATE merchants SET \"Email\" = 'legacy-' || \"Id\"::text || '@weymela.invalid' WHERE \"Email\" IS NULL OR btrim(\"Email\") = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "merchants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_NormalizedPhoneNumber",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "NormalizedPhoneNumber",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "user_accounts");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_Email",
                table: "merchants",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_NormalizedEmail",
                table: "user_accounts",
                column: "NormalizedEmail",
                unique: true);
        }
    }
}
