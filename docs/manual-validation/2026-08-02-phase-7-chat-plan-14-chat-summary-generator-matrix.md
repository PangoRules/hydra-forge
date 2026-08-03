## Validate: ChatSummaryGenerator — LLM summary on session close

### Setup
- [ ] Server running with valid `Llm` provider config (admin-routed model available for `AiFeature.PersonalChat`)
- [ ] Authenticated user with at least one OpenAI-class provider enabled and a routing config that resolves for `PersonalChat`
- [ ] A `ChatSession` seeded via API with ≥ 4 messages (alternating `User` / `Assistant`)
- [ ] A `CardChatLink` attached to that session via `POST /api/chat/links` (so the link's `Summary` column is in play)

### Happy Path — Real LLM call
1. `POST /api/chat/sessions/{id}/close` (auth as session owner) → `200 OK`
2. `GET /api/chat/sessions/{id}` → `Summary` is a 2–3 sentence natural-language recap of the message contents (not empty, not `null`)
3. `GET /api/chat/links?sessionId={id}` → corresponding `CardChatLink.Summary` matches `ChatSession.Summary`
4. Audit log `audit_log_entries` → one entry referencing the close with `SourceType=ChatSession`, `Action=Close` (existing behavior, regression only)

### Edge Cases
1. Close a session with **zero messages** → `200 OK`, `Summary = ""` (string.Empty), no LLM call recorded in `token_usage_records`
2. Close a session when **no routing config exists for `PersonalChat`** → `200 OK`, `Summary = null`, server log carries `CHAT_SUMMARY_FAILED` warn, audit log still records the close
3. Close a session whose owner has **no enabled providers** → `200 OK`, `Summary = null`, no usage row written
4. Close a session twice (idempotency — depends on plan contract) → second close returns existing summary unchanged, no second LLM call

### Regressions
1. Open + send message + close a session **without** a card link → `CardChatLink.Summary` contract untouched (no orphan link rows)
2. Search listing for sessions (`GET /api/chat/sessions`) → closed sessions still appear with `Summary` populated
3. Provider disabled mid-session (was enabled at open) → close still completes with `Summary = null` fallback, no 500
4. Existing chat message endpoints unchanged — POST/GET messages around close still work

### Cleanup
- [ ] Delete test session + link after run (session archive endpoint or DB cascade)
- [ ] Revert any seed chat providers if mutated
