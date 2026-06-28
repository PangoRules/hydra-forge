using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeRelationshipUniqueIndexPartial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_card_relationships_SourceCardId_TargetCardId",
                table: "card_relationships");

            migrationBuilder.CreateIndex(
                name: "IX_card_relationships_SourceCardId_TargetCardId",
                table: "card_relationships",
                columns: new[] { "SourceCardId", "TargetCardId" },
                unique: true,
                filter: "\"ArchivedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_card_relationships_SourceCardId_TargetCardId",
                table: "card_relationships");

            migrationBuilder.CreateIndex(
                name: "IX_card_relationships_SourceCardId_TargetCardId",
                table: "card_relationships",
                columns: new[] { "SourceCardId", "TargetCardId" },
                unique: true);
        }
    }
}
