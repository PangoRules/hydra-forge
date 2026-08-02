using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HydraForge.Application.Tests.ProjectSnapshots;

public class GenerateAiNarrativeTests
{
    [Fact]
    public async Task ActiveProjectsWithSnapshotsGetNarrativeGenerated()
    {
        var projectId = Guid.NewGuid();
        var projectRepo = new InMemoryProjectRepository();
        projectRepo.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var snapshotRepo = new InMemorySnapshotRepository();
        var snapshot = new ProjectContextSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TemplateContent = "{}",
            TemplateGeneratedAt = DateTime.UtcNow,
        };
        snapshotRepo.AddSnapshot(snapshot);

        var modelRouter = Substitute.For<IModelRouter>();
        var llmClientFactory = Substitute.For<ILlmClientFactory>();

        var providerId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var provider = new LlmProvider
        {
            Id = providerId,
            AdapterType = AdapterType.OpenAiCompatible,
        };
        var primaryConfig = new ProviderModelConfigDto(
            Id: configId,
            ProviderId: providerId,
            ModelId: "gpt-4o-mini",
            Name: "GPT-4o Mini",
            Tier: "Standard",
            PricePerToken: 0.00015m,
            MaxTokens: 5000,
            IsEnabled: true
        );

        modelRouter
            .ResolveAsync(AiFeature.ProjectNarrative, Guid.Empty, projectId, 4000, Arg.Any<CancellationToken>())
            .Returns(
                Result<RouteDecision>.Success(new RouteDecision(primaryConfig, null!, [], provider))
            );

        llmClientFactory.For(provider).Returns(new FakeLlmClient("This is the generated narrative."));
        var usageRecorder = Substitute.For<IUsageRecorder>();

        var service = new ProjectContextSnapshotService(
            Substitute.For<IColumnRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRelationshipRepository>(),
            snapshotRepo,
            projectRepo,
            modelRouter,
            llmClientFactory,
            usageRecorder,
            Substitute.For<ILogger<ProjectContextSnapshotService>>()
        );

        await service.GenerateAiNarrativeForAllActiveProjectsAsync();

