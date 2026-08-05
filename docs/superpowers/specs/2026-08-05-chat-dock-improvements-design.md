# Slice A.2: Chat Dock Improvements — Design Spec

**Date:** 2026-08-05
**Status:** Draft
**Branch:** `task/web-chat-panel` (same branch, stacked on Slice A)
**Parent:** Phase 7 Chat
**Depends on:** Slice A (ChatDock, identity prompt, ChatPanel removal)

## Goal

Fix gaps and regressions surfaced by user testing of Slice A's global chat dock. The dock needs: a history panel with infinite scroll, deferred session creation (draft mode), per-session model persistence, card-context awareness, robust auto-title fallback, proper z-index stacking above modals, half-screen height, and `/chats` page infinite scroll. Also: update the identity prompt to be model-honest about capabilities.

## Motivation

User testing of Slice A revealed several issues:

1. **No history in popup** — only way to see previous chats is archive + reload. The dock shows only the current session.
2. **Empty session created on first open** — clicking the FAB creates a server-side session before the user types anything, cluttering history with empty sessions.
3. **Auto-title broken for local models** — title generation fails silently (routing mismatch or cancellation), leaving "New chat" forever. Cloud models work fine.
4. **Model resets on refresh** — board chat defaults to `qwen3-coder:latest` after reload because model selection is localStorage-only and feature mismatch (dock passes `PersonalChat` even on board, server routes `ProjectChat`).
5. **Card context lost** — old `ChatPanel` passed `openCardId` to sessions; ChatDock has no card awareness, so chatting about the open card requires manual context.
6. **Z-index conflict** — ChatDock popup (z-50) shares the same layer as UModal; card modal backdrop blocks dock interaction.
7. **Height too small** — fixed 560px is less than half screen on most displays.
8. **`/chats` page top-20 limit** — same archive+reload problem as the dock.
9. **Identity prompt claims capabilities that don't exist** — model hallucinated project creation. Prompt should be honest about current vs planned capabilities, and model-aware.

## Scope

**In scope:**
- ChatDock draft mode + history panel + session resume (localStorage)
- Shared `useChatSessionList` composable (cursor-paginated, infinite scroll) for dock + `/chats` page
- `/chats` page infinite scroll
- Auto-title fallback (first-message truncation when LLM fails)
- Per-session model persistence (server-side: `PreferredModelConfigId` + `PreferredEffort` on `ChatSession`)
- Card context wiring (dock reads `selectedCardId` from board store, passes `openCardId` to new sessions)
- Z-index: CSS custom properties (`--z-chat-fab`, `--z-chat-popup`) set above all existing z-indexes
- Height: `h-[50vh]` (half screen)
- Identity prompt update: model-honest capabilities description
- Title inline rename (dock header + `/chats` sidebar)
- Tests for all new frontend + backend

**Out of scope (deferred):**
- Folder UI in dock history (endpoint accepts `folderId`, UI ships when folders are implemented)
- Project creation tool (Slice C — needs tool registry first)
- Unread pulse on FAB (needs notification API surface for chat)
- Window position localStorage persistence (YAGNI — default bottom-right on mount)

## Architecture

### A. Chat Bubble Behavior: Draft Mode + History + Resume

**Current:** Click FAB → auto-creates server-side session with `title: 'New chat'` → loads into ChatSessionView.

**New:** Click FAB → opens popup in **draft mode** (no server-side session). Draft mode renders a chat input + empty area (not ChatSessionView, which requires a sessionId). A history panel (list of past sessions) is accessible via a toggle/button in the header.

**Draft → session transition:**
1. User types first message and hits send.
2. Frontend POSTs `/api/chat/sessions` (with `projectId`, `openCardId` if applicable, `preferredModelConfigId`/`preferredEffort` from the model picker).
3. On success, POSTs the message to `/api/chat/sessions/{id}/messages`.
4. Switches to session mode: renders ChatSessionView with the new sessionId.

**"New chat" button:** Clears current state (draft or session) → returns to draft mode. If a session was active, it stays open server-side (no F6 close — user can resume later from history).

**Session resume:**
- `activeSessionId` persisted in localStorage (`hydraforge:chat:activeSessionId`).
- On popup open: if localStorage has a saved sessionId AND the session is still active (not archived/closed), load it into session mode instead of draft.
- Close (X/ESC): hides popup, **preserves** `activeSessionId` in localStorage. Next open → resume the same session.
- "New chat" in popup: clears `activeSessionId` from localStorage → returns to draft mode. The old session stays active server-side (user can resume from history).
- On reload: localStorage survives → session resumes.

**Navigate to `/chats` while in a session:**
- `/chats` page reads the shared Pinia store's `activeSessionId`.
- If set → `/chats` opens with that session loaded (right panel shows ChatSessionView, left panel shows history list with it highlighted).
- If not set → `/chats` opens with draft mode + history list.

