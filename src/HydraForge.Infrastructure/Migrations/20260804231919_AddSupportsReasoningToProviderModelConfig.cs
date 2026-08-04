using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportsReasoningToProviderModelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SupportsReasoning",
                table: "provider_model_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportsReasoning",
                table: "provider_model_configs");
        }
    }
}
