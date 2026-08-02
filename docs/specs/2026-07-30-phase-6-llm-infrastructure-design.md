# Phase 6 — LLM Infrastructure

**Branch:** `feat/phase-6-llm-infrastructure`
**Date:** 2026-07-30
**Status:** Design — ready for implementation planning
**Phase goal:** All AI plumbing in place before any chat or AI feature is built on top (Phase 7+).

## Context

Phase 1–5 shipped: Clean Architecture skeleton, EF Core + pgvector schema, auth, ProblemDetails/correlation, health probes, audit, project-space API + Domain, Web UI, TUI, notifications + admin foundation. The LLM & routing entities (`LlmProvider`, `ProviderModelConfig`, `FeatureRoutingConfig`, `UserTokenBudget`, `TokenUsageRecord`, `ImageUsageRecord`) already exist in Domain and are mapped by `HydraForgeDbContext`. `AdapterType`, `ProviderType`, `ModelTier`, `AiFeature` enums exist. `LlmProviderHealthProbe` reports "configured vs not" on `/health`. `LlmProvider.ApiKeyEncrypted` is a placeholder string — no encryption yet.

Phase 6 builds the runtime: client abstractions, adapters, encryption, router, compressor, usage recording, budget enforcement, admin UI, and the Hangfire recurring job for nightly `AiNarrative` generation. No chat surface is built here — Phase 7 consumes `StreamChatAsync` via SignalR/SSE.

## Approved Decisions

| ID | Decision |
|---|---|
| **A** | Add `DallE` + `StabilityAi` enum values to `AdapterType`. Implement 3 image adapters: `DallEAdapter`, `StabilityAiAdapter`, `ComfyUiAdapter`. `ComfyUiAdapter` handles both `ComfyUi` and `Diffusers` adapter types (local/self-hosted image generation). |
| **B** | Token estimation uses heuristic `chars / 4` (no tokenizer dependency). Used for pre-flight context-window checks and budget accrual estimates; provider-reported token counts remain authoritative for `TokenUsageRecord`. |
| **C** | API keys encrypted with **AES-GCM** (`AesGcm`). Encryption key from `Llm:EncryptionKey` config (base64-encoded 32-byte AES-256 key), validated at startup. `IKeyVault` port in Application; `AesGcmKeyVault` impl in Infrastructure. |
| **D** | `StreamChatAsync` returns `IAsyncEnumerable<ChatChunk>` now. Phase 7 adds the SignalR/SSE transport that bridges the `IAsyncEnumerable` to clients. |

### Approved Assumptions

1. `Llm:EncryptionKey` = base64 32-byte AES-256 key, validated at startup (fail-fast: server refuses to start if missing/invalid).
2. Phase 6 includes Web UI for admin provider/routing/usage management **and** user self-service usage view.
3. `StreamChatAsync` returns `IAsyncEnumerable<ChatChunk>`; Phase 7 adds SignalR/SSE transport.
4. Budget enforcement via existing `UserTokenBudget` entity fields (`MonthlyTokenBudget`/`MonthlyTokenUsed`/`MonthlyImageBudget`/`MonthlyImageUsed`/`PeriodStart`/`PeriodEnd`); failure → `TOKEN_BUDGET_EXCEEDED` error.
5. `FeatureRoutingConfig` seeded with sensible defaults on startup (one row per `AiFeature`, `DefaultTier = Standard`, `MaxUserTier = null`).
6. Usage-record (`TokenUsageRecord`, `ImageUsageRecord`) retention deferred to `HousekeepingBackgroundService` (not built in Phase 6 — only the retention knob `AuditLogRetentionDays` already covers them per `data-model.md`).

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Application Layer                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │ ModelRouter  │  │ContextCompr.  │  │ ILlmClient /      │   │
│  │ (port)      │  │ (port)        │  │ IImageClient /     │   │
│  │              │  │              │  │ IEmbeddingClient   │   │
│  │              │  │              │  │ (ports)            │   │
│  └──────┬───────┘  └──────────────┘  └─────────┬──────────┘   │
│         │                                       │              │
│         │           IKeyVault (port)            │              │
│         │                  ▲                    │              │
└─────────┼──────────────────┼────────────────────┼─────────────┘
          │                  │                    │
