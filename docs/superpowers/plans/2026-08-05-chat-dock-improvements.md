# Slice A.2: Chat Dock Improvements — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix gaps in the global chat dock: history panel with infinite scroll, draft mode (no session until first message), per-session model persistence, card-context awareness, robust auto-title for local models, half-screen height, and `/chats` page infinite scroll.

**Architecture:** Backend: fix session-list cursor pagination (composite `UpdatedAt+Id`), add `PreferredModelConfigId`/`PreferredEffort` to `ChatSession`, widen `UpdateSettings` to 7 params, investigate/fix local-model title-gen timeout. Frontend: shared `useChatSessionList` composable, `ChatDock` draft/history/resume modes, board store `openCardId` for card context, `ChatInput`/`ChatModelPicker` external model-id props, `/chats` page infinite scroll, identity prompt update, height change.

**Tech Stack:** ASP.NET Core 10 / EF Core 10 / Nuxt 4 / Vue 3 / Pinia / `@vueuse/core` / xUnit / NSubstitute / Vitest

**Branch:** `task/web-chat-panel` (already checked out — do NOT create or switch branches)

**Spec:** `docs/superpowers/specs/2026-08-05-chat-dock-improvements-design.md`

---

## File Structure

**Create:**
- `src/web-ui/app/composables/useChatSessionList.ts`
- `src/web-ui/app/composables/__tests__/useChatSessionList.test.ts`
- `src/web-ui/app/components/chat/ChatDockHistory.vue`
- `src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts`
- `docs/manual-validation/2026-08-05-chat-dock-improvements-matrix.md`

**Modify:**
- `src/HydraForge.Domain/Entities/Chat/ChatSession.cs` — add `PreferredModelConfigId` + `PreferredEffort`; widen `UpdateSettings` 5→7 params
- `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` — map new columns
- `src/HydraForge.Infrastructure/Migrations/<timestamp>_AddChatSessionModelPreferences.cs` — migration
- `src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs` — fix `ListAsync` cursor to composite `(UpdatedAt, Id)`
- `src/HydraForge.Application/Chat/ChatDtos.cs` — add model fields + `beforeId`
- `src/HydraForge.Application/Chat/ChatSessionService.cs` — handle model fields, widen `UpdateSettings` call
- `src/HydraForge.Application/Chat/ChatReplyGenerator.cs` — auto-title fallback, widen `UpdateSettings` call
- `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs` — accept model fields + `beforeId`
- `src/HydraForge.Infrastructure/Llm/LlmServiceCollectionExtensions.cs` — per-provider-type timeout (only if D confirms)
- `src/web-ui/app/stores/chatDock.ts` — draft/history/resume modes, localStorage persistence
- `src/web-ui/app/stores/board.ts` — add `openCardId` field
- `src/web-ui/app/components/chat/ChatDock.vue` — draft mode, history panel, height, title rename, sessionRefreshed wiring, feature prop
- `src/web-ui/app/components/chat/ChatSessionView.vue` — add `feature` prop, pass through to ChatInput
- `src/web-ui/app/components/chat/ChatInput.vue` — accept external initial model id/effort
- `src/web-ui/app/components/chat/ChatModelPicker.vue` — accept & prefer external initial model id
- `src/web-ui/app/pages/projects/[id]/board.vue` — write `openCardId` to board store
- `src/web-ui/app/pages/chats/index.vue` — use `useChatSessionList`, infinite scroll, resume session
- `src/web-ui/app/lib/routes.ts` — add `beforeId` to `ApiRoutes.Chat.sessions.list(...)`
- `src/web-ui/app/assets/css/main.css` — CSS vars for z-index (only if G confirms)
- `src/web-ui/app/stores/__tests__/chatDock.test.ts` — update 2 existing tests that assert the old `startNewChat` POST body shape
- `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`
- `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs`
- `tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs`

---

## Task 1: Fix session list cursor pagination (composite `UpdatedAt+Id`)

**Files:**
- Modify: `src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs`
- Modify: `src/HydraForge.Application/Chat/ChatDtos.cs`
- Modify: `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs`
- Test: `tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs`

**Problem:** `EfChatSessionRepository.ListAsync` filters `before` against `CreatedAt` but sorts by `UpdatedAt`, with no `beforeId` tiebreaker. Two sessions with the same `UpdatedAt` get skipped/duplicated across pages.

- [x] **Step 1: Confirm target shapes — no query DTO record exists**

Verified against current code: there is no `ListSessionsQuery`/params-record anywhere in this path. `ChatSessionsController.List` takes `[FromQuery]` params directly (`folderId, projectId, before, limit`), and `ChatSessionService.ListAsync`/`IChatSessionRepository.ListAsync`/`EfChatSessionRepository.ListAsync` all take individual positional params the same way. `beforeId` gets added as one more individual param at each layer (Steps 4, 6, 7) — no DTO to create here.

Also note: `ChatSessionPageDto(IReadOnlyList<ChatSessionDto> Items, int TotalCount)` **already exists** in `ChatDtos.cs` — nothing to add there for this task. `ChatSessionService.ListAsync` already returns `Result<ChatSessionPageDto>` (not a bare DTO) — preserve that wrapper in Step 7, don't drop it.

- [x] **Step 2: Write the failing repository test**

In `tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs`, add a test:

```csharp
[Fact]
public async Task ListAsync_CompositeCursor_DoesNotSkipSameTimestampSessions()
{
    // Arrange: seed 3 sessions with the same UpdatedAt, different Ids
    var sameTime = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
    var session1 = new ChatSession { Id = Guid.NewGuid(), OwnerId = UserId, Title = "A", UpdatedAt = sameTime, CreatedAt = sameTime };
    var session2 = new ChatSession { Id = Guid.NewGuid(), OwnerId = UserId, Title = "B", UpdatedAt = sameTime, CreatedAt = sameTime };
    var session3 = new ChatSession { Id = Guid.NewGuid(), OwnerId = UserId, Title = "C", UpdatedAt = sameTime, CreatedAt = sameTime };
    // ... seed via DbContext, then:
    
    // Page 1: limit 2, before = max DateTime, beforeId = null
    var page1 = await repo.ListAsync(UserId, null, null, DateTime.MaxValue, null, 2, ct);
    Assert.Equal(2, page1.Count);
    
    // Page 2: before = sameTime, beforeId = page1[1].Id
    var page2 = await repo.ListAsync(UserId, null, null, sameTime, page1[1].Id, 2, ct);
    Assert.Single(page2);
    Assert.NotEqual(page1[0].Id, page2[0].Id);
    Assert.NotEqual(page1[1].Id, page2[0].Id);
}
```

Follow the existing test file's fixture pattern (in-memory DB setup, `UserId` constant, etc.).

- [x] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~CompositeCursor`
Expected: FAIL — current `ListAsync` doesn't accept `beforeId` and the cursor logic is wrong.

- [x] **Step 4: Fix `EfChatSessionRepository.ListAsync`**

Real current signature uses `ownerId` (not `actorId`) as the first param — keep that name. Change the method signature to accept `beforeId`:

```csharp
public async Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    CancellationToken ct = default)
```

Update the query. Real current code (confirmed, `EfChatSessionRepository.cs:33-52`):

```csharp
// Before (broken):
if (before.HasValue) query = query.Where(s => s.CreatedAt < before.Value);
return await query.OrderByDescending(s => s.UpdatedAt).Take(limit).ToListAsync(ct);

// After (fixed — composite cursor on UpdatedAt + Id):
if (before.HasValue)
{
    query = query.Where(s =>
        s.UpdatedAt < before.Value
        || (s.UpdatedAt == before.Value && s.Id < beforeId));
}
return await query.OrderByDescending(s => s.UpdatedAt).ThenByDescending(s => s.Id).Take(limit).ToListAsync(ct);
```

Also update the `IChatSessionRepository` interface to match the new signature.

- [x] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~CompositeCursor`
Expected: PASS

