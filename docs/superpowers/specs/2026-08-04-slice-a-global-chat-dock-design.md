# Slice A: Global Floating Chat Widget + Hydra Identity Prompt

**Date:** 2026-08-04
**Status:** Approved (brainstormed 2026-08-04, revised 2026-08-04)
**Branch:** `task/web-chat-panel` (same branch as Plan 18 work — enhanced in place)
**Parent:** Phase 7 Chat (`feat/phase-7-chat`)
**Depends on:** Plan 17 (`/chats` page + `ChatSessionView`), Phase 6 LLM infrastructure
**Replaces:** Plan 18 side-rail `ChatPanel.vue` (deleted as part of this slice)

## Goal

Ship a global floating chat widget (draggable, always-on-top window — no backdrop) that works across the app except on `/chats` pages, plus a Hydra identity system prompt persisted per-conversation (hidden from UI) so models know where they're being called and what HydraForge is. Foundation for the later agent loop (Slice B) and project-creation tool (Slice C), which stack as separate plans on the same branch.

## Motivation

Plan 18's `ChatPanel.vue` side-rail had a dead-icon bug (rendered via `v-if="showChatPanel"` but internally `isOpen` stayed `false` unless a card was open — clicking the board header chat icon with no card selected did nothing). Beyond the bug, the side-rail UX was wrong: the user wants a floating chat available app-wide, usable even while a card modal is open (modals have backdrops that block page interaction — only a high-z-index floating window solves "chat while modal open"). Separately, models in `/chats` only know about themselves — they don't know they're running inside HydraForge. A compact identity prompt persisted per-conversation fixes that cheaply and benefits both `/chats` and the new popup (same send path).

## Scope

**In scope (Slice A — this plan):**
- Global floating chat widget (`ChatDock.vue`) — draggable window, no backdrop, high z-index above modals.
- Pinia store (`chatDock.ts`) owning sticky session + open/close/drag state, survives route changes.
- Hydra identity system prompt persisted as a `System` message per conversation (hidden from UI), flows with history to the LLM.
- Admin-configurable identity prompt via `SystemSettings` (existing admin settings page).
- Plan 18 cleanup: delete `ChatPanel.vue`, revert `board.vue` chat integration.
- Component + store + backend tests.
- Manual validation matrix.

**Out of scope (deferred to Slice B / C / later — separate plans, same branch):**
- **Slice B — Agent loop + tool-calling backend:** server-side tool-use (model proposes tool call → execute → feed result back). New Application-layer surface, tool registry, security/permissions (agent acting as logged-in user). Native C# tool registry (no MCP — Hydra is its own beast). Foundation everything else sits on.
- **Slice C — Project-creation tool:** first concrete tool in B's registry. AI asks clarifying questions, builds `CreateProject` call matching internal model. Later: read board, summarize panels, etc. Modular per-feature tools.
- **Page-context injection beyond projectId:** agent can read current panel state (which card open, which column focused) for richer context. Builds on Slice B.
- **Card-scoped chat from card modal:** open card → chat about that card. Uncertain — revisit after B/C (agent may handle via card-number mentions in chat rather than explicit card-scoping).
- **MCP integration:** decided against — native tool registry only.
- **Unread pulse on FAB:** surface unread chat notifications. Needs notification API surface for chat (deferred from Phase 5).

## Architecture

### A1. Global Floating Chat Widget (frontend)

**Component:** `src/web-ui/app/components/chat/ChatDock.vue` — floating draggable window. Rendered once in `default.vue` layout, survives route navigation.

**Why floating window (not rail, not modal):**
- Modal has backdrop → blocks page interaction → can't chat while card modal open. Rejected.
- Right/bottom rail attached to page → modal backdrop covers it → same problem. Rejected.
- Floating draggable window, high z-index, no backdrop → sits above modals, user interacts with page behind, draggable out of the way. Only option that satisfies "chat while modal open." Selected.

**Pinia store:** `src/web-ui/app/stores/chatDock.ts` — owns `activeSessionId`, `isOpen`, `isCreating`, window position (`{ x, y }`). Methods: `openDock()`, `closeDock()`, `toggleDock()`, `startNewChat(context)`. Survives route changes (Pinia store lifetime > page lifetime).

**Placement:** `src/web-ui/app/layouts/default.vue` — single `<ChatDock />` render. Hidden via route check: `useRoute().path.startsWith('/chats')` → hidden. Auth pages use their own layout (`auth.vue`), so `default.vue` covers app pages only.

**Dragging:** `@vueuse/core` 14.4.0 already a dep — `useDraggable` handles pointer-event dragging. Window position persisted in the Pinia store (survives route nav, resets on page reload unless localStorage-persisted — YAGNI for now, default bottom-right on mount).

### A2. Hydra Identity System Prompt (backend)

**Persistence:** On session creation, persist the identity prompt as a `ChatMessage` with `Role = System` in the DB. This is a one-time write per conversation.