┌─────────▼──────────────────▼────────────────────▼─────────────┐
│  Infrastructure Layer                                         │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────┐ │
│  │LlmClientFactory│ │AesGcmKeyVault│ │ Adapters             │ │
│  │ (resolves     │  │              │ │  OpenAiCompatible     │ │
│  │  AdapterType  │  │              │ │  Anthropic            │ │
│  │  → ILlmClient)│  │              │ │  Ollama               │ │
│  └──────────────┘  └──────────────┘ │  DallE / StabilityAi  │ │
│                                     │  ComfyUi (ComfyUi+Diff)│ │
│                                     └─────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**Layering rules:**
- Ports (`ILlmClient`, `IImageClient`, `IEmbeddingClient`, `IModelRouter`, `IContextCompressor`, `IKeyVault`, `IUsageRecorder`, `ILlmAdminService`) live in Application. Domain stays free of HTTP/SDK concerns.
- Adapters, factory, encryption impl, EF-backed services live in Infrastructure.
- `ModelRouter` is an Application service that depends on ports + Domain entities only — never on `HttpClient` or provider SDKs directly.
- Server is the **only** component that calls LLMs (D-11). TUI and Web UI never call LLMs directly.

## Data Model

All entities below already exist and are mapped. Phase 6 adds **no new tables**. Changes are limited to: enum additions, seeding, and reconciling `data-model.md` with the richer `UserTokenBudget` entity fields.

### Enum change — `AdapterType`

Add two values (decision A):

```csharp
public enum AdapterType
{
    OpenAiCompatible = 1,
    Anthropic = 2,
    Ollama = 3,
    Diffusers = 4,
    ComfyUi = 5,
    DallE = 6,        // new — OpenAI image generation
    StabilityAi = 7,  // new — Stability AI image generation
}
```

`ComfyUiAdapter` serves both `AdapterType.ComfyUi` and `AdapterType.Diffusers` (decision A — local/self-hosted image generation via ComfyUI workflow API or diffusers pipeline). `LlmClientFactory` maps both enum values to the same adapter instance.

### `LlmProvider` (existing — no schema change)

`ApiKeyEncrypted` becomes a real AES-GCM ciphertext blob (base64). A migration backfills existing plaintext placeholder rows (none expected in prod, but the migration must be idempotent: re-encrypt rows whose `ApiKeyEncrypted` is empty or matches a known sentinel). New `Create`/`Update` paths encrypt before persist; reads decrypt on demand via `IKeyVault`.

### `ProviderModelConfig` (existing — no schema change)

Already carries `ModelId`, `Name`, `Tier`, `PricePerToken`, `MaxTokens`, `IsEnabled`. `MaxTokens` is the context-window guard input. No change.

### `FeatureRoutingConfig` (existing entity — needs seeding)

Already mapped. Startup seeder inserts one row per `AiFeature` value if the table is empty:

| Feature | DefaultTier | MaxUserTier |
|---|---|---|
| PersonalChat | Standard | Premium |
| ProjectChat | Standard | Premium |
| DeepResearch | Premium | null (locked) |
| AgentPipeline | Premium | null (locked) |
| MemoryExtraction | Economy | Standard |
| NotesClassification | Economy | Standard |
| DocumentEditing | Standard | Premium |
| CardReview | Standard | Premium |
| ImageChat | Standard | Premium |
| ImageDocument | Standard | Premium |
| ImageGalleryEditor | Economy | Standard |

`null` MaxUserTier = locked to default (admin-only override). `FeatureRoutingConfigSeeder` is registered via `AddScoped` in `Program.cs` and invoked inside the migration startup scope alongside `AdminSeeder` — same startup pattern, not inside `PersistenceServiceCollectionExtensions`.

### `UserTokenBudget` (existing — reconcile docs)

The entity already has the richer budget-tracking fields. `data-model.md` currently shows a stale simpler shape. **Doc-only fix**: update `data-model.md` §`UserTokenBudget` to match the entity:

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| MonthlyTokenBudget | int | Monthly token cap (0 = unlimited) |
| MonthlyTokenUsed | int | Accumulated tokens this period |
| MonthlyImageBudget | int | Monthly image-generation cap (0 = unlimited) |
| MonthlyImageUsed | int | Accumulated images this period |
| PeriodStart | DateTime | Start of current billing period |
| PeriodEnd | DateTime | End of current billing period (auto-rollover when passed) |

Budget enforcement (assumption 4) reads `MonthlyTokenUsed` against `MonthlyTokenBudget` before each call; on rollover (now > `PeriodEnd`), reset `Used` counters and advance `PeriodStart`/`PeriodEnd` by one month.

