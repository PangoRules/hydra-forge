using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "agent_personalities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Configuration = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_personalities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "album_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_album_images", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ExternalUrl = table.Column<string>(type: "text", nullable: true),
                    WebhookSecret = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "card_assignees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_assignees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "card_chat_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_chat_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "card_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_relationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardNumber = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CardType = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chat_folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chat_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "checklist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checklist_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "columns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_columns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    VersionCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_model_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    PricePerToken = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxTokens = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_model_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gallery_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImagePath = table.Column<string>(type: "text", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gallery_images", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "image_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "image_usage_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Feature = table.Column<string>(type: "text", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    ImageCount = table.Column<int>(type: "integer", nullable: false),
                    Resolution = table.Column<string>(type: "text", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_usage_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memory_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "note_image_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImagePath = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_image_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "note_reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemindAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    ActionUrl = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "personal_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plan_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    VersionCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_context_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateContent = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_context_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "spec_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spec_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "specs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    VersionCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "token_usage_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token_usage_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_token_budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyTokenBudget = table.Column<int>(type: "integer", nullable: false),
                    MonthlyTokenUsed = table.Column<int>(type: "integer", nullable: false),
                    MonthlyImageBudget = table.Column<int>(type: "integer", nullable: false),
                    MonthlyImageUsed = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_token_budgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    UsernameNormalized = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    EmailNormalized = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_personalities_UserId",
                table: "agent_personalities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_album_images_AlbumId",
                table: "album_images",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_albums_UserId",
                table: "albums",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_CardId",
                table: "attachments",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_ActorId",
                table: "audit_log_entries",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_EntityType_EntityId",
                table: "audit_log_entries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_ProjectId",
                table: "audit_log_entries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_events_ExternalId",
                table: "calendar_events",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_events_SourceId",
                table: "calendar_events",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_sources_UserId",
                table: "calendar_sources",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_card_assignees_CardId_UserId",
                table: "card_assignees",
                columns: new[] { "CardId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_card_chat_links_CardId_SessionId",
                table: "card_chat_links",
                columns: new[] { "CardId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_card_relationships_SourceCardId_TargetCardId",
                table: "card_relationships",
                columns: new[] { "SourceCardId", "TargetCardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_ColumnId",
                table: "cards",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_cards_ProjectId_CardNumber",
                table: "cards",
                columns: new[] { "ProjectId", "CardNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_folders_UserId",
                table: "chat_folders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_SessionId",
                table: "chat_messages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_FolderId",
                table: "chat_sessions",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_UserId",
                table: "chat_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_checklist_items_CardId",
                table: "checklist_items",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_columns_ProjectId_SortOrder",
                table: "columns",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_CardId",
                table: "comments",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_DocumentId",
                table: "document_chunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_DocumentId",
                table: "document_versions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_UserId",
                table: "documents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_model_configs_ModelId",
                table: "provider_model_configs",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_model_configs_ProviderId",
                table: "provider_model_configs",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_gallery_images_UserId",
                table: "gallery_images",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_image_tags_ImageId",
                table: "image_tags",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_image_tags_Tag",
                table: "image_tags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "IX_image_usage_records_CreatedAt",
                table: "image_usage_records",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_image_usage_records_Feature",
                table: "image_usage_records",
                column: "Feature");

            migrationBuilder.CreateIndex(
                name: "IX_image_usage_records_ProviderId",
                table: "image_usage_records",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_image_usage_records_UserId",
                table: "image_usage_records",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_llm_providers_Name",
                table: "llm_providers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_memory_entries_Category",
                table: "memory_entries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_memory_entries_UserId",
                table: "memory_entries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_note_image_attachments_NoteId",
                table: "note_image_attachments",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_note_reminders_IsSent_RemindAt",
                table: "note_reminders",
                columns: new[] { "IsSent", "RemindAt" });

            migrationBuilder.CreateIndex(
                name: "IX_note_reminders_NoteId",
                table: "note_reminders",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_notes_UserId",
                table: "notes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_IsRead_CreatedAt",
                table: "notifications",
                columns: new[] { "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId",
                table: "notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_personal_tasks_IsCompleted_DueDate",
                table: "personal_tasks",
                columns: new[] { "IsCompleted", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_tasks_UserId",
                table: "personal_tasks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_versions_PlanId",
                table: "plan_versions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_plans_ProjectId",
                table: "plans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_context_snapshots_ProjectId",
                table: "project_context_snapshots",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_members_ProjectId_UserId",
                table: "project_members",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_Name",
                table: "projects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_spec_versions_SpecId",
                table: "spec_versions",
                column: "SpecId");

            migrationBuilder.CreateIndex(
                name: "IX_specs_ProjectId",
                table: "specs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_token_usage_records_CreatedAt",
                table: "token_usage_records",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_token_usage_records_UserId",
                table: "token_usage_records",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_budgets_UserId",
                table: "user_token_budgets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailNormalized",
                table: "users",
                column: "EmailNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_users_UsernameNormalized",
                table: "users",
                column: "UsernameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_personalities");

            migrationBuilder.DropTable(
                name: "album_images");

            migrationBuilder.DropTable(
                name: "albums");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "calendar_events");

            migrationBuilder.DropTable(
                name: "calendar_sources");

            migrationBuilder.DropTable(
                name: "card_assignees");

            migrationBuilder.DropTable(
                name: "card_chat_links");

            migrationBuilder.DropTable(
                name: "card_relationships");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "chat_folders");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "chat_sessions");

            migrationBuilder.DropTable(
                name: "checklist_items");

            migrationBuilder.DropTable(
                name: "columns");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "document_versions");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "provider_model_configs");

            migrationBuilder.DropTable(
                name: "gallery_images");

            migrationBuilder.DropTable(
                name: "image_tags");

            migrationBuilder.DropTable(
                name: "image_usage_records");

            migrationBuilder.DropTable(
                name: "llm_providers");

            migrationBuilder.DropTable(
                name: "memory_entries");

            migrationBuilder.DropTable(
                name: "note_image_attachments");

            migrationBuilder.DropTable(
                name: "note_reminders");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "personal_tasks");

            migrationBuilder.DropTable(
                name: "plan_versions");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropTable(
                name: "project_context_snapshots");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "spec_versions");

            migrationBuilder.DropTable(
                name: "specs");

            migrationBuilder.DropTable(
                name: "token_usage_records");

            migrationBuilder.DropTable(
                name: "user_token_budgets");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
