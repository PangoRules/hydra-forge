# Plan 15: ChatHub + IChatHub (Streaming Transport)
**Branch:** `task/chat-hub`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 15

**Goal:** SignalR hub for streaming chat. One-active-stream invariant, cancel, usage recording, context compression.

**Files:**
- Create: `src/HydraForge.Application/Realtime/IChatHub.cs`
- Create: `src/HydraForge.Infrastructure/Realtime/ChatHub.cs`
- Modify: `src/HydraForge.Infrastructure/Realtime/RealtimeServiceCollectionExtensions.cs`
- Modify: `src/HydraForge.Server/Program.cs`
- Create: `tests/HydraForge.Server.Tests/Chat/ChatHubTests.cs`

**Steps:**

- [ ] Define `IChatHub`: events `StreamStart`, `StreamDelta`, `StreamDone`, `StreamError`, `Typing`
- [ ] Implement `ChatHub` (mirrors `BoardHub` pattern): `[Authorize]`, `[EnableRateLimiting("SignalR")]`
- [ ] `JoinSession(sessionId)`: authorize (owner or project member), add to group `chat-{sessionId}`
- [ ] `LeaveSession(sessionId)`: remove from group
- [ ] `SendMessage(sessionId, userMessageId, presetId?)`: look up persisted user message, validate ownership, check session Active, check one-active-stream (`ConcurrentDictionary<sessionId, StreamContext>`), run RAG, build `ChatRequest`, route via `ModelRouter`, allocate assistant messageId, emit `StreamStart`, `await foreach` deltas → `StreamDelta`, on finish persist assistant message + record usage, emit `StreamDone`. On error → `StreamError`, discard partial
- [ ] `CancelStream(sessionId)`: cancel CTS, emit `StreamDone`/`StreamError(Cancelled)`, discard partial
- [ ] Register in `Program.cs`: `app.MapHub<ChatHub>("/hubs/chat")`
- [ ] Write tests: join/leave auth, one-active-stream reject, closed session reject, cancel

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~ChatHub"`