### `TokenUsageRecord` / `ImageUsageRecord` (existing — no schema change)

Written by `IUsageRecorder` after every call. Denormalized `ProviderId`/`ModelId`/`ModelName` preserved per data-model note. Retention handled later by `HousekeepingBackgroundService` using `AuditLogRetentionDays` (assumption 6) — no Phase 6 cleanup job.

## Interfaces (Application ports)

All in `HydraForge.Application/Llm/`.

### `ILlmClient`

```csharp
public interface ILlmClient
{
    AdapterType AdapterType { get; }

    /// <summary>Streamed chat completion. Phase 7 bridges this to SignalR/SSE.</summary>
    IAsyncEnumerable<ChatChunk> StreamChatAsync(
        ChatRequest request,
        CancellationToken ct = default);

    /// <summary>Fetch live model list from the provider (admin probe / model picker).</summary>
    Task<Result<IReadOnlyList<ProviderModel>>> GetModelsAsync(CancellationToken ct = default);

    bool SupportsToolCalling(ProviderModelConfig model);
}

public sealed record ChatRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<CacheBlock> CacheBlocks,   // prompt-caching eligible blocks
    IReadOnlyList<ToolDefinition> Tools,
    int? MaxOutputTokens,
    decimal? Temperature);

public sealed record ChatMessage(ChatRole Role, string Content);

public sealed record CacheBlock(string Content, CacheBlockType Type, bool IsPinned = false);

public enum ChatRole { System, User, Assistant, Tool }
public enum CacheBlockType { SystemContext, ProjectSnapshot, Memory }

public sealed record ChatChunk(
    string? Delta,                 // null on final chunk
    ChatChunkFinishReason? FinishReason,
    UsageSnapshot? Usage);         // populated on final chunk

public sealed record UsageSnapshot(
    int InputTokens,
    int OutputTokens,
    int CachedTokens);

public enum ChatChunkFinishReason { Stop, Length, ToolCalls, ContentFilter }
```

### `IImageClient`

```csharp
public interface IImageClient
{
    AdapterType AdapterType { get; }
    Task<Result<GeneratedImage>> GenerateImageAsync(ImageRequest request, CancellationToken ct = default);
    Task<Result<GeneratedImage>> InpaintAsync(InpaintRequest request, CancellationToken ct = default);
}

public sealed record ImageRequest(
    Guid ProviderModelConfigId, string ModelId, string Prompt,
    ImageSize Size, int Count);
public sealed record InpaintRequest(
    Guid ProviderModelConfigId, string ModelId, string Prompt,
    byte[] ImageBytes, byte[] MaskBytes, ImageSize Size, int Count = 1);
public sealed record GeneratedImage(IReadOnlyList<string> ImageDataUrlsOrKeys, string Resolution);
public enum ImageSize { Square1024, Landscape1792, Portrait1024 }
```

### `IEmbeddingClient`

```csharp
public interface IEmbeddingClient
{
    Task<Result<EmbeddingResult>> EmbedAsync(EmbeddingRequest request, CancellationToken ct = default);
}
public sealed record EmbeddingRequest(Guid ProviderModelConfigId, string ModelId, IReadOnlyList<string> Inputs);
public sealed record EmbeddingResult(IReadOnlyList<ReadOnlyMemory<float>> Vectors); // 1536-dim per schema
```

### `IModelRouter`

```csharp
public interface IModelRouter
{
    /// <summary>Resolve feature + user context → the provider/model to call, with fallback chain.</summary>
    Task<Result<RouteDecision>> ResolveAsync(AiFeature feature, Guid userId, Guid? projectId, int estimatedTokens, CancellationToken ct = default);
}
public sealed record RouteDecision(
    ProviderModelConfig Primary,
    LlmProvider PrimaryProvider,
    IReadOnlyList<FallbackProvider> Fallbacks,
    LlmProvider? Provider = null);

public sealed record FallbackProvider(ProviderModelConfig Model, LlmProvider Provider);
```

### `IContextCompressor`

```csharp
public interface IContextCompressor
{
    /// <summary>If injected context exceeds threshold, summarize the oldest non-pinned blocks. Returns (possibly compressed) blocks + estimated tokens.</summary>
    Task<Result<CompressedContext>> CompressAsync(IReadOnlyList<CacheBlock> blocks, int modelMaxTokens, CancellationToken ct = default);
}
public sealed record CompressedContext(IReadOnlyList<CacheBlock> Blocks, int EstimatedTokens, bool WasCompressed);
```