- [x] **Step 6: Update `ChatSessionsController` to accept `beforeId`**

In `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs`, add `beforeId` query param to `GET /api/chat/sessions`:

Real action is named `List` (not `ListSessions`), returns `Ok(result.Value)` where `result.Value` is the `ChatSessionPageDto` (already `{ items, totalCount }` — client-facing shape needs no change):

```csharp
[HttpGet]
public async Task<IActionResult> List(
    [FromQuery] Guid? folderId,
    [FromQuery] Guid? projectId,
    [FromQuery] DateTime? before = null,
    [FromQuery] Guid? beforeId = null,
    [FromQuery] int limit = 20)
```

Pass `beforeId` to `ChatSessionService.ListAsync(...)`.

- [x] **Step 7: Update `ChatSessionService.ListAsync` to accept and pass `beforeId`**

Preserve the existing `Result<ChatSessionPageDto>` return type — don't unwrap it:

```csharp
public async Task<Result<ChatSessionPageDto>> ListAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    CancellationToken ct = default)
```

- [x] **Step 8: Build to verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

- [x] **Step 9: Commit**

```bash
git add src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs src/HydraForge.Application/Chat/ChatDtos.cs src/HydraForge.Application/Chat/ChatSessionService.cs src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs
git commit -m "fix(chat): composite (UpdatedAt, Id) cursor for session list pagination"
```

---

## Task 2: Per-session model persistence (entity + migration)

**Files:**
- Modify: `src/HydraForge.Domain/Entities/Chat/ChatSession.cs`
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- Create: `src/HydraForge.Infrastructure/Migrations/<timestamp>_AddChatSessionModelPreferences.cs`

- [ ] **Step 1: Add fields + widen `UpdateSettings` on `ChatSession` entity**

In `src/HydraForge.Domain/Entities/Chat/ChatSession.cs`, add after existing fields:

```csharp
public Guid? PreferredModelConfigId { get; set; }
public string? PreferredEffort { get; set; }
```

Widen `UpdateSettings` from 5 to 7 params. Two corrections against the real current method (`ChatSession.cs:48-70`): `aiEditMode` is the `AiEditMode` enum, not `bool?` — a plain `bool?` would not compile against the existing `AiEditMode` property. The current method also throws `InvalidOperationException` if `Status != ChatSessionStatus.Active` — preserve that guard, don't drop it:

```csharp
public void UpdateSettings(
    string? title,
    Guid? folderId,
    Guid? personalityId,
    AiEditMode? aiEditMode,
    bool? searchAllMyDocs,
    Guid? preferredModelConfigId,
    string? preferredEffort)
{
    if (Status != ChatSessionStatus.Active)
        throw new InvalidOperationException("Cannot update settings on a non-active session.");

    if (title != null) Title = title;
    if (folderId.HasValue) FolderId = folderId;
    if (personalityId.HasValue) PersonalityId = personalityId;
    if (aiEditMode.HasValue) AiEditMode = aiEditMode.Value;
    if (searchAllMyDocs.HasValue) SearchAllMyDocs = searchAllMyDocs.Value;
    if (preferredModelConfigId.HasValue) PreferredModelConfigId = preferredModelConfigId;
    if (preferredEffort != null) PreferredEffort = preferredEffort;
    UpdatedAt = DateTime.UtcNow;
}
```

Confirm the exact guard condition/message against the real method before editing — the snippet above reconstructs it from the "throws if Status != Active" fact, not a verbatim quote.

- [ ] **Step 2: Map new columns in DbContext**

In `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`, find the `ConfigureEntity<ChatSession>` block. Add property configs:

```csharp
b.Property(s => s.PreferredEffort).HasColumnType("text");
```

`PreferredModelConfigId` is a `Guid?` — EF Core maps it to `uuid` by default, no `.HasColumnType()` needed.

- [ ] **Step 3: Generate migration**

```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations add AddChatSessionModelPreferences --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

Verify the `Up` method adds `preferred_model_config_id` (uuid, nullable) and `preferred_effort` (text, nullable) to the `chat_sessions` table.

- [ ] **Step 4: Verify model is clean**

```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

Expected: "No pending model changes."

- [ ] **Step 5: Build to verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED (existing callers of `UpdateSettings` will fail — fixed in Task 3)

- [ ] **Step 6: Commit**

```bash
git add src/HydraForge.Domain/Entities/Chat/ChatSession.cs src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs src/HydraForge.Infrastructure/Migrations/
git commit -m "feat(chat): add PreferredModelConfigId/PreferredEffort to ChatSession"
```

---

## Task 3: Per-session model persistence (DTOs + service + controller)

**Files:**
- Modify: `src/HydraForge.Application/Chat/ChatDtos.cs`
- Modify: `src/HydraForge.Application/Chat/ChatSessionService.cs`
- Modify: `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs`
- Test: `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`

- [ ] **Step 1: Add model fields to DTOs**

In `src/HydraForge.Application/Chat/ChatDtos.cs`:

```csharp
// ChatSessionDto — add:
public Guid? PreferredModelConfigId { get; init; }
public string? PreferredEffort { get; init; }

// ChatSessionDetailDto — add:
public Guid? PreferredModelConfigId { get; init; }
public string? PreferredEffort { get; init; }

// CreateChatSessionRequest — add:
public Guid? PreferredModelConfigId { get; init; }
public string? PreferredEffort { get; init; }

// UpdateChatSessionRequest — add:
public Guid? PreferredModelConfigId { get; init; }
public string? PreferredEffort { get; init; }
```

- [ ] **Step 2: Write the failing service test**

In `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`, add:

```csharp
[Fact]
public async Task CreateAsync_WithPreferredModelConfigId_PersistsIt()
{
    var fakes = new TestFakes();
    var modelConfigId = Guid.NewGuid();
    var request = new CreateChatSessionRequest(
        Title: "test",
        FolderId: null,
        ProjectId: null,
        OpenCardId: null,
        PersonalityId: null,
        AiEditMode: null,
        SearchAllMyDocs: false,
        ForkedFromSessionId: null,
        PreferredModelConfigId: modelConfigId,
        PreferredEffort: "medium"
    );
    var service = fakes.BuildService();
    var result = await service.CreateAsync(request, NewId());
    Assert.True(result.IsSuccess);
    var saved = fakes.Sessions.Single();
    Assert.Equal(modelConfigId, saved.PreferredModelConfigId);
    Assert.Equal("medium", saved.PreferredEffort);
}
```

Follow the existing `TestFakes` pattern (add `Sessions` list to the fake repo if not already exposed).

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~CreateAsync_WithPreferredModelConfigId_PersistsIt`
Expected: FAIL

- [ ] **Step 4: Update `ChatSessionService.CreateAsync` to store model fields**

After `session.UpdateSettings(...)` or direct property assignment in `CreateAsync`:

```csharp
if (request.PreferredModelConfigId.HasValue)
    session.PreferredModelConfigId = request.PreferredModelConfigId;
if (request.PreferredEffort != null)
    session.PreferredEffort = request.PreferredEffort;
```

- [ ] **Step 5: Update `ChatSessionService.UpdateAsync` to handle model fields**

In the PATCH handler, after the existing property checks:

```csharp
if (request.PreferredModelConfigId.HasValue)
    session.PreferredModelConfigId = request.PreferredModelConfigId;
if (request.PreferredEffort != null)
    session.PreferredEffort = request.PreferredEffort;
