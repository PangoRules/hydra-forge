# Plan 22: TUI — ChatListScreen + ChatSessionScreen + ChatHubConnection
**Branch:** `task/tui-chat-screens`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 22

**Goal:** TUI main menu (new top-level entry point), chat list, session screen with streaming, cancel, close. `ChatHubConnection` service.

> **Reconciled 2026-08-04:** This TUI has never had a main-menu screen — Phase 4 shipped `Login → ProjectListScreen` directly (`Program.cs:122`, `appState.CurrentScreen = projectListScreen`), with no menu concept to launch chat from. Rather than bolt a chat entry point onto `ProjectListScreen`, add a real `MainMenuScreen` as the new post-login landing screen, with `ProjectListScreen` and the new `ChatListScreen` as its two destinations. This is a small change to the Phase-4-shipped entry flow, not a redesign of `ProjectListScreen`/`BoardScreen` — both are unchanged otherwise.

**Files:**
- Create: `src/HydraForge.Tui/Screens/MainMenuScreen.cs`
- Create: `src/HydraForge.Tui/Screens/ChatListScreen.cs`
- Create: `src/HydraForge.Tui/Screens/ChatSessionScreen.cs`
- Create: `src/HydraForge.Tui/Services/ChatHubConnection.cs`
- Modify: `src/HydraForge.Tui/Program.cs:122` (`appState.CurrentScreen = projectListScreen` → `appState.CurrentScreen = mainMenuScreen`)

**Steps:**

- [ ] `MainMenuScreen.cs`: simple selection list (same `Table` + arrow-key + Enter pattern as `ProjectListScreen`) with two entries — "Projects" (Enter → `ProjectListScreen`, unchanged) and "Chat" (Enter → `ChatListScreen`). `Esc`/`q` from either child screen returns here rather than to `LoginScreen` — this becomes the new "home", not a one-shot launcher
- [ ] `ChatHubConnection.cs`: mirrors `SignalRConnectionManager` pattern. Connect to `/hubs/chat`. Events: `StreamStart`, `StreamDelta`, `StreamDone`, `StreamError`. Methods: `JoinSession`, `LeaveSession`, `SendMessage(sessionId, userMessageId, presetId?)`, `CancelStream`
- [ ] `ChatListScreen.cs`: personal chats — folder tree, session list, search. Launched from `MainMenuScreen`. Enter on session → `ChatSessionScreen`
- [ ] `ChatSessionScreen.cs`: layout: title + scope toggle + personality + AI-edit mode (top), scrollable message list (middle, markdown via Spectre), input area (bottom, multi-line). On `Enter`: two-step send, same contract as Web (§2.2/§3.2) — (1) call `POST /api/chat/sessions/{sessionId}/messages` to persist the user message and get `messageId`, (2) call `ChatHubConnection.SendMessage(sessionId, messageId, presetId?)` to start the stream. Do **not** call `SendMessage` with raw content — the hub only accepts an already-persisted `userMessageId`. Cancel stream (`Ctrl+C`), close session (`q`), help (`?`). Status bar: connection, streaming, token count
- [ ] Streaming: `await foreach` over `ChatHubConnection` deltas → accumulate into live assistant bubble
- [ ] Image messages show as `[image: filename]` placeholders (TUI can't render images)
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-22-tui-chat-screens-matrix.md` — from login, land on `MainMenuScreen`; both "Projects" and "Chat" work and `Esc` from either returns to the menu, not to login; send a message in the TUI, confirm the two-step persist-then-stream contract works and deltas render live

**Acceptance:**
- `dotnet build src/HydraForge.Tui`
- `dotnet test` — existing TUI tests pass