### `IKeyVault`

```csharp
public interface IKeyVault
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
```

### `IUsageRecorder`

```csharp
public interface IUsageRecorder
{
    Task RecordTokenAsync(TokenUsageRecordInput input, CancellationToken ct = default);
    Task RecordImageAsync(ImageUsageRecordInput input, CancellationToken ct = default);
    Task<int> AccrueTokenUsageAsync(Guid userId, int tokens, CancellationToken ct = default); // returns new used total
    Task<int> AccrueImageUsageAsync(Guid userId, int count, CancellationToken ct = default);
}
```

## Adapters (Infrastructure)

`src/HydraForge.Infrastructure/Llm/Adapters/`.

### Text adapters

| Adapter | `AdapterType` | Notes |
|---|---|---|
| `OpenAiCompatibleAdapter` | `OpenAiCompatible` | Covers OpenAI, Groq, DeepSeek, OpenRouter, vLLM, llama.cpp. Uses `HttpClient` against `{BaseUrl}/chat/completions` (SSE `stream: true`). Cache blocks rendered as repeated system messages with a stable hash prefix (OpenAI auto-caches by prefix). |
| `AnthropicAdapter` | `Anthropic` | `{BaseUrl}/v1/messages`. Emits `cache_control: { type: "ephemeral" }` on system + project-snapshot blocks (prompt caching). Maps `UsageSnapshot` from `message_delta` `usage`. |
| `OllamaAdapter` | `Ollama` | `{BaseUrl}/api/chat` (NDJSON stream). No prompt caching — `CachedTokens` always 0. |

### Image adapters (decision A)

| Adapter | `AdapterType`(s) | Notes |
|---|---|---|
| `DallEAdapter` | `DallE` | OpenAI `/images/generations` + `/images/edits` (inpaint). Returns image URLs/keys. |
| `StabilityAiAdapter` | `StabilityAi` | Stability AI `generate` + `inpaint` endpoints. |
| `ComfyUiAdapter` | `ComfyUi` **and** `Diffusers` | ComfyUI workflow API (`/prompt` + `/history`). `LlmClientFactory` maps both enum values to this adapter. Diffusers path uses a local diffusers pipeline via the same workflow surface if `Diffusers` selected. |

### `LlmClientFactory`

```csharp
public interface ILlmClientFactory
{
    ILlmClient For(LlmProvider provider);
    IImageClient ImageFor(LlmProvider provider);
    IEmbeddingClient EmbeddingFor(LlmProvider provider);

    /// <summary>Evicts cached client instances for a provider (e.g. after config update/deletion).
    /// Next For/ImageFor/EmbeddingFor call creates a fresh instance.</summary>
    void Invalidate(Guid providerId);
}
```

Resolves adapter by `provider.AdapterType`. Singletons keyed by provider id; `HttpClient` injected from `IHttpClientFactory` (named clients per `AdapterType`). API key decrypted via `IKeyVault` at call time, never cached in adapter state.

## Encryption (decision C, assumption 1)

`src/HydraForge.Infrastructure/Llm/AesGcmKeyVault.cs`.

- Algorithm: `System.Security.Cryptography.AesGcm` (AES-256-GCM, .NET built-in — no new NuGet).
- Key source: `Llm:EncryptionKey` config — base64-encoded 32-byte key.
- Startup validation: `PersistenceServiceCollectionExtensions` (or a dedicated `ValidateLlmEncryption`) decodes base64, asserts length == 32. Missing or invalid → `InvalidOperationException` → server refuses to start (fail-fast, assumption 1).
- Ciphertext format: `v1:{nonceBase64}:{ciphertextBase64}:{tagBase64}` (versioned prefix for future rotation).
- `Encrypt(plaintext)` → ciphertext string; `Decrypt(ciphertext)` → plaintext. Throws on tag mismatch (tamper detection).
- Key rotation: out of scope for Phase 6. `v1:` prefix leaves room.

## ModelRouter

`src/HydraForge.Application/Llm/ModelRouter.cs` (Application service, depends on ports + Domain).

Routing algorithm (matches `architecture.md` §ModelRouter):

