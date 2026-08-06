# Reasoning-Effort Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user pick a Low/Medium/High reasoning-effort level for models that support it, wired end-to-end from an admin-set per-model capability flag through the chat request pipeline to the two adapters (Anthropic, OpenAI-compatible) that can act on it.

**Architecture:** A new `bool SupportsReasoning` flag on `ProviderModelConfig` (admin-set, defaults `false`) flows into `AvailableModelDto` so the Web UI model picker knows when to show the effort row. A new `string? ReasoningEffort` field on `ChatRequest` carries the user's choice from `ChatModelPicker.vue` through `ChatInput` → `useChatStream` → REST → `ChatReplyGenerator` → the adapter. `AnthropicAdapter` maps it to `output_config: {effort: "..."}`; `OpenAiCompatibleAdapter` maps it to `reasoning_effort`. No other adapter is touched. `ModelRouter`'s tier-resolution logic is untouched — effort only changes how the already-selected model is called.

**Tech Stack:** .NET 10 / C#, EF Core migrations, xUnit + NSubstitute, Nuxt 4 + Vue 3 + Nuxt UI, Vitest + `@nuxt/test-utils/runtime`.

## Global Constraints

- Effort values are exactly `"low"` / `"medium"` / `"high"` (strings, lowercase) — never `xhigh`/`max` (out of scope for v1, per spec).
- `ReasoningEffort` is always optional (`null` = no opinion / not supported) — no validation, no failure mode for an invalid or unsupported value at the API layer (per spec's Error Handling section: the field is a hint the client can only set when the UI shows it).
- The effort row in `ChatModelPicker.vue` is `v-if` on the *currently selected* model's `supportsReasoning` — never shown for a model that doesn't support it, never a static/always-visible control.
- No change to `ModelRouter`'s tier-resolution or fallback logic — this feature only affects how the resolved model is called.
- xUnit + NSubstitute only (no Moq, no FluentAssertions). No `var` where the type isn't obvious. No `console.log`/`console.error`/`console.warn` in Web UI code.

---

### Task 1: Domain — `SupportsReasoning` flag on `ProviderModelConfig`

**Files:**
- Modify: `src/HydraForge.Domain/Entities/Admin/ProviderModelConfig.cs`
- Create: EF migration (via `dotnet ef migrations add`, see Step 3 below — exact filename timestamp is assigned at generation time)
- Test: `tests/HydraForge.Infrastructure.Tests/Persistence/HydraForgeDbContextModelTests.cs`

**Interfaces:**
- Produces: `ProviderModelConfig.SupportsReasoning` (`bool`, default `false`) — consumed by Task 2 (admin DTOs), Task 3 (`ModelRouter.ListAvailableModelsAsync`).

- [ ] **Step 1: Add the property**

In `src/HydraForge.Domain/Entities/Admin/ProviderModelConfig.cs`, add a new property after `IsEnabled`:

```csharp
public bool IsEnabled { get; set; } = true;
public bool SupportsReasoning { get; set; } = false;
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
```

- [ ] **Step 2: Write the failing EF-model-contract test**

In `tests/HydraForge.Infrastructure.Tests/Persistence/HydraForgeDbContextModelTests.cs`, add a new test near `GetEntityTypes_ProviderModelConfigsTable_Exists` (follow the `FindEntityType_ImageUsageRecord_HasRequiredProperties` pattern already in this file):

```csharp
[Fact]
public void FindEntityType_ProviderModelConfig_HasSupportsReasoningProperty()
{
    using var context = new HydraForgeDbContext(CreateOptions());
    var model = context.Model;

    var entity = model.FindEntityType(typeof(ProviderModelConfig));
    Assert.NotNull(entity);

    AssertProperties(entity, "SupportsReasoning");
}
```

- [ ] **Step 3: Run the test to verify it fails (or passes trivially if the property already compiles)**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests --filter FindEntityType_ProviderModelConfig_HasSupportsReasoningProperty`
Expected: PASS once Step 1's property exists — EF picks up CLR properties automatically, no fluent config needed (there is none for `IsEnabled` either, per `HydraForgeDbContext.cs`'s `ConfigureEntity<ProviderModelConfig>` block, which only has two `HasIndex` calls).

- [ ] **Step 4: Generate the migration**

Run from repo root:

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations add AddSupportsReasoningToProviderModelConfig \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

This creates `src/HydraForge.Infrastructure/Migrations/{timestamp}_AddSupportsReasoningToProviderModelConfig.cs` and its `.Designer.cs`. Verify the generated `Up()` contains an `AddColumn` for `SupportsReasoning` on `provider_model_configs` with `defaultValue: false` (matches the C# default and keeps existing rows valid without a manual backfill).

- [ ] **Step 5: Verify no pending model changes remain**

Run:

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations has-pending-model-changes \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

Expected: no pending changes reported (the migration in Step 4 fully captures the model change).

- [ ] **Step 6: Run the full test suite for this project**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests`
Expected: PASS, including the new test from Step 2.

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Domain/Entities/Admin/ProviderModelConfig.cs \
  src/HydraForge.Infrastructure/Migrations \
  tests/HydraForge.Infrastructure.Tests/Persistence/HydraForgeDbContextModelTests.cs
git commit -m "feat: add SupportsReasoning flag to ProviderModelConfig"
```

---

### Task 2: Application — Admin DTOs + `LlmAdminService` wiring

**Files:**
- Modify: `src/HydraForge.Application/Llm/LlmAdminDtos.cs`
- Modify: `src/HydraForge.Application/Llm/LlmAdminService.cs`
- Test: `tests/HydraForge.Application.Tests/Llm/LlmAdminServiceTests.cs`

**Interfaces:**
- Consumes: `ProviderModelConfig.SupportsReasoning` (Task 1).
- Produces: `ProviderModelConfigDto.SupportsReasoning`, `CreateModelInput.SupportsReasoning`, `UpdateModelInput.SupportsReasoning` (all `bool`/`bool?`) — consumed by Task 7 (admin Web UI).

- [ ] **Step 1: Extend the DTOs**

In `src/HydraForge.Application/Llm/LlmAdminDtos.cs`, update the three records:

```csharp
public sealed record ProviderModelConfigDto(
    Guid Id,
    Guid ProviderId,
    string ModelId,
    string Name,
    string Tier,
    decimal? PricePerToken,
    int? MaxTokens,
    bool IsEnabled,
    bool SupportsReasoning
);

public sealed record CreateModelInput(
    string ModelId,
    string Name,
    string Tier,
    decimal? PricePerToken,
    int? MaxTokens,
    bool IsEnabled,
    bool SupportsReasoning = false
);

public sealed record UpdateModelInput(
    string? Name,
    string? Tier,
    decimal? PricePerToken,
    int? MaxTokens,
    bool? IsEnabled,
    bool? SupportsReasoning = null
);
```

`CreateModelInput.SupportsReasoning` defaults to `false` so every existing 6-arg positional construction (tests, any other call site) keeps compiling. `UpdateModelInput.SupportsReasoning` defaults to `null` (the existing "don't change" sentinel this record already uses for every other field) for the same reason.

- [ ] **Step 2: Write the failing tests**

In `tests/HydraForge.Application.Tests/Llm/LlmAdminServiceTests.cs`, add two tests near `CreateModelAsync_NewModelId_Succeeds` (~line 65), following that test's exact fixture style — a fresh `Substitute.For<ILlmAdminRepository>()` per test, stubbed per-call, built through this file's existing `CreateService(repo)` helper (~line 13). There is no existing `UpdateModelAsync` test in this file to follow, so its stub of `GetModelConfigAsync` is new but matches the same per-test NSubstitute style as every other test here:

```csharp
[Fact]
public async Task CreateModelAsync_SupportsReasoningTrue_PersistsFlag()
{
    var providerId = Guid.NewGuid();
    var repo = Substitute.For<ILlmAdminRepository>();
    repo.GetProviderByIdAsync(providerId, Arg.Any<CancellationToken>())
        .Returns(new LlmProvider { Id = providerId, Name = "Anthropic" });
    repo.ListModelConfigsAsync(providerId, Arg.Any<CancellationToken>())
        .Returns(new List<ProviderModelConfig>());

    var service = CreateService(repo);
    var input = new CreateModelInput(
        "claude-opus-5",
        "Claude Opus 5",
        "Premium",
        null,
        null,
        true,
        SupportsReasoning: true
    );

    var result = await service.CreateModelAsync(providerId, input, CancellationToken.None);

    Assert.True(result.IsSuccess);
    Assert.True(result.Value.SupportsReasoning);
}

[Fact]
public async Task UpdateModelAsync_SupportsReasoningToggled_UpdatesFlag()
{
    var providerId = Guid.NewGuid();
    var modelId = Guid.NewGuid();
    var repo = Substitute.For<ILlmAdminRepository>();
    var config = new ProviderModelConfig
    {
        Id = modelId,
        ProviderId = providerId,
        ModelId = "gpt-5",
        Name = "GPT-5",
        Tier = ModelTier.Standard,
        SupportsReasoning = false,
    };
    repo.GetModelConfigAsync(providerId, modelId, Arg.Any<CancellationToken>()).Returns(config);

    var service = CreateService(repo);
    var input = new UpdateModelInput(null, null, null, null, null, SupportsReasoning: true);

    var result = await service.UpdateModelAsync(providerId, modelId, input, CancellationToken.None);

    Assert.True(result.IsSuccess);
    Assert.True(result.Value.SupportsReasoning);
    await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/HydraForge.Application.Tests --filter "CreateModelAsync_SupportsReasoningTrue_PersistsFlag|UpdateModelAsync_SupportsReasoningToggled_UpdatesFlag"`
Expected: FAIL — `CreateModelInput`/`UpdateModelInput` don't yet have a `SupportsReasoning` parameter, or `ToModelConfigDto` doesn't map it (compile error until Step 4 lands).

- [ ] **Step 4: Wire the flag through `LlmAdminService`**

In `src/HydraForge.Application/Llm/LlmAdminService.cs`:

`CreateModelAsync` (~line 368) — add the field to the entity construction:

```csharp
var config = new ProviderModelConfig
{
    ProviderId = providerId,
    ModelId = input.ModelId,
    Name = input.Name,
    Tier = tier,
    PricePerToken = input.PricePerToken,
    MaxTokens = input.MaxTokens,
    IsEnabled = input.IsEnabled,
    SupportsReasoning = input.SupportsReasoning,
};
```

`UpdateModelAsync` (~line 444, right after the `IsEnabled` block) — add:

```csharp
if (input.IsEnabled.HasValue)
{
    config.IsEnabled = input.IsEnabled.Value;
}

if (input.SupportsReasoning.HasValue)
{
    config.SupportsReasoning = input.SupportsReasoning.Value;
}
```

`ToModelConfigDto` (~line 972) — add the new positional argument:

```csharp
private static ProviderModelConfigDto ToModelConfigDto(ProviderModelConfig c) =>
    new(
        c.Id,
        c.ProviderId,
        c.ModelId,
        c.Name,
        c.Tier.ToString(),
        c.PricePerToken,
        c.MaxTokens,
        c.IsEnabled,
        c.SupportsReasoning
    );
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/HydraForge.Application.Tests --filter "CreateModelAsync_SupportsReasoningTrue_PersistsFlag|UpdateModelAsync_SupportsReasoningToggled_UpdatesFlag"`
Expected: PASS

- [ ] **Step 6: Run the full Application test suite**

Run: `dotnet test tests/HydraForge.Application.Tests`
Expected: PASS (no other `ToModelConfigDto`/`CreateModelInput`/`UpdateModelInput` call site breaks, since both new fields have defaults).

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Application/Llm/LlmAdminDtos.cs \
  src/HydraForge.Application/Llm/LlmAdminService.cs \
  tests/HydraForge.Application.Tests/Llm/LlmAdminServiceTests.cs
git commit -m "feat: thread SupportsReasoning through admin model CRUD"
```

---

### Task 3: Application — `AvailableModelDto.SupportsReasoning` + `ChatRequest.ReasoningEffort`

**Files:**
- Modify: `src/HydraForge.Application/Llm/LlmDtos.cs`
- Modify: `src/HydraForge.Application/Llm/ModelRouter.cs`
- Test: `tests/HydraForge.Application.Tests/Llm/ModelRouterTests.cs`

**Interfaces:**
- Consumes: `ProviderModelConfig.SupportsReasoning` (Task 1).
- Produces: `AvailableModelDto.SupportsReasoning` (`bool`) — consumed by Task 8 (`ChatModelPicker.vue`). `ChatRequest.ReasoningEffort` (`string?`, default `null`, last positional param) — consumed by Task 4, 5 (adapters) and Task 6 (`ChatReplyGenerator`).

- [ ] **Step 1: Add the two DTO fields**

In `src/HydraForge.Application/Llm/LlmDtos.cs`, update `ChatRequest` (the `string? ReasoningEffort = null` default keeps every existing 7-arg positional `new ChatRequest(...)` call site — in `ChatReplyGenerator.cs`, `LlmChatSummaryGenerator.cs`, `ProjectContextSnapshotService.cs`, `ContextCompressor.cs`, `LlmChatTitleGenerator.cs`, and every adapter test file — compiling unchanged):

```csharp
public sealed record ChatRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<CacheBlock> CacheBlocks,
    IReadOnlyList<ToolDefinition> Tools,
    int? MaxOutputTokens,
    decimal? Temperature,
    string? ReasoningEffort = null
);
```

And `AvailableModelDto`:

```csharp
public sealed record AvailableModelDto(
    Guid ProviderModelConfigId,
    string ModelName,
    string ProviderName,
    string Tier,
    bool SupportsReasoning
);
```

- [ ] **Step 2: Write the failing test**

In `tests/HydraForge.Application.Tests/Llm/ModelRouterTests.cs`, add a new test after `ResolveAsync_AllowlistConfigured_AllModelsTooSmall_ReturnsContextWindowExceeded` (still inside `ModelRouterTests`, before the closing `}` of the class):

```csharp
[Fact]
public async Task ListAvailableModelsAsync_ReturnsSupportsReasoningFlag_FromModelConfig()
{
    var providerId = Guid.NewGuid();
    var reasoningModelId = Guid.NewGuid();
    var plainModelId = Guid.NewGuid();
    var provider = new FakeRoutingConfigProvider();
    provider.AddRouting(AiFeature.PersonalChat, ModelTier.Standard, null);
    provider.AddEnabledProvider(providerId, "TestProvider", ModelTier.Standard);
    provider.AddModel(
        reasoningModelId,
        providerId,
        "claude-opus-5",
        ModelTier.Standard,
        maxTokens: 200000,
        supportsReasoning: true
    );
    provider.AddModel(
        plainModelId,
        providerId,
        "gpt-4o",
        ModelTier.Standard,
        maxTokens: 128000,
        supportsReasoning: false
    );

    var router = CreateRouter(provider);

    var result = await router.ListAvailableModelsAsync(AiFeature.PersonalChat);

    Assert.True(result.IsSuccess);
    var reasoningModel = result.Value.Single(m => m.ModelName == "claude-opus-5");
    var plainModel = result.Value.Single(m => m.ModelName == "gpt-4o");
    Assert.True(reasoningModel.SupportsReasoning);
    Assert.False(plainModel.SupportsReasoning);
}
```

This also requires adding a `supportsReasoning` parameter to `FakeRoutingConfigProvider.AddModel` (still in the same test file, near the bottom) — default `false` so every existing `AddModel(...)` call across `ModelRouterTests` keeps compiling:

```csharp
public void AddModel(
    Guid id,
    Guid providerId,
    string modelId,
    ModelTier tier,
    int? maxTokens = null,
    bool isEnabled = true,
    bool supportsReasoning = false
)
{
    _models.Add(
        new ProviderModelConfig
        {
            Id = id,
            ProviderId = providerId,
            ModelId = modelId,
            Name = modelId,
            Tier = tier,
            MaxTokens = maxTokens,
            IsEnabled = isEnabled,
            SupportsReasoning = supportsReasoning,
        }
    );
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/HydraForge.Application.Tests --filter ListAvailableModelsAsync_ReturnsSupportsReasoningFlag_FromModelConfig`
Expected: FAIL — `AvailableModelDto` construction in `ModelRouter.cs` only takes 4 positional args, so this won't compile until Step 4.

- [ ] **Step 4: Wire `SupportsReasoning` into `ListAvailableModelsAsync`**

In `src/HydraForge.Application/Llm/ModelRouter.cs`, update the `Select` at line 179:

```csharp
var result = candidates
    .Select(x => new AvailableModelDto(
        x.Model.Id,
        x.Model.Name,
        x.Provider.Name,
        x.Model.Tier.ToString(),
        x.Model.SupportsReasoning
    ))
    .ToList();
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/HydraForge.Application.Tests --filter ListAvailableModelsAsync_ReturnsSupportsReasoningFlag_FromModelConfig`
Expected: PASS

- [ ] **Step 6: Run the full Application test suite**

Run: `dotnet test tests/HydraForge.Application.Tests`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Application/Llm/LlmDtos.cs \
  src/HydraForge.Application/Llm/ModelRouter.cs \
  tests/HydraForge.Application.Tests/Llm/ModelRouterTests.cs
git commit -m "feat: add ReasoningEffort to ChatRequest and SupportsReasoning to AvailableModelDto"
```

---

### Task 4: Infrastructure — `AnthropicAdapter` maps `ReasoningEffort` to `output_config.effort`

**Files:**
- Modify: `src/HydraForge.Infrastructure/Llm/Adapters/AnthropicAdapter.cs`
- Test: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/AnthropicAdapterTests.cs`

**Interfaces:**
- Consumes: `ChatRequest.ReasoningEffort` (Task 3).

- [ ] **Step 1: Write the failing tests**

In `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/AnthropicAdapterTests.cs`, add two tests after `StreamChatAsync_SendsCorrectRequestShape` (same file, same fixture helpers — `CreateProvider`, `FakeKeyVault`, `FakeLogger`, `JsonBodyHandler`):

```csharp
[Fact]
public async Task StreamChatAsync_ReasoningEffort_MapsToOutputConfigEffort()
{
    var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
    using var http = new HttpClient(bodyHandler);
    var provider = CreateProvider();
    var logger = new FakeLogger();
    var adapter = new AnthropicAdapter(http, new FakeKeyVault("test-key"), provider, logger);

    var request = new ChatRequest(
        Guid.NewGuid(),
        "claude-opus-5",
        [new ChatMessage(ChatRole.User, "Hello")],
        [],
        [],
        1024,
        0.7m,
        "high"
    );

    await foreach (var _ in adapter.StreamChatAsync(request)) { }

    Assert.NotNull(bodyHandler.LastBody);
    var doc = JsonDocument.Parse(bodyHandler.LastBody);
    Assert.Equal(
        "high",
        doc.RootElement.GetProperty("output_config").GetProperty("effort").GetString()
    );
}

[Fact]
public async Task StreamChatAsync_NoReasoningEffort_OmitsOutputConfig()
{
    var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
    using var http = new HttpClient(bodyHandler);
    var provider = CreateProvider();
    var logger = new FakeLogger();
    var adapter = new AnthropicAdapter(http, new FakeKeyVault("test-key"), provider, logger);

    var request = new ChatRequest(
        Guid.NewGuid(),
        "claude-opus-5",
        [new ChatMessage(ChatRole.User, "Hello")],
        [],
        [],
        1024,
        0.7m
    );

    await foreach (var _ in adapter.StreamChatAsync(request)) { }

    Assert.NotNull(bodyHandler.LastBody);
    var doc = JsonDocument.Parse(bodyHandler.LastBody);
    Assert.False(doc.RootElement.TryGetProperty("output_config", out _));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests --filter "StreamChatAsync_ReasoningEffort_MapsToOutputConfigEffort|StreamChatAsync_NoReasoningEffort_OmitsOutputConfig"`
Expected: FAIL — `output_config` is never sent (first test: property not found; second test already trivially passes, so only the first needs the implementation — confirm the first fails before continuing).

- [ ] **Step 3: Implement the mapping**

In `src/HydraForge.Infrastructure/Llm/Adapters/AnthropicAdapter.cs`:

Add a new private class near `AnthropicCacheControl` (after it, ~line 305):

```csharp
private sealed class AnthropicOutputConfig
{
    [JsonPropertyName("effort")]
    public string Effort { get; set; } = "";
}
```

Add the field to `AnthropicChatRequest` (~line 256, after `Tools`):

```csharp
[JsonPropertyName("tools")]
public List<AnthropicTool>? Tools { get; set; }

[JsonPropertyName("output_config")]
public AnthropicOutputConfig? OutputConfig { get; set; }
```

Update the `body` construction (~line 108) to set it:

```csharp
var body = new AnthropicChatRequest
{
    Model = request.ModelId,
    MaxTokens = request.MaxOutputTokens ?? 4096,
    Messages = messages,
    Stream = true,
    System = systemBlocks.Count > 0 ? systemBlocks : null,
    Tools =
        request.Tools.Count > 0
            ? request.Tools.Select(t => AnthropicTool.FromDefinition(t)).ToList()
            : null,
    OutputConfig = request.ReasoningEffort is not null
        ? new AnthropicOutputConfig { Effort = request.ReasoningEffort }
        : null,
};
```

`output_config` is omitted from the JSON entirely when `null` — `JsonOptions` already has `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` set at the top of this class, same mechanism that already omits `system`/`tools`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests --filter "StreamChatAsync_ReasoningEffort_MapsToOutputConfigEffort|StreamChatAsync_NoReasoningEffort_OmitsOutputConfig"`
Expected: PASS

- [ ] **Step 5: Run the full Infrastructure test suite**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/HydraForge.Infrastructure/Llm/Adapters/AnthropicAdapter.cs \
  tests/HydraForge.Infrastructure.Tests/Llm/Adapters/AnthropicAdapterTests.cs
git commit -m "feat: map ReasoningEffort to Anthropic output_config.effort"
```

---

### Task 5: Infrastructure — `OpenAiCompatibleAdapter` maps `ReasoningEffort` to `reasoning_effort`

**Files:**
- Modify: `src/HydraForge.Infrastructure/Llm/Adapters/OpenAiCompatibleAdapter.cs`
- Test: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/OpenAiCompatibleAdapterTests.cs`

**Interfaces:**
- Consumes: `ChatRequest.ReasoningEffort` (Task 3).

- [ ] **Step 1: Write the failing tests**

In `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/OpenAiCompatibleAdapterTests.cs`, add two tests after `StreamChatAsync_SendsCorrectRequestShape` (same fixture helpers as the existing tests in this file — `CreateProvider`, `FakeKeyVault`, `JsonBodyHandler`):

```csharp
[Fact]
public async Task StreamChatAsync_ReasoningEffort_MapsToReasoningEffortField()
{
    var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
    using var http = new HttpClient(bodyHandler);
    var provider = CreateProvider();
    var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault("test-key"), provider);

    var request = new ChatRequest(
        Guid.NewGuid(),
        "gpt-5",
        [new ChatMessage(ChatRole.User, "Hello")],
        [],
        [],
        1024,
        0.7m,
        "medium"
    );

    await foreach (var _ in adapter.StreamChatAsync(request)) { }

    Assert.NotNull(bodyHandler.LastBody);
    var doc = JsonDocument.Parse(bodyHandler.LastBody);
    Assert.Equal("medium", doc.RootElement.GetProperty("reasoning_effort").GetString());
}

[Fact]
public async Task StreamChatAsync_NoReasoningEffort_OmitsReasoningEffortField()
{
    var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
    using var http = new HttpClient(bodyHandler);
    var provider = CreateProvider();
    var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault("test-key"), provider);

    var request = new ChatRequest(
        Guid.NewGuid(),
        "gpt-5",
        [new ChatMessage(ChatRole.User, "Hello")],
        [],
        [],
        1024,
        0.7m
    );

    await foreach (var _ in adapter.StreamChatAsync(request)) { }

    Assert.NotNull(bodyHandler.LastBody);
    var doc = JsonDocument.Parse(bodyHandler.LastBody);
    Assert.False(doc.RootElement.TryGetProperty("reasoning_effort", out _));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests --filter "StreamChatAsync_ReasoningEffort_MapsToReasoningEffortField|StreamChatAsync_NoReasoningEffort_OmitsReasoningEffortField"`
Expected: FAIL on the first test — `reasoning_effort` is never sent.

- [ ] **Step 3: Implement the mapping**

In `src/HydraForge.Infrastructure/Llm/Adapters/OpenAiCompatibleAdapter.cs`:

Add the field to `OpenAiChatRequest` (~line 339, after `Tools`):

```csharp
[JsonPropertyName("tools")]
public List<OpenAiTool>? Tools { get; set; }

[JsonPropertyName("reasoning_effort")]
public string? ReasoningEffort { get; set; }
```

Update the `body` construction (~line 46) to set it:

```csharp
var body = new OpenAiChatRequest
{
    Model = request.ModelId,
    Messages =
    [
        .. messages.Select(m => new OpenAiMessage
        {
            Role = m.Role.ToString().ToLowerInvariant(),
            Content = m.Content,
        }),
    ],
    Stream = true,
    MaxTokens = request.MaxOutputTokens,
    Temperature = request.Temperature,
    Tools =
        request.Tools.Count > 0
            ?
            [
                .. request.Tools.Select(t => new OpenAiTool
                {
                    Type = "function",
                    Function = new OpenAiFunction
                    {
                        Name = t.Name,
                        Description = t.Description,
                        Parameters = t.Parameters,
                    },
                }),
            ]
            : null,
    ReasoningEffort = request.ReasoningEffort,
};
```

`reasoning_effort` is omitted from the JSON when `null` via the same `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` already set on this class's `JsonOptions`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests --filter "StreamChatAsync_ReasoningEffort_MapsToReasoningEffortField|StreamChatAsync_NoReasoningEffort_OmitsReasoningEffortField"`
Expected: PASS

- [ ] **Step 5: Run the full Infrastructure test suite**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/HydraForge.Infrastructure/Llm/Adapters/OpenAiCompatibleAdapter.cs \
  tests/HydraForge.Infrastructure.Tests/Llm/Adapters/OpenAiCompatibleAdapterTests.cs
git commit -m "feat: map ReasoningEffort to OpenAI-compatible reasoning_effort field"
```

---

### Task 6: Application/Server — `ChatReplyGenerator` + `GenerateReplyRequest` threading

**Files:**
- Modify: `src/HydraForge.Application/Chat/ChatReplyGenerator.cs`
- Modify: `src/HydraForge.Server/Controllers/Chat/ChatMessagesController.cs`
- Test: `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs`

**Interfaces:**
- Consumes: `ChatRequest.ReasoningEffort` (Task 3).
- Produces: `GenerateReplyRequest.ReasoningEffort` (`string?`) — consumed by Task 9 (Web UI POST body). `ChatReplyGenerator.GenerateAsync`'s new `reasoningEffort` parameter (6th positional, before `ct`).

- [ ] **Step 1: Write the failing test**

In `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs`, add a new test modeled closely on `GenerateAsync_AsSharedProjectMember_Succeeds` (~line 414) but simpler — the session owner sending directly, asserting the effort value reaches the adapter's `ChatRequest`:

```csharp
[Fact]
public async Task GenerateAsync_ReasoningEffort_PassedThroughToChatRequest()
{
    var session = new ChatSession
    {
        Id = SessionId,
        OwnerId = UserId,
        Status = ChatSessionStatus.Active,
    };
    var userMessage = new ChatMessage
    {
        Id = MessageId,
        SessionId = SessionId,
        Role = MessageRole.User,
        Content = "hello",
    };
    _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
    _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);
    _ragRetriever
        .RetrieveAsync(
            SessionId,
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        )
        .Returns((IReadOnlyList<CacheBlock>)new List<CacheBlock>());
    _messageRepo
        .GetBySessionAsync(
            SessionId,
            Arg.Any<DateTime?>(),
            Arg.Any<Guid?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        )
        .Returns((IReadOnlyList<ChatMessage>)new List<ChatMessage>());

    var provider = new LlmProvider
    {
        Id = Guid.NewGuid(),
        Name = "anthropic",
        AdapterType = AdapterType.Anthropic,
    };
    var routeDecision = new RouteDecision(
        new ProviderModelConfigDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "claude-opus-5",
            "Claude Opus 5",
            "premium",
            null,
            null,
            true
        ),
        new ProviderDto(
            Guid.NewGuid(),
            "anthropic",
            "https://api.anthropic.com",
            "anthropic",
            "cloud",
            "premium",
            null,
            true,
            default,
            default
        ),
        [],
        provider
    );
    _modelRouter
        .ResolveAsync(
            Arg.Any<AiFeature>(),
            UserId,
            Arg.Any<Guid?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        )
        .Returns(Result<RouteDecision>.Success(routeDecision));

    var mockClient = Substitute.For<ILlmClient>();
    mockClient.AdapterType.Returns(AdapterType.Anthropic);
    mockClient
        .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
        .Returns(MakeImmediateEnumerable());
    _llmClientFactory.For(Arg.Any<LlmProvider>()).Returns(mockClient);

    await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null, "high");

    await mockClient
        .Received(1)
        .StreamChatAsync(
            Arg.Is<ChatRequest>(r => r.ReasoningEffort == "high"),
            Arg.Any<CancellationToken>()
        );
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/HydraForge.Application.Tests --filter GenerateAsync_ReasoningEffort_PassedThroughToChatRequest`
Expected: FAIL to compile — `GenerateAsync` doesn't yet accept a 6th `reasoningEffort` argument.

- [ ] **Step 3: Add the parameter and thread it through**

In `src/HydraForge.Application/Chat/ChatReplyGenerator.cs`, update the `GenerateAsync` signature (~line 67) — add `reasoningEffort` right before `ct`, with a default so every existing 5-arg call site (all of `ChatReplyGeneratorTests.cs`) keeps compiling:

```csharp
public async Task GenerateAsync(
    Guid sessionId,
    Guid userMessageId,
    Guid userId,
    Guid? presetId,
    Guid? preferredProviderModelConfigId,
    string? reasoningEffort = null,
    CancellationToken ct = default
)
```

Update the `ChatRequest` construction (~line 282):

```csharp
var request = new ChatRequest(
    route.Primary.Id,
    route.Primary.ModelId,
    chatMessages,
    cacheBlocks,
    [],
    4096,
    0.7m,
    reasoningEffort
);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/HydraForge.Application.Tests --filter GenerateAsync_ReasoningEffort_PassedThroughToChatRequest`
Expected: PASS

- [ ] **Step 5: Wire the controller and its request DTO**

In `src/HydraForge.Server/Controllers/Chat/ChatMessagesController.cs`, update `GenerateReplyRequest` (~line 119):

```csharp
public record GenerateReplyRequest(
    Guid? PresetId = null,
    Guid? PreferredProviderModelConfigId = null,
    string? ReasoningEffort = null
);
```

Update `GenerateReply` (~line 77) to read and pass it through:

```csharp
var userId = User.GetRequiredUserId();
var presetId = request?.PresetId;
var preferredProviderModelConfigId = request?.PreferredProviderModelConfigId;
var reasoningEffort = request?.ReasoningEffort;
await backgroundTaskQueue.EnqueueJobAsync<ChatReplyGenerator>(g =>
    g.GenerateAsync(
        sessionId,
        messageId,
        userId,
        presetId,
        preferredProviderModelConfigId,
        reasoningEffort,
        CancellationToken.None
    )
);
return Accepted();
```

- [ ] **Step 6: Run the full Application and Server test suites**

Run: `dotnet test tests/HydraForge.Application.Tests && dotnet test tests/HydraForge.Server.Tests`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Application/Chat/ChatReplyGenerator.cs \
  src/HydraForge.Server/Controllers/Chat/ChatMessagesController.cs \
  tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs
git commit -m "feat: thread ReasoningEffort through ChatReplyGenerator and the reply-trigger endpoint"
```

