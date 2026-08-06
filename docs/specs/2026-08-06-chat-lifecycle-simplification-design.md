# Chat Session Lifecycle Simplification

## Problem

The chat session lifecycle has two independent axes (`Status: Active|Closed` and `ArchivedAt: DateTime?`) that create a confusing 4-state matrix. The filter dropdown exposes this matrix directly (`Active & Closed`, `Active`, `Closed`, `Archived`), which is redundant — users only care about "what I'm working on" vs "what I've finished" vs "what's scheduled for deletion".

Additionally, the `Close` action is available for all chat types, but its only meaningful backend consequence (AI summary generation + `CardChatLink` creation) only applies to card chats. Project chats get a summary but no card link. Normal chats get nothing useful from closing — archiving achieves the same outcome.

The ChatDock (small popup) also has an asymmetry: it doesn't consume the `archive-session` upward event from `ChatSessionView`, so archiving from the dock leaves the view open and the history list stale.

## Design

### Three-state model

| State | Meaning | Visible in default list | Scheduled for deletion |
|---|---|---|---|
| **Active** | Ongoing conversation | Yes | No |
| **Closed** | Finished, summary generated, still visible for reference | Yes (filter to see) | No |
| **Archived** | Scheduled for deletion via housekeeping job (730 days default) | No (filter to see) | Yes |

`Status` and `ArchivedAt` remain independent in the data model (no schema change), but the UI presents them as a single linear lifecycle: Active → Closed → Archived. The "Active but Archived" state (archived without closing) remains reachable via archive, but the UI treats it as Archived regardless of Status.

### Chat types

Computed in the frontend from `projectId` + `openCardId`:

| Type | Condition | Close button | Summary on close |
|---|---|---|---|
| **Normal** | `projectId == null` | Hidden | No |
| **Project** | `projectId != null`, `openCardId == null` | Shown | Yes (LLM call) |
| **Card** | `projectId != null`, `openCardId != null` | Shown | Yes (LLM call) + `CardChatLink` |

### Actions by chat type

| Action | Normal | Project | Card |
|---|---|---|---|
| Close | Hidden | Shown (when Active) | Shown (when Active) |
| Archive | Shown (always, owner-only) | Shown (always, owner-only) | Shown (always, owner-only) |
| Reopen | N/A (never Closed) | Shown (when Closed) | Shown (when Closed) |
| Unarchive | Shown (when Archived) | Shown (when Archived) | Shown (when Archived) |