**Implementation notes:**
- ChatDock gets a mode state: `'draft' | 'session' | 'history'`.
- Draft mode: minimal UI (chat input + empty state). No ChatSessionView.
- History mode: scrollable list of sessions. Click → load → switch to session mode.
- Session mode: ChatSessionView (existing component, unchanged).
- New composable `useChatSessionList` for the history list (shared with `/chats` page).

### B. History List: `useChatSessionList` Composable

**API:** `GET /api/chat/sessions?before={cursor}&limit=20` (existing endpoint, cursor-paginated by `UpdatedAt`).

**Composable interface:**
```typescript
useChatSessionList(options?: { folderId?: string; projectId?: string })
// Returns:
//   sessions: Ref<ChatSessionDto[]>
//   loading: Ref<boolean>
//   hasMore: Ref<boolean>
//   loadMore(): Promise<void>
//   refresh(): Promise<void>
```

**Infinite scroll:** IntersectionObserver on a sentinel element at the bottom of the list. When visible → calls `loadMore()` → fetches next page using the last session's `updatedAt` as cursor. `hasMore` is `true` as long as the server returns a full page (limit == 20).

**Future-proofing:** The composable accepts `folderId` and `projectId` params. When folders UI ships, the dock history panel passes `folderId` to filter. The endpoint already supports it.

**Used by:** ChatDock history panel + `/chats` page sidebar.

### C. `/chats` Page Infinite Scroll

**Current:** Flat top-20 fetch on mount, no load-more. User must archive sessions to see older ones.

**New:** Replace the single `fetchSessions()` call with `useChatSessionList`. The sidebar list gets an IntersectionObserver sentinel at the bottom. On scroll to bottom → `loadMore()`. Same pattern as dock history panel.

**Session resume on mount:** On page load, `/chats` checks the Pinia store's `activeSessionId` (from in-app navigation) then falls back to localStorage. If a previous session is found and still active, it loads in the right panel. Otherwise, shows draft mode (empty state + chat input).

### D. Auto-Title Fallback

**Current:** `ChatReplyGenerator.cs` calls `_titleGenerator.GenerateTitleAsync(...)`. On failure, logs warning and title stays as-is ("New chat" or first-line-derived).

**New:** In `ChatReplyGenerator.cs`, after the title gen call:
```csharp
var titleResult = await _titleGenerator.GenerateTitleAsync(userId, userMessage.Content, content, ct);
var title = titleResult.IsSuccess
    ? titleResult.Value
    : userMessage.Content.Length <= 60
        ? userMessage.Content
        : userMessage.Content[..60] + "…";
session.UpdateSettings(title, null, null, null, null);
```

This guarantees every session gets a meaningful title. LLM-generated titles still win when they succeed (cloud models, fast local models). Fallback handles: routing failure, cancellation (user closed popup), timeout, empty response, content filter — everything.

**Wire `@sessionRefreshed` in ChatDock:** ChatSessionView emits `sessionRefreshed(id, title, status)` after the title is set server-side. ChatDock must listen and update the header title + history list item title.

### E. Per-Session Model Persistence

**Why:** Model selection currently lives in localStorage (`hydraforge:chat:preferredModel:{feature}`). This resets on browser refresh, doesn't survive device switch, and the dock passes `PersonalChat` feature even on boards (server routes `ProjectChat` — list mismatch).

**Server-side changes:**

1. Add to `ChatSession` entity:
   - `PreferredModelConfigId` (Guid?, nullable) — the `ProviderModelConfig.Id` the user chose.
   - `PreferredEffort` (string?, nullable) — reasoning effort if applicable.

2. EF migration for new columns.

3. DTO changes:
   - `ChatSessionDto` + `ChatSessionDetailDto`: add `preferredModelConfigId` and `preferredEffort`.
   - `CreateChatSessionRequest`: add optional `preferredModelConfigId` and `preferredEffort`.
   - `UpdateChatSessionRequest` (PATCH): add optional `preferredModelConfigId` and `preferredEffort`.

4. `ChatSessionService.CreateAsync`: store `preferredModelConfigId`/`preferredEffort` from request onto the session.

5. `ChatSessionService.UpdateAsync`: accept model fields in PATCH.

6. `ChatReplyGenerator`: on first message of a session that has no `PreferredModelConfigId` set yet, store the model config used for the reply onto the session (so the model survives refresh even if the client never explicitly set it).

**Frontend changes:**

1. `ChatSessionDetailDto` includes `preferredModelConfigId`/`preferredEffort` → ChatSessionView passes as `initialModelId`/`initialEffort` to ChatInput → ChatModelPicker selects the saved model.

2. ChatDock passes correct `feature` prop:
   - `feature="ProjectChat"` when `currentProjectId` is set (board page).
   - `feature="PersonalChat"` otherwise.
   - This ensures the model picker's list matches the server's routing tier.

