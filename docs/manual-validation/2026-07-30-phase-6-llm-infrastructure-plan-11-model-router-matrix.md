# E2E Regression Matrix — Phase 6 LLM Infrastructure

## Plan 11: ModelRouter (routing resolution + fallback chain)

### Setup
- [ ] Postgres up (`docker compose up -d postgres`)
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key
- [ ] Server builds clean: `dotnet build`
- [ ] `dotnet test --filter "FullyQualifiedName~ModelRouter"` — 11 pass (all algorithm cases via `FakeRoutingConfigProvider`)
- [ ] Apply latest EF migrations so `feature_routing_configs`, `llm_providers`, `provider_model_configs` tables exist (migration `20260730214033_AddFeatureRoutingConfigAndAdapterTypes` and any later ones)
- [ ] Seed at least one `FeatureRoutingConfig` + one `LlmProvider` + one enabled `ProviderModelConfig` via the DB or the LLM admin endpoints

### Happy Path
1. Resolve `IModelRouter` from DI scope → instance obtained, `IRoutingConfigProvider` resolves alongside it
2. Call `router.ResolveAsync(AiFeature.DeepResearch, userId, null, 1000, ct)` against a `FeatureRoutingConfig` with `DefaultTier=Standard`, one enabled Standard model with `MaxTokens=8192` → `Result.Success`, `Primary.Tier == Standard`, primary model is the alphabetically-first enabled model
3. Provider A (`FallbackProviderId = B`) at Standard tier, Provider B has one enabled Standard model → `Fallbacks.Count == 1`, fallback model is B's Standard model
4. Chain A → B → C, all Standard models enabled → `Fallbacks.Count == 2`, ordered A's-B then B's-C; primary still A's model
5. Same setup as #2 with `MaxTokens=512` on the Standard model and `estimatedTokens=1000` → router auto-bumps to a higher-tier enabled model with `MaxTokens >= 1000`; result is `Success` with the higher-tier primary
6. Provider chain A → B (one Standard model) → A again → router detects cycle, logs a warning via `IWarnLogger`, returns `Success` with primary=A's model and exactly 1 fallback (B's model); does not loop infinitely

### Edge Cases
1. Feature has no `FeatureRoutingConfig` row → `Result.Failure` with `Error.Code == Llm.NoModelForFeature`
2. `FeatureRoutingConfig` exists but no enabled provider/model at any tier → `Result.Failure` with `Error.Code == Llm.NoModelForFeature` (not ContextWindowExceeded)
3. `estimatedTokens` exceeds every enabled model's `MaxTokens` at every tier → `Result.Failure` with `Error.Code == Llm.ContextWindowExceeded`
4. `FeatureRoutingConfig.DefaultTier = Premium` with `MaxUserTier = Standard` and only a Standard-tier model enabled → `Success`; primary is the Standard model (ceiling respected)
5. `MaxUserTier == null` → routing stays at `DefaultTier` regardless of higher-tier availability
6. Provider at tier is `IsEnabled == false` → skipped; router selects the next enabled provider at that tier
7. `FallbackProviderId` points to a provider with no enabled model at the primary tier → fallback chain ends at that hop (no entry, no crash)
8. `FallbackProviderId` chain reaches a provider with no further fallback → chain stops naturally; remaining `Fallbacks` entries are returned in walk order
9. `IRoutingConfigProvider.GetProviderAsync(unknownGuid)` → returns `null`; fallback chain terminates gracefully (no NRE)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no entity changes in this plan)
2. `LlmServiceCollectionExtensions.AddLlmInfrastructure` still registers the prior: `IKeyVault` (AesGcm), `ILlmClientFactory`, named `HttpClient`s (`openai-compatible`, `anthropic`, `ollama`, `dalle`, `stability-ai`, `comfyui`)
3. New `IModelRouter` and `IRoutingConfigProvider` registered as `Scoped`; `IWarnLogger` registered (default `NullWarnLogger`)
4. `IRoutingConfigProvider` signatures return `IReadOnlyList<>` (not `List<>`) and queries are sorted by `Provider.Name` only (not `Provider.Tier`; that ordering was intentionally dropped)
5. `ModelRouter.ResolveAsync` does not throw on missing/invalid routing data — all expected failures surface via `Result.Failure` with `Llm.*` error codes
6. No new HTTP/network calls introduced by `ModelRouter.ResolveAsync` — it only reads routing config via `IRoutingConfigProvider`
7. Subsequent routing request in same scope reuses the scoped `IRoutingConfigProvider` (or re-resolves via the provider's own scope; no double-registration)
8. Plans 1–10 (adapters, error codes, key vault, client factory) still pass their existing tests after this merge

### Cleanup
- [ ] Drop seeded `FeatureRoutingConfig` + provider + model rows created for manual testing
- [ ] Unset `Llm:EncryptionKey` env var when done
