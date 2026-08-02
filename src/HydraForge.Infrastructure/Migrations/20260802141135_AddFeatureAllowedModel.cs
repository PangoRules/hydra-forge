using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureAllowedModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_allowed_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureRoutingConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderModelConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_allowed_models", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feature_allowed_models_feature_routing_configs_FeatureRouti~",
                        column: x => x.FeatureRoutingConfigId,
                        principalTable: "feature_routing_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_feature_allowed_models_provider_model_configs_ProviderModel~",
                        column: x => x.ProviderModelConfigId,
                        principalTable: "provider_model_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feature_allowed_models_FeatureRoutingConfigId",
                table: "feature_allowed_models",
                column: "FeatureRoutingConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_allowed_models_FeatureRoutingConfigId_ProviderModel~",
                table: "feature_allowed_models",
                columns: new[] { "FeatureRoutingConfigId", "ProviderModelConfigId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feature_allowed_models_ProviderModelConfigId",
                table: "feature_allowed_models",
                column: "ProviderModelConfigId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_allowed_models");
        }
    }
}
