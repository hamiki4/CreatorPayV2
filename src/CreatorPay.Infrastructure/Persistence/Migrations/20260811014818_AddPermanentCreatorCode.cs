using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPermanentCreatorCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatorCode",
                table: "creators",
                type: "character(4)",
                fixedLength: true,
                maxLength: 4,
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT COUNT(*) FROM creators) > 9000 THEN
                        RAISE EXCEPTION 'Cannot assign four-digit Creator IDs: more than 9000 Creators exist.';
                    END IF;
                END $$;

                WITH ranked AS (
                    SELECT "Id", ROW_NUMBER() OVER (ORDER BY "CreatedAtUtc", "Id") AS sequence
                    FROM creators
                )
                UPDATE creators AS creator
                SET "CreatorCode" = (999 + ranked.sequence)::text
                FROM ranked
                WHERE creator."Id" = ranked."Id";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CreatorCode",
                table: "creators",
                type: "character(4)",
                fixedLength: true,
                maxLength: 4,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(4)",
                oldFixedLength: true,
                oldMaxLength: 4,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_creators_CreatorCode",
                table: "creators",
                column: "CreatorCode",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_creators_CreatorCode_FourDigits",
                table: "creators",
                sql: "\"CreatorCode\" ~ '^[1-9][0-9]{3}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_creators_CreatorCode",
                table: "creators");

            migrationBuilder.DropCheckConstraint(
                name: "CK_creators_CreatorCode_FourDigits",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "CreatorCode",
                table: "creators");
        }
    }
}