1. Look up `FeatureRoutingConfig` for the requested `AiFeature` → `DefaultTier`.
2. Apply user tier ceiling (`MaxUserTier`): if user has an override > ceiling, cap to ceiling. `null` ceiling = locked to default.
3. Context-window guard: if `estimatedTokens` (decision B, `chars/4`) > `PrimaryProviderModelConfig.MaxTokens`, auto-bump tier up (Economy→Standard→Premium) until a model fits or no higher tier exists.
4. Select `ProviderModelConfig` at resolved tier on an enabled `LlmProvider`.
5. Build fallback chain from `LlmProvider.FallbackProviderId` (walk until null or cycle).
6. Return `RouteDecision`.

**Call-time enforcement** (in the calling service, not the router itself):
- Budget check via `IUsageRecorder.AccrueTokenUsageAsync` (or pre-check `UserTokenBudget.MonthlyTokenUsed + estimatedTokens > MonthlyTokenBudget`) → `TOKEN_BUDGET_EXCEEDED` error (assumption 4). Image calls accrue `MonthlyImageUsed`.
- On provider rate-limit (429) / 5xx: retry next fallback in chain. All fallbacks exhausted → `LLM_PROVIDER_UNAVAILABLE` (existing code).
- After successful call: `IUsageRecorder.RecordTokenAsync` with provider-reported `UsageSnapshot` (authoritative — overrides the `chars/4` estimate).

### New error codes (`DomainErrorCodes.cs`)

```csharp
public static class Llm
{
    public const string ProviderUnavailable = "LLM_PROVIDER_UNAVAILABLE"; // already exists under Infrastructure — relocate/alias
    public const string TokenBudgetExceeded = "TOKEN_BUDGET_EXCEEDED";
    public const string ImageBudgetExceeded = "IMAGE_BUDGET_EXCEEDED";
    public const string NoModelForFeature = "LLM_NO_MODEL_FOR_FEATURE";
    public const string ContextWindowExceeded = "LLM_CONTEXT_WINDOW_EXCEEDED";
    public const string EncryptionKeyInvalid = "LLM_ENCRYPTION_KEY_INVALID";
}
```

`LLM_PROVIDER_UNAVAILABLE` currently lives under `Infrastructure.*` — keep alias for back-compat, add the `Llm.*` group as the canonical home.

## ContextCompressor

`src/HydraForge.Application/Llm/ContextCompressor.cs`.

- Threshold: `modelMaxTokens * 0.75` (configurable via `Llm:ContextCompressionThresholdRatio`, default 0.75).
- Input: ordered `CacheBlock` list (system context first, then project snapshot, then memory). Pinned memory blocks (`MemoryEntry.IsPinned`) are never compressed.
- Strategy: if estimated tokens exceed threshold, summarize the oldest non-pinned memory blocks via a cheap `ILlmClient` call (Economy tier, `MemoryExtraction` feature). Replace summarized blocks with a single `CacheBlock` of type `Memory` containing the summary.
- Returns `CompressedContext` with `WasCompressed` flag for observability.
- Phase 6 ships the port + a no-op/passthrough default impl + the real summarizing impl. The summarizing impl depends on `IModelRouter` (to pick an Economy model) — wire carefully to avoid circular DI (router → client factory → ... never back to compressor).

## Token / Image Usage Recording

`IUsageRecorder` (EF-backed impl in Infrastructure). Flow:

1. Pre-call: budget check (`MonthlyTokenUsed + estimatedTokens` vs `MonthlyTokenBudget`; 0 = unlimited).
2. Call executes via adapter.
3. Post-call: write `TokenUsageRecord` with provider-reported `InputTokens`/`OutputTokens`/`CachedTokens`/`Cost` (cost computed from `ProviderModelConfig.PricePerToken` if set, else 0).
4. Accrue: `MonthlyTokenUsed += OutputTokens + InputTokens - CachedTokens` (cached tokens billed at reduced/zero rate per provider; conservative default: full input+output, cached subtracted).
5. Image: write `ImageUsageRecord` (one row per request, `ImageCount` field), accrue `MonthlyImageUsed += ImageCount`.

Budget rollover: when `DateTime.UtcNow > PeriodEnd`, reset `MonthlyTokenUsed`/`MonthlyImageUsed` to 0 and advance `PeriodStart`/`PeriodEnd` by one month. Done lazily on next budget check (no scheduled job needed).

## Budget Enforcement (assumption 4)

