# Chat History — Scope & Type Filters

**Date:** 2026-08-09
**Status:** Approved, not yet implemented
**Related:** `docs/plans/2026-08-02-phase-7-chat-plan-20-web-card-popup-chat-project-chats.md` (introduced the "participated in" broadening this spec partially reverts as a default)

## Problem

Plan 20 broadened `GET /api/chat/sessions` from "sessions I own" to "sessions I participated in" (owner OR project member), so a project's own **Chats** tab (`ProjectChatsTab.vue`) could list every member's chats for that project. That endpoint is also the one backing the personal `/chats` page and the FAB dock's History panel — both call it unscoped (no `projectId`). The result: User A's project chat now shows up in User B's *personal* chat history, for every project B happens to be a member of. Confirmed in testing (testadmin's project chat appeared in a different member's `/chats` list).

Separately: the only filter on `/chats` and the FAB History panel today is Status (Active/Closed/Archived), rendered as a single narrow `USelect` that doesn't use the available row width. Each session already renders a type badge (Chat/Project/Card, from `lib/chat-type.ts`) but there's no way to filter by it.

## Decisions

**D1 — Scope default flips to "mine", with an explicit opt-in for "participated".**
`GET /api/chat/sessions` and `GET /api/chat/search` gain a `scope` param: `mine` (default) | `participated`.
- `scope=mine` → `WHERE OwnerId = actorId`. Applies **even to admins** — an admin's own `/chats` page is their own personal history, not a system-wide audit view. This is the actual fix for the reported bug.
- `scope=participated` → today's existing behavior, unchanged: owner OR project-member (`WhereParticipatedIn`), with the existing admin bypass (admin sees every non-archived session in scope). Opt-in instead of the only option.
- `ProjectChatsTab.vue` always passes `scope=participated` explicitly — that view's entire purpose is showing every member's chats for one project. No visible change there. This is the "go to the board and open it from there" path the user described for viewing another member's project chat.
- No project picker is added anywhere. Selecting "Participated" on the global `/chats`/FAB view shows participated-in sessions across every project the caller is a member of — same breadth Plan 20 already shipped, just opt-in now.

**D2 — Type filter (Chat/Project/Card) is multi-select and server-side.**
New `types` param: comma-separated subset of `normal|project|card` (the exact string values `lib/chat-type.ts`'s `ChatType` already uses — no remapping). Omitted/empty = no filtering (all types). Filtering happens in the same `WHERE` clause as scope/status, in all three shared repository methods (`ListAsync`, `CountAsync`, `SearchByTitleAsync`), so pagination and counts stay correct under any filter combination — the same reasoning that already applies to the existing Status filter. Client-side post-filtering was considered and rejected: with a narrow filter active, a fetched page could render far fewer visible rows than its size, and "load more" would need repeated clicks to fill the view even though matching results exist further down.

The UI's multi-select must not allow zero types selected (minimum one checked at all times) — an empty selection would either mean "show nothing" or silently fall back to "show everything," both surprising. Simplest: disable unchecking the last remaining checked type.

**D3 — One shared filter-bar component, identical on both surfaces.**
`/chats` (`pages/chats/index.vue`) and the FAB history panel (`ChatDockHistory.vue`) render the exact same filter bar via one new shared component (e.g. `ChatSessionFilters.vue`), so the two surfaces can't drift apart. Layout, both places:

```
[Status: Active ▾]   [Type: All (3) ▾]      ← two selects, 50/50 width
[      Mine       ][   Participated   ]      ← 2-segment control, fills full row width
```

Status+Type on top (the filters expected to be changed most often), Scope segmented control below. Both rows use the component's full width — no more narrow dropdown floating next to empty space.

**D3a — Status filter itself is unchanged.** Same three values (Active/Closed/Archived), same default (Active), same `ChatSessionStatusFilter` enum — only its position in the layout moves (top row, next to Type, instead of being the only control).

**D4 — Fix a pre-existing bug found while in this code.**
`useChatSessionList.ts` currently builds its own `&status=...` query-string fragment by hand instead of using `ApiRoutes.Chat.sessions.list`'s existing `status` parameter. Since this composable is being extended with `scope` and `types` anyway, all three now go through the route helper properly instead of adding a third hand-rolled string next to the existing one.

**D5 — `ChatSearchService`/`/api/chat/search` gets the same `scope` param, defaulting to `mine`, for the same reason.**
`ChatSearchService.SearchAsync` currently calls `SearchByTitleAsync`/`SearchByContentAsync` unconditionally as participated-in (`isAdmin: false` hardcoded, no scope concept at all) — the identical bug class as D1, just in the search path instead of the list path. Included here because it shares the exact repository method (`SearchByTitleAsync`) already being touched for D1/D2, not as scope creep — leaving it inconsistent would mean search still leaks other members' project chats after this ships. No `types` filter is added to search in this pass (not requested, and search result rows don't currently render the type badge at all).

