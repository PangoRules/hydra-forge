# Phase 4: Project Space — TUI

**Branch:** `feat/phase-4-tui`
**Date:** 2026-07-24
**Status:** Draft

---

## Problem

HydraForge has a fully functional Web UI for project boards (Phase 3). But the Terminal Power User persona — developers working over SSH, VPN, or tmux sessions — has no way to manage boards, cards, specs, or plans without opening a browser. The existing `src/HydraForge.Tui/` project is a bare `dotnet new console` template with a single `Console.WriteLine("Hello, World!")` and a stale `ProjectReference` to the Application layer.

Phase 4 builds a full terminal UI client with feature parity to the Web UI board, using Spectre.Console for rendering, NSwag-generated HTTP client for API calls, and `Microsoft.AspNetCore.SignalR.Client` for real-time updates. Every board operation available in the browser must be available in the terminal — no mouse required, keyboard-only navigation throughout.

---

## Design Principles

### Terminal-First, Not Web-Ported

The TUI is not a Web UI rendered in ASCII. It optimizes for:
- **Keyboard speed** — vim-style `h/j/k/l` navigation, single-key actions (`n` new, `m` move, `d` dependencies, `?` help). No tab-through-every-element patterns.
- **Information density** — show more cards/columns per screen than the Web UI. Use color and markup sparingly; density is the terminal's strength.
- **Stable layout** — no scroll within main content area. The terminal window is the viewport. Views switch cleanly (full-screen replace), not scroll within a page.

### Spectre.Console Widget Choices

| View | Widget | Rationale |
|------|--------|-----------|
| Project list | `Table` with columns: Name, Members, Role, Created, Archived badge | Familiar, sortable, keyboard-navigable. Row selection via highlight. |
| Board view | Custom layout: `Panel` per column, `Panel` per card inside columns, rendered via `LiveDisplayContext` | `LiveDisplay` enables real-time board updates without screen flicker. Columns rendered as fixed-width vertical sections. |
| Card detail | `Panel` with `Rows` for field groups, `Table` for checklists/dependencies | Structured read-only view; edit actions open `$EDITOR` for long text fields. |
| Card create/edit | `TextPrompt<int>` for card number, `SelectionPrompt<CardType>` for type, `TextPrompt<string>` for title, `$EDITOR` for description | Prompt workflow for structured fields; external editor for freeform content. |
| Dependency panel | `SelectionPrompt<CardRelationshipType>` for type, `TextPrompt<string>` for card search/ID | Multi-step guided prompt: search card → select type → confirm. |
| Comments | `Panel` with `Rule` separators between comments, `TextPrompt<string>` for new comment | Inline threaded view. New comment appended at bottom via prompt. |
| Spec/Plan viewer | `Panel` with full content rendered in `Markup` (Markdown → Spectre.Console markup) | Read-only preview; `e` key opens `$EDITOR` for editing. |
| Keyboard reference | `Panel` with `Table` of keys + descriptions | Overlay panel, dismiss with `?` or `Esc`. |
| Status bar | Custom `Grid` at fixed bottom row (3 columns: left=sync status, center=notifications, right=presence+errors) | Always-visible context bar. Updated via `LiveDisplay` or manual re-render on state change. |
| Lock screen | `Panel` with centered `Markup` (warning icon + message) | Full-screen overlay when server unreachable. Auto-dismisses on reconnect. |
| Loading | `Progress` spinner for initial loads; `LiveDisplay` for ongoing sync | Spinner for startup/auth; live display for board data. |

### Color Palette

| Token | Spectre.Console Color | Usage |
|-------|-----------------------|-------|
| `primary` | `Color.Blue` | Selected item highlight, column headers, active border |
| `danger` | `Color.Red` | Blocked card indicator, errors, delete confirmations |
| `success` | `Color.Green` | Connected status, completed checklists, success toasts |
| `warning` | `Color.Yellow` | Warnings, WIP limit approaching, connection retrying |
| `muted` | `Color.Grey` | Secondary text, metadata, archived items |
| `accent` | `Color.Cyan1` | Card type badge, keyboard shortcut hints, links |

### Border Style

Single `BoxBorder` everywhere — `HeavyBorder` for the lock screen and modal overlays only. One border style avoids the "mixed furniture" look.