- Enforced at call time in the Application service that invokes `ILlmClient.StreamChatAsync` (Phase 7 chat service) — not inside the adapter. Keeps adapters pure transport.
- Pre-call: `IUsageRecorder.AccrueTokenUsageAsync` is **not** called pre-flight (would double-count on failure). Instead, read `UserTokenBudget` directly; if `MonthlyTokenBudget > 0 && MonthlyTokenUsed + estimatedTokens > MonthlyTokenBudget` → return `Result.Failure(Llm.TokenBudgetExceeded)`.
- Post-call: accrue actuals.
- `TOKEN_BUDGET_EXCEEDED` / `IMAGE_BUDGET_EXCEEDED` are expected failures → `Result<T, Error>`, mapped to `402 Payment Required` (or `429 Too Many Requests` — **open question below**).

> **Open question (non-blocking):** budget-exceeded HTTP status — `402` (semantically "payment required") vs `429` (rate-limit semantics). Default proposal: `429` with `Retry-After` hinting next period rollover. Confirm during plan-writing.

## Admin UI (assumption 2)

Web UI under `src/web-ui/app/pages/admin/`. New pages + nav entries. TUI admin screens are Phase 4 parity scope — **deferred** to a follow-up (TUI has no admin screens yet; Phase 6 ships Web UI admin only, TUI admin is out of scope).

### New pages

| Route | Page | Purpose |
|---|---|---|
| `/admin/providers` | `providers.vue` | LLM + image provider CRUD. Add/edit/disable provider: name, base URL, adapter type, provider type, tier, fallback, API key (write-only field, never displayed back). "Probe models" button → `GET /api/admin/providers/{id}/models`. |
| `/admin/providers/models` | `provider-models.vue` | `ProviderModelConfig` CRUD per provider: model id, display name, tier, price/token, max tokens, enabled. |
| `/admin/routing` | `routing.vue` | `FeatureRoutingConfig` table: one row per `AiFeature`, edit `DefaultTier` + `MaxUserTier`. |
| `/admin/usage` | `usage.vue` | Token + image usage dashboard: filter by user / feature / model / period. `DataTable` with `fillHeight`. Aggregated cost column. |
| `/account/usage` | `account/usage.vue` | User self-service: own token + image usage for current period, budget remaining, recent calls. |

### Nav

Add `Providers`, `Routing`, `Usage` to admin nav (extend `UiRoutes.Admin` + `ApiRoutes.Admin` in `app/lib/routes.ts`). `account/usage` linked from user menu.

### API surface (`AdminController` extensions)

```
GET    /api/admin/providers
POST   /api/admin/providers
GET    /api/admin/providers/{id}
PUT    /api/admin/providers/{id}
DELETE /api/admin/providers/{id}            (disable, not hard delete)
GET    /api/admin/providers/{id}/models      (live probe via adapter GetModelsAsync)
POST   /api/admin/providers/{id}/models      (add ProviderModelConfig)
PUT    /api/admin/providers/{id}/models/{modelId}
DELETE /api/admin/providers/{id}/models/{modelId}
GET    /api/admin/routing
PUT    /api/admin/routing/{feature}
GET    /api/admin/usage/tokens?...          (paginated, filtered)
GET    /api/admin/usage/images?...
GET    /api/admin/users/{userId}/budget
PUT    /api/admin/users/{userId}/budget
GET    /api/account/usage                   (current user, self-service)
```

All admin endpoints behind `[Authorize(Policy = AuthPolicies.AdminRequired)]` (existing pattern). Self-service `/api/account/usage` behind standard auth — scoped to caller's `userId` only.

### `ILlmAdminService` (Application)

```csharp
public interface ILlmAdminService
{
    Task<Result<ProviderPageDto>> ListProvidersAsync(...);
    Task<Result<ProviderDto>> CreateProviderAsync(CreateProviderInput input, CancellationToken ct);
    Task<Result<ProviderDto>> UpdateProviderAsync(Guid id, UpdateProviderInput input, CancellationToken ct);
    Task<Result> DisableProviderAsync(Guid id, CancellationToken ct);
    Task<Result<IReadOnlyList<ProviderModelDto>>> ProbeModelsAsync(Guid providerId, CancellationToken ct);
    // ... model config CRUD, routing CRUD, usage queries, budget get/set
}
```

