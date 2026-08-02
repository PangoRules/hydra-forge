# Plan 14: ChatSummaryGenerator
**Branch:** `task/chat-summary-generator`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 14

**Goal:** One LLM call on close. `CHAT_SUMMARY_FAILED` fallback.

**Files:**
- Create: `src/HydraForge.Application/Chat/ChatSummaryGenerator.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/ChatSummaryGeneratorTests.cs`

**Steps:**

- [ ] Implement `IChatSummaryGenerator.GenerateSummaryAsync(sessionId)`: fetch all messages, build prompt "Summarize this chat conversation in 2-3 sentences", route via `IModelRouter`, return summary string
- [ ] On LLM failure → return `Result.Failure(CHAT_SUMMARY_FAILED)`. Caller (ChatSessionService.CloseAsync) still closes session with `Summary=null`
- [ ] Empty session → skip LLM call, return `Result.Success(null)`
- [ ] Write tests: successful summary, empty session skip, LLM failure fallback

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatSummary"`
