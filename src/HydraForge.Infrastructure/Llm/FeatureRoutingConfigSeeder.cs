namespace HydraForge.Infrastructure.Llm;

using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class FeatureRoutingConfigSeeder
{
    private readonly HydraForgeDbContext _db;

    public FeatureRoutingConfigSeeder(HydraForgeDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.FeatureRoutingConfigs.AnyAsync(ct))
        {
            return;
        }

        var configs = new[]
        {
            new FeatureRoutingConfig
            {
                Feature = AiFeature.PersonalChat,
                DefaultTier = ModelTier.Standard,
                MaxUserTier = ModelTier.Premium,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.ProjectChat,
                DefaultTier = ModelTier.Standard,
                MaxUserTier = ModelTier.Premium,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.DeepResearch,
                DefaultTier = ModelTier.Premium,
                MaxUserTier = null,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.AgentPipeline,
                DefaultTier = ModelTier.Premium,
                MaxUserTier = null,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.MemoryExtraction,
                DefaultTier = ModelTier.Economy,
                MaxUserTier = ModelTier.Standard,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.NotesClassification,
                DefaultTier = ModelTier.Economy,
                MaxUserTier = ModelTier.Standard,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.DocumentEditing,
                DefaultTier = ModelTier.Standard,
                MaxUserTier = ModelTier.Premium,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.CardReview,
                DefaultTier = ModelTier.Standard,
                MaxUserTier = ModelTier.Premium,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.ImageChat,
                DefaultTier = ModelTier.Standard,
                MaxUserTier = ModelTier.Premium,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.ImageDocument,
                DefaultTier = ModelTier.Standard,
                MaxUserTier = ModelTier.Premium,
            },
            new FeatureRoutingConfig
            {
                Feature = AiFeature.ImageGalleryEditor,
                DefaultTier = ModelTier.Economy,
                MaxUserTier = ModelTier.Standard,
            },
        };

        _db.FeatureRoutingConfigs.AddRange(configs);
        await _db.SaveChangesAsync(ct);
    }
}