Input DTOs carry `ApiKey` as a plain string; service encrypts via `IKeyVault` before persisting. Update with null `ApiKey` = leave existing key unchanged.

## Hangfire (D-57)

Already decided. Phase 6 wires it:

- NuGet: `Hangfire.AspNetCore`, `Hangfire.PostgreSql` (same Postgres instance — no new infra).
- `Program.cs`: `services.AddHangfire(c => c.UsePostgreSqlStorage(...))` + `services.AddHangfireServer()`.
- Dashboard at `/hangfire` behind `AdminRequired` auth filter.
- **One recurring job**: `RecurringJob.AddOrUpdate("ai-narrative-gen", () => ProjectContextSnapshotService.GenerateAiNarrativeForAllActiveProjectsAsync(default), Cron.Daily)` — admin-configurable time via `SystemSettings` (new field `AiNarrativeGenerationTimeUtc`, default `00:00`). Per D-32, `TemplateContent` stays instant-on-mutation; only `AiNarrative` is nightly.
- `ProjectContextSnapshotService.GenerateAiNarrativeForAllActiveProjectsAsync` iterates active projects, calls `ModelRouter` → `ILlmClient.StreamChatAsync` with `ProjectContextSnapshot.TemplateContent` as a cache block, persists `AiNarrative` + `AiNarrativeGeneratedAt`.
- Per-project failures are logged + retried by Hangfire's retry policy; one project's failure does not abort the batch (enqueue per-project jobs, or catch per-iteration).

### `SystemSettings` field addition

Add `TimeSpan? AiNarrativeGenerationTimeUtc` (default `00:00:00`) to `SystemSettings` + `UpdateSettings`. Migration included. Admin settings UI gains a time picker.

## Testing

- **Domain/Application (xUnit, no DB):** `ModelRouter` routing logic (tier ceiling, context-window auto-bump, fallback chain walk, cycle detection), `ContextCompressor` threshold logic, budget pre-check math, `AesGcmKeyVault` round-trip + tamper detection (pure crypto, no DB).
- **Infrastructure EF model tests (`AssertProperties`):** no new entities, but verify `FeatureRoutingConfig` + `UserTokenBudget` mappings unchanged after any tweaks.
- **Adapter tests:** use `HttpMessageHandler` fakes (no live calls). Assert request shape, SSE/NDJSON stream parsing, `cache_control` block emission for Anthropic, `UsageSnapshot` extraction from final chunk.
- **No live LLM calls in CI.** Optional integration tests gated behind `HYDRAFORGE_TEST_LLM_CONNECTION_STRING` (mirrors the DB-connection-string pattern) — not required for green.
- **Web UI:** component tests for admin pages with Nuxt stubs; E2E deferred (Playwright suite seeds via API — add provider-creation seed once a test LLM stub exists).

## Open Questions (non-blocking, resolve during planning)

1. Budget-exceeded HTTP status: `402` vs `429` (default proposal `429` + `Retry-After`).
2. Cached-token cost model: subtract fully from accrual, or provider-specific rate? (Default: subtract fully.)
3. `ComfyUiAdapter` `Diffusers` path: same workflow API, or a separate local-pipeline mode? (Default: workflow API for both; revisit if a bare-diffusers deployment is real.)
4. Should `ILlmAdminService.ProbeModelsAsync` persist probed models, or just return them for admin to pick? (Default: return only — admin selects which to configure as `ProviderModelConfig`.)

## Out of Scope (Phase 7+)

- Chat surface (ChatSession/ChatMessage CRUD, streaming transport via SignalR/SSE).
- RAG `DocumentChunk` pipeline (chunking/embedding/retrieval).
- Agent pipeline orchestration.
- TUI admin screens.
- Usage-record retention/hard-delete (HousekeepingBackgroundService — assumption 6).
- Key rotation tooling.

## Tasks