**Flows with history:** `ChatReplyGenerator` pulls full history via `messageRepo.GetBySessionAsync` and maps each message (including the System role) to the LLM call's message array. The identity prompt is naturally included every reply as part of history — no per-reply injection code. LLM APIs are stateless, so history is re-sent each call regardless; the identity prompt rides along.

**Hidden from UI:** `ChatSessionView` filters `role === 'System'` messages from the rendered list. The user never sees the identity prompt — it looks flawless. (Currently `ChatMessageBubble` only distinguishes User vs other; System messages would render as assistant-style bubbles if not filtered. Filtering in `ChatSessionView` after fetch is the clean cut.)

**Token usage:** The ~200-token System message is counted in every reply's history. `UsageRecorder` (Phase 6) tracks tokens per call naturally — no special handling. This is the standard, token-efficient approach: stored once, sent as part of the history array each call (unavoidable for stateless LLM APIs).

**Model switch mid-conversation:** works — the System message is in history, the new model sees it on the next reply. No re-injection logic needed.

**Order in LLM call** (built by `ChatReplyGenerator` from history + injected personality):
1. Hydra identity `System` message (from history, persisted on creation).
2. Personality system prompt (if `session.PersonalityId` set — injected per-reply at L182, current behavior, NOT persisted as a message).
3. Preset content (if `presetId` passed — wraps user message).
4. RAG context (if retrieved — compressed separately).
5. Conversation history (User/Assistant/Tool messages).
6. Current user message.

Personality overrides tone; identity anchors scope. Additive, no conflict.

### A3. Admin-Configurable Identity Prompt

**Storage:** `SystemSettings` entity — new field `AiIdentityPrompt` (string, nullable). When null/empty, fall back to the hardcoded default. Admin settings page (existing) gets a new field editor.

**Injection:** `ChatReplyGenerator` reads `SystemSettings.AiIdentityPrompt` (via the settings repo, already injected). If set, use it; else use the hardcoded default. The persisted System message on session creation uses the value current at creation time — if admin changes it later, existing conversations keep their original prompt (already persisted), new conversations get the new one. Acceptable: avoids re-writing history.

**Default prompt (hardcoded fallback, ~200 tokens):**
```
You are HydraForge's built-in assistant. HydraForge is a project management tool: users organize work into Projects, Boards (columns + cards), Specs, Plans, and Chat sessions. You help users think through their work, draft content, and answer questions about their projects. Be concise and direct. If a user asks about something outside HydraForge's scope, say so.
```

**DB change:** add `AiIdentityPrompt` column to `SystemSettings` table (EF migration, committed with the entity change).

## UI Design

### Collapsed state
- Floating round FAB, `fixed bottom-4 right-4 z-40`.
- Chat icon (`i-lucide-messages-square`).
- Optional unread pulse (deferred — no notification API surface for chat yet).

### Expanded state
- Floating window, `fixed z-50` (above modals — modals use z-40/z-45 range; verify and pick z-index above all existing modals).
- Default position: bottom-right, above the FAB (`bottom-20 right-4`).
- Size: ~380×560px desktop; near full-screen on mobile (`inset-2` with margins).
- Header: drag handle (title bar) + title + "New chat" button + close button. Dragging via pointer events on the header.
- Body: `ChatSessionView` (reused from Plan 17 — already handles streaming, history, rollback, find, export).
- **No backdrop** (non-modal — user can interact with page behind, including card modals).
- ESC closes (handled inside `ChatDock`).

### Session lifecycle
- **First open:** if no active session, create one scoped to current page context. `projectId` if on `/projects/[id]/board`, else null (user-scoped).
- **Nav away:** session persists (store survives route change). No auto-switch. User keeps chatting in the same session.
- **"New chat" button:** creates fresh session scoped to current page context. F6 implicit-close of prior session (server-side, already implemented in `ChatSessionService` — matches on `(projectId, openCardId, ownerId)` tuple).
- **Model switch mid-conversation:** works — messages persisted server-side, full history sent to LLM each reply, identity System message in history.

### Page context detection
Store reads `useRoute()`:
- If `route.path` matches `/projects/[id]/board` → extract `projectId` from `route.params.id`.
- Else → `null` (user-scoped chat).

Passed to `POST /api/chat/sessions` as `projectId` on session creation.

## Plan 18 Cleanup

- **Delete** `src/web-ui/app/components/chat/ChatPanel.vue` (side-rail, replaced by `ChatDock`).
- **Revert** `board.vue` chat integration:
  - Remove `import ChatPanel from '~/components/chat/ChatPanel.vue'`.
  - Remove `const showChatPanel = ref(false)`.
  - Remove `showChatPanel.value = true` from `selectedCardId` watcher (keep the `presence.focusCard` / `unfocusCard` calls).
  - Remove the `i-lucide-message-square` header button.
  - Remove the `<ChatPanel>` render block.
