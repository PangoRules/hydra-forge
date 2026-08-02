using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedUserTokenBudgetLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyLimit",
                table: "user_token_budgets");

            migrationBuilder.DropColumn(
                name: "MonthlyLimit",
                table: "user_token_budgets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyLimit",
                table: "user_token_budgets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyLimit",
                table: "user_token_budgets",
                type: "integer",
                nullable: true);
        }
    }
}
