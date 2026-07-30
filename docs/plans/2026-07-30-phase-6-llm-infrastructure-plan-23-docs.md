# Plan 23: Doc updates

**Branch:** `task/phase-6-docs`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 23

## Steps

### 1. Update `docs/DECISIONS.md`
- Add entries A–D from spec §Approved Decisions:
  - **A**: `DallE` + `StabilityAi` adapter types, `ComfyUiAdapter` serves both `ComfyUi` + `Diffusers`.
  - **B**: Token estimation = `chars / 4` heuristic.
  - **C**: AES-GCM encryption via `IKeyVault`/`AesGcmKeyVault`, key from `Llm:EncryptionKey`.
  - **D**: `StreamChatAsync` returns `IAsyncEnumerable<ChatChunk>`.
- Add D-57 (Hangfire) if not already present.

### 2. Update `docs/architecture.md`
- Add adapter table with all 6 adapters (OpenAiCompatible, Anthropic, Ollama, DallE, StabilityAi, ComfyUi).
- Note `ComfyUiAdapter` dual-role for `ComfyUi` + `Diffusers`.
- Add Hangfire section under Infrastructure.

### 3. Update `docs/data-model.md`
- `AdapterType` enum: add `DallE = 6`, `StabilityAi = 7`.
- `UserTokenBudget` section: already reconciled in Task 1 — verify.
- Add `FeatureRoutingConfig` entity table (new entity created in Task 1).
- Add `SystemSettings.AiNarrativeGenerationTimeUtc` field.

### 4. Update `docs/functional-spec.md`
- Mark Phase 6 checkboxes as complete (or add Phase 6 section with all 24 task checkboxes).

### 5. Update `CLAUDE.md` / `AGENTS.md`
- Add LLM config section: `Llm:EncryptionKey` (base64 32-byte), `Llm:ContextCompressionThresholdRatio` (default 0.75).
- Add Hangfire commands: dashboard at `/hangfire` (admin-only).
- Add `appsettings.Development.json` example entries.

### 6. Update `appsettings.Development.json`
- File: `src/HydraForge.Server/appsettings.Development.json`
- Add `"Llm": { "EncryptionKey": "<base64-32-byte-key>", "ContextCompressionThresholdRatio": 0.75 }`.
- Note: placeholder key — replace in production.

## Verification
- Review all doc changes for accuracy.
- No build/test impact (doc-only).