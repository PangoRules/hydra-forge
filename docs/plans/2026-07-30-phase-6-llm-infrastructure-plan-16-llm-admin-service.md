# Plan 16: ILlmAdminService + controller + tests

**Branch:** `task/llm-admin-service`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 16

## Steps

### 1. Create `LlmAdminService` (Application)
- File: `src/HydraForge.Application/Llm/LlmAdminService.cs`
- Implements `ILlmAdminService`. Depends on `HydraForgeDbContext`, `IKeyVault`, `ILlmClientFactory`.

### 2. Implement provider CRUD
- `ListProvidersAsync`: query `LlmProviders`, project to `ProviderDto` (never return `ApiKeyEncrypted`).
- `CreateProviderAsync`: validate input, encrypt `ApiKey` via `IKeyVault`, create `LlmProvider`, save.
- `UpdateProviderAsync`: load provider, update fields. If `ApiKey` provided → re-encrypt. If null → leave existing.
- `DisableProviderAsync`: set `IsEnabled = false`, save. Never hard-delete.

### 3. Implement model config CRUD
- `ListModelsAsync(providerId)`: query `ProviderModelConfigs` for provider.
- `CreateModelAsync`: create `ProviderModelConfig`, save.
- `UpdateModelAsync`: update fields, save.
- `DeleteModelAsync`: remove row (models are disposable, unlike providers).

### 4. Implement `ProbeModelsAsync`
- Get `ILlmClient` via factory, call `GetModelsAsync()`. Return list — do NOT persist (open question 4: return only).

### 5. Implement routing CRUD
- `ListRoutingAsync`: query all `FeatureRoutingConfigs`.
- `UpdateRoutingAsync(feature, input)`: update `DefaultTier`/`MaxUserTier`.

### 6. Implement usage queries
- `ListTokenUsageAsync`: query `TokenUsageRecords` with filters (userId, feature, model, date range), paginate.
- `ListImageUsageAsync`: same for `ImageUsageRecords`.

### 7. Implement budget management
- `GetUserBudgetAsync(userId)`: query `UserTokenBudget`.
- `UpdateUserBudgetAsync(userId, input)`: update `MonthlyTokenBudget`/`MonthlyImageBudget`.

### 8. Add controller endpoints
- File: `src/HydraForge.Server/Controllers/Admin/LlmAdminController.cs` (new)
- All behind `[Authorize(Policy = AuthPolicies.AdminRequired)]`.
- Routes per spec §API surface:
  - `GET/POST /api/admin/providers`
  - `GET/PUT/DELETE /api/admin/providers/{id}`
  - `GET /api/admin/providers/{id}/models` (probe)
  - `POST/PUT/DELETE /api/admin/providers/{id}/models`
  - `GET/PUT /api/admin/routing`
  - `GET /api/admin/usage/tokens`
  - `GET /api/admin/usage/images`
  - `GET/PUT /api/admin/users/{userId}/budget`

### 9. Server tests
- File: `tests/HydraForge.Server.Tests/Controllers/Admin/LlmAdminControllerTests.cs`
- Create `TestILlmClient` stub (returns fake models), `TestIKeyVault` stub (no-op encrypt/decrypt).
- Wire stubs in test factory's `ConfigureServices` (same pattern as `AdminTestWebApplicationFactory`).
- Test: auth gating (401/403), provider CRUD round-trip, probe models, routing update, usage pagination.

## Verification
- `dotnet build`
- `dotnet test --filter "LlmAdmin"`
- All endpoints return correct status codes with stubs.