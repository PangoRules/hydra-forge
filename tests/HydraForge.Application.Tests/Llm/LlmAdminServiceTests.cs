namespace HydraForge.Application.Tests.Llm;

using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using NSubstitute;

public class LlmAdminServiceTests
{
    private static LlmAdminService CreateService(
        ILlmAdminRepository repo,
        ILlmClientFactory? factory = null
    ) => new(repo, Substitute.For<IKeyVault>(), factory ?? Substitute.For<ILlmClientFactory>());

    [Fact]
    public async Task CreateModelAsync_DuplicateModelId_ReturnsModelAlreadyExists()
    {
        var providerId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetProviderByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(new LlmProvider { Id = providerId, Name = "Local Ollama" });
        repo.ListModelConfigsAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(
                new List<ProviderModelConfig>
                {
                    new()
                    {
                        ProviderId = providerId,
                        ModelId = "qwen3-coder:latest",
                        Name = "qwen3-coder:latest",
                    },
                }
            );

        var service = CreateService(repo);
        var input = new CreateModelInput("qwen3-coder:latest", "dup", "Standard", null, null, true);

        var result = await service.CreateModelAsync(providerId, input, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.ModelAlreadyExists, result.Error.Code);
        await repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateModelAsync_NewModelId_Succeeds()
    {
        var providerId = Guid.NewGuid();
        var repo = Substitute.For<ILlmAdminRepository>();
        repo.GetProviderByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(new LlmProvider { Id = providerId, Name = "Local Ollama" });
        repo.ListModelConfigsAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(new List<ProviderModelConfig>());

        var service = CreateService(repo);
        var input = new CreateModelInput("qwen3-coder:latest", "New", "Standard", null, null, true);

        var result = await service.CreateModelAsync(providerId, input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("qwen3-coder:latest", result.Value.ModelId);
        repo.Received(1).AddModelConfig(Arg.Any<ProviderModelConfig>());
    }
}
