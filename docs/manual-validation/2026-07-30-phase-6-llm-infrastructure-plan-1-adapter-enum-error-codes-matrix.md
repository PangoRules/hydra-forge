## Validate: Plan 1 — AdapterType enum + error codes + data-model reconciliation

### Setup
- [ ] Branch `task/adapter-enum-error-codes` checked out
- [ ] `dotnet build` succeeds (0 errors)
- [ ] `dotnet test` passes (707 tests across 5 test projects)
- [ ] `dotnet ef migrations has-pending-model-changes` returns "No changes"

### Happy Path
1. Open `src/HydraForge.Domain/Enums/AdapterType.cs` → `DallE = 6`, `StabilityAi = 7` present; values 1–5 unchanged
2. Open `src/HydraForge.Domain/Entities/Admin/FeatureRoutingConfig.cs` → entity has `Id`, `Feature`, `DefaultTier`, `MaxUserTier`, `CreatedAt`, `UpdatedAt`
3. Open `src/HydraForge.Domain/Common/DomainErrorCodes.cs` → nested `Llm` class with `TokenBudgetExceeded`, `ImageBudgetExceeded`, `NoModelForFeature`, `ContextWindowExceeded`, `EncryptionKeyInvalid`, `ProviderUnavailable`
4. Run `dotnet ef migrations list` → `20260730214033_AddFeatureRoutingConfigAndAdapterTypes` present
5. Open migration `.cs` file → `feature_routing_configs` table with `Feature` unique index
6. Open `docs/data-model.md` §UserTokenBudget → `Id` row present; `MonthlyTokenBudget` and `MonthlyImageBudget` typed `int` with description `(0 = unlimited)`
7. Open `docs/data-model.md` §AdapterType → rows for `DallE = 6` and `StabilityAi = 7`

### Edge Cases
1. Reference `AdapterType.DallE` / `AdapterType.StabilityAi` from a Domain test → compiles, int values 6 and 7
2. Reference `DomainErrorCodes.Llm.TokenBudgetExceeded` → equals `"TOKEN_BUDGET_EXCEEDED"`
3. Reference legacy `DomainErrorCodes.Infrastructure.LlmProviderUnavailable` → still equals `"LLM_PROVIDER_UNAVAILABLE"` (alias preserved)

### Regressions
1. Existing `OpenAiCompatible = 1` … `ComfyUi = 5` AdapterType values → unchanged (no enum renumbering)
2. All other `DomainErrorCodes.*` static classes (Auth, Projects, Cards, etc.) → unchanged
3. Other DbContext entities → no schema changes besides `FeatureRoutingConfigs` DbSet
4. Existing `Application`/`Server` code that referenced `Infrastructure.LlmProviderUnavailable` → still compiles (alias preserved)

### Cleanup
- [ ] None — branch is doc + enum + Domain additions only; no runtime data to remove