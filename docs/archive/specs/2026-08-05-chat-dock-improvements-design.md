# Slice A.2: Chat Dock Improvements — Design Spec

**Date:** 2026-08-05
**Status:** Reviewed (draft claims verified/corrected against actual code 2026-08-05 — see corrections in sections B, D, E, F, G)
**Branch:** `task/web-chat-panel` (same branch, stacked on Slice A)
**Parent:** Phase 7 Chat
**Depends on:** Slice A (ChatDock, identity prompt, ChatPanel removal)

## Goal

Fix gaps and regressions surfaced by user testing of Slice A's global chat dock. The dock needs: a history panel with infinite scroll, deferred session creation (draft mode), per-session model persistence, card-context awareness, robust auto-title fallback, proper z-index stacking above modals, half-screen height, and `/chats` page infinite scroll. Also: update the identity prompt to be model-honest about capabilities.

## Motivation

User testing of Slice A revealed several issues:

1. **No history in popup** — only way to see previous chats is archive + reload. The dock shows only the current session.
2. **Empty session created on first open** — clicking the FAB creates a server-side session before the user types anything, cluttering history with empty sessions.
3. **Auto-title broken for local models** — title generation fails, leaving "New chat" forever. Cloud models work fine. Not a cancellation issue: `GenerateReplyAsync` runs as a decoupled Hangfire job (`CancellationToken.None`, by design — see `ChatMessagesController.cs:65-73`), so a closed popup/dropped connection can't be killing it. Most likely: the `"openai-compatible"` named `HttpClient` has a 60s timeout (`LlmServiceCollectionExtensions.cs:24-30`) — sized for cloud providers — while the dedicated `"ollama"` client is 600s specifically because local models routinely blow past 60s (see the comment at `LlmServiceCollectionExtensions.cs:44-51`, already fixed once for narrative jobs for the identical reason). If the user's local model is registered as an `OpenAiCompatible` provider (common — pointing at Ollama's/LM Studio's OpenAI-compat endpoint) rather than the native `Ollama` adapter type, title-gen's follow-up call after an already-slow full reply can trip the 60s ceiling. Needs confirming against `LlmChatTitleGenerator`'s specific failure-path warning log before deciding the fix.
4. **Model resets on refresh** — board chat defaults to `qwen3-coder:latest` after reload because model selection is localStorage-only and feature mismatch (dock passes `PersonalChat` even on board, server routes `ProjectChat`).
5. **Card context lost** — old `ChatPanel` passed `openCardId` to sessions; ChatDock has no card awareness, so chatting about the open card requires manual context.
6. **Z-index conflict (suspected, unverified)** — ChatDock popup (z-50) shares the same layer as UModal; card modal backdrop *may* block dock interaction. Not yet confirmed in a real browser — see section G, the CSS evidence points the other way.
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
- Card context wiring (promote `selectedCardId` from a `board.vue` local ref into the board Pinia store, dock reads it from there, passes `openCardId` to new sessions)
- Z-index: verify the actual failure in a browser first; only then apply a fix (see section G — current CSS analysis suggests the popup already stacks above modals)
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

**API:** `GET /api/chat/sessions?before={cursor}&limit=20` (existing endpoint).

**Pre-existing pagination bug (must fix, in scope for this plan):** `EfChatSessionRepository.ListAsync` currently filters `before` against `CreatedAt` but **sorts** by `UpdatedAt`, with no `beforeId` tiebreaker. This is dormant today because nothing pages past page 1, but the moment infinite scroll exercises page 2+, any session whose `UpdatedAt` changes (new message, rename) without its `CreatedAt` changing gets skipped or duplicated across pages. Fix: switch to a composite cursor on `(UpdatedAt, Id)` with a `beforeId Guid?` tiebreaker, matching the pattern already documented in CLAUDE.md for `IChatMessageRepository.GetBySessionAsync`'s `(CreatedAt, Id)` cursor — `WHERE UpdatedAt < @before OR (UpdatedAt == @before AND Id < @beforeId)`. The list endpoint and DTO need the extra `beforeId` param threaded through.

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

### D. Auto-Title: Fix the Root Cause First, Fallback as Belt-and-Suspenders