- **Card-scoped chat** (open card → chat about that card): deferred. When Slice B lands, agent reads page context; for Slice A, card chat isn't wired (per "step by step" direction). Multi-card chat (chat about several cards via card-number mentions) is aspirational — revisit after B/C.

## Files

**Create:**
- `src/web-ui/app/components/chat/ChatDock.vue`
- `src/web-ui/app/stores/chatDock.ts`
- `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`
- `src/web-ui/app/stores/__tests__/chatDock.test.ts`
- `docs/manual-validation/2026-08-04-slice-a-global-chat-dock-matrix.md`

**Modify:**
- `src/web-ui/app/layouts/default.vue` — render `<ChatDock />` once.
- `src/web-ui/app/components/chat/ChatSessionView.vue` — filter `role === 'System'` messages from rendered list.
- `src/web-ui/app/pages/projects/[id]/board.vue` — revert Plan 18 chat integration.
- `src/HydraForge.Application/Chat/ChatReplyGenerator.cs` — persist identity System message on session creation (or first reply); remove per-reply injection (personality stays per-reply).
- `src/HydraForge.Domain/Entities/SystemSettings.cs` — add `AiIdentityPrompt` property.
- `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` — map new column.
- `src/HydraForge.Infrastructure/Migrations/` — new migration for `AiIdentityPrompt`.
- Admin settings page (Web UI) — add identity prompt editor field.
- Admin settings controller/service (Server) — expose `AiIdentityPrompt` read/write.
- `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs` — assert identity System message persisted on creation, present in LLM call.
- `tests/HydraForge.Server.Tests/` — admin settings round-trip for `AiIdentityPrompt`.

**Delete:**
- `src/web-ui/app/components/chat/ChatPanel.vue`

## Testing

- `ChatDock.vue` component test: open/close via FAB, session creation on first open, route-hide logic (hidden on `/chats`), drag behavior.
- `chatDock.ts` store test: session lifecycle, context detection (board → projectId, else null), `startNewChat` creates new session, window position state.
- `ChatSessionView` test: System-role messages filtered from rendered list.
- `ChatReplyGenerator` test: identity System message persisted on session creation, present in LLM call messages, hidden from UI fetch.
- Admin settings round-trip: `AiIdentityPrompt` read/write via admin endpoint.
- Manual matrix: open dock on project list → chat works; nav to board → same session persists; "New chat" on board → project-scoped; model switch mid-convo → identity stays; `/chats` page → dock hidden; open card modal → chat dock usable above modal; drag window → repositions.

## Acceptance

```
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
dotnet build
dotnet test
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

## Future Scope (Slice B / C / later)

Explicitly deferred but informed this design. Stacked as separate plans on the same branch (`task/web-chat-panel`):

- **Slice B — Agent loop + tool-calling backend:** server-side tool-use (model proposes tool call → execute → feed result back). New Application-layer surface, tool registry, security/permissions (agent acting as logged-in user). Native C# tool registry (no MCP — Hydra is its own beast). Foundation everything else sits on.
- **Slice C — Project-creation tool:** first concrete tool in B's registry. AI asks clarifying questions, builds `CreateProject` call matching internal model. Later: read board, summarize panels, etc. Modular per-feature tools.
- **Page-context injection beyond projectId:** agent can read current panel state (which card open, which column focused) for richer context. Builds on Slice B.
- **Card-scoped chat from card modal:** uncertain — revisit after B/C. May be handled via card-number mentions in chat (agent resolves context) rather than explicit card-scoping.
- **Unread pulse on FAB:** surface unread chat notifications. Needs notification API surface for chat (deferred from Phase 5).
- **Window position localStorage persistence:** remember last drag position across reloads. YAGNI for now, default bottom-right on mount.

## Decisions

- **Floating draggable window** (not rail, not modal) — only option that satisfies "chat while card modal open" (modals have backdrops; only a high-z-index floating window sits above them).
- **Persist identity prompt as System message per conversation** — stored once, flows with history to LLM each reply (stateless API requires re-sending; unavoidable). Hidden from UI (filtered in `ChatSessionView`). Token usage tracked naturally by `UsageRecorder`.
- **Admin-configurable identity prompt** via `SystemSettings.AiIdentityPrompt` — null falls back to hardcoded default. Existing conversations keep their persisted prompt if admin changes it later.
- **Native tool registry over MCP** (Slice B) — Hydra is its own beast, no cross-app tool sharing. Simpler, no external process.
- **Same branch, stacked plans** — `task/web-chat-panel` carries Slice A, B, C as separate plan files + conventional commits. Docs updated as we go.
- **Non-modal popup** (no backdrop) — user can interact with the page behind the chat, including card modals.
- **`@vueuse/core` `useDraggable`** for window dragging — already a dep, no new dependency.