3. On first message send from dock draft mode, include `preferredModelConfigId`/`preferredEffort` in the create-session POST body.

### F. Card Context Wiring

**Current:** Dock knows `projectId` (from route) but not `openCardId`. Board has `selectedCardId` in its Pinia store but dock doesn't read it.

**New:**
1. Dock reads `selectedCardId` from the board Pinia store (`useBoardStore().selectedCardId`).
2. When creating a new session from the dock while on a board with a card modal open, include `openCardId` in the POST body.
3. F6 implicit close (server-side, already wired in `ChatSessionService.CreateAsync`) fires for the prior session — matches on `(projectId, openCardId, ownerId)` tuple.
4. No prefill message — user types what they want.

**Edge case:** If the card modal is closed before the user sends the first message, `openCardId` is stale. The dock reads `selectedCardId` at create time (when user types), not at open time. Since the board store updates `selectedCardId` reactively, the value is current at send time.

### G. Z-Index

**Current:** UModal has no explicit z-index (overlay is `fixed inset-0 bg-elevated/75`, content has none). ChatDock popup is `z-50`, FAB is `z-40`. Both share the same stacking context layer as UModal — modal backdrop blocks dock interaction.

**Fix:** Define CSS custom properties in `src/web-ui/app/assets/css/main.css`:
```css
:root {
  --z-chat-fab: 60;
  --z-chat-popup: 70;
}
```

ChatDock uses `z-[var(--z-chat-fab)]` and `z-[var(--z-chat-popup)]`. These are above any existing z-index (max used is 50). Single file to update if Nuxt UI ever changes its z-indexing.

### H. Height

**Current:** `h-[560px]` fixed.

**New:** `h-[50vh]` (half the viewport height). Mobile: `max-h-[calc(100vh-2rem)]` (near full screen with margins).

### I. Identity Prompt Update

**Current hardcoded default:**
```
You are HydraForge's built-in assistant. HydraForge is a project management tool: users organize work into Projects, Boards (columns + cards), Specs, Plans, and Chat sessions. You help users think through their work, draft content, and answer questions about their projects. Be concise and direct. If a user asks about something outside HydraForge's scope, say so.
```

**New default (model-honest):**
```
You are HydraForge's built-in assistant. HydraForge is a project management tool: users organize work into Projects, Boards (columns + cards), Specs, Plans, and Chat sessions.

Your capabilities:
- Answer questions about the user's projects using context from the current board, cards, specs, and plans.
- Generate and refine text content (descriptions, specs, plans, notes).
- Analyze and discuss images if your model supports vision.
- Generate images if your model supports image generation (e.g., DALL-E, Stable Diffusion).

Your limitations:
- You CANNOT create, modify, or delete projects, cards, boards, or any data. Those require future tool capabilities not yet available.
- You CANNOT access the internet or external services unless specifically configured.
- Be honest about your capabilities — only claim abilities you actually have. If you cannot view images, do not claim you can. If you cannot generate images, do not claim you can.

Future planned capabilities include: project creation, card management, deep research, and agent-driven workflows. These are not available yet.

Be concise and direct.
```

This prompt:
- Tells the model to self-describe honestly (no hallucinated capabilities).
- Lists what it CAN do (text gen, image analysis if multimodal, image gen if supported).
- Explicitly says it CANNOT create/modify data (prevents project-creation hallucination).
- Mentions future capabilities so the model can answer "what's planned?".
- The model's own system prompt + training determines what it actually can do — the prompt just tells it to be honest.

Admin can still override via the settings page.

### J. Title Inline Rename

**Dock header:** Click the title text → it becomes an input field. On blur/enter, PATCH `/api/chat/sessions/{id}` with new title. On success, update store + localStorage.

**`/chats` sidebar:** Same pattern — click title → inline edit → PATCH on blur/enter.

## UI Design

### ChatDock Layout (revised)

```
┌──────────────────────────────┐ ← z-[70], h-[50vh], draggable
│ [☰ History] [Title / ✏️] [➕][✕] │ ← drag handle header
├──────────────────────────────┤
│                              │
│  Draft mode:                 │
│  ┌────────────────────────┐  │
│  │ Type a message...      │  │
│  └────────────────────────┘  │
│                              │
│  OR                          │
│                              │
│  History list:               │
│  ┌─ Project Chat ──────────┐ │
│  │ Last message preview...  │ │
│  ├──────────────────────────┤ │
│  │ Another Chat            │ │
│  │ Last message preview...  │ │
│  ├──────────────────────────┤ │
│  │ ...                      │ │
│  │ [load more ▼]           │ │ ← IntersectionObserver sentinel
│  └──────────────────────────┘ │
│                              │
│  OR                          │
│                              │
│  Session mode:               │
│  ┌ ChatSessionView ────────┐ │
│  │ (existing component)     │ │
│  └──────────────────────────┘ │
└──────────────────────────────┘
```