**Do not ship the fallback alone.** The user's local models work and are correctly configured — they're just slower than cloud models. A silent truncation fallback masks that instead of fixing it, and the user explicitly asked for the local case to actually succeed, not degrade gracefully.

**Investigate first:** `LlmChatTitleGenerator` logs a distinct warning for each failure path (routing failure, error/content-filter finish, empty title, LLM call exception) — check server logs for which one is actually firing for the local-model case before picking a fix. Leading suspect: the `"openai-compatible"` named `HttpClient` has a **60s** timeout (`LlmServiceCollectionExtensions.cs:24-30`), while the dedicated `"ollama"` client is **600s** specifically because local models routinely exceed 60s (see the comment at `LlmServiceCollectionExtensions.cs:44-51` — this exact class of bug was already hit and fixed once for the narrative job). If the user's local model is registered as an `OpenAiCompatible` provider rather than the native `Ollama` adapter type, it inherits the 60s ceiling. `HttpClient.Timeout` cancels the whole request including a streamed read, not just the header wait, so this fails as a generic cancelled-looking error, not an obvious timeout message — consistent with "fails silently."

**If confirmed:** either route the `ChatTitle` `AiFeature` tier to a faster/smaller local model, or give `OpenAiCompatible`-typed providers pointing at local endpoints a longer timeout (config-driven, not a blanket global bump — cloud providers should keep the tight 60s so a truly hung request doesn't stall the reply pipeline).

**Fallback, on top of the real fix (still worth having for actually-transient failures):** In `ChatReplyGenerator.cs`, after the title gen call:
```csharp
var titleResult = await _titleGenerator.GenerateTitleAsync(userId, userMessage.Content, content, ct);
var title = titleResult.IsSuccess
    ? titleResult.Value
    : userMessage.Content.Length <= 60
        ? userMessage.Content
        : userMessage.Content[..60] + "…";
session.UpdateSettings(title, null, null, null, null);
```
Note: `UpdateSettings` is being widened to 7 params in section E (adds `PreferredModelConfigId`/`PreferredEffort`) — update this call site's arg list accordingly, and update the other existing caller at `ChatSessionService.cs:340` too.

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

**Widening `UpdateSettings` breaks existing callers — fix both:** `ChatSession.UpdateSettings` currently takes 5 params (`title, folderId, personalityId, aiEditMode, searchAllMyDocs`); adding `preferredModelConfigId`/`preferredEffort` makes 7. Both existing call sites need their arg list updated: `ChatSessionService.cs:340` and `ChatReplyGenerator.cs:357` (the latter is also the auto-title call site from section D).

**Frontend changes:**

1. `ChatSessionDetailDto` includes `preferredModelConfigId`/`preferredEffort` → ChatSessionView passes as `initialModelId`/`initialEffort` to ChatInput → ChatModelPicker selects the saved model. **Not free today:** `ChatSessionView` already declares `initialModelId`/`initialEffort` props, but currently only uses them to seed the first auto-send call — they're never forwarded into `ChatInput`/`ChatModelPicker` for actual preselection, and `ChatModelPicker` currently self-manages its selection purely from localStorage in `fetchModels()`. Both `ChatInput.vue` and `ChatModelPicker.vue` need changes to accept and apply an externally-supplied initial model id — add both to the Files list.

2. ChatDock passes correct `feature` prop:
   - `feature="ProjectChat"` when `currentProjectId` is set (board page).
   - `feature="PersonalChat"` otherwise.
   - This ensures the model picker's list matches the server's routing tier.

3. On first message send from dock draft mode, include `preferredModelConfigId`/`preferredEffort` in the create-session POST body.

### F. Card Context Wiring

**Current:** Dock knows `projectId` (from route) but not `openCardId`. **Correction — the earlier draft of this spec claimed "board has `selectedCardId` in its Pinia store"; that's false.** `selectedCardId` (the open-card-modal id) is a plain local `ref` inside `board.vue` (`pages/projects/[id]/board.vue:37`). The board Pinia store (`stores/board.ts:33`) only has `selectedCardIds` (plural) — a `Record<string, boolean>` for bulk multi-select checkboxes, an unrelated feature. Since `ChatDock` is rendered once in `default.vue`'s layout (a separate component tree from the page), it has no way to reach a local ref living inside `board.vue` — there is nothing to "read" yet.

**New:**
1. Promote the open-card-modal id into the board Pinia store as a new field (e.g. `openCardId`, distinct from the existing `selectedCardIds` bulk-select map) — `board.vue` writes to it instead of a local ref. This follows the existing D-43 convention (shared state that crosses component boundaries belongs in the Pinia store, not local refs synced by watchers).
2. Dock reads it via `useBoardStore().openCardId`. When creating a new session from the dock while on a board with a card modal open, include `openCardId` in the POST body.
3. F6 implicit close (server-side, already wired in `ChatSessionService.CreateAsync`) fires for the prior session — matches on `(projectId, openCardId, ownerId)` tuple.
4. No prefill message — user types what they want.

**Edge case:** If the card modal is closed before the user sends the first message, `openCardId` is stale. The dock reads `selectedCardId` at create time (when user types), not at open time. Since the board store updates `selectedCardId` reactively, the value is current at send time.

### G. Z-Index — Verify Before Fixing

**Correction — the earlier draft's root cause is unconfirmed and the CSS evidence contradicts it.** Checked the compiled Nuxt UI v4 theme (`.nuxt/ui/modal.ts`): UModal's overlay and content slots carry **no z-index at all** (`overlay: "fixed inset-0"`, no `z-*` class anywhere), and no app-level override exists (`app.config.ts` has no `ui.modal` z-index, `main.css` has no z-index rules, `reka-ui`'s Dialog primitives set no z-index either). ChatDock's popup is already `z-50` — an explicit positive z-index always paints above a `z-index: auto` box regardless of DOM order, so **the popup should already stack above the modal today.** No ancestor (`UApp`, `UDashboardGroup`, `UDashboardPanel`) sets `transform`/`filter`/`isolation` either, which rules out a stacking-context trap that would make bumping the number pointless.

Repo-wide, the max plain-Tailwind z-index anywhere is `z-50` (`BoardCard`, `BoardFilterBar`, `BoardMobileList`, `ProjectCard`, `ChatDock`) — nothing currently outranks it.

**Before writing any CSS:** manually verify in a real browser — open a card modal, try to type/click in the chat dock. If it's genuinely broken, the more likely mechanism is Reka UI's Dialog *modality* (focus trap / pointer-blocking layer, not paint order) — `UModal`'s `modal` prop defaults to `true` (`Modal.vue:34`), and reka-ui ships a separate `DialogContentNonModal` variant specifically for the non-blocking case. The real fix in that scenario is passing `:modal="false"` to the relevant `UModal` usage(s) (e.g. `CardModal.vue`), not a bigger z-index number. If the browser check confirms it's actually paint-order stacking (e.g. some other component sets an explicit z-index we haven't found), then the CSS-custom-property fix below is still the right shape:
```css
:root {
  --z-chat-fab: 60;
  --z-chat-popup: 70;
}
```
ChatDock uses `z-[var(--z-chat-fab)]` and `z-[var(--z-chat-popup)]` — single file to update if Nuxt UI's z-indexing ever changes. Don't apply this speculatively; confirm the actual failure mode first.

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

**Reuse, don't reinvent:** `ChatSessionView.vue` already has a complete, working inline-rename implementation for its own header (`isEditingTitle`/`startEditTitle`/`submitTitleEdit`, lines 52, 271-312) — copy that pattern rather than writing a new one. The actual gap is only the dock's separate drag-handle header (outside `ChatSessionView`), which currently shows static text with no rename affordance.

**Dock header:** Click the title text → it becomes an input field. On blur/enter, PATCH `/api/chat/sessions/{id}` with new title. On success, update store + localStorage.

**`/chats` sidebar:** Same pattern — click title → inline edit → PATCH on blur/enter.

## UI Design

### ChatDock Layout (revised)

```
┌──────────────────────────────┐ ← z-50 (unless browser check in section G proves a real fix is needed), h-[50vh], draggable
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
- `src/web-ui/app/components/chat/ChatDock.vue` — draft mode, history panel, height, title rename (reuse `ChatSessionView`'s existing rename pattern), sessionRefreshed wiring, feature prop; z-index only if section G's browser check confirms a real bug
- `src/web-ui/app/components/chat/ChatSessionView.vue` — add `feature` prop, pass through to ChatInput; pass `initialModelId`/`initialEffort` from session detail to ChatInput
- `src/web-ui/app/components/chat/ChatInput.vue` — accept externally-supplied initial model id/effort and apply it (currently only `ChatModelPicker`'s own localStorage read drives selection)
- `src/web-ui/app/components/chat/ChatModelPicker.vue` — same; currently self-manages selection purely from localStorage in `fetchModels()`, needs to accept & prefer a passed-in initial value
- `src/web-ui/app/pages/chats/index.vue` — use `useChatSessionList`, infinite scroll, resume active session
- `src/web-ui/app/pages/projects/[id]/board.vue` — write open-card-modal id to the board Pinia store instead of a local ref (see section F — no such store field exists yet)
- `src/web-ui/app/stores/board.ts` — add new `openCardId` field (distinct from existing `selectedCardIds` bulk-select map)
- `src/web-ui/app/assets/css/main.css` — add `--z-chat-fab`/`--z-chat-popup` CSS variables, only if section G's browser check confirms they're needed
- `src/HydraForge.Domain/Entities/Chat/ChatSession.cs` — add `PreferredModelConfigId` + `PreferredEffort`; widen `UpdateSettings` from 5 to 7 params
- `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` — map new columns with `.HasColumnType("text")` for `PreferredEffort`
- `src/HydraForge.Infrastructure/Migrations/<timestamp>_AddChatSessionModelPreferences.cs` — migration
- `src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs` — fix `ListAsync` cursor: sort field (`UpdatedAt`) and filter field (`CreatedAt`) currently mismatch, no `beforeId` tiebreaker; switch to composite `(UpdatedAt, Id)` cursor (see section B)
- `src/HydraForge.Application/Chat/ChatDtos.cs` — add model fields to DTOs + request records; add `beforeId` to the list-sessions query params
- `src/HydraForge.Application/Chat/ChatSessionService.cs` — handle model fields in create/update, store model on first message; update `UpdateSettings` call site at line 340 for the new param count
- `src/HydraForge.Application/Chat/ChatReplyGenerator.cs` — investigate/fix local-model title-gen failure (section D); auto-title fallback to first-message truncation on top; update `UpdateSettings` call site at line 357 for the new param count
- `src/HydraForge.Application/Chat/LlmChatTitleGenerator.cs` — no change expected, but check its failure-path logs first to confirm root cause before touching anything
- `src/HydraForge.Infrastructure/Llm/LlmServiceCollectionExtensions.cs` — only if section D's investigation confirms an HTTP timeout is the root cause: per-provider-type timeout instead of blanket `"openai-compatible"` 60s
- `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs` — accept model fields in create/update; accept `beforeId` on list
- `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs` — test model fields on create/update
- `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs` — test auto-title fallback
- `tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs` — regression test for the composite cursor (same-`UpdatedAt` sessions must not skip/duplicate across pages, mirroring the existing `ChatMessage` same-timestamp regression test)
- `tests/HydraForge.Server.Tests/` — model fields round-trip via API

## Testing

- `useChatSessionList` composable: pagination, load-more, refresh, hasMore detection, composite-cursor edge case (multiple sessions sharing `updatedAt`).
- `ChatDock` component: draft mode renders, history panel toggles, session mode renders ChatSessionView, title rename, height classes applied; z-index only if section G's browser check adds it.
- `ChatDockHistory` component: renders session list, load-more on scroll, click loads session, empty state.
- `chatDock` store: draft/session/history mode transitions, localStorage persistence, card context detection.
- `EfChatSessionRepository.ListAsync`: regression test for the `(UpdatedAt, Id)` composite cursor — sessions sharing the same `UpdatedAt` must not be skipped or duplicated across pages.
- `ChatSessionService` tests: model fields persisted on create/update, stored on first message.
- `ChatReplyGenerator` tests: auto-title fallback fires when LLM title fails, LLM title wins when it succeeds; regression test for whatever section D's investigation identifies as the local-model root cause.
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