### Signature Element

The **board view live-update** — columns and cards rendered as a `LiveDisplay` panel that updates in-place as SignalR events arrive. No screen flicker, no full redraw. The user sees cards appear, move between columns, and update their titles in real time, same as the Web UI but in the terminal. This is the single memorable moment.

---

## UI / Layout Approach

### Screen Map

```
┌─────────────────────────────────────────────────────────────┐
│  Login Screen          →  Project List          →  Board    │
│  (first run / expired)     (select/create)     View        │
│                                                    │       │
│                                          Card Detail ◄──┘   │
│                                           ├─ Comments       │
│                                           ├─ Checklists     │
│                                           ├─ Dependencies   │
│                                           ├─ Spec/Plan      │
│                                           └─ Attachments    │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Status Bar: [● Connected] [0 unread] [3 online] [⚠]│    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### View States

**1. Lock Screen** — full-screen overlay. Triggered when:
- Server unreachable on startup
- Connection lost and reconnect fails for >15s
- JWT expired and refresh fails

Content: centered `Panel` with `⚠ Server unreachable. Retrying...` + spinner. Auto-dismiss when connection restored. `q` to quit.

**2. Login Screen** — shown when no JWT exists or stored JWT is expired and refresh fails. Two `TextPrompt<string>` fields (username, password). On success: store JWT, transition to project list. On failure: show error, retry.

**3. Project List** — full-screen `Table`. Columns: Name (bold), Members (count badge), My Role, Created (relative), Archived (badge if archived). Row selected via `j/k`. Actions:
- `Enter` — open board
- `c` — create new project (prompt workflow)
- `/` — filter/search
- `q` — quit

**4. Board View** — the core screen. Rendered as `LiveDisplay` with:
- **Title bar** (top 2 rows): project name, column count, card count, connection indicator
- **Column area** (remaining rows minus status bar): fixed-width columns rendered left-to-right. Each column is a `Panel` with:
  - Header: `[color]Column Name[/]` + WIP count `(3/5)`
  - Cards: list of `Panel` elements, each showing:
    - `CardNumber` + Title (truncated to column width)
    - Type badge (Task/Issue/Goal/Idea) in accent color
    - Blocked indicator (`🔴` prefix if blocked)
    - Assignee avatar (first letter in colored circle)
  - Scroll indicator if cards overflow column height
- **Status bar** (bottom 2 rows): persistent

Keyboard:
- `h/l` — move between columns
- `j/k` — move between cards within column
- `Enter` — open card detail
- `n` — new card in selected column
- `m` — move selected card (prompt: target column)
- `d` — dependency panel for selected card
- `r` — reorder card within column (up/down)
- `e` — edit card title inline
- `Del` — archive selected card
- `/` — filter cards (text search)
- `?` — keyboard reference
- `Esc` — back to project list
- `Tab` — cycle focus between columns and detail panel

**5. Card Detail View** — full-screen `Panel` replacing board view. Sections:
- **Header**: CardNumber + Title (editable via `e` key)
- **Metadata**: Type badge, Status, Priority, Due date (color-coded if overdue)
- **Description**: rendered content or `[No description]` — `e` opens `$EDITOR`
- **Assignees**: list of avatars
- **Checklists**: `Table` with checkbox column (toggle with `Space`)
- **Comments**: threaded list, `a` to add
- **Dependencies**: list with type labels (blocks / blocked by / relates to)
- **Spec/Plan**: link to spec/plan viewer
- **Attachments**: list with filenames and sizes

Navigation: `Tab`/`Shift+Tab` cycles between sections. `Esc` back to board. `e` edits current section.

**6. Spec/Plan Viewer** — `Panel` with full document content rendered as `Markup`. `e` opens `$EDITOR` with the raw markdown content. On editor exit, reads file back and POSTs update. `Esc` back to card detail.

### Status Bar Layout

```
┌──────────────────────────────────────────────────────────────────┐
│ ● Connected    │  2 unread notifications  │  3 online  │ ⚠ 1 err │
└──────────────────────────────────────────────────────────────────┘
```

Three `Grid` columns:
- **Left** (left-aligned): connection indicator (`● Connected` green / `○ Reconnecting...` yellow / `○ Disconnected` red)
- **Center** (center-aligned): unread notification count (if > 0)
- **Right** (right-aligned): online presence count + error badge (if errors exist, shows count + correlationId on hover/expand)

Error panel expands on `E` key: shows last 5 errors with correlationId. `Del` dismisses individual errors.

---

## Component Inventory

### Screens (full-screen views)

| Screen | File | Description |
|--------|------|-------------|
| `LockScreen` | `Screens/LockScreen.cs` | Server unreachable overlay with auto-retry |
| `LoginScreen` | `Screens/LoginScreen.cs` | Username/password prompt + JWT storage |
| `ProjectListScreen` | `Screens/ProjectListScreen.cs` | Project table with create/filter |
| `BoardScreen` | `Screens/BoardScreen.cs` | Live-updating column/card board |
| `CardDetailScreen` | `Screens/CardDetailScreen.cs` | Full card view with section navigation |
| `SpecViewerScreen` | `Screens/SpecViewerScreen.cs` | Spec/Plan document viewer |
| `KeyboardReferenceScreen` | `Screens/KeyboardReferenceScreen.cs` | Overlay panel of all shortcuts |
| `ErrorPanelScreen` | `Screens/ErrorPanelScreen.cs` | Expandable error list in status bar |

### Services

| Service | File | Responsibility |
|---------|------|----------------|
| `ApiClientFactory` | `Services/ApiClientFactory.cs` | Creates NSwag-generated HTTP client with JWT auth header, handles token refresh |
| `SignalRConnectionManager` | `Services/SignalRConnectionManager.cs` | Manages `HubConnection` lifecycle for BoardHub + PresenceHub, reconnect/backoff |
| `ConfigStore` | `Services/ConfigStore.cs` | Read/write `.hydraforge/config.json` at the repo root (D-49) with 0600 perms |
| `EditorLauncher` | `Services/EditorLauncher.cs` | Opens `$EDITOR` (or `vi`/`notepad.exe` fallback), waits for close, reads temp file |
| `KeyboardDispatcher` | `Services/KeyboardDispatcher.cs` | Central key handler — routes keys to active screen, manages modal stack |
| `ErrorCollector` | `Services/ErrorCollector.cs` | Collects errors with correlationId, exposes list for status bar, supports dismiss |

### Models (TUI-local DTOs)

| Model | File | Description |
|-------|------|-------------|
| `TuiConfig` | `Models/TuiConfig.cs` | Serialized to config.json: `ServerUrl`, `JwtToken`, `ExpiresAt`, `RefreshToken` |
| `AppState` | `Models/AppState.cs` | Global state: current screen, selected project/card, connection status, error list |
| `ScreenStack` | `Models/ScreenStack.cs` | Navigation stack — push/pop for modal overlays (keyboard reference, error panel) |

---

## Keyboard Interaction Model

### Global Shortcuts (any screen)

| Key | Action |
|-----|--------|
| `?` | Toggle keyboard reference overlay |
| `E` | Toggle error panel in status bar |
| `q` | Quit application (with confirm if unsaved) |
| `Ctrl+C` | Quit immediately (no confirm) |
| `Esc` | Back / close overlay / deselect |

### Project List

| Key | Action |
|-----|--------|
| `j` / `Down` | Move selection down |
| `k` / `Up` | Move selection up |
| `Enter` | Open selected project board |
| `c` | Create new project (prompt: name, description, template) |
| `/` | Focus search/filter input |
| `a` | Toggle show archived |
| `g` / `Home` | Go to first project |
| `G` / `End` | Go to last project |

### Board View

| Key | Action |
|-----|--------|
| `h` / `Left` | Move column selection left |
| `l` / `Right` | Move column selection right |
| `j` / `Down` | Move card selection down within column |
| `k` / `Up` | Move card selection up within column |
| `Enter` | Open card detail |
| `n` | New card in selected column (prompt: title, type) |
| `m` | Move selected card (prompt: target column) |
| `r` | Reorder card — enters reorder mode (`j/k` to move, `Enter` to confirm, `Esc` to cancel) |
| `e` | Edit selected card title inline |
| `d` | Open dependency panel for selected card |
| `Del` | Archive selected card (with confirm) |
| `/` | Focus filter/search input |
| `Tab` | Cycle focus: columns → cards → status bar → columns |
| `g` | Go to first column |
| `G` | Go to last column |

### Card Detail

| Key | Action |
|-----|--------|
| `Tab` / `Shift+Tab` | Cycle between sections (metadata, description, checklists, comments, dependencies) |
| `e` | Edit current section (description → `$EDITOR`, title → inline, etc.) |
| `Space` | Toggle checklist item completion |
| `a` | Add comment (prompt: text) |
| `d` | Add dependency (prompt: card number, type) |
| `s` | Open spec/plan viewer |
| `Esc` | Back to board view |

### Dependency Panel (modal)

| Key | Action |
|-----|--------|
| Type card number | Search field auto-filters |
| `Tab` | Move between: search field → relationship type selector → confirm button |
| `Enter` | Confirm selected dependency |
| `Esc` | Cancel |

---

## SignalR Integration

### Connection Lifecycle

```
Startup: no connection
  ↓
