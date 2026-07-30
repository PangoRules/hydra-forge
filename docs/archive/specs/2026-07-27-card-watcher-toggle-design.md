# Card Watcher Toggle — Design

**Date:** 2026-07-27
**Status:** Approved

## Problem

`CardWatcher` rows are only ever auto-created (on assign, on comment — see `CardService.AssignAsync`, `CommentService.EnsureWatcherAsync`). There is no way for a user to explicitly start or stop watching a card. Notification triggers (Plan 7) already read from `CardWatcher` for card-move and comment notifications, so a manual toggle plugs directly into existing plumbing — no other changes needed.

## Scope

Self-toggle only (watch/unwatch yourself). No visible "who else is watching" list — that's a natural follow-up, not needed for this pass (YAGNI).

## API

- `CardService.WatchAsync(cardId, userId)` / `UnwatchAsync(cardId, userId)` — thin wrappers over the existing `ICardWatcherRepository.AddAsync`/`RemoveAsync` (already implemented, previously unused). Idempotent: watching twice or unwatching when not watching is a no-op, not an error.
- `POST api/projects/{projectId}/cards/{cardId:guid}/watch` and `DELETE` same route.
- `CardDto` gains `IsWatchedByCurrentUser: bool`, computed server-side per requesting user, included in both the board-list endpoint and the single-card endpoint. This is the one new piece of data both clients need — the board kebab menu needs it to label Watch/Unwatch without opening the card; the detail view needs it for the eye-icon state.

## Web UI

- Card detail (`CardModal.vue`): eye icon button next to the existing Archive button in the header. Toggles via the new endpoint, flips local state on success.
- Board card (`BoardCard.vue`): new item in the existing three-dot kebab menu (alongside Archive/Restore), labeled "Watch" or "Unwatch" based on `IsWatchedByCurrentUser`.

## TUI

- `CardDetailScreen`: new metadata row ("Watching: Yes/No") alongside Title/Due Date/Assignees.
- New keybinding: `W` toggles watch state for the open card (free key, confirmed no conflict with existing bindings in this screen). Added to the screen's `ShowHelp()` binding table per D-47 convention.

## Testing

- Application: `CardServiceTests` — watch/unwatch idempotency, `IsWatchedByCurrentUser` computed correctly for the requesting user (not globally).
- Server: smoke-test the new routes in the Cards `.http` file.
- Web UI: component test on the eye-icon toggle and kebab menu item.
- TUI: existing screen test pattern for key dispatch, if one exists for this screen.
