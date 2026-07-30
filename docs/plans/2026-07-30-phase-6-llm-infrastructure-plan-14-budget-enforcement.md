# Plan 14: Budget enforcement + tests

**Branch:** `task/budget-enforcement`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 14

## Steps

### 1. Create `LlmCallGuard` helper
- File: `src/HydraForge.Application/Llm/LlmCallGuard.cs`
- Static helper or injectable service. Depends on `HydraForgeDbContext` (read-only for budget check) and `IUsageRecorder` (for post-call accrual).
- Purpose: enforce budget before LLM calls, accrue after. Keeps adapters pure transport.

### 2. Implement pre-call budget check
- Method: `CheckTokenBudgetAsync(userId, estimatedTokens, ct)` → `Result`.
- Read `UserTokenBudget` for userId. If not found → pass (no budget configured).
- Lazy rollover check (same as Task 13).
- If `MonthlyTokenBudget > 0 && MonthlyTokenUsed + estimatedTokens > MonthlyTokenBudget` → `Result.Failure(Llm.TokenBudgetExceeded)`.
- HTTP status mapping: `429 Too Many Requests` with `Retry-After` header hinting next period rollover (open question 1 resolved to 429).

### 3. Implement image budget check
- Method: `CheckImageBudgetAsync(userId, count, ct)` → `Result`.
- Same pattern for `MonthlyImageBudget`/`MonthlyImageUsed`.
- Failure → `Llm.ImageBudgetExceeded`.

### 4. Implement post-call accrual
- Method: `AccrueAfterCallAsync(userId, usage, ct)`.
- Calls `IUsageRecorder.RecordTokenAsync` + `AccrueTokenUsageAsync`.
- Provider-reported `UsageSnapshot` is authoritative (overrides estimate).

### 5. Register DI
- `services.AddScoped<LlmCallGuard>()`

### 6. Unit tests
- File: `tests/HydraForge.Application.Tests/Llm/LlmCallGuardTests.cs`
- Test cases:
  - Under budget → pass.
  - Over token budget → `TokenBudgetExceeded`.
  - Over image budget → `ImageBudgetExceeded`.
  - Unlimited budget (0) → always pass.
  - No budget row → pass.
  - Lazy rollover resets counters before check.
  - Post-call accrual updates counters.

## Verification
- `dotnet build`
- `dotnet test --filter "LlmCallGuard"`