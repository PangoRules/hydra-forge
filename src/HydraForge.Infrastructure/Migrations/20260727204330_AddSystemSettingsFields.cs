using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class AddSystemSettingsFields : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
			    name: "BrandLogoUrl",
			    table: "system_settings",
			    type: "text",
			    nullable: true
			);

			migrationBuilder.AddColumn<string>(
			    name: "BrandName",
			    table: "system_settings",
			    type: "text",
			    nullable: true
			);

			migrationBuilder.AddColumn<string>(
			    name: "NtfyServerUrl",
			    table: "system_settings",
			    type: "text",
			    nullable: true
			);

			migrationBuilder.AddColumn<string>(
			    name: "SearXngUrl",
			    table: "system_settings",
			    type: "text",
			    nullable: true
			);

			migrationBuilder.UpdateData(
			    table: "system_settings",
			    keyColumn: "Id",
			    keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
			    columns: ["BrandLogoUrl", "BrandName", "NtfyServerUrl", "SearXngUrl"],
			    values: [null, null, null, null]
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "BrandLogoUrl", table: "system_settings");

			migrationBuilder.DropColumn(name: "BrandName", table: "system_settings");

			migrationBuilder.DropColumn(name: "NtfyServerUrl", table: "system_settings");

			migrationBuilder.DropColumn(name: "SearXngUrl", table: "system_settings");
		}
	}
}