```

- [ ] **Step 6: Fix the existing `UpdateSettings` call site in `ChatSessionService.cs`**

Find line ~340 (the existing `session.UpdateSettings(...)` call) and add the two new params (`null, null`):

```csharp
session.UpdateSettings(title, folderId, personalityId, aiEditMode, searchAllMyDocs, null, null);
```

- [ ] **Step 7: Update controller to accept model fields**

In `ChatSessionsController.cs`, the `CreateChatSessionRequest` binding already uses the DTO — no manual mapping needed if the DTO is the parameter type. Verify the POST action signature uses `[FromBody] CreateChatSessionRequest request`. If it maps manually, add the new fields.

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~CreateAsync_WithPreferredModelConfigId_PersistsIt`
Expected: PASS

- [ ] **Step 9: Run full ChatSessionServiceTests suite**

Run: `dotnet test --filter FullyQualifiedName~ChatSessionServiceTests`
Expected: All PASS (existing tests may need `null, null` added to `UpdateSettings` calls in test setup code)

- [ ] **Step 10: Commit**

```bash
git add src/HydraForge.Application/Chat/ChatDtos.cs src/HydraForge.Application/Chat/ChatSessionService.cs src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs
git commit -m "feat(chat): per-session model persistence (DTOs, service, controller)"
```

---

## Task 4: Auto-title investigation + fallback

**Files:**
- Investigate: server logs for `LlmChatTitleGenerator` warning messages
- Modify: `src/HydraForge.Infrastructure/Llm/LlmServiceCollectionExtensions.cs` (only if timeout confirmed)
- Modify: `src/HydraForge.Application/Chat/ChatReplyGenerator.cs`
- Test: `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs`

- [ ] **Step 1: Check server logs for title-gen failure path**

The user's local model (Ollama gemma4:26b via OpenAiCompatible adapter) fails title gen. Check logs for one of these warnings from `LlmChatTitleGenerator`:
- `"Failed to resolve LLM route for chat title generation"` — routing issue
- `"LLM returned an error/content-filter finish reason for chat title generation"` — model error
- `"LLM call failed for chat title generation"` with exception — likely this one if it's a timeout

If the log shows `"LLM call failed"` with a `TaskCanceledException` or `OperationCanceledException`, the 60s `"openai-compatible"` HttpClient timeout is confirmed.

If it shows routing failure, the `ChatTitle` feature needs the model in its allowlist.

- [ ] **Step 2: Fix timeout per-provider-type (if confirmed)**

If Step 1 confirms the `"openai-compatible"` 60s timeout is the cause, change `LlmServiceCollectionExtensions.cs` to make the timeout configurable per-provider-type. The simplest approach: add a second named client for local OpenAiCompatible providers:

```csharp
// Keep the existing 60s for cloud OpenAI-compatible
services.AddHttpClient(
    "openai-compatible",
    client => { client.Timeout = TimeSpan.FromSeconds(60); }
);

// Add a longer timeout for local OpenAI-compatible (Ollama/LM Studio via OpenAI compat endpoint)
services.AddHttpClient(
    "openai-compatible-local",
    client => { client.Timeout = TimeSpan.FromSeconds(600); }
);
```

Then in `LlmClientFactory.cs`, when creating a client for an `OpenAiCompatible` provider, check if the base URL is a local address (`localhost`, `127.0.0.1`, `192.168.*`, `10.*`) and use the `-local` named client. Or simpler: just bump the `"openai-compatible"` timeout to 600s — the comment about "hung cloud request stalling the pipeline" is already handled by the 600s ceiling being bounded, not infinite. The user's local model is the common case for OpenAiCompatible.

**Simplest fix (recommended):** bump `"openai-compatible"` timeout to 600s, matching `"ollama"`. The 60s was arbitrary and already caused this exact bug once for narrative jobs. Add a comment explaining why.

```csharp
services.AddHttpClient(
    "openai-compatible",
    client =>
    {
        // Local models (Ollama/LM Studio via OpenAI-compat endpoint) routinely exceed
        // 60s for cold-model-load + generation. 600s matches the dedicated ollama client.
        client.Timeout = TimeSpan.FromSeconds(600);
    }
);
```

- [ ] **Step 3: Write the failing fallback test**

In `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs`, add:

```csharp
[Fact]
public async Task GenerateAsync_TitleGenFails_FallsBackToFirstMessage()
{
    // Arrange: seed session with System identity + first user message
    var session = new ChatSession { Id = SessionId, OwnerId = UserId, Status = ChatSessionStatus.Active };
    var identityMsg = new ChatMessage { Id = Guid.NewGuid(), SessionId = SessionId, Role = MessageRole.System, Content = "You are HydraForge." };
    var userMsg = new ChatMessage { Id = MessageId, SessionId = SessionId, Role = MessageRole.User, Content = "Hello world this is a test message" };
    _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
    _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMsg);
    _messageRepo
        .GetBySessionAsync(SessionId, Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(new List<ChatMessage> { identityMsg, userMsg });

    // Make title generator fail
    _titleGenerator.GenerateTitleAsync(UserId, "Hello world this is a test message", Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(Result<string>.Failure(new Error("Chat.TitleFailed", "Simulated failure")));

    // Wire up streaming to succeed (mirror an existing happy-path test)
    // ... (follow existing test pattern for MakeImmediateEnumerable / StreamChatAsync)

    await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

    // Assert fallback title = first 60 chars of first user message
    await _sessionRepo.Received(1).UpdateAsync(
        Arg.Is<ChatSession>(s => s.Title == "Hello world this is a test message"),
        Arg.Any<CancellationToken>());
}
```

Follow the existing test fixture pattern (make `_titleGenerator` a field, set up via `_titleGenerator = Substitute.For<IChatTitleGenerator>()`).

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~GenerateAsync_TitleGenFails_FallsBackToFirstMessage`
Expected: FAIL

- [ ] **Step 5: Add fallback in `ChatReplyGenerator.cs`**

Find the title-generation block (around line 345-369). Replace:

```csharp
var titleResult = await _titleGenerator.GenerateTitleAsync(
    userId, userMessage.Content, content, ct);
if (titleResult.IsSuccess)
{
    session.UpdateSettings(titleResult.Value, null, null, null, null);
    await _sessionRepo.UpdateAsync(session, ct);
}
else
{
    _logger.LogWarning("Failed to generate chat title: {Error}", titleResult.Error.Message);
}
```

With:

```csharp
var titleResult = await _titleGenerator.GenerateTitleAsync(
    userId, userMessage.Content, content, ct);
var title = titleResult.IsSuccess
    ? titleResult.Value
    : userMessage.Content.Length <= 60
        ? userMessage.Content
        : userMessage.Content[..60] + "…";
session.UpdateSettings(title, null, null, null, null, null, null);
await _sessionRepo.UpdateAsync(session, ct);
```

Note: `UpdateSettings` now takes 7 params — the two new `null, null` are for `preferredModelConfigId` and `preferredEffort`.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~GenerateAsync_TitleGenFails_FallsBackToFirstMessage`
Expected: PASS

- [ ] **Step 7: Run full ChatReplyGeneratorTests suite**

Run: `dotnet test --filter FullyQualifiedName~ChatReplyGeneratorTests`
Expected: All PASS

- [ ] **Step 8: Commit**

```bash
git add src/HydraForge.Application/Chat/ChatReplyGenerator.cs src/HydraForge.Infrastructure/Llm/LlmServiceCollectionExtensions.cs tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs
git commit -m "fix(chat): auto-title fallback + bump openai-compatible timeout to 600s"
```

---

## Task 5: Card context wiring (board store + dock)