Project List: no connection
  ↓
Enter Board: connect to BoardHub + PresenceHub
  ↓
BoardHub.JoinProject(projectId) → group subscription
PresenceHub.JoinProject(projectId) → presence tracking
  ↓
LiveDisplay updates on every ProjectBoardEventEnvelope
  ↓
Leave Board: disconnect both hubs
  ↓
Project List: no connection
```

### BoardHub Events

Received via `HubConnection.On<ProjectBoardEventEnvelope>("OnBoardEvent", ...)`:

| Event | Action |
|-------|--------|
| `Card.Created` | Add card to column in LiveDisplay |
| `Card.Updated` | Update card title/type/assignees |
| `Card.Moved` | Remove from old column, add to new column |
| `Card.Deleted` | Remove card from display |
| `Card.Archived` | Grey out / hide card |
| `Card.Restored` | Restore card visibility |
| `Column.Created` | Add column to board |
| `Column.Updated` | Update column name/WIP/color |
| `Column.Deleted` | Remove column |
| `ChecklistItem.*` | Update checklist in card detail |
| `Comment.*` | Update comments in card detail |
| `Spec.*` / `Plan.*` | Update spec/plan indicators |
| `CardRelationship.*` | Update dependency list |

### PresenceHub Events

| Event | Action |
|-------|--------|
| `CurrentUsers` | Initialize presence list in status bar |
| `UserJoined` | Increment online count, show username |
| `UserLeft` | Decrement online count |
| `CardFocused` | Show "User X viewing card Y" in status bar |
| `CardUnfocused` | Clear focus indicator |

### Reconnect Strategy

- `HubConnection` configured with `WithAutomaticReconnect(new[] { 0, 1000, 2000, 5000, 10000, 30000 })` — exponential backoff capped at 30s
- On `Reconnecting`: status bar indicator turns yellow "○ Reconnecting..."
- On `Reconnected`: re-`JoinProject` to restore group subscription, status bar turns green
- On `Closed`: after 5 failed retries, show lock screen overlay
- `HubConnection` state tracked in `AppState.ConnectionStatus` enum: `Connected | Reconnecting | Disconnected`

### Threading

- SignalR callbacks run on thread-pool threads
- All UI updates marshalled through `AnsiConsole` thread-safe methods or a render queue
- `LiveDisplayContext.Update(Action)` from SignalR callback — Spectre.Console handles thread safety internally for `LiveDisplay`

---

## Error Handling Strategy

### Error Sources

| Source | Handling |
|--------|----------|
| API HTTP errors (4xx/5xx) | NSwag client throws `ApiException<T>`. Catch in screen, extract correlationId from response headers or ProblemDetails body. Surface in status bar error panel. |
| Network errors (connection refused, DNS failure) | `HttpRequestException` caught by `SignalRConnectionManager` and `ApiClientFactory`. Trigger lock screen if persistent. |
| SignalR disconnects | Automatic reconnect with backoff. Status bar shows reconnect state. |
| `$EDITOR` launch failure | Fallback to inline `TextPrompt` for text input. Show warning toast. |
| Config file permission errors | Log warning, fall back to in-memory-only session (no JWT persistence). |
| Token expiry | `ApiClientFactory` intercepts 401, attempts `/api/auth/refresh`. If refresh fails, show login screen. |

### Error Panel

- Accessible via `E` key from any screen
- Shows last 5 errors in a scrollable list
- Each entry: `[correlationId:12] Error message` with timestamp
- `Del` key dismisses individual error
- `Esc` closes panel
- Badge in status bar: `⚠ N` where N is count of undismissed errors

### Lock Screen

- Full-screen overlay, no interaction possible except quit (`q`)
- Shows: `⚠ Server unreachable. Retrying...` with spinner
- Retry interval: 5s, 10s, 30s, 60s (capped)
- On successful reconnect: dismiss lock screen, return to previous view
- On `q`: confirm quit, exit application

### Error Correlation

- All API responses include `X-Correlation-Id` header
- `ApiClientFactory` reads it from response, attaches to error objects
- `ErrorCollector` stores `(DateTime, string correlationId, string message)` tuples
- CorrelationId displayed in error panel as truncated `[abc123...]` prefix

---

## Auth Flow

### Startup Sequence

```
1. Read config.json from `.hydraforge/config.json` at the repo root (D-49)
   ├─ File exists? → Parse TuiConfig
   │   ├─ Token expired? → Try /api/auth/refresh
   │   │   ├─ Success → Update stored token, proceed
   │   │   └─ Failure → Show login screen
   │   └─ Token valid → Proceed to project list
   └─ File missing → Show login screen

