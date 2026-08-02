## Validate: Phase 6 Plan 17 — `GET /api/account/usage` endpoint

### Setup
- [ ] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [ ] `Llm:EncryptionKey` set in shell env (32-byte base64; e.g. the test key `0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=` for local)
- [ ] Server builds clean: `dotnet build`
- [ ] `dotnet test --filter "FullyQualifiedName~AccountControllerTests"` — 3 pass
- [ ] `dotnet run --project src/HydraForge.Server` up with a known seeded user (any user with login + JWT)
- [ ] Issue a JWT for that user (use the Web UI login flow or a local token-issuer script)

### Happy Path
1. `curl -i -H "Authorization: Bearer <jwt>" http://localhost:5000/api/account/usage` → `200 OK`, JSON body with all 7 top-level fields present: `tokensUsed`, `tokensBudget`, `imagesUsed`, `imagesBudget`, `periodStart`, `periodEnd`, `recentCalls`
2. `recentCalls[].feature` and `recentCalls[].model` are non-empty strings; each item also has `tokens` (int ≥ 0), `images` (int ≥ 0), `cost` (decimal ≥ 0), `timestamp` (ISO-8601)
3. Body has `content-type: application/json` and any deserialized `recentCalls` list sorts by `timestamp` descending
4. A user with no usage history and no `UserTokenBudget` row → `tokensUsed=0`, `tokensBudget=0`, `imagesUsed=0`, `imagesBudget=0`, `recentCalls=[]`, both period fields populated to a sensible current-month window

### Edge Cases
1. No `Authorization` header → `401 Unauthorized`
2. `Authorization: Bearer <expired-jwt>` → `401 Unauthorized`
3. `Authorization: Bearer <garbage>` → `401 Unauthorized`
4. `tokensBudget == 0` in DB (unlimited) → response echoes `tokensBudget: 0`; client interprets 0 as unlimited per DTO contract
5. Period window straddles a month boundary (periodStart=2026-07-15, periodEnd=2026-08-14) → only `token_usage_records` / `image_usage_records` with `created_at` inside the window are summed and surfaced in `recentCalls`
6. A user with >20 token+image rows in the period → `recentCalls` is capped at 20 items, sorted by timestamp desc across both types (interleaved, not "20 tokens then images")
7. Two different users with overlapping usage → each user's `GET /api/account/usage` only returns their own rows (no cross-user bleed)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no schema change in this plan)
2. `dotnet test` — full suite still green (previous plan suites unaffected)
3. `ILlmAdminService` other methods (provider CRUD, model CRUD, routing, budget) still resolve via DI in `WebApplicationFactory<Program>` test fixtures
4. JWT role-claim fix (Plan 5) unaffected — `[Authorize(Policy = AuthPolicies.UserIdRequired)]` still extracts the userId from `NameIdentifier`

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
