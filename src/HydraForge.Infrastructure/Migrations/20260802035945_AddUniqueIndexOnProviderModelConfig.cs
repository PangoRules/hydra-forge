using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnProviderModelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_provider_model_configs_ModelId",
                table: "provider_model_configs");

            migrationBuilder.CreateIndex(
                name: "IX_provider_model_configs_ProviderId_ModelId",
                table: "provider_model_configs",
                columns: new[] { "ProviderId", "ModelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_provider_model_configs_ProviderId_ModelId",
                table: "provider_model_configs");

            migrationBuilder.CreateIndex(
                name: "IX_provider_model_configs_ModelId",
                table: "provider_model_configs",
                column: "ModelId");
        }
    }
}