2. Login screen:
   ├─ Prompt: Server URL (default http://localhost:5000)
   ├─ Prompt: Username
   ├─ Prompt: Password (masked)
   ├─ POST /api/auth/login
   │   ├─ Success → Store JWT + expiry in config.json
   │   └─ Failure → Show error, retry
   └─ On success → Proceed to project list
```

### Token Refresh

- `ApiClientFactory` checks token expiry before each request
- If token expires within 60s, preemptively refresh
- On 401 response, attempt single refresh before failing
- Refresh failure → clear stored token → show login screen

### Config File Format

```json
{
  "serverUrl": "http://localhost:5000",
  "jwtToken": "eyJ...",
  "expiresAt": "2026-07-24T12:00:00Z",
  "refreshToken": null
}
```

- Written with `File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite)` (0600) on POSIX
- On Windows: default user-profile ACL via `File.Create` + `File.SetAttributes` for hidden
- No encryption for MVP (per D-48)

---

## NSwag Integration

### Build Step

- MSBuild target `GenerateApiClient` runs before build
- Invokes `nswag openapi2csclient` against the running server's `/openapi/v1.json`
- Output: `Generated/HydraForgeApiClient.cs`
- Target condition: skip if server not running (fallback to last generated client)

Alternative: checked-in generated client, regenerated manually when API changes. Decision at implementation time.

### Client Usage

```csharp
// ApiClientFactory creates and caches the NSwag client
var client = _apiClientFactory.CreateClient();
var projects = await client.ProjectsAsync();
```

- `ApiClientFactory` injects `Authorization: Bearer <token>` header via `HttpClient` message handler
- Handles token refresh transparently
- All API calls wrapped in try/catch for `ApiException<T>` → `ErrorCollector`

---

## Package Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Spectre.Console` | ^0.49 | Terminal UI framework (panels, tables, prompts, LiveDisplay) |
| `Spectre.Console.Cli` | ^0.49 | (Optional) command parsing for future extensibility |
| `Microsoft.AspNetCore.SignalR.Client` | 10.0.x | SignalR hub connection for real-time events |
| `NSwag.ApiDescription.Client` | ^14.x | NSwag MSBuild integration for codegen |
| `System.Text.Json` | (inbox) | Config file serialization |