---

### Task 7: Web UI — admin Provider Models page toggle

**Files:**
- Modify: `src/web-ui/app/pages/admin/provider-models.vue`

**Interfaces:**
- Consumes: `ProviderModelConfigDto.SupportsReasoning`, `CreateModelInput.SupportsReasoning`, `UpdateModelInput.SupportsReasoning` (Task 2 — server-side; the TS interfaces in this file are hand-kept mirrors, same pattern the file already uses for every other DTO field).

- [ ] **Step 1: Extend the TS interfaces**

In `src/web-ui/app/pages/admin/provider-models.vue`, update the four local interfaces to add the new field, matching the existing style (required on the DTO/create input, optional on the update input):

```typescript
interface ProviderModelConfigDto {
  id: string
  providerId: string
  modelId: string
  name: string
  tier: string
  pricePerToken: number | null
  maxTokens: number | null
  isEnabled: boolean
  supportsReasoning: boolean
}

interface CreateModelInput {
  modelId: string
  name: string
  tier: string
  pricePerToken: number | null
  maxTokens: number | null
  isEnabled: boolean
  supportsReasoning: boolean
}

interface UpdateModelInput {
  name?: string | null
  tier?: string | null
  pricePerToken?: number | null
  maxTokens?: number | null
  isEnabled?: boolean
  supportsReasoning?: boolean
}
```

