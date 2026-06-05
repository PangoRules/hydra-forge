namespace HydraForge.Infrastructure.Tests.Persistence;

using HydraForge.Infrastructure.Persistence;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using System.Linq;

public class HydraForgeDbContextModelTests
{
    private static void AssertProperties(Microsoft.EntityFrameworkCore.Metadata.IEntityType entity, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            Assert.True(
                entity.GetProperties().Any(p => p.Name == propName),
                $"{entity.ClrType.Name} missing property: {propName}");
        }
    }

    private static DbContextOptions<HydraForgeDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql("Host=localhost;Database=hydraforge_test;Username=postgres;Password=password", o => o.UseVector())
            .Options;
    }

    [Fact]
    public void FindEntityType_MemoryEntry_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(MemoryEntry)));
    }

    [Fact]
    public void FindEntityType_DocumentChunk_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(DocumentChunk)));
    }

    [Fact]
    public void GetEntityTypes_UsersTable_Exists()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "users");
    }

    [Fact]
    public void GetEntityTypes_CardsTable_Exists()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "cards");
    }

    [Fact]
    public void GetEntityTypes_ProviderModelConfigsTable_Exists()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(ProviderModelConfig)));
        Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "provider_model_configs");
    }

    [Fact]
    public void FindEntityType_ImageUsageRecord_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(ImageUsageRecord));
        Assert.NotNull(entity);

        AssertProperties(entity, "UserId", "Feature", "ProviderModelConfigId", "ProviderId", "ModelId", "ModelName", "ImageCount", "Resolution", "Cost");
    }

    [Fact]
    public void FindEntityType_ImageUsageRecord_FeatureUsesSharedAiFeatureEnum()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(ImageUsageRecord));
        Assert.NotNull(entity);

        var feature = entity.FindProperty(nameof(ImageUsageRecord.Feature));
        Assert.NotNull(feature);
        Assert.Equal(typeof(AiFeature), feature.ClrType);
    }

    [Fact]
    public void FindEntityType_LlmProvider_HasRoutingAndAdapterProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(LlmProvider));
        Assert.NotNull(entity);

        var requiredProps = new[] { "ApiKeyEncrypted", "AdapterType", "ProviderType", "Tier", "FallbackProviderId" };
        foreach (var propName in requiredProps)
        {
            Assert.True(
                entity.GetProperties().Any(p => p.Name == propName),
                $"LlmProvider missing property: {propName}");
        }

        Assert.Equal(typeof(AdapterType), entity.FindProperty(nameof(LlmProvider.AdapterType))?.ClrType);
        Assert.Equal(typeof(ProviderType), entity.FindProperty(nameof(LlmProvider.ProviderType))?.ClrType);
        Assert.Equal(typeof(ModelTier), entity.FindProperty(nameof(LlmProvider.Tier))?.ClrType);
        Assert.Null(entity.FindProperty("Models"));
    }

    [Fact]
    public void FindEntityType_TokenUsageRecord_HasUsageBreakdownProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(TokenUsageRecord));
        Assert.NotNull(entity);

        AssertProperties(entity, "Feature", "ProviderModelConfigId", "ProviderId", "ModelId", "ModelName", "InputTokens", "OutputTokens", "CachedTokens", "PipelineRunId", "Cost", "CreatedAt");

        Assert.Equal(typeof(AiFeature), entity.FindProperty(nameof(TokenUsageRecord.Feature))?.ClrType);
    }

    [Fact]
    public void FindEntityTypes_UserAndBudgetSchema_MatchesFoundationRequirements()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var user = model.FindEntityType(typeof(User));
        Assert.NotNull(user);
        AssertProperties(user, "Name", "LastName", "IsAdmin", "LastLoginAt");

        var budget = model.FindEntityType(typeof(UserTokenBudget));
        Assert.NotNull(budget);
        AssertProperties(budget, "DailyLimit", "MonthlyLimit");
    }

    [Fact]
    public void FindEntityTypes_ProjectBoardSchema_MatchesFoundationRequirements()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var project = model.FindEntityType(typeof(Project));
        Assert.NotNull(project);
        AssertProperties(project, "GitRemoteUrl", "GitProvider");

        var column = model.FindEntityType(typeof(Column));
        Assert.NotNull(column);
        AssertProperties(column, "Position", "WipLimit", "Color");

        var card = model.FindEntityType(typeof(Card));
        Assert.NotNull(card);
        AssertProperties(card, "ParentCardId", "SpecId", "PlanId", "Type", "Position", "DueAt", "MovedAt", "ArchivedAt");
        Assert.Equal(typeof(int), card.FindProperty("CardNumber")?.ClrType);
        Assert.Equal(typeof(CardType), card.FindProperty("Type")?.ClrType);

        var assignee = model.FindEntityType(typeof(CardAssignee));
        Assert.NotNull(assignee);
        AssertProperties(assignee, "AssignedByUserId");

        var relationship = model.FindEntityType(typeof(CardRelationship));
        Assert.NotNull(relationship);
        AssertProperties(relationship, "Type", "CreatedByUserId", "ArchivedAt");

        var checklistItem = model.FindEntityType(typeof(ChecklistItem));
        Assert.NotNull(checklistItem);
        AssertProperties(checklistItem, "Position", "AssignedTo");

        var attachment = model.FindEntityType(typeof(Attachment));
        Assert.NotNull(attachment);
        AssertProperties(attachment, "Size", "UploadedByUserId");

        Assert.NotNull(model.FindEntityType(typeof(CardWatcher)));
    }

    [Fact]
    public void FindEntityTypes_ProjectDocsAndSnapshotSchema_MatchesFoundationRequirements()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var spec = model.FindEntityType(typeof(Spec));
        Assert.NotNull(spec);
        AssertProperties(spec, "Title", "Content", "Version", "CreatedByUserId");

        var plan = model.FindEntityType(typeof(Plan));
        Assert.NotNull(plan);
        AssertProperties(plan, "Title", "Content", "Version", "CreatedByUserId");

        var specVersion = model.FindEntityType(typeof(SpecVersion));
        Assert.NotNull(specVersion);
        AssertProperties(specVersion, "CreatedByUserId");

        var planVersion = model.FindEntityType(typeof(PlanVersion));
        Assert.NotNull(planVersion);
        AssertProperties(planVersion, "CreatedByUserId");

        var snapshot = model.FindEntityType(typeof(ProjectContextSnapshot));
        Assert.NotNull(snapshot);
        AssertProperties(snapshot, "AiNarrative", "TemplateGeneratedAt", "AiNarrativeGeneratedAt");
    }

    [Fact]
    public void FindEntityTypes_ChatSchema_MatchesFoundationRequirements()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var folder = model.FindEntityType(typeof(ChatFolder));
        Assert.NotNull(folder);
        AssertProperties(folder, "ParentFolderId", "ProjectId", "ArchivedAt");

        var session = model.FindEntityType(typeof(ChatSession));
        Assert.NotNull(session);
        AssertProperties(session, "ProjectId", "IsShared", "UpdatedAt", "ArchivedAt");

        var message = model.FindEntityType(typeof(ChatMessage));
        Assert.NotNull(message);
        AssertProperties(message, "InputTokens", "OutputTokens", "CachedTokens", "ModelName");

        var cardChatLink = model.FindEntityType(typeof(CardChatLink));
        Assert.NotNull(cardChatLink);
        AssertProperties(cardChatLink, "OwnerId", "Summary", "ArchivedAt");
    }

    [Fact]
    public void FindEntityTypes_PersonalSchema_MatchesFoundationRequirements()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var agentPersonality = model.FindEntityType(typeof(AgentPersonality));
        Assert.NotNull(agentPersonality);
        AssertProperties(agentPersonality, "Description", "SystemPrompt", "IsDefault", "CreatedAt", "UpdatedAt", "ArchivedAt");

        var notification = model.FindEntityType(typeof(Notification));
        Assert.NotNull(notification);
        AssertProperties(notification, "Message", "CardId", "ProjectId");

        var memoryEntry = model.FindEntityType(typeof(MemoryEntry));
        Assert.NotNull(memoryEntry);
        AssertProperties(memoryEntry, "IsPinned", "Source", "ArchivedAt");

        var note = model.FindEntityType(typeof(Note));
        Assert.NotNull(note);
        AssertProperties(note, "IsPinned", "ArchivedAt", "SortOrder");

        var reminder = model.FindEntityType(typeof(NoteReminder));
        Assert.NotNull(reminder);
        AssertProperties(reminder, "TriggerAt", "RepeatPattern", "LastTriggeredAt");

        var task = model.FindEntityType(typeof(PersonalTask));
        Assert.NotNull(task);
        AssertProperties(task, "CronExpression", "ArchivedAt");

        var calendarSource = model.FindEntityType(typeof(CalendarSource));
        Assert.NotNull(calendarSource);
        AssertProperties(calendarSource, "CalDavUrl", "ArchivedAt");

        var calendarEvent = model.FindEntityType(typeof(CalendarEvent));
        Assert.NotNull(calendarEvent);
        AssertProperties(calendarEvent, "RecurrenceRule", "ArchivedAt");
    }

    [Fact]
    public void FindEntityTypes_DocumentAndGallerySchema_MatchesFoundationRequirements()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var document = model.FindEntityType(typeof(Document));
        Assert.NotNull(document);
        AssertProperties(document, "Content", "ContentType", "FilePath", "Language", "Version", "ArchivedAt");

        var version = model.FindEntityType(typeof(DocumentVersion));
        Assert.NotNull(version);
        AssertProperties(version, "CreatedByUserId");

        var chunk = model.FindEntityType(typeof(DocumentChunk));
        Assert.NotNull(chunk);
        AssertProperties(chunk, "UserId", "SourceType", "SourceId");

        var image = model.FindEntityType(typeof(GalleryImage));
        Assert.NotNull(image);
        AssertProperties(image, "FilePath", "OriginalFilename", "ContentType", "Size", "Width", "Height", "Hash", "TakenAt", "CameraModel", "Latitude", "Longitude", "IsFavorite", "UpdatedAt", "ArchivedAt");

        var album = model.FindEntityType(typeof(Album));
        Assert.NotNull(album);
        AssertProperties(album, "CoverImageId", "Description", "UpdatedAt", "ArchivedAt");

        var albumImage = model.FindEntityType(typeof(AlbumImage));
        Assert.NotNull(albumImage);
        AssertProperties(albumImage, "Position", "CreatedAt", "ArchivedAt");
    }

    [Fact]
    public void FindEntityTypes_ArchiveAndHousekeepingSchema_MatchesFoundationRequirements()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var card = model.FindEntityType(typeof(Card));
        Assert.NotNull(card);
        AssertProperties(card, "ArchivedAt");

        var document = model.FindEntityType(typeof(Document));
        Assert.NotNull(document);
        AssertProperties(document, "ArchivedAt");
        Assert.Null(document.FindProperty("IsArchived"));

        var note = model.FindEntityType(typeof(Note));
        Assert.NotNull(note);
        AssertProperties(note, "ArchivedAt");
        Assert.Null(note.FindProperty("IsArchived"));

        var memoryEntry = model.FindEntityType(typeof(MemoryEntry));
        Assert.NotNull(memoryEntry);
        AssertProperties(memoryEntry, "ArchivedAt");

        var calendarEvent = model.FindEntityType(typeof(CalendarEvent));
        Assert.NotNull(calendarEvent);
        AssertProperties(calendarEvent, "ArchivedAt");

        var calendarSource = model.FindEntityType(typeof(CalendarSource));
        Assert.NotNull(calendarSource);
        AssertProperties(calendarSource, "ArchivedAt");

        var personalTask = model.FindEntityType(typeof(PersonalTask));
        Assert.NotNull(personalTask);
        AssertProperties(personalTask, "ArchivedAt");

        var cardChatLink = model.FindEntityType(typeof(CardChatLink));
        Assert.NotNull(cardChatLink);
        AssertProperties(cardChatLink, "ArchivedAt");

        var comment = model.FindEntityType(typeof(Comment));
        Assert.NotNull(comment);
        AssertProperties(comment, "ArchivedAt");

        var systemSettings = model.FindEntityType(typeof(SystemSettings));
        Assert.NotNull(systemSettings);
        AssertProperties(systemSettings, "ArchivedItemRetentionDays", "AuditLogRetentionDays", "NotificationRetentionDays");
    }

    [Fact]
    public void GetIndexes_UsernameNormalized_IsUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var userEntity = model.FindEntityType(typeof(User));
        Assert.NotNull(userEntity);

        var usernameNormalizedIndex = userEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "UsernameNormalized"));

        Assert.NotNull(usernameNormalizedIndex);
        Assert.True(usernameNormalizedIndex.IsUnique);
    }

    [Fact]
    public void GetIndexes_CardProjectIdCardNumber_IsUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var cardEntity = model.FindEntityType(typeof(Card));
        Assert.NotNull(cardEntity);

        var compositeIndex = cardEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "ProjectId") &&
                                  i.Properties.Any(p => p.Name == "CardNumber"));

        Assert.NotNull(compositeIndex);
        Assert.True(compositeIndex.IsUnique);
    }

    [Fact]
    public void GetIndexes_ProjectContextSnapshot_ProjectId_IsUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(ProjectContextSnapshot));
        Assert.NotNull(entity);

        var projectIdIndex = entity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "ProjectId"));

        Assert.NotNull(projectIdIndex);
        Assert.True(projectIdIndex.IsUnique);
    }
}