## API changes

`GET /api/chat/sessions`
```
?folderId=&projectId=&before=&beforeId=&limit=&status=&scope=&types=
```
- `scope`: `mine` (default) | `participated`
- `types`: comma-separated subset of `normal,project,card`; omitted = all

`GET /api/chat/search`
```
?q=&projectId=&scope=
```
- `scope`: `mine` (default) | `participated`

### Backend signature changes

- New enum `ChatSessionScope { Mine, Participated }` (default `Mine`) — mirrors the existing `ChatSessionStatusFilter` pattern.
- New enum `ChatSessionKind { Normal, Project, Card }` — mirrors `lib/chat-type.ts`'s `ChatType`. ASP.NET's default query-string enum binding is case-insensitive, so no custom converter is needed; ships as a comma-joined single query value (`types=normal,project`), parsed server-side by splitting on `,`, consistent with how `routes.ts` builds query strings elsewhere (plain string concatenation, not `URLSearchParams`).
- `IChatSessionRepository.ListAsync` / `CountAsync` / `SearchByTitleAsync` each gain `ChatSessionScope scope = ChatSessionScope.Mine` and `IReadOnlySet<ChatSessionKind>? types = null` parameters, alongside the existing `isAdmin` param.
- `EfChatSessionRepository`: `WhereParticipatedIn` is only applied when `scope == Participated` (with `isAdmin` bypass preserved inside that branch, unchanged from today); `scope == Mine` applies `s.OwnerId == actorId` unconditionally, ignoring `isAdmin` entirely. A new `WhereOfKind` (or folded into the same predicate builder) applies the `types` filter via `s.ProjectId`/`s.OpenCardId` nullability, matching `lib/chat-type.ts`'s exact derivation:
  - `Normal`: `ProjectId == null`
  - `Project`: `ProjectId != null && OpenCardId == null`
  - `Card`: `OpenCardId != null`
- `ChatSessionService.ListAsync` gains `scope`/`types` params, forwarded to the repository. `ChatSessionsController.List` reads `scope`/`types` from query and passes through.
- `ChatSearchService.SearchAsync` gains a `scope` param (default `Mine`), forwarded to `SearchByTitleAsync`/`SearchByContentAsync`'s new scope param instead of the current hardcoded `isAdmin: false`/implicit-participated call.
- `ProjectChatsTab.vue`'s existing direct `ApiRoutes.Chat.sessions.list(...)` call is updated to pass `scope=participated` explicitly (its behavior doesn't otherwise change).

## Frontend changes

- `useChatSessionList.ts`: add `scope` (`'mine' | 'participated'`, default `'mine'`) and `types` (`Set<ChatType>` or array, default all three) reactive refs alongside the existing `statusFilter`; any change triggers refetch via the existing `watch(...) => refresh()` pattern. Query building goes through `ApiRoutes.Chat.sessions.list`'s params (extended to accept `scope`/`types`) instead of hand-appended strings.
- New `ChatSessionFilters.vue`: renders the Status+Type row and the Mine/Participated segmented row, `v-model`-style bindings for `status`/`types`/`scope`. Used by both `pages/chats/index.vue` and `ChatDockHistory.vue` — replaces each surface's own ad hoc filter markup (today, `/chats` has an inline `USelect`; `ChatDockHistory.vue` has none).
- Type multi-select: Nuxt UI `USelectMenu` with `multiple`, backed by the existing `CHAT_TYPE_BADGE` labels/colors from `lib/chat-type.ts` (no new label strings). Enforce "at least one selected" in the component itself.
- `ProjectChatsTab.vue`: no new UI (no Scope toggle) — its one hand-rolled fetch call adds `scope=participated` to the existing URL construction.

## Testing

- Backend: `ChatSessionServiceTests` — scope=mine excludes a project session the caller is a member but not owner of (the regression this whole spec exists to fix); scope=participated preserves existing Plan 20 coverage unchanged; admin + scope=mine sees only their own sessions (the admin-bypass-must-not-apply-to-mine case); types filter isolates each of the three kinds and combinations thereof; default (`scope` omitted) behaves as `mine`.
- `EfChatSessionRepositoryTests`: real-Postgres-gated tests (existing pattern, skipped without `HYDRAFORGE_TEST_CONNECTION_STRING`) verifying `WhereOfKind`'s EF translation doesn't throw and returns correct rows for each `ChatSessionKind`.
- `ChatSearchServiceTests`: scope=mine excludes a participated-but-not-owned session from search results.
- Frontend: `useChatSessionList` composable tests for scope/types query building (replacing/extending whatever exists for the current status-only behavior); `ChatSessionFilters.vue` component test for the "can't uncheck the last type" constraint and that all three v-models emit correctly.
