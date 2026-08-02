using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiNarrativeGenerationTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "AiNarrativeGenerationTimeUtc",
                table: "system_settings",
                type: "interval",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "AiNarrativeGenerationTimeUtc",
                value: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiNarrativeGenerationTimeUtc",
                table: "system_settings");
        }
    }
}