        Assert.Single(snapshotRepo.UpdatedSnapshots);
        Assert.Equal(
            "This is the generated narrative.",
            snapshotRepo.UpdatedSnapshots[0].AiNarrative
        );
        Assert.NotNull(snapshotRepo.UpdatedSnapshots[0].AiNarrativeGeneratedAt);
        await usageRecorder
            .Received(1)
            .RecordTokenAsync(
                Arg.Is<TokenUsageRecordInput>(i =>
                    i.ProjectId == projectId && i.Feature == AiFeature.ProjectNarrative
                ),
                Arg.Any<CancellationToken>()
            );
        Assert.True(
            DateTime.UtcNow - snapshotRepo.UpdatedSnapshots[0].AiNarrativeGeneratedAt!.Value
                < TimeSpan.FromSeconds(5)
        );
    }

    [Fact]
    public async Task ArchivedProjectsAreSkipped()
    {
        var projectId = Guid.NewGuid();
        var projectRepo = new InMemoryProjectRepository();
        projectRepo.AddProject(
            new Project
            {
                Id = projectId,
                Name = "Archived Project",
                ArchivedAt = DateTime.UtcNow.AddDays(-1),
            }
        );

        var snapshotRepo = new InMemorySnapshotRepository();
        var snapshot = new ProjectContextSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TemplateContent = "{}",
            TemplateGeneratedAt = DateTime.UtcNow,
        };
        snapshotRepo.AddSnapshot(snapshot);

        var service = new ProjectContextSnapshotService(
            Substitute.For<IColumnRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRelationshipRepository>(),
            snapshotRepo,
            projectRepo,
            Substitute.For<IModelRouter>(),
            Substitute.For<ILlmClientFactory>(),
            Substitute.For<IUsageRecorder>(),
            Substitute.For<ILogger<ProjectContextSnapshotService>>()
        );

        await service.GenerateAiNarrativeForAllActiveProjectsAsync();

        Assert.Empty(snapshotRepo.UpdatedSnapshots);
        Assert.Null(snapshot.AiNarrative);
    }

    [Fact]
    public async Task PerProjectFailureDoesNotAbortBatch()
    {
        var projectId1 = Guid.NewGuid();
        var projectId2 = Guid.NewGuid();

        var projectRepo = new InMemoryProjectRepository();
        projectRepo.AddProject(new Project { Id = projectId1, Name = "Project 1" });
        projectRepo.AddProject(new Project { Id = projectId2, Name = "Project 2" });

        var snapshotRepo = new InMemorySnapshotRepository();
        snapshotRepo.AddSnapshot(
            new ProjectContextSnapshot
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId1,
                TemplateContent = "{}",
            }
        );
        snapshotRepo.AddSnapshot(
            new ProjectContextSnapshot
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId2,
                TemplateContent = "{}",
            }
        );

        var modelRouter = Substitute.For<IModelRouter>();
        var llmClientFactory = Substitute.For<ILlmClientFactory>();

        var providerId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var provider = new LlmProvider
        {
            Id = providerId,
            AdapterType = AdapterType.OpenAiCompatible,
        };
        var primaryConfig = new ProviderModelConfigDto(
            configId,
            providerId,
            "gpt-4o-mini",
            "GPT-4o Mini",
            "Standard",
            0.00015m,
            5000,
            true
        );

        modelRouter
            .ResolveAsync(AiFeature.ProjectNarrative, Guid.Empty, Arg.Any<Guid?>(), 4000, Arg.Any<CancellationToken>())
            .Returns(
                Result<RouteDecision>.Success(new RouteDecision(primaryConfig, null!, [], provider))
            );

        llmClientFactory.For(provider).Returns(
            new FakeLlmClient(new InvalidOperationException("LLM failure")),
            new FakeLlmClient("Project 2 narrative."));

        var service = new ProjectContextSnapshotService(
            Substitute.For<IColumnRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRelationshipRepository>(),
            snapshotRepo,
            projectRepo,
            modelRouter,
            llmClientFactory,
            Substitute.For<IUsageRecorder>(),
            Substitute.For<ILogger<ProjectContextSnapshotService>>()
        );

        await service.GenerateAiNarrativeForAllActiveProjectsAsync();

        var updatedSnapshots = snapshotRepo.UpdatedSnapshots;
        Assert.Single(updatedSnapshots);
        Assert.Equal(projectId2, updatedSnapshots[0].ProjectId);
        Assert.Equal("Project 2 narrative.", updatedSnapshots[0].AiNarrative);
    }

    [Fact]
    public async Task AiNarrativeGeneratedAtIsSetOnSuccess()
    {
        var projectId = Guid.NewGuid();
        var projectRepo = new InMemoryProjectRepository();
        projectRepo.AddProject(new Project { Id = projectId, Name = "Test Project" });

        var snapshotRepo = new InMemorySnapshotRepository();
        var snapshot = new ProjectContextSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TemplateContent = "{}",
            TemplateGeneratedAt = DateTime.UtcNow,
        };
        snapshotRepo.AddSnapshot(snapshot);

        var modelRouter = Substitute.For<IModelRouter>();
        var llmClientFactory = Substitute.For<ILlmClientFactory>();

        var providerId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var provider = new LlmProvider
        {
            Id = providerId,
            AdapterType = AdapterType.OpenAiCompatible,
        };
        var primaryConfig = new ProviderModelConfigDto(
            Id: configId,
            ProviderId: providerId,
            ModelId: "gpt-4o-mini",
            Name: "GPT-4o Mini",
            Tier: "Standard",
            PricePerToken: 0.00015m,
            MaxTokens: 5000,
            IsEnabled: true
        );

        modelRouter
            .ResolveAsync(AiFeature.ProjectNarrative, Guid.Empty, projectId, 4000, Arg.Any<CancellationToken>())
            .Returns(
                Result<RouteDecision>.Success(new RouteDecision(primaryConfig, null!, [], provider))
            );

        llmClientFactory.For(provider).Returns(new FakeLlmClient("Narrative."));

        var service = new ProjectContextSnapshotService(
            Substitute.For<IColumnRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRepository>(),
            Substitute.For<HydraForge.Application.Cards.ICardRelationshipRepository>(),
            snapshotRepo,
            projectRepo,
            modelRouter,
            llmClientFactory,
            Substitute.For<IUsageRecorder>(),
            Substitute.For<ILogger<ProjectContextSnapshotService>>()
        );

        var before = DateTime.UtcNow;
        await service.GenerateAiNarrativeForAllActiveProjectsAsync();
        var after = DateTime.UtcNow;

        Assert.Single(snapshotRepo.UpdatedSnapshots);
        var generatedAt = snapshotRepo.UpdatedSnapshots[0].AiNarrativeGeneratedAt;
        Assert.NotNull(generatedAt);
        Assert.True(before <= generatedAt.Value && generatedAt.Value <= after);
    }

    private static async IAsyncEnumerable<ChatChunk> FakeStream(string text)
    {
        yield return new ChatChunk(
            Delta: text,
            FinishReason: ChatChunkFinishReason.Stop,
            Usage: null
        );
        await Task.CompletedTask;
    }

    private static IAsyncEnumerable<ChatChunk> ThrowingStream(Exception ex) =>
        new ThrowingAsyncEnumerable(ex);

    private sealed class FakeAsyncEnumerable : IAsyncEnumerable<ChatChunk>
    {
        private readonly string _text;

        public FakeAsyncEnumerable(string text) => _text = text;

        public IAsyncEnumerator<ChatChunk> GetAsyncEnumerator(CancellationToken _) =>
            new FakeAsyncEnumerator(_text);
    }

    private sealed class FakeAsyncEnumerator : IAsyncEnumerator<ChatChunk>
    {
        private readonly string _text;
        private bool _moved;

        public FakeAsyncEnumerator(string text) => _text = text;

        public ChatChunk Current => _moved
            ? new ChatChunk(Delta: _text, FinishReason: ChatChunkFinishReason.Stop, Usage: null)
            : default;

        public ValueTask<bool> MoveNextAsync()
        {
            if (_moved) return new ValueTask<bool>(false);
            _moved = true;
            return new ValueTask<bool>(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingAsyncEnumerable : IAsyncEnumerable<ChatChunk>
    {
        private readonly Exception _ex;

        public ThrowingAsyncEnumerable(Exception ex) => _ex = ex;

        public IAsyncEnumerator<ChatChunk> GetAsyncEnumerator(CancellationToken _) =>
            new ThrowingAsyncEnumerator(_ex);
    }

    // Concrete test double that wraps FakeAsyncEnumerable for use as ILlmClient.
    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly string? _narrative;
        private readonly Exception? _exception;

        public AdapterType AdapterType => AdapterType.OpenAiCompatible;

        public FakeLlmClient(string? narrative) => _narrative = narrative;
        public FakeLlmClient(Exception ex) => _exception = ex;

        public IAsyncEnumerable<ChatChunk> StreamChatAsync(
            ChatRequest request,
            CancellationToken ct = default
        )
        {
            if (_exception is not null)
                return new ThrowingAsyncEnumerable(_exception);
            return new FakeAsyncEnumerable(_narrative!);
        }

        public Task<Result<IReadOnlyList<ProviderModelDto>>> GetModelsAsync(CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<ProviderModelDto>>.Success(
                Array.Empty<ProviderModelDto>()));

        public bool SupportsToolCalling(ProviderModelConfigDto model) => false;
    }

    private sealed class ThrowingAsyncEnumerator : IAsyncEnumerator<ChatChunk>
    {
        private readonly Exception _ex;

        public ThrowingAsyncEnumerator(Exception ex) => _ex = ex;

        public ChatChunk Current => default!;

        public ValueTask<bool> MoveNextAsync() => new(Task.FromException<bool>(_ex));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
