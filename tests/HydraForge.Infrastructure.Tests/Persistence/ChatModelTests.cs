namespace HydraForge.Infrastructure.Tests.Persistence;

using System.Linq;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class ChatModelTests
{
    private static void AssertProperties(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity,
        params string[] propertyNames
    )
    {
        foreach (var propName in propertyNames)
        {
            Assert.True(
                entity.GetProperties().Any(p => p.Name == propName),
                $"{entity.ClrType.Name} missing property: {propName}"
            );
        }
    }

    private static DbContextOptions<HydraForgeDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password",
                o => o.UseVector()
            )
            .Options;
    }

    [Fact]
    public void FindEntityType_ChatSessionDocument_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        Assert.NotNull(model.FindEntityType(typeof(ChatSessionDocument)));
    }

    [Fact]
    public void FindEntityType_PromptPresetGroup_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        Assert.NotNull(model.FindEntityType(typeof(PromptPresetGroup)));
    }

    [Fact]
    public void FindEntityType_PromptPreset_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;
        Assert.NotNull(model.FindEntityType(typeof(PromptPreset)));
    }

    [Fact]
    public void GetTableName_ChatSessionDocument_IsChatSessionDocuments()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatSessionDocument));
        Assert.NotNull(entity);
        Assert.Equal("chat_session_documents", entity.GetTableName());
    }

    [Fact]
    public void GetTableName_PromptPresetGroup_IsPromptPresetGroups()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(PromptPresetGroup));
        Assert.NotNull(entity);
        Assert.Equal("prompt_preset_groups", entity.GetTableName());
    }

    [Fact]
    public void GetTableName_PromptPreset_IsPromptPresets()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(PromptPreset));
        Assert.NotNull(entity);
        Assert.Equal("prompt_presets", entity.GetTableName());
    }

    [Fact]
    public void FindEntityType_ChatSessionDocument_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatSessionDocument));
        Assert.NotNull(entity);
        AssertProperties(entity, "Id", "SessionId", "DocumentId", "AddedByUserId", "AddedAt");
    }

    [Fact]
    public void FindEntityType_PromptPresetGroup_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(PromptPresetGroup));
        Assert.NotNull(entity);
        AssertProperties(entity, "Id", "UserId", "Name", "CreatedAt", "UpdatedAt", "ArchivedAt");
    }

    [Fact]
    public void FindEntityType_PromptPreset_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(PromptPreset));
        Assert.NotNull(entity);
        AssertProperties(
            entity,
            "Id",
            "UserId",
            "GroupId",
            "Name",
            "Content",
            "CreatedAt",
            "UpdatedAt",
            "ArchivedAt"
        );
    }

    [Fact]
    public void FindEntityType_ChatSession_HasEnumProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatSession));
        Assert.NotNull(entity);

        var status = entity.FindProperty(nameof(ChatSession.Status));
        Assert.NotNull(status);
        Assert.Equal(typeof(ChatSessionStatus), status.ClrType);
        Assert.Equal(ChatSessionStatus.Active, status.GetDefaultValue());

        var aiEditMode = entity.FindProperty(nameof(ChatSession.AiEditMode));
        Assert.NotNull(aiEditMode);
        Assert.Equal(typeof(AiEditMode), aiEditMode.ClrType);
        Assert.Equal(AiEditMode.PerMutation, aiEditMode.GetDefaultValue());
    }

    [Fact]
    public void FindEntityType_ChatSession_HasPersonalityIdFk_SetNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatSession));
        Assert.NotNull(entity);

        var fk = entity
            .GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Any(p => p.Name == "PersonalityId"));
        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.SetNull, fk.DeleteBehavior);
        Assert.Contains(fk.Properties, p => p.Name == "PersonalityId");
    }

    [Fact]
    public void FindEntityType_ChatMessage_ImagesJson_IsText()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatMessage));
        Assert.NotNull(entity);

        var prop = entity.GetProperties().FirstOrDefault(p => p.Name == "ImagesJson");
        Assert.NotNull(prop);
        Assert.Equal("text", prop.GetColumnType());
    }

    [Fact]
    public void FindEntityType_ChatSession_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatSession));
        Assert.NotNull(entity);
        AssertProperties(
            entity,
            "Id",
            "FolderId",
            "OwnerId",
            "ProjectId",
            "Title",
            "IsShared",
            "CreatedAt",
            "UpdatedAt",
            "ArchivedAt",
            "Status",
            "AiEditMode",
            "SearchAllMyDocs",
            "PersonalityId",
            "OpenCardId",
            "ClosedAt",
            "Summary"
        );
    }

    [Fact]
    public void FindEntityType_ChatMessage_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatMessage));
        Assert.NotNull(entity);
        AssertProperties(
            entity,
            "Id",
            "SessionId",
            "Role",
            "Content",
            "InputTokens",
            "OutputTokens",
            "CachedTokens",
            "ModelName",
            "ImagesJson",
            "CreatedAt"
        );
    }

    [Fact]
    public void GetIndexes_ChatSessionDocument_SessionIdDocumentId_IsUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatSessionDocument));
        Assert.NotNull(entity);

        var index = entity
            .GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == "SessionId")
                && i.Properties.Any(p => p.Name == "DocumentId")
            );

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void GetForeignKeys_ChatSessionDocument_SessionId_CascadeDelete()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(ChatSessionDocument));
        Assert.NotNull(entity);

        var fk = entity
            .GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Any(p => p.Name == "SessionId"));
        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void GetIndexes_PromptPresetGroup_UserId_IsConfigured()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(PromptPresetGroup));
        Assert.NotNull(entity);

        var index = entity
            .GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "UserId"));
        Assert.NotNull(index);
    }

    [Fact]
    public void GetIndexes_PromptPreset_UserId_IsConfigured()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(PromptPreset));
        Assert.NotNull(entity);

        var index = entity
            .GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "UserId"));
        Assert.NotNull(index);
    }

    [Fact]
    public void GetForeignKeys_PromptPreset_GroupId_CascadeDelete()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var entity = context.Model.FindEntityType(typeof(PromptPreset));
        Assert.NotNull(entity);

        var fk = entity
            .GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Any(p => p.Name == "GroupId"));
        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }
}