- [x] Task 1: Add `DallE` + `StabilityAi` to `AdapterType`; add `Llm.*` error codes (`TokenBudgetExceeded`, `ImageBudgetExceeded`, `NoModelForFeature`, `ContextWindowExceeded`, `EncryptionKeyInvalid`); reconcile `data-model.md` `UserTokenBudget` section with entity.
- [x] Task 2: `IKeyVault` port (Application) + `AesGcmKeyVault` impl (Infrastructure) + startup validation of `Llm:EncryptionKey`; migration to re-encrypt placeholder rows idempotently.
- [x] Task 3: Define Application ports: `ILlmClient`, `IImageClient`, `IEmbeddingClient`, `IModelRouter`, `IContextCompressor`, `IUsageRecorder`, `ILlmAdminService`, `ILlmClientFactory` + DTOs (`ChatRequest`/`ChatChunk`/`CacheBlock`/`RouteDecision`/`CompressedContext`/usage inputs).
- [x] Task 4: `OpenAiCompatibleAdapter` (SSE stream parse, cache-block prefix hashing, `UsageSnapshot` from final chunk, `GetModelsAsync`, `SupportsToolCalling`).
- [x] Task 5: `AnthropicAdapter` (`cache_control` ephemeral blocks, `/v1/messages` stream, `message_delta` usage mapping).
- [x] Task 6: `OllamaAdapter` (`/api/chat` NDJSON stream, no caching, usage from final message).
- [x] Task 7: `DallEAdapter` (`/images/generations` + `/images/edits` inpaint).
- [x] Task 8: `StabilityAiAdapter` (generate + inpaint endpoints).
- [x] Task 9: `ComfyUiAdapter` (workflow API; serves both `ComfyUi` + `Diffusers` adapter types via factory mapping).
- [x] Task 10: `LlmClientFactory` (resolve `AdapterType` → adapter; `IHttpClientFactory` named clients; decrypt key at call time via `IKeyVault`).
- [x] Task 11: `ModelRouter` service (feature config lookup, tier ceiling, context-window auto-bump, fallback chain walk + cycle guard) + unit tests.
- [x] Task 12: `ContextCompressor` service (threshold check, pinned-block preservation, Economy-tier summarize call) + unit tests; wire DI to avoid router cycle.
- [x] Task 13: `IUsageRecorder` EF impl: write `TokenUsageRecord`/`ImageUsageRecord`, accrue `UserTokenBudget` counters, lazy period rollover + unit tests.
- [x] Task 14: Budget pre-check + `TOKEN_BUDGET_EXCEEDED`/`IMAGE_BUDGET_EXCEEDED` enforcement in call-path service contract (router or a `LlmCallGuard` helper) + tests.
- [x] Task 15: `FeatureRoutingConfig` startup seeder (idempotent, one row per `AiFeature` per the default table).
- [ ] Task 16: `ILlmAdminService` + controller endpoints (providers CRUD, model config CRUD, live probe, routing CRUD, usage queries, budget get/set) + server tests (with `TestILlmClient`/`TestIKeyVault` stubs wired in every factory).
- [ ] Task 17: `/api/account/usage` self-service endpoint (caller-scoped) + tests.
- [ ] Task 18: Web UI — admin providers page (`/admin/providers`) + provider-models page + nav + `ApiRoutes.Admin`/`UiRoutes.Admin` extensions.
- [ ] Task 19: Web UI — admin routing page (`/admin/routing`).
- [ ] Task 20: Web UI — admin usage dashboard (`/admin/usage`, `DataTable` `fillHeight`, filters) + account self-service usage (`/account/usage`).
- [ ] Task 21: Hangfire wiring (`AddHangfire` + `UsePostgreSqlStorage` + `AddHangfireServer`, `/hangfire` dashboard behind admin auth) + `SystemSettings.AiNarrativeGenerationTimeUtc` field + migration + settings UI time picker.
- [ ] Task 22: `ProjectContextSnapshotService.GenerateAiNarrativeForAllActiveProjectsAsync` recurring job (per-project enqueue, `ModelRouter` → `StreamChatAsync` with `TemplateContent` cache block, persist `AiNarrative`/`AiNarrativeGeneratedAt`) + tests with stub client.
- [ ] Task 23: Update `docs/DECISIONS.md` (A–D entries), `docs/architecture.md` (adapter table with DallE/StabilityAi/ComfyUi), `docs/data-model.md` (`UserTokenBudget` reconciliation, `AdapterType` enum note), `docs/functional-spec.md` (Phase 6 checkboxes), `CLAUDE.md`/`AGENTS.md` (LLM config + commands), `appsettings` example (`Llm:EncryptionKey`, `Llm:ContextCompressionThresholdRatio`).
- [ ] Task 24: Manual validation matrix (provider CRUD round-trip, model probe, routing edit, usage dashboard filters, budget-exceeded error path, AiNarrative nightly job dry-run) — `docs/manual-validation/2026-07-30-phase-6-llm-infrastructure-matrix.md`.