**Files:**
- Modify: `src/web-ui/app/stores/board.ts`
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue`
- Modify: `src/web-ui/app/stores/chatDock.ts`

- [ ] **Step 1: Add `openCardId` to board Pinia store**

In `src/web-ui/app/stores/board.ts`, add a new field:

```typescript
export const useBoardStore = defineStore('board', () => {
  // ... existing state ...
  const openCardId = ref<string | null>(null)

  function setOpenCardId(id: string | null) {
    openCardId.value = id
  }

  return {
    // ... existing returns ...
    openCardId,
    setOpenCardId
  }
})
```

- [ ] **Step 2: Write `openCardId` from `board.vue` instead of local ref**

In `src/web-ui/app/pages/projects/[id]/board.vue`, find the local `selectedCardId` ref (line ~37). Replace:

```typescript
const selectedCardId = ref<string | null>(null)
```

With:

```typescript
const boardStore = useBoardStore()
const selectedCardId = computed({
  get: () => boardStore.openCardId,
  set: (val) => boardStore.setOpenCardId(val)
})
```

Also add `import { useBoardStore } from '~/stores/board'` at the top.

Verify all usages of `selectedCardId` in the template still work (they reference the ref — now it's a computed that reads/writes the store).

- [ ] **Step 3: Read `openCardId` in `chatDock` store on new chat**

Note: Task 8 rewrites `startNewChat` again as part of the larger draft/history/resume store overhaul — this step's edit will be superseded there. Doing it here first still verifies the board-store wiring in isolation before Task 8's bigger rewrite lands on top of it.

In `src/web-ui/app/stores/chatDock.ts`, in `startNewChat`. `useBoardStore()` always succeeds once Pinia is active (it lazily creates the store on first call) — no try/catch needed, unlike an earlier draft of this step assumed:

```typescript
async function startNewChat() {
  if (isCreating.value) return
  isCreating.value = true
  try {
    const openCardId = currentProjectId.value ? useBoardStore().openCardId : null
    const body: Record<string, unknown> = { title: '' }
    if (currentProjectId.value) body.projectId = currentProjectId.value
    if (openCardId) body.openCardId = openCardId
    const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), { body })
    if (data) {
      activeSessionId.value = data.id
      localStorage.setItem('hydraforge:chat:activeSessionId', data.id)
    }
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : 'Failed to create chat session')
  } finally {
    isCreating.value = false
  }
}
```

- [ ] **Step 4: Verify typecheck + lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/stores/board.ts src/web-ui/app/pages/projects/[id]/board.vue src/web-ui/app/stores/chatDock.ts
git commit -m "feat(chat): card context wiring — board store openCardId, dock passes to new sessions"
```

---

## Task 6: ChatInput/ChatModelPicker external model id

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatModelPicker.vue`
- Modify: `src/web-ui/app/components/chat/ChatInput.vue`
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue`

- [ ] **Step 1: Add `initialModelId`/`initialEffort` props to `ChatModelPicker`**

In `src/web-ui/app/components/chat/ChatModelPicker.vue`, add to `defineProps`:

```typescript
const props = defineProps<{
  feature?: string
  initialModelId?: string | null
  initialEffort?: string | null
}>()
```

Note: `modelId`/`effort` in this component are `defineModel()`/`defineModel('effort')` two-way-bound props (parent-owned via `v-model`/`v-model:effort` from `ChatInput`), not plain internal refs. Writing `modelId.value = ...` still works — `defineModel`'s setter emits `update:modelValue` to the parent, so the assignment propagates up through the existing v-model contract. No structural change needed, just be aware it's not local-only state.

In `fetchModels()` (`ChatModelPicker.vue:82-98`, not ~85-91), after models are loaded, apply the initial value:

```typescript
async function fetchModels() {
  try {
    const { data } = await api.GET<AvailableModelDto[]>(ApiRoutes.Llm.models(props.feature))
    if (data) {
      models.value = data
      // Prefer external initialModelId over localStorage
      if (props.initialModelId && data.some(m => m.providerModelConfigId === props.initialModelId)) {
        modelId.value = props.initialModelId
        if (props.initialEffort) effort.value = props.initialEffort
      } else {
        // Fall back to localStorage (existing logic)
        const saved = localStorage.getItem(storageKey.value)
        if (saved && data.some(m => m.providerModelConfigId === saved)) {
          modelId.value = saved
        }
      }
    }
  } catch (err) {
    console.error('Failed to fetch models', err)
  }
}
```

- [ ] **Step 2: Add `initialModelId`/`initialEffort` props to `ChatInput`**

In `src/web-ui/app/components/chat/ChatInput.vue`, add to `defineProps`:

```typescript
const props = defineProps<{
  feature?: string
  initialModelId?: string | null
  initialEffort?: string | null
}>()
```

`feature` prop already exists on `ChatInput` (default `'PersonalChat'`) — only add `initialModelId`/`initialEffort`, don't redeclare `feature`.

**Preserve the existing `v-model` bindings** — `ChatInput`'s current template already renders `<ChatModelPicker v-model="selectedModelId" v-model:effort="selectedEffort" :feature="feature" :disabled="disabled" />`. Add the two new props alongside those, don't replace them:

```vue
<ChatModelPicker
  v-model="selectedModelId"
  v-model:effort="selectedEffort"
  :feature="feature"
  :disabled="disabled"
  :initial-model-id="initialModelId"
  :initial-effort="initialEffort"
/>
```

- [ ] **Step 3: Add `feature` prop to `ChatSessionView` + forward `initialModelId`/`initialEffort` (already exist as props, just unused)**

`initialModelId`/`initialEffort` already exist on `ChatSessionView`'s `defineProps` — today they're only used to seed the first auto-send call, never forwarded to `ChatInput`. Only `feature` is genuinely new:

```typescript
const props = defineProps<{
  sessionId: string
  initialMessage?: string | null
  autoSendInitial?: boolean
  initialPresetId?: string | null
  initialModelId?: string | null
  initialEffort?: string | null
  feature?: string
}>()
```

In the template, pass `feature` and `initialModelId`/`initialEffort` to `ChatInput`:

```vue
<ChatInput
  ref="chatInputRef"
  :disabled="awaitingReply || session?.status !== 'Active'"
  :feature="feature"
  :initial-model-id="initialModelId"
  :initial-effort="initialEffort"
  @send="handleSend"
  @cancel="handleCancel"
>
```

- [ ] **Step 4: Resolve session-persisted model in `ChatSessionView.fetchSession`**

`initialModelId`/`initialEffort` are **props**, not local refs — they can't be reassigned (`props.initialModelId.value = ...` doesn't compile / mutating a prop directly is a Vue anti-pattern the linter will flag). The plan's earlier draft of this step named new local refs identically to the existing props, which would shadow them and silently break the case where a caller (e.g. `board.vue`) explicitly passes `initial-model-id` for a fresh session.

Instead, add a computed that prefers an explicitly-passed prop over the session's persisted value, and bind that computed to `ChatInput` (not the raw props):

```typescript
const resolvedInitialModelId = computed(() => props.initialModelId ?? session.value?.preferredModelConfigId ?? null)
const resolvedInitialEffort = computed(() => props.initialEffort ?? session.value?.preferredEffort ?? null)
```

Update the `<ChatInput>` binding from Step 3 to use these computeds instead of `initialModelId`/`initialEffort` directly:

```vue
<ChatInput
  ref="chatInputRef"
  :disabled="awaitingReply || session?.status !== 'Active'"
  :feature="feature"
  :initial-model-id="resolvedInitialModelId"
  :initial-effort="resolvedInitialEffort"
  @send="handleSend"
  @cancel="handleCancel"
>
```

- [ ] **Step 5: Pass feature from ChatDock to ChatSessionView**

