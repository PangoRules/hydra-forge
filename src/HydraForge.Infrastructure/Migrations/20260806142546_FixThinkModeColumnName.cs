using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixThinkModeColumnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 20260805164019_AddOllamaThinkModeToProviderModelConfig added the column as
            // "ollama_think_mode", but HydraForgeDbContext's ThinkMode property has always
            // mapped to "think_mode" (HasColumnName("think_mode")) — a naming mismatch baked
            // into that migration that the model snapshot never caught, since the snapshot
            // already declared "think_mode" independent of what the migration actually ran.
            // Any query touching provider_model_configs.ThinkMode fails with Postgres 42703
            // (column does not exist) until this rename lands.
            migrationBuilder.RenameColumn(
                name: "ollama_think_mode",
                table: "provider_model_configs",
                newName: "think_mode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "think_mode",
                table: "provider_model_configs",
                newName: "ollama_think_mode");
        }
    }
}