**Header buttons:**
- ☰ History: toggles history panel overlay (slides in from left within the popup).
- Title: click to rename inline.
- ➕ New chat: clears current state → draft mode.
- ✕ Close: closes popup (ESC also works).

### Mode State Machine

```
         ┌──────────┐
         │  CLOSED  │
         └────┬─────┘
              │ click FAB
              ▼
         ┌──────────┐
         │  DRAFT   │◄─────────────────────┐
         └────┬─────┘                      │
              │                            │
     ┌────────┼────────┐                   │
     │        │        │                   │
     ▼        ▼        ▼                   │
 ┌──────┐ ┌──────┐ ┌──────┐               │
 │SEND  │ │HIST  │ │NEW   │               │
 │MSG   │ │ORY   │ │CHAT  │               │
 └──┬───┘ └──┬───┘ └──────┘               │
    │        │        │                   │
    ▼        ▼        └───────────────────┘
 ┌──────────┐
 │ SESSION  │──→ [NEW CHAT] ──────────────┘
 └──────────┘
    │
    │ [CLOSE/ESC]
    ▼
 ┌──────────┐
 │  CLOSED  │
 └──────────┘
```

## Files

**Create:**
- `src/web-ui/app/composables/useChatSessionList.ts` — shared cursor-paginated session list composable
- `src/web-ui/app/composables/__tests__/useChatSessionList.test.ts`
- `src/web-ui/app/components/chat/ChatDockHistory.vue` — history list panel for dock
- `src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts`
- `docs/manual-validation/2026-08-05-chat-dock-improvements-matrix.md`

**Modify:**
- `src/web-ui/app/stores/chatDock.ts` — add draft mode, history mode, localStorage persistence, card context
- `src/web-ui/app/components/chat/ChatDock.vue` — draft mode, history panel, z-index, height, title rename, sessionRefreshed wiring, feature prop
- `src/web-ui/app/components/chat/ChatSessionView.vue` — add `feature` prop, pass through to ChatInput; pass `initialModelId`/`initialEffort` from session detail to ChatInput
- `src/web-ui/app/pages/chats/index.vue` — use `useChatSessionList`, infinite scroll, resume active session
- `src/web-ui/app/pages/projects/[id]/board.vue` — export `selectedCardId` to dock (already in board store, ensure dock can read it)
- `src/web-ui/app/assets/css/main.css` — add `--z-chat-fab` and `--z-chat-popup` CSS variables
- `src/HydraForge.Domain/Entities/Chat/ChatSession.cs` — add `PreferredModelConfigId` + `PreferredEffort`
- `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` — map new columns with `.HasColumnType("text")` for `PreferredEffort`
- `src/HydraForge.Infrastructure/Migrations/<timestamp>_AddChatSessionModelPreferences.cs` — migration
- `src/HydraForge.Application/Chat/ChatDtos.cs` — add model fields to DTOs + request records
- `src/HydraForge.Application/Chat/ChatSessionService.cs` — handle model fields in create/update, store model on first message
- `src/HydraForge.Application/Chat/ChatReplyGenerator.cs` — auto-title fallback to first-message truncation
- `src/HydraForge.Application/Chat/LlmChatTitleGenerator.cs` — no change needed (fallback is in caller)
- `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs` — accept model fields in create/update
- `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs` — test model fields on create/update
- `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs` — test auto-title fallback
- `tests/HydraForge.Server.Tests/` — model fields round-trip via API

## Testing

- `useChatSessionList` composable: pagination, load-more, refresh, hasMore detection.
- `ChatDock` component: draft mode renders, history panel toggles, session mode renders ChatSessionView, title rename, z-index classes applied, height classes applied.
- `ChatDockHistory` component: renders session list, load-more on scroll, click loads session, empty state.
- `chatDock` store: draft/session/history mode transitions, localStorage persistence, card context detection.
- `ChatSessionService` tests: model fields persisted on create/update, stored on first message.
- `ChatReplyGenerator` tests: auto-title fallback fires when LLM title fails, LLM title wins when it succeeds.
- `/chats` page: infinite scroll loads more sessions, resume active session from store.
- Admin settings: identity prompt round-trip (existing, no change needed).

## Acceptance

```
dotnet build
dotnet test
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build && pnpm test
```

## Future Scope

- **Folder UI in dock history** — endpoint accepts `folderId`; composable is ready; UI ships when folders are implemented.
- **Project creation tool** — Slice C, needs tool registry (Slice B) first.
- **Unread pulse on FAB** — needs notification API surface for chat.
- **Window position localStorage** — YAGNI, default bottom-right on mount.
- **Deep research / agent-driven workflows** — future capability mentioned in identity prompt, not yet implemented.