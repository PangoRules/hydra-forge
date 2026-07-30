# Plan 13: IUsageRecorder EF impl + tests

**Branch:** `task/usage-recorder`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 13

## Steps

### 1. Create EF-backed implementation
- File: `src/HydraForge.Infrastructure/Llm/EfUsageRecorder.cs`
- Implements `IUsageRecorder`. Constructor takes `HydraForgeDbContext`.

### 2. Implement `RecordTokenAsync`
- Create `TokenUsageRecord` from `TokenUsageRecordInput` (UserId, ProjectId, Feature, ProviderModelConfigId, ProviderId, ModelId, ModelName, InputTokens, OutputTokens, CachedTokens, Cost).
- Cost computed: `(InputTokens + OutputTokens) * PricePerToken` from `ProviderModelConfig` (if set, else 0). Cached tokens subtracted from cost.
- `db.TokenUsageRecords.Add(record)`.

### 3. Implement `RecordImageAsync`
- Create `ImageUsageRecord` from `ImageUsageRecordInput`.
- `db.ImageUsageRecords.Add(record)`.

### 4. Implement `AccrueTokenUsageAsync`
- Load `UserTokenBudget` for userId (create if not exists with defaults: `MonthlyTokenBudget = 0` unlimited, `PeriodStart = now`, `PeriodEnd = now + 1 month`).
- Lazy rollover: if `DateTime.UtcNow > PeriodEnd`, reset `MonthlyTokenUsed = 0`, `MonthlyImageUsed = 0`, advance `PeriodStart`/`PeriodEnd` by one month.
- `MonthlyTokenUsed += tokens`.
- `await db.SaveChangesAsync(ct)`.
- Return new `MonthlyTokenUsed`.

### 5. Implement `AccrueImageUsageAsync`
- Same as token but for `MonthlyImageUsed += count`.

### 6. Register DI
- `services.AddScoped<IUsageRecorder, EfUsageRecorder>()`

### 7. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/EfUsageRecorderTests.cs`
- Use in-memory EF `HydraForgeDbContext`.
- Test cases:
  - Record token → `TokenUsageRecord` persisted with correct fields.
  - Record image → `ImageUsageRecord` persisted.
  - Accrue token → `MonthlyTokenUsed` incremented.
  - Accrue image → `MonthlyImageUsed` incremented.
  - Lazy rollover: `PeriodEnd` passed → counters reset, period advanced.
  - Auto-create `UserTokenBudget` on first accrue.
  - Cost calculation from `PricePerToken`.

## Verification
- `dotnet build`
- `dotnet test --filter "EfUsageRecorder"`
- `dotnet ef migrations has-pending-model-changes` — clean (no schema changes).