Remove existing: `ProjectReference` to `HydraForge.Application`.

---

## File Structure

```
src/HydraForge.Tui/
├── HydraForge.Tui.csproj
├── Program.cs                          # Entry point, app bootstrap
├── Generated/
│   └── HydraForgeApiClient.cs          # NSwag-generated (gitignored or checked-in)
├── Screens/
│   ├── IScreen.cs                      # Screen interface: Render(), HandleKey(), OnEnter()/OnExit()
│   ├── LockScreen.cs
│   ├── LoginScreen.cs
│   ├── ProjectListScreen.cs
│   ├── BoardScreen.cs
│   ├── CardDetailScreen.cs
│   ├── SpecViewerScreen.cs
│   └── KeyboardReferenceScreen.cs
├── Services/
│   ├── ApiClientFactory.cs
│   ├── SignalRConnectionManager.cs
│   ├── ConfigStore.cs
│   ├── EditorLauncher.cs
│   ├── KeyboardDispatcher.cs
│   └── ErrorCollector.cs
├── Models/
│   ├── TuiConfig.cs
│   ├── AppState.cs
│   └── ScreenStack.cs
└── Renderers/
    ├── BoardRenderer.cs                # Column + card layout logic for LiveDisplay
    ├── StatusBarRenderer.cs            # Status bar content builder
    └── MarkupHelper.cs                 # Markdown → Spectre.Console markup converter
```