- [ ] **Step 2: Add form state**

Near the other `form*` refs (~line 132, after `formEnabled`):

```typescript
const formEnabled = ref(true)
const formSupportsReasoning = ref(false)
```

- [ ] **Step 3: Read/write it in the open/reset/submit/probe flows**

In `openEditModal` (~line 203-213), add after `formEnabled.value = model.isEnabled`:

```typescript
formEnabled.value = model.isEnabled
formSupportsReasoning.value = model.supportsReasoning
```

In `resetForm` (~line 215-222), add after `formEnabled.value = true`:

```typescript
formEnabled.value = true
formSupportsReasoning.value = false
```

In `handleModalSubmit` (~line 224-264), add `supportsReasoning: formSupportsReasoning.value` to both the `UpdateModelInput` body (~line 230) and the `CreateModelInput` body (~line 243):

```typescript
const body: UpdateModelInput = {
  name: formName.value,
  tier: formTier.value,
  pricePerToken: formPricePerToken.value,
  maxTokens: formMaxTokens.value,
  isEnabled: formEnabled.value,
  supportsReasoning: formSupportsReasoning.value
}
```

```typescript
const body: CreateModelInput = {
  modelId: formModelId.value,
  name: formName.value,
  tier: formTier.value,
  pricePerToken: formPricePerToken.value,
  maxTokens: formMaxTokens.value,
  isEnabled: formEnabled.value,
  supportsReasoning: formSupportsReasoning.value
}
```

