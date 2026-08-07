# Web Chat Toolbar + Personality Management: Manual Validation

**Date:** 2026-08-06
**Feature:** ChatDock kebab menu, attached-docs collapse, search highlight, personality management modal, seeded personalities, dismiss/close/archive/reopen split, status filter, single-row dock header, composer personality parity

## Environment

- API server running (`ASPNETCORE_ENVIRONMENT=Development`), or the full Docker stack rebuilt (`docker compose up -d --build web server`) — the `web`/`server` services COPY source at image build time, so a code change is invisible until rebuilt.
- Web UI dev server (`pnpm dev`) running, or the Docker stack's `web` service.
- Login as `testadmin` / `TestAdmin123!` (or any seeded user).

## Test Cases

### TC-1: Seeded personalities appear on first fetch

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open any personal (non-project) chat for a user who has never opened the Personality dropdown before | — |
| 2 | Open the Personality dropdown (full-page header) | All 6 seeded personalities appear: well, Carter, Commander Erwin, Captain Levi, Fire_Keeper, Tarnished |
| 3 | Reopen the dropdown / refresh the page | Same 6 personalities — no duplicates, no re-seeding |

**Pass/Fail:** _____

### TC-2: ChatDock kebab menu at dock width

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open the floating chat dock (bottom-right bubble) | Dock opens at 440px wide |
| 2 | Observe the session title | Fully readable, single row, not squeezed by icon buttons |
| 3 | Click the kebab (⋮) button | Menu opens with Rename, Export, Find, Select Personality (submenu), Manage personalities…, Close/Reopen/Archive chat |
| 4 | Click "Manage personalities…" | Modal opens over the dock |

**Pass/Fail:** _____

### TC-3: Full-page chat keeps inline controls

| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to a full-page chat session (`/chats`, select a session) | — |
| 2 | Observe the header | Pencil, download, search, Personality select, Manage personalities, AI-edit-mode (project chats), Close/Reopen/Archive, Dismiss all visible inline — no kebab menu |

**Pass/Fail:** _____

### TC-4: Personality management modal CRUD

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open "Manage personalities" from either full-page header, dock kebab menu, or the composer (see TC-12) | Modal lists existing personalities |
| 2 | Click "New", fill name + system prompt, Save | New personality appears in the list; Personality select in the header now includes it |
| 3 | Click the pencil on a personality, change its description, Save | Change persists, reflected in the list |
| 4 | Click "Set as default" on a non-default personality | Default badge moves to that personality |
| 5 | Click "Archive" on a personality | It disappears from the list and from the header's Personality select |

**Pass/Fail:** _____

### TC-5: Attached docs collapsed by default

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open an owned, active chat session | "Attached docs (N)" summary row shown, list not expanded |
| 2 | Click the summary row | Expands to show the doc list / empty state + "+" attach button |

**Pass/Fail:** _____

### TC-6: Search highlights matched text inline

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open a chat with several messages, click the search icon | Find bar opens |
| 2 | Type a word that appears in a message | The matching message bubble gets a ring outline AND the matched word itself is highlighted with a `<mark>` background inside the bubble text |

**Pass/Fail:** _____

### TC-7: Dismiss vs Close — X never ends the session

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open an active chat (full-page or dock), click the X ("Dismiss") | The chat view/dock closes or deselects — the session itself stays Active, no API call fires |
| 2 | Reopen the same chat | It's still Active, fully interactive, nothing was lost |
| 3 | Click "Close chat" (inline on full page, or the kebab item in the dock) | A confirm dialog appears explaining the summary + AI-edit-revocation + read-only consequence |
| 4 | Confirm | Real `POST .../close` fires, session becomes Closed and read-only, still visible in the list |

**Pass/Fail:** _____

### TC-8: Archive reachable from inside an open chat

| Step | Action | Expected |
|------|--------|----------|
| 1 | On an Active session (full page), click "Archive chat" | Confirm dialog appears |
| 2 | Confirm | Real `DELETE` archive call fires; `archivedAt` is set — Status stays Active (archiving never changes Status) |
| 3 | Repeat from the dock's kebab menu on a different session | Same behavior, reachable without leaving the dock |

**Pass/Fail:** _____

### TC-9: Reopen a closed/archived session

| Step | Action | Expected |
|------|--------|----------|
| 1 | On a Closed session, click "Reopen chat" (inline, kebab, or the `/chats` list row's hover action) | Real `POST .../reopen` fires; session becomes Active again, AI-edit mode resets to Per-mutation |
| 2 | On a session that is both Closed and Archived, click Reopen once | One-step revive — both `ArchivedAt` clears and Status becomes Active in the same action, not two separate steps |

**Pass/Fail:** _____

### TC-10: Status filter on `/chats`

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open `/chats`, use the status filter dropdown | Options: Active & Closed (default), Active, Closed, Archived |
| 2 | Archive a chat, then select "Archived" | The archived chat appears; it was hidden under the default "Active & Closed" filter |
| 3 | Select "Active" only | Only genuinely Active, non-archived sessions show |

**Pass/Fail:** _____

### TC-11: ChatDock single header row, width, full-height

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open the dock in draft mode (no session yet) | One header row: back-to-history chevron, "New Chat" label, "+", full-height toggle, Dismiss |
| 2 | Select or start a session | Exactly one header row (`< back, real TITLE, ⋮ kebab, + new chat, full-height toggle, x dismiss`) — no duplicate/second row |
| 3 | Click the full-height toggle | Dock expands to fill the viewport height, anchored to the top |
| 4 | Click it again | Returns to the normal 50vh height at its previous draggable position |
| 5 | Rename the session from the dock (pencil via kebab, or click the title) | Title updates live in the single header row — no stale "Chat" placeholder |

**Pass/Fail:** _____

### TC-12: Composer personality/manage-personalities before a session exists

| Step | Action | Expected |
|------|--------|----------|
| 1 | On `/chats` with no active session (empty compose state) | Composer shows Personality picker + Manage personalities button, alongside the existing model picker and preset picker |
| 2 | Pick a personality, type a message, send | New session is created with that personality already set — the header's Personality select reflects it immediately |
| 3 | Open the dock in draft mode (no session yet) | Same Personality picker + Manage personalities button present in the draft composer |
| 4 | Confirm the "All docs" checkbox is gone everywhere | No trace of it in the full-page header, the dock kebab, or the composer |

**Pass/Fail:** _____

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Tester | | | |
