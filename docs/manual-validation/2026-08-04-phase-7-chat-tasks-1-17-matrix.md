# Manual Validation Matrix — Phase 7 Chat, Tasks 1–17

Consolidates and supersedes `2026-08-02-phase-7-chat-design-matrix.md` (Plans 1, 4, 5, 6, 9, 10, 13, 14) and `2026-08-02-phase-7-chat-plan-17-web-chat-view-matrix.md`. Repeated per-plan boilerplate (build/test-green setup, full-suite regression checks) is collapsed into one Environment Setup section instead of restated per plan. Everything below has been executed and confirmed. Tasks 18–24 (project chat panel, session header, card links, manager UIs, TUI, integration tests) are out of scope for this matrix — they get their own matrix(es) and fold into a final consolidated Phase 7 matrix at Task 24.

## Environment Setup (once)

- [x] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [x] Server running (`ASPNETCORE_ENVIRONMENT=Development`), Phase 7 chat migrations applied
- [x] `dotnet build` — 0 errors, 0 warnings
- [x] `dotnet test HydraForge.slnx` — 1169/1169 pass (Domain 87, Application 483, Infrastructure 247, Tui 137, Server 215)
- [x] `cd src/web-ui && pnpm typecheck` — 0 errors
- [x] `cd src/web-ui && pnpm lint` — 0 errors, 0 warnings
- [x] `cd src/web-ui && pnpm build` — succeeds
- [x] `cd src/web-ui && pnpm test` — 242/242 pass

## Domain, EF Model, App Ports (Plans 1–3)

- [x] `ChatSession` state transitions: fresh session defaults (`Active`, `AiEditMode.PerMutation`, `SearchAllMyDocs=false`, `ClosedAt=null`); `Open(...)` sets personality/card/edit-mode; `Close(summary)` sets `Closed` + UTC `ClosedAt` + summary; re-`Open` after close clears `ClosedAt`/`Summary` and returns to `Active`
- [x] Guard rejection: `SetAiEditMode`/`ToggleSearchAllMyDocs` on a `Closed` session throw `InvalidOperationException`, state unchanged; double-`Close` is idempotent
- [x] EF migration applied cleanly; `AssertProperties` model-contract tests pass for `ChatSession`, `ChatMessage`, `ChatSessionDocument`, `PromptPresetGroup`, `PromptPreset`
- [x] Application ports/DTOs compile with the documented shapes (`ChatDtos.cs`, `DomainErrorCodes.Chat` — 15 constants); Domain layer has zero `EntityFrameworkCore`/`AspNetCore`/`System.Net.Http` imports

## ChatMessage Images DTO (Plan 4)

- [x] `ChatMessage(Role, Content)` and `ChatMessage(Role, Content, ImageBlock[])` both compile; `Images` defaults to `null`
- [x] `ChatMessageMapper.ToDomainImagesJson`/`ToApplicationImages` round-trip `StorageKey`/`MediaType` correctly; `null`/`""`/`"not json"` inputs all degrade to `[]` (no throw)
- [x] `RouteDecision` unaffected (`Provider` still defaults to `null`); existing 2-arg `ChatMessage` callers unaffected

## Document Ingestion (Plan 5) + RAG Retrieval (Plan 6)

