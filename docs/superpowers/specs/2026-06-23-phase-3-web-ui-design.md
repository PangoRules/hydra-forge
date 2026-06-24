# Phase 3: Project Space Web UI — Design Spec

**Branch:** `feat/phase-3-web-ui`

> **Date:** 2026-06-23
> **Status:** Approved
> **Phase:** 3 — Project Space Web UI

---

## Tasks

- [x] Task 1: Foundation — CORS, API Client, Auth, Layouts
- [x] Task 2: Project List & Board View
- [x] Task 3: Card Modal Core — Desktop Split + Mobile Tabs + Tiptap Description
- [ ] Task 4: Card Modal Panels — Checklist, Comments, Attachments, Dependencies, Metadata
- [ ] Task 5: Specs, Plans & Real-time — Spec/Plan Editors + SignalR BoardHub + PresenceHub
- [ ] Task 6: Polish & Hardening — Keyboard, Errors, Blocked Cards, Archive, ARIA, Tablet, PWA

---

## 1. Goal

Full project board usable in browser. Feature-complete project workspace. Mobile-first, responsive across three breakpoints. All Phase 2 API endpoints consumed. Real-time via SignalR.

---

## 2. Prerequisites (Blocking)

### 2.1 CORS

Backend has zero CORS configuration. Browser calls from Nuxt dev origin (`localhost:3000`) to API (`localhost:5000`) will be blocked. **Must be added before any frontend work.**

Add to `src/HydraForge.Server/Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
// ...
app.UseCors(); // after UseRouting, before UseAuthentication
```

### 2.2 Port Convention

Lock in **port 5000** as the canonical API URL. Docker Compose exposes 5000. `nuxt.config.ts` `runtimeConfig.public.apiBaseUrl` defaults to `http://localhost:5000`. Override via `NUXT_PUBLIC_API_BASE_URL` env var for non-standard setups.

---

## 3. Library Decisions

### 3.1 API Client: `openapi-fetch` + `openapi-typescript`

- **Dep:** `openapi-fetch` (runtime), `openapi-typescript` (devDep)
- **Script:** `"generate:api-types": "openapi-typescript http://localhost:5000/openapi/v1.json -o ./app/types/api.d.ts"`
- **Generated types committed** to `app/types/api.d.ts` (same logic as EF migrations — CI has no live backend)
- **Excluded from ESLint** stylistic rules in `eslint.config.mjs`
- **Live codegen** — backend must be running. Manual step, not part of dev/build/postinstall.

### 3.2 Markdown Editor: Tiptap

- **Deps:** `@tiptap/vue-3`, `@tiptap/starter-kit`, `@tiptap/extension-placeholder`
- **Headless** — styled with Nuxt UI / Tailwind, no default skin to fight
- **Company-backed** (Tiptap GmbH) — security posture beats single-maintainer alternatives
- **Used in:** Card description, comment input, spec editor, plan editor

### 3.3 Drag-and-Drop: Plain `v-for` (vue-draggable-plus removed)

- **`vue-draggable-plus`** (SortableJS-based) was removed during Plan 2 implementation — SSR-incompatible with Nuxt 4 (SortableJS requires browser APIs, component fails inside `ClientOnly`, hydration mismatches with `v-model`). See D-37.
- **Current:** Plain `v-for` for column and card lists. Card moves and column reorder work via API only (curl).
- **Planned:** Native HTML5 drag-and-drop for re-implementation (no library dependency, touch support via pointer events).
- **`@vueuse/core` `useDraggable`** — free positioning if needed (unlikely in Phase 3)

### 3.4 State Management: Pinia

- **Dep:** `pinia` + `@pinia/nuxt`
- **Optimistic updates** for card moves (drag-and-drop needs instant feedback)
- **Server-confirmed** for edits (title, description) — less urgency, simpler
- **SignalR events** call store actions directly — clean separation

### 3.5 SignalR Client

- **Dep:** `@microsoft/signalr`
- **Two connections:** BoardHub (`/hubs/board`) + PresenceHub (`/hubs/presence`)
- **Auth:** `access_token` query parameter on WebSocket URL
- **Reconnection:** SignalR built-in auto-retry with backoff. Show "Reconnecting..." banner.

---

