using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSpecCardsToGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spec card type (integer 3) retired — migrate existing rows to Goal (integer 5).
            migrationBuilder.Sql(@"UPDATE ""cards"" SET ""Type"" = 5 WHERE ""Type"" = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot distinguish rows that were originally Goal from those migrated from Spec — no-op.
        }
    }
}
