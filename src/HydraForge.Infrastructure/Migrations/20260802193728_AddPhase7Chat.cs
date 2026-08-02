using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase7Chat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiEditMode",
                table: "chat_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "chat_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpenCardId",
                table: "chat_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalityId",
                table: "chat_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SearchAllMyDocs",
                table: "chat_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "chat_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "chat_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagesJson",
                table: "chat_messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chat_session_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_session_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_session_documents_chat_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "chat_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_preset_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_preset_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_presets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_presets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_presets_prompt_preset_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "prompt_preset_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_PersonalityId",
                table: "chat_sessions",
                column: "PersonalityId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_session_documents_SessionId_DocumentId",
                table: "chat_session_documents",
                columns: new[] { "SessionId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_preset_groups_UserId",
                table: "prompt_preset_groups",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_presets_GroupId",
                table: "prompt_presets",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_presets_UserId",
                table: "prompt_presets",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_chat_sessions_agent_personalities_PersonalityId",
                table: "chat_sessions",
                column: "PersonalityId",
                principalTable: "agent_personalities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_sessions_agent_personalities_PersonalityId",
                table: "chat_sessions");

            migrationBuilder.DropTable(
                name: "chat_session_documents");

            migrationBuilder.DropTable(
                name: "prompt_presets");

            migrationBuilder.DropTable(
                name: "prompt_preset_groups");

            migrationBuilder.DropIndex(
                name: "IX_chat_sessions_PersonalityId",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "AiEditMode",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "OpenCardId",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "PersonalityId",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "SearchAllMyDocs",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "ImagesJson",
                table: "chat_messages");
        }
    }
}