## 4. Navigation Architecture

**Hybrid: Sidebar + Modal Overlays**

### Desktop (≥1200px)
- Persistent sidebar (left): project name, navigation links, member list
- Main content area: board view
- Card detail: modal (centered, backdrop)
- Spec/Plan editor: modal
- Project list: separate route

### Tablet (768–1199px)
- Collapsible sidebar (hamburger toggle)
- Board: compact horizontal scroll (2-3 columns visible)
- Card detail: slide-over or full modal

### Mobile (<768px)
- Bottom navigation bar (Projects, Board, — future: Chat, Settings)
- Board: card list grouped by column headers
- Card detail: full-screen modal
- Sidebar: drawer from left

### Routes
```
/                   → redirect to /projects or /login
/login              → login form
/setup              → first-run admin setup
/projects           → project list
/projects/[id]/board → board view (main workspace)
```

---

## 5. Board Layout Per Breakpoint

### Desktop (≥1200px)
- Horizontal scrollable columns
- Cards stacked vertically per column
- Column header: name, card count, WIP limit indicator
- Card: title, type badge, assignee avatars, blocked indicator, due date
- Drag cards between columns via `vue-draggable-plus`

### Tablet (768–1199px)
- Compact horizontal scroll (same as desktop, narrower columns)
- 2-3 columns visible at a time
- Cards: title + type badge only (compact mode)

### Mobile (<768px)
- Single scrollable list grouped by column
- Column headers as section dividers
- Cards: full-width rows — title + type badge + assignee avatar + blocked indicator
- Tap card → full-screen modal
- No drag-and-drop on mobile (use move action in card modal instead)

---

## 6. Card Detail Modal

### Desktop: Two-Column Split
- **Left (60%):** Description (Tiptap editor) + Comments (list + input)
- **Right (40%):** Metadata sidebar
  - Type selector (Task/Bug/Epic)
  - Column selector (move card)
  - Assignees (add/remove)
  - Due date picker
  - Parent Epic link
  - Checklist (collapsible)
  - Attachments (list + upload)
  - Specs (linked list + create)
  - Plans (linked list + create)
  - Dependencies (BlockedBy/Precedes/Relates)

### Mobile: Tabbed
- Tabs: "Details" | "Checklist" | "Comments" | "Related"
- Each tab = vertical scroll
- "Details" tab: title, description, type, column, assignees, due date
- "Checklist" tab: checklist items with toggle
- "Comments" tab: comment list + input
- "Related" tab: specs, plans, attachments, dependencies

### Blocked Card Behavior
- Lock icon + blocked-by count badge on card (always visible, all breakpoints)
- Moving blocked card → soft warning modal: "Blocked by #42 (Title). Move anyway?"
- User confirms → retry with `confirmBlockedMove: true`

### Archive Card
- Archive button in card actions menu
- If card has dependents → warning modal listing affected cards
- Confirm → archive with cascade

---

## 7. Spec & Plan Editors

### Spec Editor
- Modal with Tiptap markdown editor
- Version history sidebar (collapsible): list of versions with date + author
- Restore button per version → confirmation dialog → restores title, description, content
- Save creates new version

### Plan Editor
- Same pattern as spec editor
- Numbered steps markdown (convention, not enforced)
- Version history + restore

---

## 8. Auth Screens

### Login (`/login`)
- Centered card on all sizes
- Logo + "HydraForge" title
- Username + password fields
- "Sign in" button
- Error displayed below form (inline, not floating toast)
- Dark/light toggle in corner
- On success → store JWT in cookie → redirect to `/projects`

### First-Run Setup (`/setup`)
- Dedicated route, not part of login page
- Detection: client attempts login with default admin credentials on first visit. If successful → redirect to `/setup` to force password change. If default login fails → normal `/login` flow. No dedicated backend endpoint needed.
- Form: new admin password + confirm password
- On success → redirect to `/login`
- After setup complete → route inaccessible (middleware redirects to `/login`)

### Auth Middleware
- `app/middleware/auth.ts` — redirects to `/login` if no valid token
- Applied to all routes except `/login` and `/setup`

---

## 9. State Management (Pinia)

