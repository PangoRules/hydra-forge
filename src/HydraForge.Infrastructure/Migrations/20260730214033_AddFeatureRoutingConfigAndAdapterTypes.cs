using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureRoutingConfigAndAdapterTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_routing_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    DefaultTier = table.Column<int>(type: "integer", nullable: false),
                    MaxUserTier = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_routing_configs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feature_routing_configs_Feature",
                table: "feature_routing_configs",
                column: "Feature",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_routing_configs");
        }
    }
}
