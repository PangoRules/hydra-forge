## Validate: Reasoning-Effort Picker

### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`, `ASPNETCORE_ENVIRONMENT=Development`)
- [ ] `pnpm dev` running in `src/web-ui`
- [ ] At least one LLM provider configured in `/admin/providers` with at least one model — flag one model `Supports reasoning effort = true` and leave another as `false`
- [ ] An authenticated user with access to the chats page

### Happy Path — Admin Toggle
1. `/admin/provider-models` → select provider → Edit model → toggle `Supports reasoning effort` ON → Save → row persists → reopen Edit modal → checkbox still ON
2. Same flow with the checkbox OFF → row persists as OFF
3. Add Model (manual) → checkbox visible, default unchecked → submit → row created with checkbox state preserved on reopen
4. Discover Models → pick a probed model → Add Model modal opens → checkbox defaults unchecked → submit → row created

### Happy Path — Chat Picker
1. `/chats` → open or start a chat → click the model-picker button → pick a `SupportsReasoning = true` model → effort row (Low / Medium / High) appears under the model list, "Medium" pre-selected by default on first visit
2. Pick `Low` → button highlights → reload page, reopen picker, re-select same model → "Low" still highlighted (localStorage `hydraforge:chat:preferredEffort:PersonalChat` = `low`)
3. Switch to a `SupportsReasoning = false` model → effort row disappears entirely
4. Switch back to a reasoning-supporting model → row reappears with the previously-saved value highlighted (not the "Medium" default)
5. Type a message → Send → wait for assistant reply → inspect Network tab: `POST /api/chat/sessions/{id}/messages/{mid}/generate` body has `reasoningEffort: "low"` (or whatever was selected)

### Happy Path — Adapter Wire-Format (Sanity)
1. With an Anthropic-backed reasoning model selected and effort = High, send a message → check server logs / capture HTTP — outbound Anthropic request includes `"output_config": { "effort": "high" }`
2. With an OpenAI-compatible reasoning model selected and effort = Medium, send a message → outbound OpenAI request includes `"reasoning_effort": "medium"`
3. Switch to a non-reasoning model, send a message → neither `output_config` nor `reasoning_effort` keys appear in the outbound JSON

### Happy Path — New Chat Compose
1. `/chats` (empty list) → select a reasoning model in the top composer → pick `High` → type message → Send → new chat created → first message sends → assistant reply uses `High` effort (verify via Network or DB behavior)
2. Same flow picking `Low` instead → first message uses `Low` (the picker state at compose-time is what propagates through `pendingFirstMessage` → `initialEffort` prop)

### Edge Cases
1. localStorage contains a stale / invalid value (e.g. `xhigh`) for the effort key → picker still renders, that value gets sent → server sends it through to adapter → adapter sends it through → no 500, no crash (per spec: "no validation, no failure mode")
2. Edit a reasoning-supporting model and toggle `Supports reasoning effort` OFF in admin → next time the picker is opened for that model, effort row is hidden and `lastUsedEffort` is cleared on next send
3. Regenerate an assistant reply after picking a different effort in the picker → the regen uses the effort from the ORIGINAL send, not the current picker state (`lastUsedEffort` capture) — matches the design comment
4. Open a chat session whose initialMessage path was triggered → first-send effort matches `pendingFirstMessage[id].reasoningEffort` → regenerate works using that same effort
5. Send a message, then roll back to a user message, edit, resend → effort is the same as the original send (chatStream.resend preserves `lastUsedEffort`)

### Regressions
1. Sending a message with a non-reasoning model selected still works (no `output_config` / `reasoning_effort` key in request, no UI regression)
2. `/admin/provider-models` Edit Model → other fields (tier / price / maxTokens / isEnabled) still round-trip correctly
3. `ModelRouter` model selection (tier fallback / routing) is unchanged — selecting a model in the picker still routes to the chosen model
4. Existing chat flows unaffected: rename, export, find-in, rollback — all still work
5. `ChatHub.SendMessage` still enqueues a generation when invoked over SignalR (it now passes `null` for effort — by design; REST is the canonical trigger path the Web UI uses)
6. EF migration `20260804231919_AddSupportsReasoningToProviderModelConfig` applies cleanly on a fresh DB; column exists with `defaultValue: false`

### Cleanup
- [ ] Reset admin `Supports reasoning effort` flag on any test models back to `false` to avoid leaving "high effort" routed by accident
- [ ] Clear localStorage keys `hydraforge:chat:preferredEffort:*` if you want to reset the picker to its `Medium` default for the next session