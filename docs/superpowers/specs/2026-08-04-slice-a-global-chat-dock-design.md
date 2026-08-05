# Slice A: Global Floating Chat Widget + Hydra Identity Prompt

**Date:** 2026-08-04
**Status:** Approved (brainstormed 2026-08-04)
**Parent:** Phase 7 Chat (`feat/phase-7-chat`)
**Depends on:** Plan 17 (`/chats` page + `ChatSessionView`), Phase 6 LLM infrastructure
**Replaces:** Plan 18 side-rail `ChatPanel.vue` (deleted as part of this slice)

## Goal

Ship a global floating chat widget (bottom-right FAB + popup dialog) that works across the app except on `/chats` pages, plus a Hydra identity system prompt injected per-reply so models know where they're being called and what HydraForge is. Foundation for the later agent loop (Slice B) and project-creation tool (Slice C).

## Motivation

Plan 18's `ChatPanel.vue` side-rail had a dead-icon bug (rendered via `v-if="showChatPanel"` but internally `isOpen` stayed `false` unless a card was open — clicking the board header chat icon with no card selected did nothing). Beyond the bug, the side-rail UX was wrong: the user wants a floating chat bubble available app-wide, not a per-page rail. Separately, models in `/chats` only know about themselves — they don't know they're running inside HydraForge. A compact identity prompt fixes that cheaply and benefits both `/chats` and the new popup (same send path).

## Scope

**In scope:**
- Global floating chat widget (`ChatDock.vue`) — FAB + popup, app-wide via layout.
- Pinia store (`chatDock.ts`) owning sticky session + open/close state, survives route changes.
- Hydra identity system prompt injected per-reply in `ChatReplyGenerator`.
- Plan 18 cleanup: delete `ChatPanel.vue`, revert `board.vue` chat integration.
- Component + store + backend tests.
- Manual validation matrix.

**Out of scope (deferred to Slice B / C / later):**
- Agent loop, tool-calling backend, project-creation tool.
- Page-context injection beyond `projectId` (panel-aware context for agent).
- Card-scoped chat from card modal.
- Admin-configurable identity prompt (currently static in code).
- MCP integration (decided: native tool registry for Slice B, no MCP).

## Architecture

Two independent pieces, one slice:

### A1. Global Floating Chat Widget (frontend)

**Component:** `src/web-ui/app/components/chat/ChatDock.vue` — floating round FAB bottom-right + popup dialog. Rendered once at layout level (`app.vue` or default layout), survives route navigation.

**Pinia store:** `src/web-ui/app/stores/chatDock.ts` — owns `activeSessionId`, `isOpen`, `isCreating`. Methods: `openDock()`, `closeDock()`, `toggleDock()`, `startNewChat(context)`. Survives route changes (Pinia store lifetime > page lifetime).

**Placement:** `app/layouts/default.vue` — single `<ChatDock />` render. Hidden via route check: `useRoute().path.startsWith('/chats')` → hidden, plus hidden on auth pages (auth uses its own layout, so `default.vue` render covers the app pages).

### A2. Hydra Identity System Prompt (backend)

**Injection point:** `ChatReplyGenerator.cs` ~L175, before the personality prompt. Builds the `chatMessages` list per-reply — add a base system message first.

**Per-send injection:** the identity prompt is added on every reply generation, not stored on the session. Model switch mid-conversation stays coherent — the new model gets the identity prompt on the next reply automatically.

## UI Design

### Collapsed state
- Floating round FAB, `fixed bottom-4 right-4 z-40`.
- Chat icon (`i-lucide-message-square` or `i-lucide-messages-square`).
- Optional unread pulse (deferred — no notification API surface for chat yet).

### Expanded state
- Popup dialog, `fixed bottom-20 right-4 z-50`.
- Size: ~380×560px desktop; near full-screen on mobile.
- Header: title + "New chat" button + close button.
- Body: `ChatSessionView` (reused from Plan 17 — already handles streaming, history, rollback, find, export).
- **No backdrop** (non-modal — user can interact with page behind the popup).
- ESC closes (handled inside `ChatDock`).

### Session lifecycle
- **First open:** if no active session, create one scoped to current page context. `projectId` if on `/projects/[id]/board`, else null (user-scoped).
- **Nav away:** session persists (store survives route change). No auto-switch. User keeps chatting in the same session.
- **"New chat" button:** creates fresh session scoped to current page context. F6 implicit-close of prior session (server-side, already implemented in `ChatSessionService` — matches on `(projectId, openCardId, ownerId)` tuple).
- **Model switch mid-conversation:** works — messages persisted server-side, full history sent to LLM each reply, identity prompt re-injected per-reply.

### Page context detection
Store reads `useRoute()`:
- If `route.path` matches `/projects/[id]/board` → extract `projectId` from `route.params.id`.
- Else → `null` (user-scoped chat).