**Reopen** (clears `Status`, `ClosedAt`, `Summary`, `ArchivedAt` → fully restores to Active) — only meaningful when `Status == Closed`. Normal chats can never be Closed (no Close button), so Reopen is N/A for them. For project/card chats, Reopen appears when the chat is Closed (whether or not it's also Archived).

**Unarchive** (only clears `ArchivedAt`, keeps `Status`) — shown when `ArchivedAt != null` for all chat types. If a chat was Closed+Archived, Unarchive returns it to Closed (not Active). To fully restore a Closed+Archived chat, use Reopen instead.

### Filter changes

**Before:** `Active & Closed` (default), `Active`, `Closed`, `Archived`
**After:** `Active` (default), `Closed`, `Archived`

The `ActiveAndClosed` enum value remains in `ChatSessionStatusFilter` for backend internal use (e.g. `ChatArchiveService.ArchiveFolderAsync` lists all non-archived sessions to cascade-archive them). It is removed from the UI filter dropdown only.

Default filter changes from `ActiveAndClosed` to `Active` so the list stays focused on ongoing work.

### Type badge

A small chip/badge shown in the chat list (both `/chats` sidebar and `ChatDockHistory`) indicating the chat type:

- **Normal** — gray badge, label "Chat"
- **Project** — blue badge, label "Project"
- **Card** — green badge, label "Card"

Computed via a shared utility (e.g. `app/lib/chat-type.ts` — `getChatType(session): 'normal' | 'project' | 'card'`, `CHAT_TYPE_BADGE` map with label + color).

### Summary display

For closed chats, the summary (if present) is shown as a subtitle/secondary line in the chat list item, below the title. This gives users a preview of what the closed chat was about before reopening it.

### Archive confirm modal

All archive actions (in-view header button, sidebar hover button, dock kebab menu item) trigger a `ConfirmDialog` with a deletion warning:

- **Title:** "Archive chat"
- **Message:** "This chat will be archived and eventually deleted. You can unarchive it to restore it."
- **Confirm text:** "Archive"
- **Confirm color:** `warning`

The existing `ConfirmDialog` component is reused. The message does not hardcode the retention days (admin-configurable) — it says "eventually deleted" instead.

### Symmetric behavior: dock vs full view

Both the ChatDock (compact popup) and the full `/chats` page must handle lifecycle events identically:

1. **Archive** — both consume `@archive-session` from `ChatSessionView`:
   - Full page: clears `activeSessionId`, removes from sidebar list (no reload)
   - Dock: clears the active session, returns to "new chat" mode, refreshes history list (soft update, no scroll jump)

2. **Close** — both consume `@session-refreshed` from `ChatSessionView`:
   - Full page: patches sidebar item status (already works via `syncSession`)
   - Dock: patches history list item status (currently broken — dock doesn't listen to `@session-refreshed`)

3. **Reopen** — same as Close, via `@session-refreshed`

4. **Close button visibility** — gated on `projectId != null` in both modes (header checks `session.projectId` before showing Close in both inline buttons and kebab menu items)

5. **Confirm dialogs** — owned by `ChatSessionHeader` in both modes (already the case — the dock renders its own header instance with its own confirm dialogs)

## Backend changes

Minimal — the backend already supports everything:

1. **`ChatSessionStatusFilter` enum** — no change. `ActiveAndClosed` stays for internal use; UI just stops offering it.
2. **`ChatSessionService.ArchiveAsync`** — no change. Already just sets `ArchivedAt`.
3. **`ChatSessionService.CloseAsync`** — no change. Already generates summary + card link for card chats.
4. **`ChatSession.Close()`** — no change. Already sets Status/ClosedAt/Summary/RevokeAiEdit.
5. **`ChatSession.Reopen()` / `Unarchive()`** — no change.

## Frontend changes

### `app/lib/chat-type.ts` (new)

Shared utility for chat type computation and badge metadata:

```typescript
export type ChatType = 'normal' | 'project' | 'card'

export function getChatType(session: { projectId: string | null; openCardId: string | null }): ChatType {
  if (session.projectId && session.openCardId) return 'card'
  if (session.projectId) return 'project'
  return 'normal'
}

export const CHAT_TYPE_BADGE: Record<ChatType, { label: string; color: 'neutral' | 'primary' | 'success' }> = {
  normal: { label: 'Chat', color: 'neutral' },
  project: { label: 'Project', color: 'primary' },
  card: { label: 'Card', color: 'success' },
}
```

### `app/components/chat/ChatSessionHeader.vue`

1. **Close button gating** — add `session.projectId` check to both inline button (`v-if`) and kebab menu item (`v-if`/conditional inclusion):
   - Inline: `v-if="!compact && isOwner && isActive && session.projectId"`
   - Kebab: only include Close item when `session.projectId` is set

2. **Archive confirm message** — update the `ConfirmDialog` for archive to include the deletion warning text.

3. **Type badge in header** (optional, if header has room) — show the type badge next to the title.

### `app/components/chat/ChatSessionView.vue`

1. **`handleCloseSession`** — no change to the API call. Keep emitting `sessionRefreshed` so both dock and full page can sync their lists.

2. **`handleArchiveSession`** — no change to the API call. Keep emitting `archiveSession(id)` upward.

3. **`handleReopen`** — no change. Keep emitting `sessionRefreshed`.

### `app/components/chat/ChatDock.vue`

1. **Listen to `@session-refreshed`** on `<ChatSessionView>` — patch the history list item's status/title in place (soft update, no reload).

2. **Listen to `@archive-session`** on `<ChatSessionView>` — clear the active session, return to "new chat" mode, soft-remove from history list (no reload, preserve scroll position).

### `app/pages/chats/index.vue`

1. **Filter dropdown** — remove `ActiveAndClosed` option. Three options: `Active` (default), `Closed`, `Archived`.

2. **Default filter** — change `statusFilter` initial value from `'ActiveAndClosed'` to `'Active'`.

3. **Type badge in sidebar list** — show the type badge on each chat list item.

4. **Summary subtitle** — for closed chats with a summary, show it as a secondary line below the title.

### `app/composables/useChatSessionList.ts`

1. **Default filter** — change `statusFilter` ref default from `'ActiveAndClosed'` to `'Active'`.

2. **Type** — update the `statusFilter` type to remove `'ActiveAndClosed'` (or keep it but never default to it).

### `app/components/chat/ChatDockHistory.vue` (or equivalent history list)

1. **Type badge** — show on each history list item.

2. **Summary subtitle** — for closed chats, show summary as secondary line.

3. **Soft-remove on archive** — when a session is archived, remove it from the list without a full reload (preserve scroll position).

## TUI considerations

The TUI (`HydraForge.Tui`) has its own chat views. The filter and lifecycle changes should be mirrored:

1. **Filter options** — Active / Closed / Archived (remove Active & Closed).
2. **Close button** — only for project/card chats.
3. **Type indicator** — show chat type in the TUI list.
4. **Archive confirm** — warn about eventual deletion.

This is a follow-up task — the Web UI changes come first, TUI parity second.

## Out of scope

- Schema changes (none needed — `Status` and `ArchivedAt` stay as-is)
- Backend service logic changes (none needed — `CloseAsync`, `ArchiveAsync`, `ReopenAsync` all work correctly already)
- Removing `ActiveAndClosed` from the backend enum (kept for internal use)
- Changing the housekeeping retention logic (stays as-is)
- Changing F6 auto-close behavior (stays as-is — auto-closes old card chat when opening a new one)
