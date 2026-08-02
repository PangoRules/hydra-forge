# Plan 22: TUI — ChatListScreen + ChatSessionScreen + ChatHubConnection
**Branch:** `task/tui-chat-screens`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 22

**Goal:** TUI chat list, session screen with streaming, cancel, close. `ChatHubConnection` service.

**Files:**
- Create: `src/HydraForge.Tui/Screens/ChatListScreen.cs`
- Create: `src/HydraForge.Tui/Screens/ChatSessionScreen.cs`
- Create: `src/HydraForge.Tui/Services/ChatHubConnection.cs`
- Modify: `src/HydraForge.Tui/Program.cs` (or main menu)

**Steps:**

- [ ] `ChatHubConnection.cs`: mirrors `SignalRConnectionManager` pattern. Connect to `/hubs/chat`. Events: `StreamStart`, `StreamDelta`, `StreamDone`, `StreamError`. Methods: `JoinSession`, `LeaveSession`, `SendMessage`, `CancelStream`
- [ ] `ChatListScreen.cs`: personal chats — folder tree, session list, search. Launch from main menu `c`. Enter on session → `ChatSessionScreen`
- [ ] `ChatSessionScreen.cs`: layout: title + scope toggle + personality + AI-edit mode (top), scrollable message list (middle, markdown via Spectre), input area (bottom, multi-line). Send (`Enter`), cancel stream (`Ctrl+C`), close session (`q`), help (`?`). Status bar: connection, streaming, token count
- [ ] Streaming: `await foreach` over `ChatHubConnection` deltas → accumulate into live assistant bubble
- [ ] Image messages show as `[image: filename]` placeholders (TUI can't render images)
- [ ] Wire into main menu navigation

**Acceptance:**
- `dotnet build src/HydraForge.Tui`
- `dotnet test` — existing TUI tests pass
