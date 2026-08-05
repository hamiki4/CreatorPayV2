using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260805120000_NormalizeEthiopianRegistrationPhones")]
public sealed class NormalizeEthiopianRegistrationPhones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(NormalizeTable("\"Customers\""));
        migrationBuilder.Sql(NormalizeTable("creators"));
        migrationBuilder.Sql(NormalizeTable("merchants"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Canonical E.164 values are intentionally retained on rollback; their original formatting is not recoverable.
    }

    private static string NormalizeTable(string table) => $$"""
        UPDATE {{table}}
        SET "PhoneNumber" = CASE
                WHEN "NormalizedPhoneNumber" ~ '^0[79][0-9]{8}$' THEN '+251' || substring("NormalizedPhoneNumber" from 2)
                WHEN "NormalizedPhoneNumber" ~ '^251[79][0-9]{8}$' THEN '+' || "NormalizedPhoneNumber"
                ELSE "NormalizedPhoneNumber"
            END,
            "NormalizedPhoneNumber" = CASE
                WHEN "NormalizedPhoneNumber" ~ '^0[79][0-9]{8}$' THEN '+251' || substring("NormalizedPhoneNumber" from 2)
                WHEN "NormalizedPhoneNumber" ~ '^251[79][0-9]{8}$' THEN '+' || "NormalizedPhoneNumber"
                ELSE "NormalizedPhoneNumber"
            END
        WHERE "NormalizedPhoneNumber" ~ '^(0[79][0-9]{8}|251[79][0-9]{8})$';
        """;
}
