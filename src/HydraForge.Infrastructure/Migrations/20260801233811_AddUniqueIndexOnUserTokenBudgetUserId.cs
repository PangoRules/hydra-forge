using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnUserTokenBudgetUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_token_budgets_UserId",
                table: "user_token_budgets");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_budgets_UserId",
                table: "user_token_budgets",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_token_budgets_UserId",
                table: "user_token_budgets");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_budgets_UserId",
                table: "user_token_budgets",
                column: "UserId");
        }
    }
}