Passed to `POST /api/chat/sessions` as `projectId` on session creation.

## Hydra Identity Prompt

**Content (draft, ~200 tokens):**
```
You are HydraForge's built-in assistant. HydraForge is a project management tool: users organize work into Projects, Boards (columns + cards), Specs, Plans, and Chat sessions. You help users think through their work, draft content, and answer questions about their projects. Be concise and direct. If a user asks about something outside HydraForge's scope, say so.
```

**Order in LLM call:**
1. Hydra identity (always, first).
2. Personality system prompt (if `session.PersonalityId` set — user customization layer).
3. Preset content (if `presetId` passed — wraps user message).
4. RAG context (if retrieved).
5. Conversation history.
6. Current user message.

Personality overrides tone; identity anchors scope. No conflict — they're additive.

**No DB change** — prompt is static, built in code. Could later move to `SystemSettings` if admin-configurable, but YAGNI for now.

**Token cost:** ~200 tokens per reply, one-time per request. Negligible.

## Plan 18 Cleanup

- **Delete** `src/web-ui/app/components/chat/ChatPanel.vue` (side-rail, replaced by `ChatDock`).
- **Revert** `board.vue` chat integration:
  - Remove `import ChatPanel from '~/components/chat/ChatPanel.vue'`.
  - Remove `const showChatPanel = ref(false)`.
  - Remove `showChatPanel.value = true` from `selectedCardId` watcher (keep the `presence.focusCard` / `unfocusCard` calls).
  - Remove the `i-lucide-message-square` header button.
  - Remove the `<ChatPanel>` render block.
- **Card-scoped chat** (open card → chat about that card): deferred. When Slice B lands, agent reads page context; for Slice A, card chat isn't wired (per "step by step" direction).

## Files

**Create:**
- `src/web-ui/app/components/chat/ChatDock.vue`
- `src/web-ui/app/stores/chatDock.ts`
- `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`
- `src/web-ui/app/stores/__tests__/chatDock.test.ts`
- `docs/manual-validation/2026-08-04-slice-a-global-chat-dock-matrix.md`

**Modify:**
- `src/web-ui/app/layouts/default.vue` — render `<ChatDock />` once.
- `src/web-ui/app/pages/projects/[id]/board.vue` — revert Plan 18 chat integration.
- `src/HydraForge.Application/Chat/ChatReplyGenerator.cs` — inject Hydra identity system message.
- `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs` (or equivalent) — assert identity prompt present.

**Delete:**
- `src/web-ui/app/components/chat/ChatPanel.vue`

## Testing

- `ChatDock.vue` component test: open/close via FAB, session creation on first open, route-hide logic (hidden on `/chats`).
- `chatDock.ts` store test: session lifecycle, context detection (board → projectId, else null), `startNewChat` creates new session.
- `ChatReplyGenerator` test: Hydra identity prompt present in LLM call messages, before personality prompt.
- Manual matrix: open dock on project list → chat works; nav to board → same session persists; "New chat" on board → project-scoped; model switch mid-convo → identity stays; `/chats` page → dock hidden.

## Acceptance

```
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
dotnet build
dotnet test
```

## Future Scope (Slice B / C / later)

These are explicitly deferred but informed this design:

- **Slice B — Agent loop + tool-calling backend:** server-side tool-use (model proposes tool call → execute → feed result back). New Application-layer surface, tool registry, security/permissions (agent acting as logged-in user). Native C# tool registry (no MCP — Hydra is its own beast). Foundation everything else sits on.
- **Slice C — Project-creation tool:** first concrete tool in B's registry. AI asks clarifying questions, builds `CreateProject` call matching internal model. Later: read board, summarize panels, etc. Modular per-feature tools.
- **Page-context injection beyond projectId:** agent can read current panel state (which card open, which column focused) for richer context. Builds on Slice B.
- **Card-scoped chat from card modal:** open card → chat about that card. Deferred — when Slice B lands, agent reads page context.
- **Admin-configurable identity prompt:** move the Hydra identity prompt to `SystemSettings` so admins can customize. YAGNI for now.
- **Unread pulse on FAB:** surface unread chat notifications. Needs notification API surface for chat (deferred from Phase 5).

## Decisions

- **Native tool registry over MCP** (Slice B) — Hydra is its own beast, no cross-app tool sharing needed. Simpler, no external process.
- **Per-send identity injection** (not per-session) — model switch mid-conversation stays coherent, new model gets identity on next reply automatically.
- **Approach 3 with caveats** — sticky session + "New chat" picks up context, but no auto-switch on nav (session persists across route changes). Per-send context injection (not per-session) so the chat feels flowing even when user changes model mid-conversation.
- **Non-modal popup** (no backdrop) — user can interact with the page behind the chat.
- **Static identity prompt in code** — YAGNI admin config for now.
