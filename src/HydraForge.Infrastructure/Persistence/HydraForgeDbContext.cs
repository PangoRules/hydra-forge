namespace HydraForge.Infrastructure.Persistence;

using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.PersonalSpace;
using Microsoft.EntityFrameworkCore;

public class HydraForgeDbContext : DbContext
{
    public HydraForgeDbContext(DbContextOptions<HydraForgeDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Column> Columns => Set<Column>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardAssignee> CardAssignees => Set<CardAssignee>();
    public DbSet<CardRelationship> CardRelationships => Set<CardRelationship>();
    public DbSet<CardWatcher> CardWatchers => Set<CardWatcher>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Spec> Specs => Set<Spec>();
    public DbSet<SpecVersion> SpecVersions => Set<SpecVersion>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectContextSnapshot> ProjectContextSnapshots => Set<ProjectContextSnapshot>();
    public DbSet<ChatFolder> ChatFolders => Set<ChatFolder>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<CardChatLink> CardChatLinks => Set<CardChatLink>();
    public DbSet<LlmProvider> LlmProviders => Set<LlmProvider>();
    public DbSet<ProviderModelConfig> ProviderModelConfigs => Set<ProviderModelConfig>();
    public DbSet<UserTokenBudget> UserTokenBudgets => Set<UserTokenBudget>();
    public DbSet<TokenUsageRecord> TokenUsageRecords => Set<TokenUsageRecord>();
    public DbSet<ImageUsageRecord> ImageUsageRecords => Set<ImageUsageRecord>();
    public DbSet<AgentPersonality> AgentPersonalities => Set<AgentPersonality>();
    public DbSet<MemoryEntry> MemoryEntries => Set<MemoryEntry>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteReminder> NoteReminders => Set<NoteReminder>();
    public DbSet<NoteImageAttachment> NoteImageAttachments => Set<NoteImageAttachment>();
    public DbSet<PersonalTask> PersonalTasks => Set<PersonalTask>();
    public DbSet<CalendarSource> CalendarSources => Set<CalendarSource>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<AlbumImage> AlbumImages => Set<AlbumImage>();
    public DbSet<ImageTag> ImageTags => Set<ImageTag>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        ConfigureEntity<User>(modelBuilder, "users", b =>
        {
            b.HasIndex(e => e.UsernameNormalized).IsUnique();
            b.HasIndex(e => e.EmailNormalized);
        });

        ConfigureEntity<Project>(modelBuilder, "projects", b =>
        {
            b.HasIndex(e => e.Name);
        });

        ConfigureEntity<Column>(modelBuilder, "columns", b =>
        {
            b.HasIndex(e => new { e.ProjectId, e.Position });
        });

        ConfigureEntity<Card>(modelBuilder, "cards", b =>
        {
            b.HasIndex(e => new { e.ProjectId, e.CardNumber }).IsUnique();
            b.HasIndex(e => e.ColumnId);
            b.HasIndex(e => e.ParentCardId);
            b.HasIndex(e => e.SpecId);
            b.HasIndex(e => e.PlanId);
        });

        ConfigureEntity<CardAssignee>(modelBuilder, "card_assignees", b =>
        {
            b.HasIndex(e => new { e.CardId, e.UserId }).IsUnique();
        });

        ConfigureEntity<CardRelationship>(modelBuilder, "card_relationships", b =>
        {
            b.HasIndex(e => new { e.SourceCardId, e.TargetCardId }).IsUnique();
        });

        ConfigureEntity<CardWatcher>(modelBuilder, "card_watchers", b =>
        {
            b.HasKey(e => new { e.CardId, e.UserId });
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<Comment>(modelBuilder, "comments", b =>
        {
            b.HasIndex(e => e.CardId);
        });

        ConfigureEntity<ChecklistItem>(modelBuilder, "checklist_items", b =>
        {
            b.HasIndex(e => e.CardId);
        });

        ConfigureEntity<Attachment>(modelBuilder, "attachments", b =>
        {
            b.HasIndex(e => e.CardId);
        });

        ConfigureEntity<Spec>(modelBuilder, "specs", b =>
        {
            b.HasIndex(e => e.ProjectId);
        });

        ConfigureEntity<SpecVersion>(modelBuilder, "spec_versions", b =>
        {
            b.HasIndex(e => e.SpecId);
        });

        ConfigureEntity<Plan>(modelBuilder, "plans", b =>
        {
            b.HasIndex(e => e.ProjectId);
        });

        ConfigureEntity<PlanVersion>(modelBuilder, "plan_versions", b =>
        {
            b.HasIndex(e => e.PlanId);
        });

        ConfigureEntity<AuditLogEntry>(modelBuilder, "audit_log_entries", b =>
        {
            b.HasIndex(e => e.ProjectId);
            b.HasIndex(e => e.ActorId);
            b.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        ConfigureEntity<ProjectMember>(modelBuilder, "project_members", b =>
        {
            b.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();
            b.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureEntity<ProjectContextSnapshot>(modelBuilder, "project_context_snapshots", b =>
        {
            b.HasIndex(e => e.ProjectId).IsUnique();
        });

        ConfigureEntity<ChatFolder>(modelBuilder, "chat_folders", b =>
        {
            b.HasIndex(e => e.OwnerId);
            b.HasIndex(e => e.ParentFolderId);
            b.HasIndex(e => e.ProjectId);
        });

        ConfigureEntity<ChatSession>(modelBuilder, "chat_sessions", b =>
        {
            b.HasIndex(e => e.OwnerId);
            b.HasIndex(e => e.FolderId);
            b.HasIndex(e => e.ProjectId);
        });

        ConfigureEntity<CardChatLink>(modelBuilder, "card_chat_links", b =>
        {
            b.HasIndex(e => new { e.CardId, e.ChatSessionId }).IsUnique();
        });

        ConfigureEntity<LlmProvider>(modelBuilder, "llm_providers", b =>
        {
            b.HasIndex(e => e.Name);
            b.HasIndex(e => e.AdapterType);
            b.HasIndex(e => e.ProviderType);
            b.HasIndex(e => e.Tier);
            b.HasIndex(e => e.FallbackProviderId);
        });

        ConfigureEntity<ProviderModelConfig>(modelBuilder, "provider_model_configs", b =>
        {
            b.HasIndex(e => e.ProviderId);
            b.HasIndex(e => e.ModelId);
        });

        ConfigureEntity<UserTokenBudget>(modelBuilder, "user_token_budgets", b =>
        {
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<TokenUsageRecord>(modelBuilder, "token_usage_records", b =>
        {
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => e.ProviderModelConfigId);
            b.HasIndex(e => e.ProviderId);
            b.HasIndex(e => e.ModelId);
            b.HasIndex(e => e.Feature);
            b.HasIndex(e => e.PipelineRunId);
            b.HasIndex(e => e.CreatedAt);
        });

        ConfigureEntity<ImageUsageRecord>(modelBuilder, "image_usage_records", b =>
        {
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => e.ProviderModelConfigId);
            b.HasIndex(e => e.ProviderId);
            b.HasIndex(e => e.ModelId);
            b.HasIndex(e => e.Feature);
            b.HasIndex(e => e.CreatedAt);
        });

        ConfigureEntity<AgentPersonality>(modelBuilder, "agent_personalities", b =>
        {
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<MemoryEntry>(modelBuilder, "memory_entries", b =>
        {
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => e.Category);
            b.Property(e => e.Embedding).HasColumnType("vector(1536)");
        });

        ConfigureEntity<Note>(modelBuilder, "notes", b =>
        {
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<PersonalTask>(modelBuilder, "personal_tasks", b =>
        {
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => new { e.IsCompleted, e.DueAt });
        });

        ConfigureEntity<CalendarSource>(modelBuilder, "calendar_sources", b =>
        {
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<CalendarEvent>(modelBuilder, "calendar_events", b =>
        {
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => e.CalendarSourceId);
            b.HasIndex(e => e.ExternalUid);
        });

        ConfigureEntity<Document>(modelBuilder, "documents", b =>
        {
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<GalleryImage>(modelBuilder, "gallery_images", b =>
        {
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<Album>(modelBuilder, "albums", b =>
        {
            b.HasIndex(e => e.UserId);
        });

        ConfigureEntity<AlbumImage>(modelBuilder, "album_images", b =>
        {
            b.HasIndex(e => e.AlbumId);
        });

        ConfigureEntity<ImageTag>(modelBuilder, "image_tags", b =>
        {
            b.HasIndex(e => e.ImageId);
            b.HasIndex(e => e.Tag);
        });

        ConfigureEntity<Notification>(modelBuilder, "notifications", b =>
        {
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => new { e.IsRead, e.CreatedAt });
        });

        ConfigureEntity<SystemSettings>(modelBuilder, "system_settings", b =>
        {
            b.HasData(new SystemSettings
            {
                Id = SystemSettingsSingletonId,
                ArchivedItemRetentionDays = 730,
                AuditLogRetentionDays = 90,
                NotificationRetentionDays = 30,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        ConfigureEntity<DocumentVersion>(modelBuilder, "document_versions", b =>
        {
            b.HasIndex(e => e.DocumentId);
            b.HasOne<Document>()
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureEntity<DocumentChunk>(modelBuilder, "document_chunks", b =>
        {
            b.HasIndex(e => e.DocumentId);
            b.HasIndex(e => new { e.SourceType, e.SourceId });
            b.Property(e => e.Embedding).HasColumnType("vector(1536)");
        });

        ConfigureEntity<NoteReminder>(modelBuilder, "note_reminders", b =>
        {
            b.HasIndex(e => e.NoteId);
            b.HasIndex(e => new { e.IsSent, e.TriggerAt });
            b.HasOne<Note>()
                .WithMany()
                .HasForeignKey(e => e.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureEntity<NoteImageAttachment>(modelBuilder, "note_image_attachments", b =>
        {
            b.HasIndex(e => e.NoteId);
            b.HasOne<Note>()
                .WithMany()
                .HasForeignKey(e => e.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureEntity<ChatMessage>(modelBuilder, "chat_messages", b =>
        {
            b.HasIndex(e => e.SessionId);
            b.HasOne<ChatSession>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public static readonly Guid SystemSettingsSingletonId = new("00000000-0000-0000-0000-000000000001");

    private static void ConfigureEntity<T>(ModelBuilder modelBuilder, string tableName, Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T>>? configure = null)
        where T : class
    {
        modelBuilder.Entity<T>(b =>
        {
            b.ToTable(tableName);
            configure?.Invoke(b);
        });
    }
}
