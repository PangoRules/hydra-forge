# Plan 1: AdapterType enum + error codes + data-model reconciliation

**Branch:** `task/adapter-enum-error-codes`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 1

## Steps

### 1. Add `DallE` + `StabilityAi` to `AdapterType` enum
- File: `src/HydraForge.Domain/Enums/AdapterType.cs`
- Add `DallE = 6` and `StabilityAi = 7` after `ComfyUi = 5`.
- Keep existing values unchanged (no renumbering).

### 2. Create `FeatureRoutingConfig` entity
- File: `src/HydraForge.Domain/Entities/Admin/FeatureRoutingConfig.cs`
- Spec says entity "already exists and is mapped" but it's absent from codebase. Create it:
  ```csharp
  public class FeatureRoutingConfig
  {
      public Guid Id { get; set; } = Guid.NewGuid();
      public AiFeature Feature { get; set; }
      public ModelTier DefaultTier { get; set; } = ModelTier.Standard;
      public ModelTier? MaxUserTier { get; set; }
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
  }
  ```

### 3. Map `FeatureRoutingConfig` in DbContext
- File: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- Add `public DbSet<FeatureRoutingConfig> FeatureRoutingConfigs => Set<FeatureRoutingConfig>();`
- Add `ConfigureEntity<FeatureRoutingConfig>(modelBuilder, "feature_routing_configs", b => { b.HasIndex(e => e.Feature).IsUnique(); });`

### 4. Add `Llm.*` error codes
- File: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`
- Add new static class `Llm` with:
  - `TokenBudgetExceeded = "TOKEN_BUDGET_EXCEEDED"`
  - `ImageBudgetExceeded = "IMAGE_BUDGET_EXCEEDED"`
  - `NoModelForFeature = "LLM_NO_MODEL_FOR_FEATURE"`
  - `ContextWindowExceeded = "LLM_CONTEXT_WINDOW_EXCEEDED"`
  - `EncryptionKeyInvalid = "LLM_ENCRYPTION_KEY_INVALID"`
- Keep existing `Infrastructure.LlmProviderUnavailable` as alias; add `Llm.ProviderUnavailable = "LLM_PROVIDER_UNAVAILABLE"` as canonical home.

### 5. Reconcile `data-model.md` UserTokenBudget section
- File: `docs/data-model.md` §UserTokenBudget (line ~457)
- Replace stale `DailyLimit`/`MonthlyLimit` fields with actual entity fields:
  `MonthlyTokenBudget`, `MonthlyTokenUsed`, `MonthlyImageBudget`, `MonthlyImageUsed`, `PeriodStart`, `PeriodEnd`.
- Add `AdapterType` enum note: `DallE = 6`, `StabilityAi = 7`.

### 6. Add EF migration
- Run: `dotnet ef migrations add AddFeatureRoutingConfigAndAdapterTypes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`
- Verify no pending model changes.

## Verification
- `dotnet build` — all projects compile.
- `dotnet test` — existing tests pass (no new tests yet, but enum additions must not break existing code).
- `dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` — clean.