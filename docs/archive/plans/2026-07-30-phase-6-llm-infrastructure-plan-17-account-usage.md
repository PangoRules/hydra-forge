# Plan 17: /api/account/usage endpoint

**Branch:** `task/account-usage`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 17

## Steps

### 1. Add endpoint to `UsersController` or new `AccountController`
- File: `src/HydraForge.Server/Controllers/UsersController.cs` (add method) or new `AccountController.cs`.
- Route: `GET /api/account/usage`.
- Auth: standard `[Authorize]` — scoped to caller's `userId` only (extracted from JWT).

### 2. Implement query logic
- Query `TokenUsageRecords` for current user, current period (since `UserTokenBudget.PeriodStart`).
- Return:
  - `tokensUsed`: sum of `InputTokens + OutputTokens` this period.
  - `tokensBudget`: `MonthlyTokenBudget` (0 = unlimited).
  - `imagesUsed`: sum of `ImageCount` this period.
  - `imagesBudget`: `MonthlyImageBudget`.
  - `recentCalls`: last 20 `TokenUsageRecord`/`ImageUsageRecord` rows (feature, model, tokens/images, cost, timestamp).
  - `periodStart`, `periodEnd`.

### 3. DTO
- File: `src/HydraForge.Application/Llm/AccountUsageDto.cs`
- `AccountUsageResponse(TokensUsed, TokensBudget, ImagesUsed, ImagesBudget, PeriodStart, PeriodEnd, RecentCalls)`.

### 4. Server tests
- File: `tests/HydraForge.Server.Tests/Controllers/AccountControllerTests.cs`
- Test: authenticated user gets own usage (scoped).
- Test: unauthenticated → 401.
- Test: empty usage → zeros, empty recent calls.

## Verification
- `dotnet build`
- `dotnet test --filter "AccountUsage"`
- Manual: `GET /api/account/usage` with valid JWT returns correct shape.