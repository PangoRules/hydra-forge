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
        var existingFeatures = await _db
            .FeatureRoutingConfigs.Select(c => c.Feature)
            .ToListAsync(ct);

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
            new FeatureRoutingConfig
            {
                Feature = AiFeature.ProjectNarrative,
                DefaultTier = ModelTier.Economy,
                MaxUserTier = null,
            },
        };

        var missing = configs.Where(c => !existingFeatures.Contains(c.Feature)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        _db.FeatureRoutingConfigs.AddRange(missing);
        await _db.SaveChangesAsync(ct);
    }
}