In `src/web-ui/app/components/chat/ChatDock.vue`, pass `feature` based on page context:

```vue
<ChatSessionView
  v-if="dock.activeSessionId"
  :key="dock.activeSessionId"
  :session-id="dock.activeSessionId"
  :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
/>
```

- [ ] **Step 6: Verify typecheck + lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: All pass

- [ ] **Step 7: Commit**

```bash
git add src/web-ui/app/components/chat/ChatModelPicker.vue src/web-ui/app/components/chat/ChatInput.vue src/web-ui/app/components/chat/ChatSessionView.vue src/web-ui/app/components/chat/ChatDock.vue
git commit -m "feat(chat): external model id props + feature prop wiring for model persistence"
```

---

## Task 7: `useChatSessionList` composable

**Files:**
- Create: `src/web-ui/app/composables/useChatSessionList.ts`
- Test: `src/web-ui/app/composables/__tests__/useChatSessionList.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/composables/__tests__/useChatSessionList.test.ts`:

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

// Mock useApi — follow existing composable test pattern
const mockGET = vi.fn()
vi.mock('#imports', () => ({
  useApi: () => ({ GET: mockGET }),
  useAppToast: () => ({ error: vi.fn() })
}))

describe('useChatSessionList', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loads initial page on mount', async () => {
    mockGET.mockResolvedValue({
      data: { items: [{ id: '1', title: 'Test' }], totalCount: 1 },
      error: undefined
    })
    const { sessions, loading } = useChatSessionList()
    expect(loading.value).toBe(true)
    await vi.dynamicImportSettled()
    expect(sessions.value.length).toBe(1)
    expect(loading.value).toBe(false)
  })

  it('loadMore appends next page', async () => {
    // Page 1: 2 items
    mockGET.mockResolvedValueOnce({
      data: {
        items: [
          { id: '1', title: 'A', updatedAt: '2026-08-05T12:00:00Z' },
          { id: '2', title: 'B', updatedAt: '2026-08-05T11:00:00Z' }
        ],
        totalCount: 2
      },
      error: undefined
    })
    const { sessions, loadMore, hasMore } = useChatSessionList()
    await vi.dynamicImportSettled()
    expect(sessions.value.length).toBe(2)

    // Page 2: 1 more item
    mockGET.mockResolvedValueOnce({
      data: {
        items: [{ id: '3', title: 'C', updatedAt: '2026-08-05T10:00:00Z' }],
        totalCount: 1
      },
      error: undefined
    })
    await loadMore()
    expect(sessions.value.length).toBe(3)
    expect(hasMore.value).toBe(false)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- useChatSessionList`
Expected: FAIL — composable doesn't exist

- [ ] **Step 2b: Add `beforeId` to `ApiRoutes.Chat.sessions.list`**

`useApi().GET<T>()` takes a single URL-string argument only — no second `{ params }` object (unlike `POST`/`PATCH`/`PUT`/`DELETE`, which do accept an `opts` arg). `routes.ts` bakes query params into the URL string itself; there is no openapi-fetch `{ params: { query } }` convention in this codebase. Confirmed by the sibling `messages` route (`routes.ts:208-209`), which already does exactly this for its own `before`/`beforeId`/`limit` cursor — mirror it:

In `src/web-ui/app/lib/routes.ts`, extend `Chat.sessions.list`:

```typescript
list: (folderId?: string, projectId?: string, before?: string, beforeId?: string, limit = 20) =>
  `/api/chat/sessions?${folderId ? `folderId=${folderId}&` : ''}${projectId ? `projectId=${projectId}&` : ''}${before ? `before=${before}&` : ''}${beforeId ? `beforeId=${beforeId}&` : ''}limit=${limit}`,
```

- [ ] **Step 3: Implement the composable**

Create `src/web-ui/app/composables/useChatSessionList.ts`. Build the full URL via `ApiRoutes` and call `api.GET<T>(url)` with a single argument — no `{ params }` object:

```typescript
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'

interface ChatSessionPageDto {
  items: ChatSessionDto[]
  totalCount: number
}

export function useChatSessionList(options?: { folderId?: string; projectId?: string }) {
  const sessions = ref<ChatSessionDto[]>([])
  const loading = ref(false)
  const hasMore = ref(true)
  const api = useApi()

  async function loadMore() {
    if (loading.value || !hasMore.value) return
    loading.value = true
    try {
      const last = sessions.value[sessions.value.length - 1]
      const url = ApiRoutes.Chat.sessions.list(
        options?.folderId,
        options?.projectId,
        last?.updatedAt,
        last?.id,
        20
      )
      const { data } = await api.GET<ChatSessionPageDto>(url)
      if (data) {
        sessions.value.push(...data.items)
        hasMore.value = data.items.length === 20
      }
    } catch {
      // Silently fail — next scroll attempt will retry
    } finally {
      loading.value = false
    }
  }

  async function refresh() {
    sessions.value = []
    hasMore.value = true
    await loadMore()
  }

  // Load initial page
  loadMore()

  return {
    sessions: readonly(sessions),
    loading: readonly(loading),
    hasMore: readonly(hasMore),
    loadMore,
    refresh
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- useChatSessionList`
Expected: PASS. Adjust mocks as needed for the composable's import pattern.

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/composables/useChatSessionList.ts src/web-ui/app/composables/__tests__/useChatSessionList.test.ts
git commit -m "feat(chat): useChatSessionList composable for cursor-paginated session list"
```

---

## Task 8: ChatDock draft mode + history panel + session resume

**Files:**
- Modify: `src/web-ui/app/stores/chatDock.ts`
- Modify: `src/web-ui/app/components/chat/ChatDock.vue`
- Create: `src/web-ui/app/components/chat/ChatDockHistory.vue`
- Test: `src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts`

- [ ] **Step 1: Update `chatDock` store with draft/history/resume modes**

In `src/web-ui/app/stores/chatDock.ts`, replace the store with:

```typescript
import { defineStore } from 'pinia'
import { ApiError } from '~/lib/api-error'
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'

const LS_ACTIVE_SESSION_KEY = 'hydraforge:chat:activeSessionId'

export const useChatDockStore = defineStore('chatDock', () => {
  const isOpen = ref(false)
  const mode = ref<'draft' | 'session' | 'history'>('draft')
  const activeSessionId = ref<string | null>(null)
  const isCreating = ref(false)
  const position = ref({ x: 0, y: 0 })

  const route = useRoute()
  const api = useApi()
  const toast = useAppToast()

  const currentProjectId = computed(() => {
    if (route.path.match(/^\/projects\/[^/]+\/board/)) {
      return route.params.id as string
    }
    return null
  })

  function toggleDock() {
    isOpen.value = !isOpen.value
  }

  function openDock() {
    isOpen.value = true
    // Resume last active session from localStorage
    if (!activeSessionId.value) {
      const saved = localStorage.getItem(LS_ACTIVE_SESSION_KEY)
      if (saved) {
        activeSessionId.value = saved
        mode.value = 'session'
        return
      }
    }
    // Default to draft mode
    if (!activeSessionId.value) {
      mode.value = 'draft'
    }
  }

  function closeDock() {
    isOpen.value = false
    // Preserve activeSessionId in localStorage for resume
    if (activeSessionId.value) {
      localStorage.setItem(LS_ACTIVE_SESSION_KEY, activeSessionId.value)
    }
  }

  function loadSession(sessionId: string) {
    activeSessionId.value = sessionId
    mode.value = 'session'
    localStorage.setItem(LS_ACTIVE_SESSION_KEY, sessionId)
  }

  function newChat() {
    activeSessionId.value = null
    localStorage.removeItem(LS_ACTIVE_SESSION_KEY)
    mode.value = 'draft'
  }

  function showHistory() {
    mode.value = 'history'
  }

  function hideHistory() {
    mode.value = activeSessionId.value ? 'session' : 'draft'
  }

  async function startNewChat() {
    if (isCreating.value) return
    isCreating.value = true
    try {
      // useBoardStore() always succeeds once Pinia is active (lazily creates
      // the store on first call) — no try/catch needed here. On a page with
      // no board mounted, openCardId is simply the store's untouched default (null).
      const openCardId = currentProjectId.value ? useBoardStore().openCardId : null
      const body: Record<string, unknown> = { title: '' }
      if (currentProjectId.value) body.projectId = currentProjectId.value
      if (openCardId) body.openCardId = openCardId
      const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), { body })
      if (data) {
        activeSessionId.value = data.id
        mode.value = 'session'
        localStorage.setItem(LS_ACTIVE_SESSION_KEY, data.id)
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to create chat session')
    } finally {
      isCreating.value = false
    }
  }

  return {
    isOpen, mode, activeSessionId, isCreating, position, currentProjectId,
    toggleDock, openDock, closeDock, loadSession, newChat, showHistory, hideHistory, startNewChat
  }
})
```

- [ ] **Step 1b: Update the 2 existing `chatDock.test.ts` tests that assert the old `startNewChat` body**

`src/web-ui/app/stores/__tests__/chatDock.test.ts` currently has tests asserting `startNewChat` POSTs `body: { title: 'New chat', projectId: 'abc' }` and `body: { title: 'New chat' }` (no project). The rewrite above changes the body to `{ title: '' }` plus conditional `projectId`/`openCardId` — those 2 tests will fail unmodified. Update their expected `body` assertions to match the new shape (`title: ''`, `projectId` only when set, `openCardId` only when set).

- [ ] **Step 2: Update `ChatDock.vue` with draft mode + history panel + height**

Replace the template body section. Key changes:
- Remove the auto-create watch (no more `watch(() => dock.isOpen, ...)`)
- Add draft mode (chat input + empty state, no ChatSessionView)
- Add history mode (ChatDockHistory component)
- Change height from `h-[560px]` to `h-[50vh]`
- Wire `@sessionRefreshed` on ChatSessionView

```vue
<script setup lang="ts">
import { useDraggable } from '@vueuse/core'
import { useChatDockStore } from '~/stores/chatDock'
import ChatSessionView from '~/components/chat/ChatSessionView.vue'
import ChatDockHistory from '~/components/chat/ChatDockHistory.vue'

const dock = useChatDockStore()
const route = useRoute()

const isHidden = computed(() => route.path.startsWith('/chats'))

const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)

const { x, y } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: { x: 0, y: 0 },
  preventDefault: true
})

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && dock.isOpen) {
    dock.closeDock()
  }
}

const isClient = import.meta.client
onMounted(() => {
  if (isClient) window.addEventListener('keydown', onKeydown)
})
onUnmounted(() => {
  if (isClient) window.removeEventListener('keydown', onKeydown)
})

function onSessionRefreshed(id: string, title: string, _status: string) {
  // Title updated server-side — store refreshes on next open
}

function handleSendInDraft(message: string) {
  // Create session + send message in one flow
  dock.startNewChat()
  // The session is created; the message will be sent via ChatSessionView
  // after it mounts with the new sessionId. This is handled by the
  // autoSendInitial flow — we need to pass the message.
  // For simplicity: create session, then switch to session mode.
  // The user will see the session and can type again.
  // (Full draft→send flow is a future refinement.)
}
</script>

<template>
  <ClientOnly>
    <template v-if="!isHidden">
      <!-- FAB -->
      <UButton
        v-if="!dock.isOpen"
        icon="i-lucide-messages-square"
        size="lg"
        color="primary"
        rounded="full"
        class="fixed bottom-4 right-4 z-[var(--z-chat-fab)] shadow-lg"
        title="Open chat"
        aria-label="Open chat"
        @click="dock.openDock()"
      />

      <!-- Popup -->
      <div
        v-if="dock.isOpen"
        ref="popupRef"
        class="fixed z-[var(--z-chat-popup)] w-[380px] h-[50vh] max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
        :style="{ left: `${x}px`, top: `${y}px` }"
      >
        <!-- Header -->
        <div
          ref="dragHandle"
          class="shrink-0 flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700 cursor-move select-none"
        >
          <div class="flex items-center gap-2">
            <UButton
              v-if="dock.mode === 'session'"
              icon="i-lucide-chevron-left"
              variant="ghost"
              size="xs"
              title="History"
              @click="dock.showHistory()"
            />
            <h2 class="font-semibold text-sm truncate">
              {{ dock.activeSessionId ? 'Chat' : 'New Chat' }}
            </h2>
          </div>
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-plus"
              variant="ghost"
              size="xs"
              title="New chat"
              :disabled="dock.isCreating"
              @click="dock.newChat()"
            />
            <UButton
              icon="i-lucide-x"
              variant="ghost"
              size="xs"
              title="Close"
              @click="dock.closeDock()"
            />
          </div>
        </div>

        <!-- Body -->
        <div class="flex-1 min-h-0 flex flex-col">
          <!-- Draft mode -->
          <div v-if="dock.mode === 'draft'" class="flex-1 flex flex-col">
            <div class="flex-1 flex items-center justify-center p-4">
              <p class="text-sm text-muted text-center">
                Start a new conversation.
              </p>
            </div>
            <div class="shrink-0 px-4 pb-4">
              <UInput
                placeholder="Type a message..."
                @keydown.enter="handleSendInDraft"
              />
            </div>
          </div>

          <!-- History mode -->
          <ChatDockHistory
            v-if="dock.mode === 'history'"
            @select="dock.loadSession"
            @back="dock.hideHistory"
          />

          <!-- Session mode -->
          <ChatSessionView
            v-if="dock.mode === 'session' && dock.activeSessionId"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
            :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
            @session-refreshed="onSessionRefreshed"
          />
        </div>
      </div>
    </template>
  </ClientOnly>
</template>
```

- [ ] **Step 3: Create `ChatDockHistory.vue`**

Create `src/web-ui/app/components/chat/ChatDockHistory.vue`:

```vue
<script setup lang="ts">
import { useChatSessionList } from '~/composables/useChatSessionList'

const emit = defineEmits<{
  select: [sessionId: string]
  back: []
}>()

const { sessions, loading, hasMore, loadMore } = useChatSessionList()

// IntersectionObserver sentinel for infinite scroll
const sentinel = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

onMounted(() => {
  observer.value = new IntersectionObserver(
    ([entry]) => {
      if (entry.isIntersecting && !loading.value && hasMore.value) {
        loadMore()
      }
    },
    { rootMargin: '100px' }
  )
  if (sentinel.value) observer.value.observe(sentinel.value)
})

onUnmounted(() => {
  observer.value?.disconnect()
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700">
      <UButton
        icon="i-lucide-chevron-left"
        variant="ghost"
        size="xs"
        @click="emit('back')"
      >
        Back
      </UButton>
      <span class="text-sm font-medium">History</span>
      <div class="w-8" />
    </div>
    <div class="flex-1 overflow-y-auto">
      <div
        v-for="session in sessions"
        :key="session.id"
        class="px-4 py-3 border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800 cursor-pointer"
        @click="emit('select', session.id)"
      >
        <div class="text-sm font-medium truncate">{{ session.title || 'New Chat' }}</div>
        <div class="text-xs text-muted truncate mt-0.5">{{ session.summary || 'No messages' }}</div>
      </div>
      <div v-if="loading" class="px-4 py-3 text-sm text-muted text-center">
        Loading...
      </div>
      <div
        v-if="hasMore"
        ref="sentinel"
        class="h-4"
      />
      <div
        v-if="!hasMore && sessions.length > 0"
        class="px-4 py-3 text-sm text-muted text-center"
      >
        All caught up
      </div>
      <div
        v-if="!loading && sessions.length === 0"
        class="px-4 py-8 text-sm text-muted text-center"
      >
        No conversations yet.
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 4: Write the `ChatDockHistory` test**

Create `src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ChatDockHistory from '~/components/chat/ChatDockHistory.vue'

// Stub useChatSessionList
vi.mock('~/composables/useChatSessionList', () => ({
  useChatSessionList: () => ({
    sessions: ref([]),
    loading: ref(false),
    hasMore: ref(false),
    loadMore: vi.fn(),
    refresh: vi.fn()
  })
}))

describe('ChatDockHistory', () => {
  it('renders empty state', async () => {
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('No conversations yet')
  })

  it('renders session list', async () => {
    // Override mock to return sessions
    vi.mocked(useChatSessionList).mockReturnValue({
      sessions: ref([{ id: '1', title: 'Test Chat', summary: 'Hello', updatedAt: '2026-01-01' }]),
      loading: ref(false),
      hasMore: ref(false),
      loadMore: vi.fn(),
      refresh: vi.fn()
    })
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('Test Chat')
  })
})
```

- [ ] **Step 5: Run tests**

Run: `cd src/web-ui && pnpm test -- ChatDock`
Expected: PASS (existing tests + new history test)

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/stores/chatDock.ts src/web-ui/app/components/chat/ChatDock.vue src/web-ui/app/components/chat/ChatDockHistory.vue src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts
git commit -m "feat(chat): ChatDock draft mode, history panel, session resume, half-screen height"
```

---

## Task 9: `/chats` page infinite scroll + session resume

**Files:**
- Modify: `src/web-ui/app/pages/chats/index.vue`

- [ ] **Step 1: Replace flat fetch with `useChatSessionList`**

In `src/web-ui/app/pages/chats/index.vue`, replace the existing `fetchSessions()` function with:

```typescript
import { useChatSessionList } from '~/composables/useChatSessionList'
import { useChatDockStore } from '~/stores/chatDock'

const dock = useChatDockStore()
const { sessions, loading, hasMore, loadMore } = useChatSessionList()

// Resume active session from dock store or localStorage
const activeSessionId = ref<string | null>(null)
onMounted(() => {
  if (dock.activeSessionId) {
    activeSessionId.value = dock.activeSessionId
  } else {
    const saved = localStorage.getItem('hydraforge:chat:activeSessionId')
    if (saved) activeSessionId.value = saved
  }
})
```

Replace the sidebar list template to use `sessions` from the composable and add an IntersectionObserver sentinel for infinite scroll (same pattern as `ChatDockHistory.vue`).

**Preserve existing state this file already has and the snippet above doesn't mention:** `pendingMessage`, `syncSession`, `archiveTargetId`. Don't drop them while swapping in `useChatSessionList` — they're unrelated to the fetch-and-paginate change (archive flow, cross-session message handoff).

- [ ] **Step 2: Verify typecheck + lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: All pass

- [ ] **Step 3: Commit**

```bash
git add src/web-ui/app/pages/chats/index.vue
git commit -m "feat(chat): /chats page infinite scroll + session resume from dock"
```

---

## Task 10: Identity prompt update + title rename + z-index verify

**Files:**
- Modify: `src/HydraForge.Application/Chat/ChatSessionService.cs`
- Modify: `src/web-ui/app/components/chat/ChatDock.vue`
- Modify: `src/web-ui/app/pages/chats/index.vue`
- Modify: `src/web-ui/app/assets/css/main.css` (only if z-index fix needed)

- [ ] **Step 1: Update default identity prompt**

In `src/HydraForge.Application/Chat/ChatSessionService.cs`, replace the `DefaultIdentityPrompt` constant:

```csharp
private const string DefaultIdentityPrompt =
    "You are HydraForge's built-in assistant. HydraForge is a project management tool: " +
    "users organize work into Projects, Boards (columns + cards), Specs, Plans, and Chat sessions.\n\n" +
    "Your capabilities:\n" +
    "- Answer questions about the user's projects using context from the current board, cards, specs, and plans.\n" +
    "- Generate and refine text content (descriptions, specs, plans, notes).\n" +
    "- Analyze and discuss images if your model supports vision.\n" +
    "- Generate images if your model supports image generation (e.g., DALL-E, Stable Diffusion).\n\n" +
    "Your limitations:\n" +
    "- You CANNOT create, modify, or delete projects, cards, boards, or any data. Those require future tool capabilities not yet available.\n" +
    "- You CANNOT access the internet or external services unless specifically configured.\n" +
    "- Be honest about your capabilities — only claim abilities you actually have. If you cannot view images, do not claim you can. If you cannot generate images, do not claim you can.\n\n" +
    "Future planned capabilities include: project creation, card management, deep research, and agent-driven workflows. These are not available yet.\n\n" +
    "Be concise and direct.";
```

- [ ] **Step 2: Add title rename to dock header**

In `ChatDock.vue`, add inline rename state and handler:

```typescript
const isEditingTitle = ref(false)
const editTitle = ref('')

function startEditTitle() {
  editTitle.value = dock.activeSessionId ? 'Chat' : 'New Chat' // fetch real title from session
  isEditingTitle.value = true
}

async function submitTitleEdit() {
  if (!dock.activeSessionId || !editTitle.value.trim()) {
    isEditingTitle.value = false
    return
  }
  try {
    const api = useApi()
    await api.PATCH(ApiRoutes.Chat.sessions.update(dock.activeSessionId), {
      body: { title: editTitle.value.trim() }
    })
    isEditingTitle.value = false
  } catch {
    isEditingTitle.value = false
  }
}
```

In the template, replace the static title with the rename pattern:

```vue
<h2
  v-if="!isEditingTitle"
  class="font-semibold text-sm truncate cursor-pointer hover:text-primary"
  @click="startEditTitle"
>
  {{ dock.activeSessionId ? 'Chat' : 'New Chat' }}
</h2>
<input
  v-else
  v-model="editTitle"
  class="text-sm font-semibold bg-transparent border-b border-primary outline-none w-full"
  @blur="submitTitleEdit"
  @keydown.enter="submitTitleEdit"
  @keydown.escape="isEditingTitle = false"
  v-focus
/>
```

- [ ] **Step 3: Verify z-index in browser (manual check)**

Run the app and open a card modal, then try to click the chat FAB and type in the chat popup. If it works (the popup is above the modal), no CSS change needed. If the modal blocks the popup, add to `src/web-ui/app/assets/css/main.css`:

```css
:root {
  --z-chat-fab: 60;
  --z-chat-popup: 70;
}
```

And verify the ChatDock uses `z-[var(--z-chat-fab)]` and `z-[var(--z-chat-popup)]` (already done in Task 8's template code).

- [ ] **Step 4: Build + typecheck**

Run: `dotnet build && cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add src/HydraForge.Application/Chat/ChatSessionService.cs src/web-ui/app/components/chat/ChatDock.vue src/web-ui/app/pages/chats/index.vue
git commit -m "feat(chat): update identity prompt, add title rename, verify z-index"
```

---

## Task 11: Full verification + manual validation matrix

**Files:**
- Create: `docs/manual-validation/2026-08-05-chat-dock-improvements-matrix.md`

- [ ] **Step 1: Run full .NET verification**

```bash
dotnet build
dotnet test
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

Expected: All build, all tests pass, no pending model changes.

- [ ] **Step 2: Run full Web UI verification**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build && pnpm test
```

Expected: All pass.

- [ ] **Step 3: Write the manual validation matrix**

Create `docs/manual-validation/2026-08-05-chat-dock-improvements-matrix.md`:

```markdown
# Manual Validation Matrix — Slice A.2: Chat Dock Improvements

**Plan:** docs/superpowers/plans/2026-08-05-chat-dock-improvements-design.md
**Spec:** docs/superpowers/specs/2026-08-05-chat-dock-improvements-design.md
**Branch:** task/web-chat-panel

## TC-1: Draft mode — click FAB, no session created
**Steps:**
1. Click the chat FAB.
2. Observe the popup.
**Expected:** Popup opens in draft mode (empty state, no session created server-side). No "New chat" session appears in the history list. No POST to `/api/chat/sessions` was made.

## TC-2: First message creates session
**Steps:**
1. In draft mode, type "Hello" and press Enter.
**Expected:** A new session is created via POST `/api/chat/sessions`. The message is sent. The popup switches to session mode (ChatSessionView renders with the new session).

## TC-3: History panel shows past sessions
**Steps:**
1. Open the chat popup.
2. Click the history button (☰ or chevron).
**Expected:** A scrollable list of past sessions appears. Each item shows title and summary. Clicking a session loads it.

## TC-4: Infinite scroll in history
**Steps:**
1. Have 25+ chat sessions.
2. Open history panel.
3. Scroll to the bottom.
**Expected:** More sessions load automatically (IntersectionObserver triggers `loadMore`). "All caught up" appears when all sessions are loaded.

## TC-5: Session resume on reopen
**Steps:**
1. Open chat, send a message (session created).
2. Close the popup (X or ESC).
3. Reopen the popup.
**Expected:** The same session is loaded (not draft mode). Messages are preserved.

## TC-6: New chat clears to draft
**Steps:**
1. In an active session, click "New chat" (➕).
**Expected:** Popup returns to draft mode. The old session stays active server-side (can be found in history).

## TC-7: Model persistence across refresh
**Steps:**
1. Open a new chat on a board page.
2. Select a specific model from the model picker (e.g., deepseek).
3. Send a message.
4. Refresh the page.
5. Open the chat popup — it resumes the session.
**Expected:** The model picker shows the same model (deepseek) that was selected before the refresh. The server stored `PreferredModelConfigId` on the session.

## TC-8: Correct feature routing on board
**Steps:**
1. On a board page, open the chat popup.
2. Check the model picker's model list.
**Expected:** The model list matches the `ProjectChat` feature's routing config (server routes as `ProjectChat`). Not the `PersonalChat` list.

## TC-9: Card context in new chat
**Steps:**
1. On a board page, open a card modal.
2. Open the chat popup.
3. Click "New chat".
**Expected:** The new session includes `openCardId` set to the open card's ID. F6 implicit close of the prior panel session fires server-side.

## TC-10: Height is half screen
**Steps:**
1. Open the chat popup on a desktop browser.
**Expected:** The popup height is approximately 50% of the viewport height (not the previous fixed 560px).

## TC-11: Auto-title fallback
**Steps:**
1. Open a new chat, send a message.
2. Wait for the reply.
**Expected:** The session gets a title. If the LLM title generator fails (slow local model), the title falls back to the first user message (truncated to 60 chars). The title appears in the dock header and history list.

## TC-12: /chats page infinite scroll
**Steps:**
1. Navigate to `/chats`.
2. Scroll the sidebar session list to the bottom.
**Expected:** More sessions load automatically. Same cursor-paginated behavior as the dock history panel.

## TC-13: /chats page resumes dock session
**Steps:**
1. Open the chat popup, send a message (active session).
2. Click "Chats" in the left navigation.
**Expected:** The `/chats` page opens with the same session loaded in the right panel. The session is highlighted in the sidebar.

## TC-14: Identity prompt is model-honest
**Steps:**
1. Open a new chat.
2. Ask "What can you do?"
**Expected:** The model describes its actual capabilities. It does NOT claim it can create projects. It mentions future planned capabilities. It says "be honest about your capabilities" — a text-only model should not claim vision/image generation.

## TC-15: Title rename in dock
**Steps:**
1. Open a chat session in the popup.
2. Click the title in the header.
3. Type a new title and press Enter.
**Expected:** The title updates via PATCH. The new title appears in the header and in the history list.

## TC-16: Z-index (verify in browser)
**Steps:**
1. On a board page, open a card modal.
2. Open the chat popup (click FAB).
**Expected:** The chat popup is visible and usable above the card modal. You can type in the chat while the card modal remains open behind it.
```

- [ ] **Step 4: Commit**

```bash
git add docs/manual-validation/2026-08-05-chat-dock-improvements-matrix.md
git commit -m "docs: manual validation matrix for Slice A.2 (chat dock improvements)"
```

---

## Self-Review Notes

**Spec coverage:**
- A (draft mode + history + resume): Tasks 8, 9 ✓
- B (useChatSessionList composable): Task 7 ✓
- C (/chats infinite scroll): Task 9 ✓
- D (auto-title investigation + fallback): Task 4 ✓
- E (per-session model persistence): Tasks 2, 3, 6 ✓
- F (card context): Task 5 ✓
- G (z-index verify): Task 10 ✓
- H (height): Task 8 ✓
- I (identity prompt): Task 10 ✓
- J (title rename): Task 10 ✓
- Pagination cursor fix: Task 1 ✓
- Verification + matrix: Task 11 ✓

**Placeholder scan:** No TBD/TODO. All steps have concrete code.

**Drift review (2026-08-05):** Plan checked against actual current code (not the spec's prose) before execution. Corrections applied: `AiEditMode` is the `AiEditMode` enum, not `bool?` (Task 2); `UpdateSettings` currently guards `Status != Active` — preserved, an earlier draft dropped it (Task 2); `ChatSessionPageDto` already exists, `ListAsync` already returns `Result<ChatSessionPageDto>` — preserved the wrapper, an earlier draft unwrapped it (Task 1); no `ListSessionsQuery` DTO exists — `beforeId` threads through as an individual param at every layer like its siblings, not a new record (Task 1); `useApi().GET` takes a single URL-string arg only, no `{ params }` object — `useChatSessionList` builds the full URL via `ApiRoutes` instead, mirroring the existing `messages` cursor route (Task 7, plus a `routes.ts` edit Task 1 originally omitted); `ChatInput`'s existing `v-model="selectedModelId" v-model:effort="selectedEffort"` bindings to `ChatModelPicker` were being silently dropped by the new template snippet — restored alongside the new props (Task 6); `initialModelId`/`initialEffort` are already-existing props on `ChatSessionView`, not new fields, and can't be reassigned as if they were local refs — replaced with a `resolvedInitialModelId`/`resolvedInitialEffort` computed that layers the session's persisted value under an explicitly-passed prop (Task 6); the board-store try/catch around `useBoardStore()` was defending against a failure mode that doesn't exist (Pinia stores always resolve) — simplified (Tasks 5, 8); Task 8's full `chatDock.ts` rewrite changes `startNewChat`'s POST body shape without updating the 2 existing `chatDock.test.ts` tests that assert the old shape — added as an explicit step; Task 9's `/chats` rewrite didn't mention preserving `pendingMessage`/`syncSession`/`archiveTargetId`, unrelated existing state in that file — flagged to preserve.

**Type consistency:** `PreferredModelConfigId` (Guid?) and `PreferredEffort` (string?) consistent across Tasks 2, 3, 6. `UpdateSettings` widened to 7 params consistently in Tasks 2, 3, 4. `useChatSessionList` return shape consistent across Tasks 7, 8, 9. `openCardId` field name consistent across Tasks 5, 8.