using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creator_ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantCreatorPartnershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_ratings", x => x.Id);
                    table.CheckConstraint("CK_creator_ratings_rating", "\"Rating\" >= 1 AND \"Rating\" <= 5");
                    table.ForeignKey(
                        name: "FK_creator_ratings_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_creator_ratings_merchant_creator_partnerships_MerchantCreat~",
                        column: x => x.MerchantCreatorPartnershipId,
                        principalTable: "merchant_creator_partnerships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_creator_ratings_merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_creator_ratings_creator",
                table: "creator_ratings",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_creator_ratings_creator_rating",
                table: "creator_ratings",
                columns: new[] { "CreatorId", "Rating" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_ratings_merchant",
                table: "creator_ratings",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "UX_creator_ratings_partnership",
                table: "creator_ratings",
                column: "MerchantCreatorPartnershipId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_ratings");
        }
    }
}
