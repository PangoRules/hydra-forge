using HydraForge.Application.Cards;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HydraForge.Application.ProjectSnapshots;

public class ProjectContextSnapshotService(
    IColumnRepository columnRepo,
    ICardRepository cardRepo,
    ICardRelationshipRepository relationshipRepo,
    IProjectContextSnapshotRepository snapshotRepo,
    IProjectRepository projectRepo,
    IModelRouter modelRouter,
    ILlmClientFactory llmClientFactory,
    IUsageRecorder usageRecorder,
    ILogger<ProjectContextSnapshotService> logger
) : IProjectSnapshotRefresher
{
    // Batch job — no user context. ModelRouter.ResolveAsync does not currently use
    // the userId parameter (TODO in ModelRouter applies user-specific tier ceilings).
    // When that TODO is addressed, this batch context must provide a system/admin
    // identity or routing must support nullable/non-user invocations.
    private static readonly Guid BatchJobUserId = Guid.Empty;

    public async Task GenerateAiNarrativeForAllActiveProjectsAsync(CancellationToken ct = default)
    {
        var page = await projectRepo.ListAllAsync(
            includeArchived: false,
            search: null,
            ProjectSortField.CreatedAt,
            sortDescending: false,
            skip: 0,
            take: int.MaxValue,
            ct
        );

        if (page.Items.Count == 0)
            return;

        // Bulk-fetch all snapshots in one DB round-trip instead of N individual calls.
        var projectIds = page.Items.Select(p => p.Id).ToList();
        var snapshots = await snapshotRepo.GetByProjectIdsAsync(projectIds, ct);
        var snapshotByProjectId = snapshots.ToDictionary(s => s.ProjectId);

        // Phase 1 (sequential — HydraForgeDbContext is scoped to this job and is not
        // thread-safe, so every DB-backed call, including ModelRouter's routing-config
        // lookups, must run one at a time here rather than inside the parallel phase below).
        var narrativeJobs = new List<NarrativeJob>();
        foreach (var project in page.Items)
        {
            if (!snapshotByProjectId.TryGetValue(project.Id, out var snapshot))
                continue;

            var routeResult = await modelRouter.ResolveAsync(
                AiFeature.ProjectNarrative,
                BatchJobUserId,
                project.Id,
                estimatedTokens: 4000,
                ct
            );

            if (routeResult.IsFailure)
            {
                logger.LogWarning(
                    "Failed to resolve model for project {ProjectId}: {Error}",
                    project.Id,
                    routeResult.Error.Message
                );
                continue;
            }

            narrativeJobs.Add(new NarrativeJob(project.Id, snapshot, routeResult.Value));
        }

        if (narrativeJobs.Count == 0)
            return;

        // Phase 2 (parallel — pure network calls to LLM providers, no shared DbContext
        // access, so concurrent dispatch here is safe).
        var updatedSnapshots = new List<ProjectContextSnapshot>();
        var pendingUsageRecords = new List<TokenUsageRecordInput>();

        await Parallel.ForEachAsync(
            narrativeJobs,
            new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct },
            async (job, innerCt) =>
            {
                var (projectId, snapshot, route) = job;

                try
                {
                    var client = llmClientFactory.For(route.Provider!);

                    var systemPrompt =
                        "Generate a concise project narrative (3-5 sentences) based on the following project snapshot. "
                        + "The narrative should describe the current state of the project, key themes, and notable work items. "
                        + "Be descriptive but succinct.";

                    var request = new ChatRequest(
                        route.Primary.Id,
                        route.Primary.ModelId,
                        Messages: [new ChatMessage(ChatRole.User, systemPrompt)],
                        CacheBlocks:
                        [
                            new CacheBlock(
                                snapshot.TemplateContent,
                                CacheBlockType.ProjectSnapshot
                            ),
                        ],
                        Tools: [],
                        MaxOutputTokens: 500,
                        Temperature: 0.3m
                    );

                    var fullResponse = new List<string>();
                    UsageSnapshot? usage = null;
                    await foreach (var chunk in client.StreamChatAsync(request, innerCt))
                    {
                        if (chunk.Delta is not null)
                            fullResponse.Add(chunk.Delta);
                        if (chunk.Usage is not null)
                            usage = chunk.Usage;
                    }

                    var narrative = string.Join("", fullResponse);
                    if (fullResponse.Count == 0)
                    {
                        logger.LogWarning(
                            "Empty AI narrative response for project {ProjectId} — skipping update",
                            projectId
                        );
                        return;
                    }

                    snapshot.AiNarrative = narrative;
                    snapshot.AiNarrativeGeneratedAt = DateTime.UtcNow;

                    lock (updatedSnapshots)
                    {
                        updatedSnapshots.Add(snapshot);
                    }

                    lock (pendingUsageRecords)
                    {
                        pendingUsageRecords.Add(
                            new TokenUsageRecordInput(
                                UserId: BatchJobUserId,
                                ProjectId: projectId,
                                Feature: AiFeature.ProjectNarrative,
                                ProviderModelConfigId: route.Primary.Id,
                                ProviderId: route.Provider!.Id,
                                ModelId: route.Primary.ModelId,
                                ModelName: route.Primary.Name,
                                InputTokens: usage?.InputTokens ?? 0,
                                OutputTokens: usage?.OutputTokens ?? 0,
                                CachedTokens: usage?.CachedTokens ?? 0,
                                PipelineRunId: null,
                                Cost: 0
                            )
                        );
                    }
                }
                // Outer cancellation (the job's own ct) must propagate so the batch stops;
                // anything else (a per-iteration hiccup) is caught below and logged instead.
                catch (OperationCanceledException) when (innerCt.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    // Not caused by outer cancellation — treat as a per-project failure.
                    logger.LogWarning(
                        "AI narrative generation for project {ProjectId} was cancelled unexpectedly",
                        projectId
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to generate AI narrative for project {ProjectId}",
                        projectId
                    );
                }
            }
        );

        // Bulk-write all updated snapshots in one DB round-trip.
        if (updatedSnapshots.Count > 0)
        {
            await snapshotRepo.UpdateRangeAsync(updatedSnapshots, ct);
        }

        // Record usage for all completed generations.
        foreach (var record in pendingUsageRecords)
        {
            await usageRecorder.RecordTokenAsync(record, ct);
        }
    }

    public async Task RefreshAsync(Guid projectId, CancellationToken ct = default)
    {
        var columns = await columnRepo.GetByProjectIdAsync(projectId, ct);
        var activeCards = await cardRepo.ListByProjectAsync(
            projectId,
            new CardListFilter(IncludeArchived: false),
            ct
        );
        var activeRelationships = await relationshipRepo.ListActiveByProjectAsync(projectId, ct);

        var templateContent = ProjectContextSnapshotRenderer.Render(
            columns,
            activeCards,
            activeRelationships
        );

        var existing = await snapshotRepo.GetByProjectIdAsync(projectId, ct);

        if (existing != null)
        {
            existing.TemplateContent = templateContent;
            existing.TemplateGeneratedAt = DateTime.UtcNow;
            await snapshotRepo.UpdateAsync(existing, ct);
        }
        else
        {
            var snapshot = new ProjectContextSnapshot
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                TemplateContent = templateContent,
                TemplateGeneratedAt = DateTime.UtcNow,
            };
            await snapshotRepo.AddAsync(snapshot, ct);
        }
    }

    Task<ProjectContextSnapshot?> IProjectSnapshotRefresher.GetSnapshotAsync(
        Guid projectId,
        CancellationToken ct
    )
    {
        return snapshotRepo.GetByProjectIdAsync(projectId, ct);
    }

    private sealed record NarrativeJob(
        Guid ProjectId,
        ProjectContextSnapshot Snapshot,
        RouteDecision Route
    );
}