`addProbedModel` (~line 321-333) does not need this field set — a newly-discovered, not-yet-configured model defaults `formSupportsReasoning` to `false` via `resetForm`'s reset semantics already covered above (it does not call `resetForm`, but it directly assigns every field it cares about the same way `resetForm` does — leave `formSupportsReasoning` at its already-`false` default from the previous `resetForm`/initial state; no new line needed here since the field isn't part of the discovered-model prefill).

- [ ] **Step 4: Add the checkbox to the template**

In the Add/Edit modal's form (~line 571-574), add after the `Enabled` checkbox:

```html
<UCheckbox
  v-model="formEnabled"
  label="Enabled"
/>

<UCheckbox
  v-model="formSupportsReasoning"
  label="Supports reasoning effort"
  help="Shows the Low/Medium/High effort picker in chat when this model is selected"
/>
```

- [ ] **Step 5: Manually verify**

Run `cd src/web-ui && pnpm dev` (with the API server running in Development, per CLAUDE.md's E2E prerequisites). Navigate to `/admin/provider-models`, select a provider, open "Add Model", confirm the new checkbox renders with its help text, submit, and confirm the created row's edit modal reopens with the checkbox in the state it was submitted in (proves the round-trip through the real API, not just the client-side type).

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/pages/admin/provider-models.vue
git commit -m "feat: add SupportsReasoning toggle to admin Provider Models form"
```

---

### Task 8: Web UI — `ChatModelPicker.vue` effort row

**Files:**
- Modify: `src/web-ui/app/types/chat.ts`
- Modify: `src/web-ui/app/components/chat/ChatModelPicker.vue`
- Test: Create `src/web-ui/app/components/chat/__tests__/ChatModelPicker.test.ts`

**Interfaces:**
- Consumes: `AvailableModelDto.SupportsReasoning` (Task 3, mirrored client-side).
- Produces: `ChatModelPicker.vue`'s `effort` model (`defineModel<string | null>('effort')`) — consumed by Task 9 (`ChatInput.vue`).

- [ ] **Step 1: Update the TS type**

In `src/web-ui/app/types/chat.ts`, update `AvailableModelDto`:

```typescript
export interface AvailableModelDto {
  providerModelConfigId: string
  modelName: string
  providerName: string
  tier: string
  supportsReasoning: boolean
}
```

- [ ] **Step 2: Write the failing component tests**

Create `src/web-ui/app/components/chat/__tests__/ChatModelPicker.test.ts`, following the mock-harness pattern already established in `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts` (`mockGET`, `mountSuspended`-based mounting, `mockNuxtImport` for `useApi`/`localStorage`-backed composables — read that file's top-of-file mock setup before writing this one, since `ChatModelPicker.vue` uses the same `useApi()` + `import.meta.client` + `localStorage` pattern):

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ChatModelPicker from '~/components/chat/ChatModelPicker.vue'
import type { AvailableModelDto } from '~/types/chat'

const mockGET = vi.fn()
mockNuxtImport('useApi', () => () => ({ GET: mockGET }))

const reasoningModel: AvailableModelDto = {
  providerModelConfigId: 'model-reasoning',
  modelName: 'Claude Opus 5',
  providerName: 'Anthropic',
  tier: 'Premium',
  supportsReasoning: true
}

const plainModel: AvailableModelDto = {
  providerModelConfigId: 'model-plain',
  modelName: 'GPT-4o',
  providerName: 'OpenAI',
  tier: 'Standard',
  supportsReasoning: false
}

beforeEach(() => {
  mockGET.mockReset()
  localStorage.clear()
})

describe('ChatModelPicker effort row', () => {
  it('hides the effort row when the selected model does not support reasoning', async () => {
    mockGET.mockResolvedValue({ data: [plainModel] })
    const wrapper = await mountSuspended(ChatModelPicker, {
      props: { modelValue: null, feature: 'PersonalChat' }
    })
    await new Promise(resolve => setTimeout(resolve, 0))

    expect(wrapper.find('[data-testid="reasoning-effort-row"]').exists()).toBe(false)
  })

  it('shows the effort row when the selected model supports reasoning', async () => {
    mockGET.mockResolvedValue({ data: [reasoningModel] })
    const wrapper = await mountSuspended(ChatModelPicker, {
      props: { modelValue: null, feature: 'PersonalChat' }
    })
    await new Promise(resolve => setTimeout(resolve, 0))

    expect(wrapper.find('[data-testid="reasoning-effort-row"]').exists()).toBe(true)
  })

  it('persists the selected effort to localStorage per-feature', async () => {
    mockGET.mockResolvedValue({ data: [reasoningModel] })
    const wrapper = await mountSuspended(ChatModelPicker, {
      props: { modelValue: null, feature: 'PersonalChat' }
    })
    await new Promise(resolve => setTimeout(resolve, 0))

    const highButton = wrapper.findAll('[data-testid^="effort-option-"]')
      .find(el => el.text() === 'High')
    await highButton?.trigger('click')

    expect(localStorage.getItem('hydraforge:chat:preferredEffort:PersonalChat')).toBe('high')
  })
})
```

This mirrors `ChatSessionView.test.ts`'s own harness (`mockNuxtImport('useApi', ...)` returning `{ GET: mockGET }`, `mockGET.mockReset()` + a fresh `.mockResolvedValue(...)` per test) — `ChatModelPicker.vue` calls `useApi().GET(...)` the same way `ChatSessionView.vue` does.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd src/web-ui && pnpm vitest run app/components/chat/__tests__/ChatModelPicker.test.ts`
Expected: FAIL — no `data-testid="reasoning-effort-row"` or `effort-option-*` elements exist yet, and `modelValue`/`effort` model wiring doesn't exist yet.

- [ ] **Step 4: Implement the effort row**

In `src/web-ui/app/components/chat/ChatModelPicker.vue`, add a second `defineModel` for the effort value, effort constants, and the persistence/selection logic:

```typescript
const modelId = defineModel<string | null>({ default: null })
const effort = defineModel<string | null>('effort', { default: null })

const EFFORT_LEVELS = [
  { value: 'low', label: 'Low' },
  { value: 'medium', label: 'Medium' },
  { value: 'high', label: 'High' }
]

const effortStorageKey = computed(() => `hydraforge:chat:preferredEffort:${props.feature}`)

function selectEffort(value: string) {
  effort.value = value
  if (import.meta.client) {
    localStorage.setItem(effortStorageKey.value, value)
  }
}
```

Add a shared effort-resolution helper and route both `selectModel` (explicit user pick) and `fetchModels`'s auto-select-on-mount (~line 65, `modelId.value = savedIsValid ? saved! : models.value[0]!.providerModelConfigId`) through it — a model without `supportsReasoning` should never carry a stale effort value forward, and the initial auto-selected model on page load needs the same resolution as an explicit click, or the effort row renders with no level highlighted until the user re-clicks the model:

```typescript
function applyEffortForModel(id: string) {
  const model = models.value.find(m => m.providerModelConfigId === id)
  if (model?.supportsReasoning) {
    const saved = import.meta.client ? localStorage.getItem(effortStorageKey.value) : null
    effort.value = saved ?? 'medium'
  } else {
    effort.value = null
  }
}

function selectModel(id: string) {
  modelId.value = id
  if (import.meta.client) {
    localStorage.setItem(storageKey.value, id)
  }
  applyEffortForModel(id)
  open.value = false
}
```

Update `fetchModels` (~line 56-71) to call the same helper right after resolving the initial `modelId.value`:

```typescript
async function fetchModels() {
  loading.value = true
  try {
    const { data } = await api.GET<AvailableModelDto[]>(ApiRoutes.Llm.models(props.feature))
    models.value = data ?? []
    if (models.value.length === 0) return

    const saved = import.meta.client ? localStorage.getItem(storageKey.value) : null
    const savedIsValid = !!saved && models.value.some(m => m.providerModelConfigId === saved)
    modelId.value = savedIsValid ? saved! : models.value[0]!.providerModelConfigId
    applyEffortForModel(modelId.value)
  } catch {
    // No models configured/reachable — chat falls back to server-side tier routing
  } finally {
    loading.value = false
  }
}
```

Add the effort row to the popover's `#content` template, directly under the `UCommandPalette`:

```html
<template #content>
  <UCommandPalette
    :groups="groups"
    placeholder="Type to filter models..."
    class="w-80 max-h-96"
    :ui="{ input: 'text-sm' }"
  />
  <div
    v-if="selectedModel?.supportsReasoning"
    data-testid="reasoning-effort-row"
    class="flex items-center gap-1 border-t border-gray-200 dark:border-gray-700 px-3 py-2"
  >
    <span class="text-xs text-muted mr-1">Effort:</span>
    <UButton
      v-for="level in EFFORT_LEVELS"
      :key="level.value"
      :data-testid="`effort-option-${level.value}`"
      size="xs"
      :variant="effort === level.value ? 'solid' : 'ghost'"
      :color="effort === level.value ? 'primary' : 'neutral'"
      @click="selectEffort(level.value)"
    >
      {{ level.label }}
    </UButton>
  </div>
</template>
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd src/web-ui && pnpm vitest run app/components/chat/__tests__/ChatModelPicker.test.ts`
Expected: PASS

- [ ] **Step 6: Run the full Web UI test suite and typecheck**

Run: `cd src/web-ui && pnpm vitest run && pnpm typecheck` (or the project's equivalent typecheck script if `typecheck` isn't the exact name — check `package.json`'s `scripts` block first)
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/web-ui/app/types/chat.ts \
  src/web-ui/app/components/chat/ChatModelPicker.vue \
  src/web-ui/app/components/chat/__tests__/ChatModelPicker.test.ts
git commit -m "feat: add reasoning-effort row to ChatModelPicker"
```

---

### Task 9: Web UI — thread effort through `ChatInput` → `ChatSessionView` → `useChatStream`

**Files:**
- Modify: `src/web-ui/app/composables/useChatStream.ts`
- Modify: `src/web-ui/app/components/chat/ChatInput.vue`
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue`
- Test: `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts`

**Interfaces:**
- Consumes: `ChatModelPicker.vue`'s `effort` model (Task 8). `GenerateReplyRequest.ReasoningEffort` (Task 6, server-side POST body field).
- Produces: `useChatStream().send`/`resend`'s new `reasoningEffort` parameter (5th positional, after `preferredProviderModelConfigId`).

- [ ] **Step 1: Add the parameter to `useChatStream`**

In `src/web-ui/app/composables/useChatStream.ts`, update `send` (~line 338):

```typescript
async function send(
  sessionId: string,
  content: string,
  presetId?: string,
  preferredProviderModelConfigId?: string,
  reasoningEffort?: string | null
): Promise<{ userMessage: ChatMessageDto, streamStarted: boolean } | undefined> {
  if (sendingLock.value) return
  sendingLock.value = true
  try {
    const result = await api.POST<ChatMessageDto>(
      ApiRoutes.Chat.sessions.sendMessage(sessionId),
      { body: { content }, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) }
    )
    if (!result.data) throw new Error('Failed to send message: no response')
    const userMessage = result.data

    try {
      await api.POST(ApiRoutes.Chat.sessions.generateReply(sessionId, userMessage.id), {
        body: {
          presetId: presetId ?? null,
          preferredProviderModelConfigId: preferredProviderModelConfigId ?? null,
          reasoningEffort: reasoningEffort ?? null
        },
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
      })
      return { userMessage, streamStarted: true }
    } catch {
      toast.error('Message saved, but the AI reply could not start — try again from the message.')
      return { userMessage, streamStarted: false }
    }
  } finally {
    sendingLock.value = false
  }
}
```

Update `resend` (~line 376) the same way:

```typescript
async function resend(
  sessionId: string,
  userMessageId: string,
  presetId?: string,
  preferredProviderModelConfigId?: string,
  reasoningEffort?: string | null
) {
  if (sendingLock.value) return
  sendingLock.value = true
  try {
    await api.POST(ApiRoutes.Chat.sessions.generateReply(sessionId, userMessageId), {
      body: {
        presetId: presetId ?? null,
        preferredProviderModelConfigId: preferredProviderModelConfigId ?? null,
        reasoningEffort: reasoningEffort ?? null
      },
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
    })
  } catch {
    toast.error('Could not start the reply — try again.')
  } finally {
    sendingLock.value = false
  }
}
```

- [ ] **Step 2: Thread it through `ChatInput.vue`**

In `src/web-ui/app/components/chat/ChatInput.vue`, add the effort ref and pass it to `ChatModelPicker` and the `send` emit:

```typescript
const emit = defineEmits<{
  send: [
    content: string,
    presetId?: string | null,
    preferredModelId?: string | null,
    reasoningEffort?: string | null
  ]
  cancel: []
}>()
```

```typescript
const selectedModelId = ref<string | null>(null)
const selectedEffort = ref<string | null>(null)
```

```typescript
function submit() {
  const trimmed = content.value.trim()
  if (!trimmed || props.disabled) return
  emit('send', trimmed, selectedPresetId.value, selectedModelId.value, selectedEffort.value)
  content.value = ''
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }
}
```

Update the `ChatModelPicker` usage in the template (~line 143) to bind the new `effort` model:

```html
<ChatModelPicker
  v-model="selectedModelId"
  v-model:effort="selectedEffort"
  :feature="feature"
  :disabled="disabled"
/>
```

- [ ] **Step 3: Write the failing test for `ChatSessionView`'s pass-through**

`ChatSessionView.test.ts` has no existing test that exercises `ChatInput`'s `send` emit (its current tests cover rename/export/find-in only), so this is new coverage, not an extension of an existing case. Add it as a new top-level `describe` block, reusing the file's existing `mockChatStream`, `baseSession`, `stubs`, and `mountView()` — import the real `ChatInput` component to locate the stub via `findComponent` (VTU matches auto-stubbed children by their original component object, per the `stubs = { ChatInput: true, ... }` already declared at the top of this file):

```typescript
import ChatInput from '~/components/chat/ChatInput.vue'

describe('ChatSessionView — send with reasoning effort', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { ...baseSession }, error: undefined })
    mockChatStream.send.mockReset()
    mockChatStream.send.mockResolvedValue({
      userMessage: { id: 'm1', sessionId: 's1', role: 'User', content: 'hello' },
      streamStarted: true
    })
  })

  it('passes the selected reasoning effort through to chatStream.send', async () => {
    const wrapper = await mountView()
    const chatInput = wrapper.findComponent(ChatInput)

    await chatInput.vm.$emit('send', 'hello', null, 'model-1', 'high')
    await flushPromises()

    expect(mockChatStream.send).toHaveBeenCalledWith('s1', 'hello', null, 'model-1', 'high')
  })
})
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `cd src/web-ui && pnpm vitest run app/components/chat/__tests__/ChatSessionView.test.ts -t "passes the selected reasoning effort"`
Expected: FAIL — `handleSend` doesn't yet accept or forward a 4th argument.

- [ ] **Step 5: Wire `handleSend` and the regenerate call site in `ChatSessionView.vue`**

In `src/web-ui/app/components/chat/ChatSessionView.vue`, update `handleSend` (lines 207-255) — only the parameter list and the `chatStream.send(...)` call gain the new argument, every other line is unchanged:

```typescript
async function handleSend(
  content: string,
  presetId?: string | null,
  preferredModelId?: string | null,
  reasoningEffort?: string | null
) {
  if (!session.value) return

  streamError.value = null

  // Add user message optimistically
  const userMsg: ChatMessageDto = {
    id: randomId(),
    sessionId: props.sessionId,
    role: MessageRole.User,
    content,
    inputTokens: 0,
    outputTokens: 0,
    cachedTokens: 0,
    modelName: null,
    imagesJson: null,
    createdAt: new Date().toISOString()
  }
  session.value.messages.push(userMsg)

  try {
    const result = await chatStream.send(
      props.sessionId,
      content,
      presetId ?? undefined,
      preferredModelId ?? undefined,
      reasoningEffort ?? undefined
    )
    // undefined only when another send was already in flight (sendingLock) —
    // the optimistic message stays as-is, nothing to reconcile.
    if (result) {
      const idx = session.value.messages.findIndex(m => m.id === userMsg.id)
      if (idx !== -1) session.value.messages[idx] = result.userMessage
      if (result.streamStarted) {
        awaitingBaselineCount = session.value.messages.length
        awaitingReply.value = true
      }
    }
  } catch (err) {
    // Only reached if persisting the message itself failed — a failed/slow
    // reply (streamStarted: false) is not an error, the message was saved.
    const idx = session.value.messages.findIndex(m => m.id === userMsg.id)
    if (idx !== -1) session.value.messages.splice(idx, 1)
    toast.error(err instanceof Error ? err.message : 'Failed to send message')
  }
}
```

The regenerate call site (~line 360, `chatStream.resend(props.sessionId, precedingUserMessage.id)`) is intentionally left unchanged — regenerate re-invokes the same already-persisted user message without re-showing the composer, so there is no new effort selection to pass; it keeps using whatever the model was originally sent with (out of scope, matches the spec's "no per-request override independent of model choice").

- [ ] **Step 6: Run the test to verify it passes**

Run: `cd src/web-ui && pnpm vitest run app/components/chat/__tests__/ChatSessionView.test.ts -t "passes the selected reasoning effort"`
Expected: PASS

- [ ] **Step 7: Run the full Web UI test suite and typecheck**

Run: `cd src/web-ui && pnpm vitest run && pnpm typecheck`
Expected: PASS

- [ ] **Step 8: Manually verify the end-to-end flow**

With the API server running (Development env) and `pnpm dev` running: open a chat, select a model flagged `SupportsReasoning` in the admin Provider Models page (Task 7), confirm the effort row appears and defaults to Medium, pick High, send a message, and confirm (via server logs or a network inspector) that the outbound `POST .../generate` body contains `"reasoningEffort":"high"`.

- [ ] **Step 9: Commit**

```bash
git add src/web-ui/app/composables/useChatStream.ts \
  src/web-ui/app/components/chat/ChatInput.vue \
  src/web-ui/app/components/chat/ChatSessionView.vue \
  src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts
git commit -m "feat: thread reasoning effort from ChatInput through to the generate-reply request"
```