### Store: `useBoardStore`
```
State:
  project: ProjectResponse | null
  columns: ColumnResponse[]
  cards: Map<columnId, CardResponse[]>
  loading: boolean
  error: string | null

Actions:
  fetchBoard(projectId)
  moveCard(cardId, targetColumnId, targetPosition) — optimistic
  createCard(columnId, data)
  updateCard(cardId, data)
  archiveCard(cardId)
  // ... per-entity CRUD

SignalR handlers:
  onBoardEvent(envelope) → dispatch to matching action
```

### Store: `usePresenceStore`
```
State:
  onlineUsers: Map<projectId, User[]>
  focusedCards: Map<userId, cardId>

Actions:
  joinProject(projectId)
  leaveProject(projectId)
```

### Store: `useAuthStore`
```
State:
  token: string | null
  user: { userId, username, isAdmin } | null
  isAuthenticated: boolean

Actions:
  login(username, password)
  logout()
  checkAuth() — validate stored token
```

---

## 10. Composables

| Composable | Purpose |
|-----------|---------|
| `useApi` | `openapi-fetch` client + auth middleware + error parsing |
| `useAuth` | login/logout/isAuthenticated (wraps useAuthStore + useAuthToken) |
| `useAuthToken` | cookie-backed JWT storage (useCookie) |
| `useRealtime` | SignalR BoardHub connection lifecycle + event dispatch |
| `usePresence` | SignalR PresenceHub connection + online users |
| `useKeyboard` | keyboard shortcut registry (register/unregister by component) |
| `useErrorToast` | ProblemDetails → Nuxt UI toast with correlationId copy button |

---

## 11. Error Handling

### API Errors
- `useApi` `onResponse` hook parses `application/problem+json` → throws `ApiError`
- `ApiError` class: `status`, `code`, `title`, `detail` (nullable), `type`, `correlationId`
- Components catch `ApiError` → `useErrorToast` → Nuxt UI toast
- Toast shows: `title` + `correlationId` copy button
- 401 → redirect to `/login`
- 409 (blocked move) → handled specially: show warning modal, not error toast

### SignalR Errors
- Connection lost → "Reconnecting..." banner (non-blocking)
- Reconnected → banner disappears, board re-fetches
- Failed after retries → "Connection lost. Refresh?" banner

### Optimistic Update Rollback
- Card move: optimistic update → POST → on error → rollback + toast
- Card edit: no optimistic → POST → on success update store → on error toast

---

## 12. Keyboard Shortcuts

### Board View
| Key | Action |
|-----|--------|
| `j` / `↓` | Next card |
| `k` / `↑` | Previous card |
| `h` / `←` | Previous column |
| `l` / `→` | Next column |
| `Enter` | Open selected card |
| `n` | New card in current column |
| `m` | Move card (then arrow keys to target column) |
| `/` | Search/filter cards |
| `Escape` | Close modal / clear selection |
| `?` | Keyboard shortcut overlay |

### Card Modal
| Key | Action |
|-----|--------|
| `Escape` | Close modal |
| `Ctrl+Enter` | Save description |

---

## 13. Component Organization

```
components/
  auth/
    LoginForm.vue
    SetupForm.vue
  project/
    ProjectList.vue
    ProjectCreateModal.vue
    ProjectSidebar.vue
  board/
    BoardView.vue          — desktop kanban
    BoardMobileList.vue    — mobile card list
    BoardColumn.vue        — single column
    BoardCard.vue          — single card
    ColumnHeader.vue
  card/
    CardModal.vue          — container
    CardDescription.vue    — Tiptap editor
    CardChecklist.vue
    CardComments.vue
    CardAttachments.vue
    CardDependencies.vue
    CardMetadata.vue       — type, column, assignees, due date
  spec/
    SpecEditor.vue
    SpecVersionHistory.vue
  plan/
    PlanEditor.vue
    PlanVersionHistory.vue
  shared/
    UserAvatar.vue
    MarkdownEditor.vue     — Tiptap wrapper
    ConfirmDialog.vue
    ErrorToast.vue
    KeyboardShortcutOverlay.vue
    ReconnectingBanner.vue
```

---

## 14. Pages & Routing

