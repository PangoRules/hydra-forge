# Chat UX Polish — Rename, Copy, Export, Find-in

## Context

Task 17 (Web UI chat streaming, `task/web-chat-view`) shipped ahead of its own written scope, and in reviewing it against ChatGPT/Claude-style chat UX we found four small gaps and one already-shipped-but-undocumented feature:

- **Edit + regenerate a message** — already fully built (`ChatMessageBubble.vue`'s pencil/rotate-ccw buttons → `ChatSessionView.vue`'s rollback flow → `POST /api/chat/sessions/{id}/messages/{messageId}/rollback`, which truncates that message and everything after it). Not in scope here — noted only because it was mistakenly listed as a gap before this was found.
- **Rename session, copy message, export chat, find-in-conversation** — genuine gaps, not covered by any of Plans 18–24. This spec covers all four.

## Scope

Web UI only. No backend/API changes:
- Rename uses the existing `PATCH /api/chat/sessions/{id}` endpoint (`ChatSessionsController.Update`), which is wired but has no UI caller today.
- Copy, export, and find-in are pure client-side — no new endpoints.

Out of scope: server-backed full-history search within a session (find-in searches only what's currently loaded — same "last 50, windowed" content already visible per the existing scroll-window behavior). If a session outgrows that window, this is a known, accepted limit, not a bug — matches how export behaves too (both operate on `session.value.messages` as currently loaded, consistently).

## Design

### 1. Rename session
Pencil-icon button next to the session title in `ChatSessionView.vue`'s header (currently static text, `session?.title ?? 'Chat'`). Click swaps it for an inline `<input>`; Enter or blur submits.

`UpdateChatSessionRequest` (`ChatDtos.cs`) is whole-field replace, not a true partial patch: `{ Title, FolderId, PersonalityId, AiEditMode, SearchAllMyDocs }`. `ChatSessionDto`/`ChatSessionDetailDto` already carry all four non-`Title` fields from the initial session fetch, so no extra round-trip is needed — the rename call reuses the already-loaded session's current values for everything except `Title`.

On success, propagate the new title back to the sidebar list the same way `ChatSessionView` already does for status changes (`@session-refreshed="syncSession"` in `pages/chats/index.vue` — extend `syncSession`'s existing `(id, title, status)` signature usage, no new event needed since it already carries title).

### 2. Copy message
Small copy-icon button on `ChatMessageBubble.vue`, next to the existing edit/regenerate button (same hover-reveal treatment). Copies the raw `message.content` (markdown source, not the rendered/sanitized HTML) via `navigator.clipboard.writeText`. Icon flips to a checkmark for ~1.5s as feedback — no toast, matching the existing edit/regenerate buttons' icon-only feedback style (low-stakes action, no need for toast noise).

### 3. Export chat
Download-icon button in `ChatSessionView.vue`'s header. Purely client-side: walks `session.value.messages`, builds a markdown string (`## User` / `## Assistant` role headers, blank line, message content, blank line between messages), wraps in a `Blob`, triggers a download named `{session.title}.md`. No shared filename-sanitizing helper exists in the codebase today (checked) — inline a one-line regex strip of filesystem-unsafe characters (`/[/\\?%*:|"<>]/g`) rather than adding a new lib file for a single call site.

### 4. Find-in-conversation
Search icon in `ChatSessionView.vue`'s header; click reveals a small input inline. Client-side filter over `session.value.messages` (substring match, case-insensitive), highlights matches within the rendered bubble text, shows a "N of M" counter with next/prev arrows that scroll the matched bubble into view (`scrollIntoView`). Closing the search (Esc or icon toggle again) clears highlights and the input.

## Error handling

- Rename: failed `PATCH` → toast error, input reverts to the last-known title (don't leave the UI showing an unsaved edit as if it succeeded).
- Copy: `navigator.clipboard.writeText` rejection (rare — permissions) → toast error, no icon-flip.
- Export: no failure mode — pure client-side string building + Blob download, nothing to fail against.
- Find-in: no failure mode — pure client-side filter.

## Testing

Unit tests per component, matching the existing `ChatSessionView`/`ChatInput`/`ChatMessageBubble` test file pattern:
- Rename: input shows current title, submits the full `UpdateChatSessionRequest` shape with only `Title` changed, reverts on API failure.
- Copy: clicking the button calls `navigator.clipboard.writeText` with the raw (unrendered) message content.
- Export: given a fixed `messages` array, the generated markdown string matches the expected role-header format.
- Find-in: given a fixed `messages` array and a query, the match count and highlighted-message set are correct; next/prev cycles through matches in order.

No new E2E coverage required — no Playwright chat specs exist yet regardless (a separate, already-flagged gap, not blocking this).
