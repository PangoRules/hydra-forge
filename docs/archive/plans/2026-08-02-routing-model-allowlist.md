# Plan: Per-feature model allowlist for admin Routing page

**Status:** Backlog — not scheduled, no target phase/branch yet.
**Origin:** Found while testing the Phase 6 `ai-narrative-gen` job manually. Splitting `AiNarrative` into its own `AiFeature.ProjectNarrative` routing slot (see `AiFeature.cs`, `FeatureRoutingConfigSeeder.cs`) surfaced that the only lever admins have per feature is a **tier** (Economy/Standard/Premium) — there's no way to say "this feature always uses exactly this provider + this model," which matters for self-hosted/mixed setups (e.g. Ollama for cheap bulk jobs, a cloud provider for user-facing chat) where a tier bucket doesn't map cleanly to "the one model I want."

## Problem

`ModelRouter.ResolveAsync` picks the first enabled `ProviderModelConfig` at `FeatureRoutingConfig.DefaultTier`, ordered arbitrarily by provider name (`FindModelAtTierAsync`, `src/HydraForge.Application/Llm/ModelRouter.cs`). Admin can change the tier, but not:
- which specific provider serves a feature, if multiple providers share a tier
- which specific model, if a provider has multiple models at a tier
- a preference/fallback order among a hand-picked set of models

## Proposed structure

### Schema — additive, no changes to existing tables

```
FeatureAllowedModel
  Id                      Guid (PK)
  FeatureRoutingConfigId  Guid (FK → FeatureRoutingConfig)
  ProviderModelConfigId   Guid (FK → ProviderModelConfig)
  Priority                int   (0 = most preferred)
```

`FeatureRoutingConfig.DefaultTier` / `MaxUserTier` are untouched — tier-based routing keeps working exactly as today for any feature that doesn't opt in.

### Router behavior — opt-in, backward compatible

- **No `FeatureAllowedModel` rows for a feature** → today's behavior, unchanged. Zero migration cost for the 11 existing features.
- **Rows exist** → `ModelRouter` restricts candidates to that list only, tried in `Priority` order. Token-overflow auto-bump (`TryAutoBumpTierAsync`) still runs, just scoped to the allowed set instead of the whole provider table — a lower-priority allowed model can still serve as the bump target if a higher-priority one's `MaxTokens` is too small for the request.

### Admin UI — Routing page (`src/web-ui/app/pages/admin/routing.vue`)

- Per-feature row expands to a multi-select sourced from Provider Models (all providers, not tier-filtered — admin picks literal `ProviderModelConfig` rows directly)
- Reorder (drag or up/down) sets `Priority`
- Empty list renders an "Auto (tier-based)" badge — makes the fallback-to-tier behavior explicit in the UI rather than implicit

### API

Extend the routing DTO with an ordered `allowedModelConfigIds: Guid[]`, or add dedicated `POST/DELETE /api/admin/llm/routing/{feature}/models/{modelConfigId}` + a reorder endpoint. Check how `routing.vue` currently patches `FeatureRoutingConfig` before picking — match that shape rather than introducing a second update pattern.

## Migration

One EF migration adding `FeatureAllowedModel`. No data migration needed — empty table means every existing feature keeps its current (tier-only) behavior.

## Out of scope for this plan

- Removing or replacing the tier system — it stays as the default/fallback mechanism.
- User-specific model selection (`MaxUserTier` ceiling logic is separate and already has its own TODO in `ModelRouter.ResolveAsync`).