```
pages/
  index.vue                — redirect middleware
  login.vue                — LoginForm
  setup.vue                — SetupForm
  projects.vue             — ProjectList + ProjectCreateModal
  projects/
    [id]/
      board.vue            — BoardView (desktop) / BoardMobileList (mobile)
```

### Middleware
```
middleware/
  auth.ts                  — redirect to /login if no token
  setup.ts                 — redirect to /setup if first-run detected
```

### Layouts
```
layouts/
  default.vue              — UApp shell (replaces current app.vue boilerplate)
  auth.vue                 — minimal layout for login/setup (no sidebar)
```

---

## 15. SignalR Integration

### Connection Lifecycle
1. `useRealtime` composable connects on board page mount
2. `JoinProject(projectId)` → subscribes to `project-{projectId}` group
3. `OnBoardEvent` handler → dispatches to Pinia store action
4. On page unmount → `LeaveProject` → disconnect

### Presence
1. `usePresence` composable connects on board page mount
2. `JoinProject(projectId)` → broadcasts `UserJoined`
3. `FocusCard(projectId, cardId)` when card modal opens
4. `UserJoined`/`UserLeft`/`CardFocused` events → update `usePresenceStore`

### Reconnection
- SignalR `withAutomaticReconnect()` — built-in retry with backoff
- `onreconnecting` → show `ReconnectingBanner`
- `onreconnected` → hide banner, re-join project group, re-fetch board
- `onclose` → show "Connection lost" banner with manual refresh button

---

## 16. Implementation Order

Tasks ordered by dependency:

| # | Task | Depends On |
|---|------|-----------|
| 1 | CORS + port convention | Nothing |
| 2 | `openapi-fetch` + `openapi-typescript` + `useApi` + `useAuthToken` + `ApiError` | #1 |
| 3 | `useAuth` + `useAuthStore` + login page + setup page + auth middleware | #2 |
| 4 | Layouts (default + auth) + replace app.vue boilerplate | #3 |
| 5 | Pinia setup + `useBoardStore` | #2 |
| 6 | Project list page + create project modal | #3, #5 |
| 7 | Board view (desktop): columns + cards + drag-and-drop | #5, #6 |
| 8 | Board mobile list view | #7 |
| 9 | Card modal (desktop two-column) | #7 |
| 10 | Card modal (mobile tabs) | #9 |
| 11 | Card description (Tiptap) | #9 |
| 12 | Card checklist | #9 |
| 13 | Card comments | #9 |
| 14 | Card attachments (upload/download) | #9 |
| 15 | Card dependencies panel | #9 |
| 16 | Card metadata (type, column, assignees, due date) | #9 |
| 17 | Spec editor + version history | #9 |
| 18 | Plan editor + version history | #9 |
| 19 | SignalR BoardHub integration + `useRealtime` | #7 |
| 20 | SignalR PresenceHub integration + `usePresence` | #19 |
| 21 | Keyboard shortcuts + overlay | #7, #9 |
| 22 | Error toast system | #2 |
| 23 | Blocked card indicators + move warning | #7, #15 |
| 24 | Archive card with dependents warning | #9, #15 |
| 25 | ARIA labels + focus management + color contrast | All |
| 26 | Tablet responsive pass | #7, #8, #9 |
| 27 | PWA manifest + installable | #3 |

---

## 17. Out of Scope (Deferred)

- Chat panel (Phase 7)
- AI features (Phase 8)
- Notifications / ntfy (Phase 5)
- Admin dashboard (Phase 5)
- TUI (Phase 4)
- Personal workspace (Phase 9)
- Dark mode beyond Nuxt UI's built-in toggle (already works)
- Custom themes (Phase 9)
- Offline mode (explicitly rejected — D-3)
- View-only member role (post-MVP)

---

## 18. Verification

After all tasks complete:
1. `pnpm typecheck` — zero errors
2. `pnpm lint` — zero errors
3. `pnpm build` — successful production build
4. Manual: login → create project → add columns → create cards → move cards → open card → edit description → add checklist → comment → upload attachment → add dependency → archive card → verify real-time sync across two browser tabs
5. Mobile: all above on <768px viewport
6. Tablet: all above on 768-1199px viewport
7. Keyboard: navigate board, open card, close modal, move card — all via keyboard only
8. WCAG AA: color contrast audit on all components
