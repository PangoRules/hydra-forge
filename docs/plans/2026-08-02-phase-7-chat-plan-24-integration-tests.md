# Plan 24: Server Integration Tests + SignalR Hub Tests + Manual Validation
**Branch:** `task/integration-tests`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 24

**Goal:** Server integration tests (endpoint auth, close idempotency, F6, error paths), SignalR hub tests, manual validation matrix.

**Files:**
- Create: `tests/HydraForge.Server.Tests/Chat/ChatSessionsControllerTests.cs`
- Create: `tests/HydraForge.Server.Tests/Chat/ChatHubIntegrationTests.cs`
- Create: `docs/archive/manual-validation/2026-08-02-phase-7-chat-matrix.md`

**Steps:**

- [ ] `ChatSessionsControllerTests`: create session (auth), F6 implicit close (old session closed in background), close idempotency, `CHAT_SESSION_CLOSED` on send-after-close, `CHAT_STREAM_IN_PROGRESS`, document attach/detach auth
- [ ] `ChatHubIntegrationTests`: join/leave auth (owner vs project member vs non-member), `SendMessage` with valid/invalid `userMessageId`, one-active-stream invariant, cancel mid-stream, `StreamError` on closed session, `StreamError` on `CHAT_MESSAGE_NOT_FOUND`
- [ ] Register test stubs for new Application ports in all `CustomWebApplicationFactory` subclasses (per repo checklist)
- [ ] Manual validation matrix: consolidate the per-plan files written during plans 5–23 (`docs/manual-validation/2026-08-02-phase-7-chat-plan-*-matrix.md`) into `docs/archive/manual-validation/2026-08-02-phase-7-chat-matrix.md`, then delete the per-plan files in the same pass (per repo convention — don't leave them lying around after consolidation)

**Acceptance:**
- `dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~Chat"`
- All integration tests pass