---

## Implementation Order

The work is organized into 14 tasks matching the Phase 4 checklist, ordered to minimize blocked dependencies:

| # | Task | Depends On | Effort |
|---|------|------------|--------|
| 1 | Scaffold: remove Application ref, add NuGet packages, NSwag codegen, `Program.cs` bootstrap | — | M |
| 2 | ConfigStore: read/write `.hydraforge/config.json` at the repo root (D-49) with 0600 perms | 1 | S |
| 3 | ApiClientFactory: NSwag client with JWT auth header + token refresh | 1, 2 | M |
| 4 | Auth: login screen + startup auth flow | 2, 3 | M |
| 5 | Connection handling: lock screen, auto-reconnect, status bar indicators | 1 | M |
| 6 | Project list view + create project | 3, 4 | M |
| 7 | Board view: Spectre.Console column/card layout + keyboard nav | 3, 4, 5 | L |
| 8 | SignalR integration: BoardHub + PresenceHub connection lifecycle, LiveDisplay updates | 5, 7 | L |
| 9 | Card detail view: all fields, section navigation, `$EDITOR` for description | 3, 7 | L |
| 10 | Create / edit / move cards via keyboard | 7, 9 | M |
| 11 | Dependency panel: search card, select type, confirm | 3, 9 | M |
| 12 | Blocked card indicator in board view | 7, 11 | S |
| 13 | Spec + plan viewer/editor (`$EDITOR`) | 3, 9 | M |
| 14 | Comments: inline view + add | 3, 9 | M |
| 15 | Checklists: toggle completion from keyboard | 3, 9 | M |
| 16 | Keyboard shortcut reference (`?` overlay) | 1 | S |
| 17 | Status bar: sync status, unread notifications, online presence, error panel | 5, 8 | M |

---

## Tasks

- [x] Task 1: Scaffold — packages, NSwag codegen, Program.cs bootstrap, remove Application ref
- [x] Task 2: ConfigStore — read/write `.hydraforge/config.json` at the repo root (D-49) with 0600 perms
- [x] Task 3: ApiClientFactory — NSwag client with JWT auth, token refresh
- [x] Task 4: Auth — login screen + startup auth flow
- [ ] Task 5: Connection handling — lock screen, auto-reconnect, status bar indicators
- [ ] Task 6: Project list view + create project
- [ ] Task 7: Board view — Spectre.Console column/card layout + keyboard nav
- [ ] Task 8: SignalR integration — BoardHub + PresenceHub, LiveDisplay updates
- [ ] Task 9: Card detail view — all fields, section nav, `$EDITOR` for description
- [ ] Task 10: Create / edit / move cards via keyboard
- [ ] Task 11: Dependency panel — search card, select type, confirm
- [ ] Task 12: Blocked card indicator in board view
- [ ] Task 13: Spec + plan viewer/editor (`$EDITOR`)
- [ ] Task 14: Comments — inline view + add
- [ ] Task 15: Checklists — toggle completion from keyboard
- [ ] Task 16: Keyboard shortcut reference (`?` overlay)
- [ ] Task 17: Status bar — sync status, unread notifications, online presence, error panel

---

## Out of Scope

- AI/chat features (Phase 6)
- Personal workspace (Phase 5)
- Admin dashboard (Phase 5)
- Attachment upload (TUI can view attachment metadata but cannot upload files — multipart/form-data in terminal is awkward; revisit post-MVP)
- Mouse support (not required per brief; all interactions keyboard-only)
- OS keychain integration (deferred per D-48)
- Offline mode / local caching (server is authoritative per architecture constraint)
- Multiple server profiles in config (single active server per D-48)
