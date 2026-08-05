# Reasoning-Effort Picker

## Context

Competitive UI research (Duck.ai, Perplexity, Copilot, Gemini, Claude) found four independent products converged on the same control: a Low/Medium/High(/Max) reasoning-depth picker alongside model selection. HydraForge's chat UI (Task 17) has model selection (`ChatModelPicker.vue`) but no equivalent — and the backend has zero concept of reasoning effort anywhere (`ChatRequest`, `ModelRouter`, all adapters checked — none reference `thinking`/`reasoning_effort`/`effort`).

This is the first of three candidate features identified in that research (the other two — incognito chat, folder-level document scope + instructions — are not in scope here).

## Scope

Backend: new `SupportsReasoning` flag on `ProviderModelConfig` (admin-set per model), a new optional `ReasoningEffort` field threaded through `ChatRequest`, and adapter-level mapping for the two adapter families that can act on it (Anthropic, OpenAI-compatible). No change to model *selection* logic (`ModelRouter` tier resolution is untouched) — effort only affects how the already-selected model is called.

Frontend: an effort row in `ChatModelPicker.vue`'s popover, shown only when the currently-selected model has `SupportsReasoning = true`. Persisted the same way the model choice already is.

Out of scope: exposing effort as a per-request override independent of model choice (some providers vary supported levels per model — not handled, three fixed presets for everything); any UI indication in the model *list itself* (before selection) of which models support it, beyond the row appearing/disappearing after selection — a small "supports reasoning" badge in the list is a natural follow-up, not required for v1. Anthropic's `xhigh`/`max` effort levels are also out of scope for v1 — HydraForge's UI only exposes low/medium/high; `AnthropicAdapter` never sends `xhigh`/`max`.

## Design

### Backend: model capability flag

- `ProviderModelConfig` (`src/HydraForge.Domain/Entities/Admin/ProviderModelConfig.cs`) gains `bool SupportsReasoning` (default `false`). New EF migration.
- Admin Provider Models page (existing, Phase 6) gets a toggle for this field in the model create/edit form, next to `Tier`/`PricePerToken`/`MaxTokens`.
- `AvailableModelDto` (`src/HydraForge.Application/Llm/LlmDtos.cs`, returned by `GET /api/llm/models/{feature}` — the endpoint `ChatModelPicker.vue` already calls) gains `SupportsReasoning`.

### Backend: request plumbing

- `ChatRequest` (`LlmDtos.cs`) gains `string? ReasoningEffort` — one of `"low"`/`"medium"`/`"high"`, `null` when the model doesn't support it or the user left it at default.
- `GenerateReplyRequest` (`ChatMessagesController.cs`) gains `ReasoningEffort`, threaded through `ChatReplyGenerator`'s `ChatRequest` construction (`ChatReplyGenerator.cs:282`).
- `AnthropicAdapter`: maps the three levels straight through to `output_config: {effort: "low"|"medium"|"high"}` — no token-budget math. (Confirmed against current Anthropic API docs: `thinking.budget_tokens` is removed/400s on all current-generation models — Opus 5, Sonnet 5, Fable 5, Opus 4.7/4.8. The `effort` string enum, not a numeric budget, is the real mechanism. `thinking: {type: "adaptive"}` stays on as Anthropic's own default regardless of `effort` — HydraForge's field doesn't need to touch `thinking` at all, just `output_config.effort`.)
- `OpenAiCompatibleAdapter`: passes the string straight through as `reasoning_effort` for providers that accept it. Since the field is now only ever populated for models explicitly flagged `SupportsReasoning`, no "does this provider silently reject unknown fields" concern applies — an admin who flags a non-reasoning model incorrectly is a config mistake, not a code path to defend against.
- All other adapters (`OllamaAdapter`, image adapters) never receive the field — no code change needed there.

### Frontend

- `ChatModelPicker.vue`: effort row rendered `v-if` on the currently-selected model's `supportsReasoning`, directly under the model list in the same popover (mirrors Claude's own layout). Switching to a model without the flag hides the row.
- Selected effort persisted to `localStorage` per-feature (`hydraforge:chat:preferredEffort:${feature}`), same pattern as the existing `preferredModelId` key.
- Threaded alongside `preferredModelId` through `ChatInput`'s `send` emit → `useChatStream.send()`/`resend()` → the `messages`/`generateReply` POST bodies.

## Error handling

None needed. The field is an optional hint the user can only ever set when it's actually applicable (picker hidden otherwise) — no validation, no failure mode to handle beyond the adapter's normal request-failure path (unchanged).

## Testing

- `AnthropicAdapterTests` / `OpenAiCompatibleAdapterTests`: request-shape mapping for each effort level, and confirm the field is omitted when `null`.
- `ChatReplyGeneratorTests` (if a suite exists for it — verify at plan time) covering the pass-through from `GenerateReplyRequest` into `ChatRequest`.
- `ChatModelPicker.test.ts` (new — no existing test file for this component): effort row visibility toggles with `supportsReasoning` on the selected model, persistence round-trip through `localStorage`.
- Infrastructure EF model-contract test (`AssertProperties`) for the new `SupportsReasoning` column, matching the project's standard pattern for entity field additions.
