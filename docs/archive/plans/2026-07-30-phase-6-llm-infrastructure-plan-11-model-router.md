# Plan 11: ModelRouter + unit tests

**Branch:** `task/model-router`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 11

## Steps

### 1. Create `ModelRouter` service
- File: `src/HydraForge.Application/Llm/ModelRouter.cs`
- Implements `IModelRouter`. Depends on `HydraForgeDbContext` (read-only) for querying `FeatureRoutingConfig`, `LlmProvider`, `ProviderModelConfig`.
- Application service — no `HttpClient` or provider SDKs.

### 2. Implement `ResolveAsync` algorithm
1. Query `FeatureRoutingConfig` by `AiFeature` → get `DefaultTier`. If not found → `Llm.NoModelForFeature`.
2. Apply user tier ceiling: query user's role/budget tier. If user has override > `MaxUserTier`, cap to ceiling. `null` ceiling = locked to default.
3. Context-window guard: if `estimatedTokens > primaryModel.MaxTokens`, auto-bump tier (Economy→Standard→Premium) until a model fits or no higher tier exists. If no model fits → `Llm.ContextWindowExceeded`.
4. Select `ProviderModelConfig` at resolved tier on an enabled `LlmProvider` (ordered by `Tier` then `Name`).
5. Build fallback chain: walk `LlmProvider.FallbackProviderId` until null or cycle detected. Cycle → stop chain at cycle point, log warning.
6. Return `RouteDecision(Primary, PrimaryProvider, Fallbacks)`.

### 3. Cycle detection
- Track visited provider IDs in `HashSet<Guid>`. If `FallbackProviderId` already visited → break.

### 4. Register DI
- In `LlmServiceCollectionExtensions`: `services.AddScoped<IModelRouter, ModelRouter>()`.

### 5. Unit tests
- File: `tests/HydraForge.Application.Tests/Llm/ModelRouterTests.cs`
- Use in-memory EF Core `HydraForgeDbContext` with seeded test data.
- Test cases:
  - Feature not configured → `NoModelForFeature`.
  - Default tier resolution with enabled provider.
  - Tier ceiling: user override capped to `MaxUserTier`.
  - `null` MaxUserTier → locked to default.
  - Context-window auto-bump: estimated tokens exceed model → bump to higher tier.
  - No model fits after all tiers → `ContextWindowExceeded`.
  - Fallback chain walk (single, multi-hop).
  - Fallback cycle detection.
  - Disabled providers skipped.
  - No enabled provider at tier → error.

## Verification
- `dotnet build`
- `dotnet test --filter "ModelRouter"`
- Pure logic tests — no DB required (in-memory EF).