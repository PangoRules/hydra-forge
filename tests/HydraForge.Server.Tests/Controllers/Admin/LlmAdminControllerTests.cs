namespace HydraForge.Server.Tests.Controllers.Admin;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydraForge.Application.Llm;
using HydraForge.Application.ProjectDocuments;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class LlmAdminControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static LlmAdminTestWebApplicationFactory CreateFactory(ILlmAdminService mock)
    {
        var factory = new LlmAdminTestWebApplicationFactory();
        factory.LlmAdmin = mock;
        return factory;
    }

    private static string AdminToken => LlmAdminTestWebApplicationFactory.IssueAdminToken();

    private static string NonAdminToken => LlmAdminTestWebApplicationFactory.IssueUserToken();

    [Fact]
    public async Task ListProviders_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.ListProvidersAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<ProviderPageDto>.Success(new ProviderPageDto([], 0)));

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync("api/admin/providers", CancellationToken.None);

        Assert.Equal(200, (int)response.StatusCode);
    }

    [Fact]
    public async Task CreateProvider_WithValidInput_ReturnsCreated()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var providerId = Guid.NewGuid();
        mock.CreateProviderAsync(Arg.Any<CreateProviderInput>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProviderDto>.Success(
                    new ProviderDto(
                        providerId,
                        "New Provider",
                        "https://api.new.com",
                        "OpenAiCompatible",
                        "Text",
                        "Standard",
                        null,
                        true,
                        DateTime.UtcNow,
                        DateTime.UtcNow
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new CreateProviderInput(
            "New Provider",
            "https://api.new.com",
            "sk-test-key",
            "OpenAiCompatible",
            "Text",
            "Standard",
            null
        );
        var response = await client.PostAsJsonAsync(
            "api/admin/providers",
            input,
            CancellationToken.None
        );

        Assert.Equal(201, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProviderDto>(CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal("New Provider", dto.Name);
    }

    [Fact]
    public async Task CreateProvider_WithMissingName_ReturnsBadRequest()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.CreateProviderAsync(Arg.Any<CreateProviderInput>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProviderDto>.Failure(new Error("PROVIDER_NAME_REQUIRED", "Name required"))
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new CreateProviderInput(
            "",
            "https://api.test.com",
            null,
            "OpenAiCompatible",
            "Text",
            "Standard",
            null
        );
        var response = await client.PostAsJsonAsync(
            "api/admin/providers",
            input,
            CancellationToken.None
        );

        Assert.Equal(400, (int)response.StatusCode);
    }

    [Fact]
    public async Task CreateProvider_WithoutAuth_Returns401()
    {
        using var factory = CreateFactory(Substitute.For<ILlmAdminService>());
        var client = factory.CreateClient();

        var input = new CreateProviderInput(
            "Test",
            "https://api.test.com",
            null,
            "OpenAiCompatible",
            "Text",
            "Standard",
            null
        );
        var response = await client.PostAsJsonAsync(
            "api/admin/providers",
            input,
            CancellationToken.None
        );

        Assert.Equal(401, (int)response.StatusCode);
    }

    [Fact]
    public async Task CreateProvider_AsNonAdmin_Returns403()
    {
        using var factory = CreateFactory(Substitute.For<ILlmAdminService>());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            NonAdminToken
        );

        var input = new CreateProviderInput(
            "Test",
            "https://api.test.com",
            null,
            "OpenAiCompatible",
            "Text",
            "Standard",
            null
        );
        var response = await client.PostAsJsonAsync(
            "api/admin/providers",
            input,
            CancellationToken.None
        );

        Assert.Equal(403, (int)response.StatusCode);
    }

    [Fact]
    public async Task ProbeModels_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.ProbeModelsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<IReadOnlyList<ProviderModelDto>>.Success(
                    new List<ProviderModelDto> { new("gpt-4o", "GPT-4o", null, null) }
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{Guid.NewGuid()}/models",
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dtos = await response.Content.ReadFromJsonAsync<IReadOnlyList<ProviderModelDto>>(
            CancellationToken.None
        );
        Assert.NotNull(dtos);
        Assert.Single(dtos);
        Assert.Equal("gpt-4o", dtos[0].ModelId);
    }

    [Fact]
    public async Task ProbeModels_ProviderUnavailable_ReturnsError()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.ProbeModelsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<IReadOnlyList<ProviderModelDto>>.Failure(
                    new Error("LLM_PROVIDER_UNAVAILABLE", "Failed to probe models")
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{Guid.NewGuid()}/models",
            CancellationToken.None
        );

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ListModels_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var providerId = Guid.NewGuid();
        mock.ListModelsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<IReadOnlyList<ProviderModelConfigDto>>.Success(
                    new List<ProviderModelConfigDto>
                    {
                        new(
                            Guid.NewGuid(),
                            providerId,
                            "gpt-4o",
                            "GPT-4o",
                            "Standard",
                            null,
                            null,
                            true
                        ),
                    }
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{providerId}/models/configured",
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dtos = await response.Content.ReadFromJsonAsync<IReadOnlyList<ProviderModelConfigDto>>(
            CancellationToken.None
        );
        Assert.NotNull(dtos);
        Assert.Single(dtos);
        Assert.Equal("gpt-4o", dtos[0].ModelId);
    }

    [Fact]
    public async Task ListModels_ProviderNotFound_ReturnsError()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.ListModelsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<IReadOnlyList<ProviderModelConfigDto>>.Failure(
                    new Error("LLM_PROVIDER_NOT_FOUND", "Provider not found")
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{Guid.NewGuid()}/models/configured",
            CancellationToken.None
        );

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetProvider_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var providerId = Guid.NewGuid();
        mock.GetProviderAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProviderDto>.Success(
                    new ProviderDto(
                        providerId,
                        "Test Provider",
                        "https://api.test.com",
                        "OpenAiCompatible",
                        "Text",
                        "Standard",
                        null,
                        true,
                        DateTime.UtcNow,
                        DateTime.UtcNow
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{providerId}",
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProviderDto>(CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal("Test Provider", dto.Name);
    }

    [Fact]
    public async Task GetProvider_NotFound_Returns404()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.GetProviderAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProviderDto>.Failure(new Error("PROVIDER_NOT_FOUND", "Not found")));

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{Guid.NewGuid()}",
            CancellationToken.None
        );

        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task UpdateProvider_WithValidInput_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var providerId = Guid.NewGuid();
        mock.UpdateProviderAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpdateProviderInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<ProviderDto>.Success(
                    new ProviderDto(
                        providerId,
                        "New Name",
                        "https://api.new.com",
                        "OpenAiCompatible",
                        "Text",
                        "Premium",
                        null,
                        true,
                        DateTime.UtcNow,
                        DateTime.UtcNow
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new UpdateProviderInput(
            "New Name",
            "https://api.new.com",
            null,
            null,
            null,
            "Premium",
            null,
            null
        );
        var response = await client.PutAsJsonAsync(
            $"api/admin/providers/{providerId}",
            input,
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProviderDto>(CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal("New Name", dto.Name);
    }

    [Fact]
    public async Task UpdateProvider_NotFound_Returns404()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.UpdateProviderAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpdateProviderInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<ProviderDto>.Failure(new Error("PROVIDER_NOT_FOUND", "Not found")));

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new UpdateProviderInput("New Name", null, null, null, null, null, null, null);
        var response = await client.PutAsJsonAsync(
            $"api/admin/providers/{Guid.NewGuid()}",
            input,
            CancellationToken.None
        );

        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task DisableProvider_ReturnsNoContent()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.DisableProviderAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.DeleteAsync(
            $"api/admin/providers/{Guid.NewGuid()}",
            CancellationToken.None
        );

        Assert.Equal(204, (int)response.StatusCode);
    }

    [Fact]
    public async Task CreateModel_WithValidInput_ReturnsCreated()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var providerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        mock.CreateModelAsync(
                Arg.Any<Guid>(),
                Arg.Any<CreateModelInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<ProviderModelConfigDto>.Success(
                    new ProviderModelConfigDto(
                        modelId,
                        providerId,
                        "gpt-4o",
                        "GPT-4o",
                        "Premium",
                        0.00001m,
                        128000,
                        true
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new CreateModelInput("gpt-4o", "GPT-4o", "Premium", 0.00001m, 128000, true);
        var response = await client.PostAsJsonAsync(
            $"api/admin/providers/{providerId}/models",
            input,
            CancellationToken.None
        );

        Assert.Equal(201, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProviderModelConfigDto>(
            CancellationToken.None
        );
        Assert.NotNull(dto);
        Assert.Equal("gpt-4o", dto.ModelId);
    }

    [Fact]
    public async Task CreateModel_ProviderNotFound_Returns404()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.CreateModelAsync(
                Arg.Any<Guid>(),
                Arg.Any<CreateModelInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<ProviderModelConfigDto>.Failure(new Error("PROVIDER_NOT_FOUND", "Not found"))
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new CreateModelInput("gpt-4o", "GPT-4o", "Premium", 0.00001m, 128000, true);
        var response = await client.PostAsJsonAsync(
            $"api/admin/providers/{Guid.NewGuid()}/models",
            input,
            CancellationToken.None
        );

        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task CreateModel_DuplicateModelId_Returns409()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.CreateModelAsync(
                Arg.Any<Guid>(),
                Arg.Any<CreateModelInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<ProviderModelConfigDto>.Failure(
                    new Error("MODEL_ALREADY_EXISTS", "Model already configured for this provider")
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new CreateModelInput("gpt-4o", "GPT-4o", "Premium", 0.00001m, 128000, true);
        var response = await client.PostAsJsonAsync(
            $"api/admin/providers/{Guid.NewGuid()}/models",
            input,
            CancellationToken.None
        );

        Assert.Equal(409, (int)response.StatusCode);
    }

    [Fact]
    public async Task UpdateModel_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var providerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        mock.UpdateModelAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<UpdateModelInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<ProviderModelConfigDto>.Success(
                    new ProviderModelConfigDto(
                        modelId,
                        providerId,
                        "gpt-4o",
                        "GPT-4o Updated",
                        "Premium",
                        0.00002m,
                        256000,
                        false
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new UpdateModelInput("GPT-4o Updated", "Premium", 0.00002m, 256000, false);
        var response = await client.PutAsJsonAsync(
            $"api/admin/providers/{providerId}/models/{modelId}",
            input,
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProviderModelConfigDto>(
            CancellationToken.None
        );
        Assert.NotNull(dto);
        Assert.Equal("GPT-4o Updated", dto.Name);
    }

    [Fact]
    public async Task DeleteModel_ReturnsNoContent()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.DeleteModelAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.DeleteAsync(
            $"api/admin/providers/{Guid.NewGuid()}/models/{Guid.NewGuid()}",
            CancellationToken.None
        );

        Assert.Equal(204, (int)response.StatusCode);
    }

    [Fact]
    public async Task ListRouting_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.ListRoutingAsync(Arg.Any<CancellationToken>())
            .Returns(
                Result<IReadOnlyList<FeatureRoutingDto>>.Success(
                    new List<FeatureRoutingDto>
                    {
                        new(
                            Guid.NewGuid(),
                            AiFeature.PersonalChat,
                            "Standard",
                            null,
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            Array.Empty<Guid>()
                        ),
                    }
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync("api/admin/routing", CancellationToken.None);

        Assert.Equal(200, (int)response.StatusCode);
        var dtos = await response.Content.ReadFromJsonAsync<IReadOnlyList<FeatureRoutingDto>>(
            JsonOptions,
            CancellationToken.None
        );
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
    }

    [Fact]
    public async Task UpdateRouting_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var featureId = Guid.NewGuid();
        mock.UpdateRoutingAsync(
                Arg.Any<string>(),
                Arg.Any<UpdateRoutingInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<FeatureRoutingDto>.Success(
                    new FeatureRoutingDto(
                        featureId,
                        AiFeature.PersonalChat,
                        "Premium",
                        "Economy",
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        Array.Empty<Guid>()
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new UpdateRoutingInput("Premium", "Economy");
        var response = await client.PutAsJsonAsync(
            $"api/admin/routing/PersonalChat",
            input,
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<FeatureRoutingDto>(
            JsonOptions,
            CancellationToken.None
        );
        Assert.NotNull(dto);
        Assert.Equal("Premium", dto.DefaultTier);
    }

    [Fact]
    public async Task SetAllowedModels_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var featureId = Guid.NewGuid();
        var modelConfigId = Guid.NewGuid();
        mock.SetAllowedModelsAsync(
                Arg.Any<string>(),
                Arg.Any<SetAllowedModelsInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<FeatureRoutingDto>.Success(
                    new FeatureRoutingDto(
                        featureId,
                        AiFeature.PersonalChat,
                        "Standard",
                        null,
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        new[] { modelConfigId }
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new SetAllowedModelsInput(new[] { modelConfigId });
        var response = await client.PutAsJsonAsync(
            $"api/admin/routing/PersonalChat/allowed-models",
            input,
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<FeatureRoutingDto>(
            JsonOptions,
            CancellationToken.None
        );
        Assert.NotNull(dto);
        Assert.Equal(modelConfigId, Assert.Single(dto.AllowedModelConfigIds));
    }

    [Fact]
    public async Task GetBudget_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var userId = Guid.NewGuid();
        mock.GetBudgetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<UserBudgetDto>.Success(
                    new UserBudgetDto(
                        userId,
                        100000,
                        500,
                        50000,
                        100,
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddMonths(1)
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/users/{userId}/budget",
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserBudgetDto>(CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal(100000, dto.MonthlyTokenBudget);
    }

    [Fact]
    public async Task UpdateBudget_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var userId = Guid.NewGuid();
        mock.UpdateBudgetAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpdateBudgetInput>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Result<UserBudgetDto>.Success(
                    new UserBudgetDto(
                        userId,
                        200000,
                        500,
                        500,
                        0,
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddMonths(1)
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var input = new UpdateBudgetInput(200000, 500);
        var response = await client.PutAsJsonAsync(
            $"api/admin/users/{userId}/budget",
            input,
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserBudgetDto>(CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal(200000, dto.MonthlyTokenBudget);
        Assert.Equal(500, dto.MonthlyImageBudget);
    }

    [Fact]
    public async Task QueryTokenUsage_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.QueryTokenUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<TokenUsagePageDto>.Success(new TokenUsagePageDto([], 0, 0, 0, 0m)));

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync("api/admin/usage/tokens", CancellationToken.None);

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<TokenUsagePageDto>(
            CancellationToken.None
        );
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task QueryImageUsage_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.QueryImageUsageAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result<ImageUsagePageDto>.Success(new ImageUsagePageDto([], 0, 0, 0m)));

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync("api/admin/usage/images", CancellationToken.None);

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ImageUsagePageDto>(
            CancellationToken.None
        );
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task GetModel_ReturnsOk()
    {
        var mock = Substitute.For<ILlmAdminService>();
        var providerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        mock.GetModelAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProviderModelConfigDto>.Success(
                    new ProviderModelConfigDto(
                        modelId,
                        providerId,
                        "gpt-4o",
                        "GPT-4o",
                        "Premium",
                        0.00001m,
                        128000,
                        true
                    )
                )
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{providerId}/models/{modelId}",
            CancellationToken.None
        );

        Assert.Equal(200, (int)response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProviderModelConfigDto>(
            CancellationToken.None
        );
        Assert.NotNull(dto);
        Assert.Equal("gpt-4o", dto.ModelId);
    }

    [Fact]
    public async Task GetModel_NotFound_Returns404()
    {
        var mock = Substitute.For<ILlmAdminService>();
        mock.GetModelAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProviderModelConfigDto>.Failure(new Error("MODEL_NOT_FOUND", "Not found"))
            );

        using var factory = CreateFactory(mock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminToken
        );

        var response = await client.GetAsync(
            $"api/admin/providers/{Guid.NewGuid()}/models/{Guid.NewGuid()}",
            CancellationToken.None
        );

        Assert.Equal(404, (int)response.StatusCode);
    }
}

internal class LlmAdminTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public ILlmAdminService LlmAdmin { get; set; } = Substitute.For<ILlmAdminService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Environment", "Test");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting(
            "Jwt:SigningKey",
            "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
        );
        builder.UseSetting("Llm:EncryptionKey", "0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(ILlmAdminService)
            );
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddScoped<ILlmAdminService>(_ => LlmAdmin);
            services.AddScoped<IProjectDocumentRepository>(_ => Substitute.For<IProjectDocumentRepository>());
            services.AddScoped<ProjectDocumentService>();
        });
    }

    public static string IssueAdminToken() =>
        IssueToken([
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin"),
        ]);

    public static string IssueUserToken() =>
        IssueToken([
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "user"),
        ]);

    private static string IssueToken(System.Security.Claims.Claim[] roleClaims)
    {
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                userId.ToString()
            ),
        }.Concat(roleClaims);
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(
                "test-secret-key-that-is-at-least-32-chars-long-for-hs256"
            )
        );
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256
        );

        return handler.CreateToken(
            new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = identity,
                Issuer = "HydraForge",
                Audience = "HydraForge",
                SigningCredentials = credentials,
                Expires = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime,
            }
        );
    }
}
