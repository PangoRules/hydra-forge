using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveAndHousekeepingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "documents");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "personal_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "notes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "memory_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "card_chat_links",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "calendar_sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "calendar_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchivedItemRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    AuditLogRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    NotificationRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "Id", "ArchivedItemRetentionDays", "AuditLogRetentionDays", "CreatedAt", "NotificationRetentionDays", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), 730, 90, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 30, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_chat_sessions_SessionId",
                table: "chat_messages",
                column: "SessionId",
                principalTable: "chat_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_document_versions_documents_DocumentId",
                table: "document_versions",
                column: "DocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_note_image_attachments_notes_NoteId",
                table: "note_image_attachments",
                column: "NoteId",
                principalTable: "notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_note_reminders_notes_NoteId",
                table: "note_reminders",
                column: "NoteId",
                principalTable: "notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_chat_sessions_SessionId",
                table: "chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_document_versions_documents_DocumentId",
                table: "document_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_note_image_attachments_notes_NoteId",
                table: "note_image_attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_note_reminders_notes_NoteId",
                table: "note_reminders");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "personal_tasks");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "memory_entries");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "card_chat_links");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "calendar_sources");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "calendar_events");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
