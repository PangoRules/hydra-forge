using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCardDueDateToDueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DueDate",
                table: "cards",
                newName: "DueAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "comments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "comments");

            migrationBuilder.RenameColumn(
                name: "DueAt",
                table: "cards",
                newName: "DueDate");
        }
    }
}