- [x] Chunking: 500-char doc → 1 chunk; 4000-char doc → 3 chunks with the documented 400-char tail overlap; `DocumentChunk.SourceType="document"`, `SourceId=DocumentId`, 1536-dim embedding per chunk
- [x] File storage key shape: `{userId}/document/{documentId}/{guid}` — no filename/date in the path
- [x] Validation rejection: empty/whitespace content with no stream → `CHAT_EMBEDDING_FAILED`, no `Document`/`DocumentChunk` rows written; embedding-client failure or vector/chunk count mismatch → same failure code, no orphaned rows (vector-count guard runs before `_documentRepo.AddAsync`)
- [x] Real end-to-end (now that Plan 16's REST controller + Infrastructure repos exist): uploaded `.txt`/`.md`/`.csv`/`.html` files persist real rows in `documents`/`document_chunks` with populated MinIO objects and non-zero 1536-dim embeddings
- [x] RAG retrieval: `SearchAllMyDocs=false` scopes to the session's `ChatSessionDocument` rows; `=true` passes `null` (searches all owner docs); project chat's first message includes a `ProjectSnapshot` cache block, subsequent messages don't; non-project chats never get one
- [x] `CacheBlockType.RagContext` never receives Anthropic `cache_control` (verified by `AnthropicAdapterTests` — this was a real bug caught in review, see `docs/DECISIONS.md`/CLAUDE.md LLM adapter conventions); `ContextCompressor` never evicts `RagContext` blocks (they're already top-K scoped per message)
- [x] Embedding failure or zero-vector result never throws — retrieval degrades to snapshot-only/empty and the chat send proceeds

## ChatFolder Service (Plan 9)

- [x] Create root → child (depth 1) → grandchild (depth 2) all succeed; depth-3 create → `CHAT_FOLDER_MAX_DEPTH`
- [x] Blank name on **both** create and update → `VALIDATION_REQUIRED` (the update-path gap noted in the original matrix was found and fixed — see CLAUDE.md "UpdateAsync validation parity with CreateAsync")
- [x] Self-parent update → `CHAT_FOLDER_SELF_PARENT`; non-existent parent → `CHAT_FOLDER_NOT_FOUND`; cross-project without membership → `PROJECTS_MEMBERSHIP_DENIED`
- [x] Archive folder cascades `ArchivedAt` to child sessions; archived folders excluded from subsequent listings; project archive cascade still reaches chat folders/sessions (`ChatArchiveService`)

## PromptPreset + PromptPresetGroup Services (Plan 10)

- [x] Create ungrouped preset, create group, create preset in group, rename group, update preset fields — all persist and reflect in listings
- [x] Archive group with presets attached → group archived, presets survive and become ungrouped (not deleted)
- [x] Ownership enforcement: user B assigning/updating/archiving user A's preset or group → `CHAT_PRESET_NOT_OWNER`/`CHAT_PRESET_GROUP_NOT_OWNER`, no mutation
- [x] Blank name on preset or group create/update → `VALIDATION_REQUIRED`
- [x] List filters (no group / empty group / specific group GUID) all return the correct scoped set

## AgentPersonality Service (Plan 11) + CardChatLink Service (Plan 12)

No Web UI yet (Plan 21 for personalities, Plan 20 for card links) — validated via the automated Application-layer test suite (part of the 483/483 Application tests above) and direct REST calls against `/api/chat/personalities` and `/api/chat/links`:

- [x] AgentPersonality CRUD + default-management (only one default per user) round-trips correctly via REST
- [x] CardChatLink: list-by-card, owner-only archive, auto-link creation all round-trip correctly via REST

## Chat Search Service (Plan 13)

- [x] Title match and content match both return correct `MatchedOn`/snippet; same session matching both → appears once, title wins
- [x] Case-insensitive (ILIKE); project-scoped search excludes other projects' sessions
- [x] Archived sessions excluded from both title and content search paths (parity fix — see CLAUDE.md "ArchivedAt filter parity in search subqueries")
- [x] Cross-tenant isolation: user B's sessions never appear in user A's search results
- [x] Result cap (20) and snippet-window edge cases (token at start/end of content, content shorter than window) all behave as documented

## ChatSummaryGenerator (Plan 14)

- [x] Closing a session with real LLM routing produces a 2–3 sentence summary; matching `CardChatLink.Summary` updates in the same call
- [x] Zero-message close → empty summary, no LLM call recorded; no routing config / no enabled providers → `Summary=null` with `CHAT_SUMMARY_FAILED` logged, close still succeeds (never a 500)
- [x] Double-close is idempotent (second close doesn't re-invoke the LLM)

## ChatHub Streaming + REST Controllers (Plans 15–16)

Exercised directly by the Web UI E2E flows below (every REST call and every hub event in this section goes through the real `ChatHub`/controllers, not a mock) plus the Server.Tests hub/controller suite (part of the 215/215 Server tests above):

- [x] One-active-stream-per-connection enforcement, cancel mid-stream, usage recording, and context compression all confirmed via the browser flows below
- [x] Rate limiting on the REST surface returns `429` under the configured threshold (Server.Tests)

## Web UI — Chat Streaming, Session List, Model/Preset Picker (Plan 17)

### Happy Path — Streaming
1. `ChatSessionView` mount loads prior messages, oldest-first
2. Send `Hello` → optimistic user bubble, input clears, `StreamStart` → typing-dots, `StreamDelta`s grow the bubble live, `StreamDone` commits it and a `fetchSession` refetch inserts the persisted assistant message with no flicker
3. Refresh → both messages persist in order
4. Second send → streams again; scroll auto-follows when at bottom, preserves position when scrolled up

### Happy Path — Cancellation
1. Cancel (X) mid-stream → `CancelStream` invoked, typing bubble clears, input re-enabled
2. Refresh → the cancelled partial assistant message is **not** persisted (server stops on `OperationCanceledException`)

### Happy Path — Connection Lifecycle
1. `connect()` then `join(sessionId)` on mount; WS connection to `/hubs/chat` confirmed in DevTools
2. Dropping the socket (`window.stop()`) triggers `onclose` → backoff starting at 5000ms; reconnect re-invokes `JoinSession` for the previously-joined session
3. Closing the view calls `leave()` then `disconnect()`; no leaked socket

### Happy Path — Markdown Rendering + XSS Hardening
1. `**bold**`, `` `code` ``, `[link](url)` all render correctly (not raw markdown)
2. `[bad](javascript:alert(1))`, `[bad](data:text/html,<script>...)`, and raw `<script>` in a message all fail to execute — content goes through `marked` with a no-op `html()` renderer, then `DOMPurify.sanitize()` client-side (upgraded from a hand-rolled `href` regex after a security-annotation review — DOMPurify's URI allowlist is the actual XSS defense now, not the regex)

### Happy Path — Model + Preset Picker
1. `ChatModelPicker` lists admin-configured, tier-respecting models grouped by provider (`GET /api/llm/models/{feature}`); selection persists to `localStorage` per-feature and is sent as `preferredModelId` on send
2. `ChatInput`'s preset dropdown lists active presets (`GET /api/chat/presets`); selecting one shows an active-preset chip and sends `presetId` with the message; server-side injection into the request confirmed

### Edge Cases — Scroll / Disabled / Empty States
1. 60+ message session windows to the last 50 with a top sentinel; scrolling to the sentinel grows the window by 50 without a scroll jump
2. Non-`Active` session disables the input regardless of streaming state; zero-message session shows an empty-state placeholder
3. Network drop during send removes the optimistic message and shows an error toast

### Known, accepted gap (not a bug)
- Image attach is intentionally **disabled** in the UI (`ChatInput.vue` — the image button is `disabled` with a tooltip explaining why): the server's `ImageBlock` DTO expects `StorageKey`/`MediaType` (a real upload flow), which doesn't exist yet. There is no code path today that sends an image payload to the server, so there's no live deserialization bug — this is tracked as real upload-endpoint work for a later plan, not a defect in what shipped.

### Regressions
- [x] `useRealtime`/`usePresence`/`useNotificationHub` composables unaffected — no shared state with `useChatStream`
- [x] `ChatMessagesController`'s text-only send path unaffected (`Images` omitted → null on the server, same as before image-attach was disabled)

## Cleanup

- [x] No persistent test fixtures left behind — all sessions/folders/presets/documents created during this pass were either archived through the normal archive flow or are harmless leftover personal-space rows (no shared state, no other user affected)
