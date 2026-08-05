# Slice A: Global Floating Chat Widget + Hydra Identity Prompt — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a global floating draggable chat widget (`ChatDock`) usable app-wide (even over modals), plus a Hydra identity system prompt persisted per-conversation (hidden from UI) and admin-configurable via System Settings.

**Architecture:** Frontend: a Pinia store (`chatDock.ts`) owns the sticky session + window open/position state, surviving route changes. `ChatDock.vue` renders a FAB + draggable popup (via `@vueuse/core` `useDraggable`), reusing `ChatSessionView` from Plan 17. Rendered once in `default.vue` layout, hidden on `/chats` routes. Backend: `ChatSessionService.CreateAsync` persists a `System`-role `ChatMessage` with the identity prompt on every new session (regular + forked paths), reading from `ISettingsProvider` (admin-configurable, null → hardcoded default). `ChatSessionView` filters `System`-role messages from the rendered list. Plan 18's `ChatPanel.vue` + board.vue chat integration are deleted/reverted.

**Tech Stack:** Nuxt 4 / Vue 3 / Pinia / `@vueuse/core` 14.4.0 (`useDraggable`) / Nuxt UI v4 / ASP.NET Core 10 / EF Core 10 / xUnit / NSubstitute.

**Spec:** `docs/superpowers/specs/2026-08-04-slice-a-global-chat-dock-design.md`

**Branch:** `task/web-chat-panel` (already checked out — confirm with `git branch --show-current` before starting)

---

## File Structure

**Create:**
- `src/web-ui/app/stores/chatDock.ts` — Pinia store: sticky session, open/close, window position, page-context detection.
- `src/web-ui/app/components/chat/ChatDock.vue` — FAB + draggable popup window, hosts `ChatSessionView`.
- `src/web-ui/app/stores/__tests__/chatDock.test.ts` — store unit tests.
- `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts` — component tests.
- `docs/manual-validation/2026-08-04-slice-a-global-chat-dock-matrix.md` — manual validation matrix.

**Modify:**
- `src/web-ui/app/layouts/default.vue` — render `<ChatDock />` once.
- `src/web-ui/app/components/chat/ChatSessionView.vue` — filter `System`-role messages from rendered list.
- `src/web-ui/app/pages/projects/[id]/board.vue` — revert Plan 18 chat integration (remove `ChatPanel` import, `showChatPanel`, header button, `<ChatPanel>` render).
- `src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs` — add `AiIdentityPrompt` property + `SetAiIdentityPrompt` method.
- `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` — map new column (snake_case `ai_identity_prompt`, `.HasColumnType("text")`).
- `src/HydraForge.Infrastructure/Migrations/<timestamp>_AddAiIdentityPrompt.cs` — EF migration.
- `src/HydraForge.Application/Chat/ChatSessionService.cs` — inject `ISettingsProvider`, persist identity `System` message on creation (both regular + forked paths).
- `src/HydraForge.Application/Chat/ChatReplyGenerator.cs` — remove per-reply identity injection (personality stays per-reply; identity now flows from history).
- `src/HydraForge.Server/Controllers/Admin/AdminController.cs` — add `AiIdentityPrompt` to GET response + PUT request record.
- `src/web-ui/app/pages/admin/settings.vue` — add AI Identity Prompt editor card.
- `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs` — assert identity System message persisted on creation.
- `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs` — update constructor if signature changes (personality injection stays; no identity assertion needed since it's in history now).
- `tests/HydraForge.Server.Tests/` — admin settings round-trip for `AiIdentityPrompt` (if a factory exists; otherwise skip — admin controller is thin).

**Delete:**
- `src/web-ui/app/components/chat/ChatPanel.vue`

---

## Task 1: Add `AiIdentityPrompt` to `SystemSettings` entity

**Files:**
- Modify: `src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs`
- Test: `tests/HydraForge.Domain.Tests/` (existing entity tests if any; otherwise skip — entity is a POCO, no behavior to test beyond the setter)

- [ ] **Step 1: Add the property and setter method**

In `src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs`, add the property after `HousekeepingRunTimeUtc` (line 16) and a setter method after `SetHousekeepingRunTime` (line 60):

```csharp
    public string? AiIdentityPrompt { get; set; }
```

```csharp
    public void SetAiIdentityPrompt(string? value)
    {
        AiIdentityPrompt = string.IsNullOrWhiteSpace(value) ? null : value;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: BUILD SUCCEEDED (no tests yet — property is a POCO field, tested via migration + service tests later)

- [ ] **Step 3: Commit**

```bash
git add src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs
git commit -m "feat(settings): add AiIdentityPrompt to SystemSettings"
```

---

## Task 2: Map `AiIdentityPrompt` in DbContext + create migration

**Files:**
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs:503-520` (SystemSettings seed block)
- Create: `src/HydraForge.Infrastructure/Migrations/<timestamp>_AddAiIdentityPrompt.cs`

- [ ] **Step 1: Update the seed data to include the new column**

In `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`, the `SystemSettings` seed block (around line 508-518) creates a `new SystemSettings { ... }`. The new `AiIdentityPrompt` property defaults to `null` (hardcoded fallback used when null), so no seed value is needed — but verify the column maps correctly. The `ConfigureEntity<SystemSettings>` call uses the default property mapping. Since `AiIdentityPrompt` is `string?`, Npgsql would default to `nvarchar(max)` — per repo convention (AGENTS.md "Database And Migrations"), chain `.HasColumnType("text")`.

Find the `ConfigureEntity<SystemSettings>` block and confirm there's a property-level config section. If the block only has `HasData`, add a property config. The block currently looks like:

```csharp
        ConfigureEntity<SystemSettings>(
            modelBuilder,
            "system_settings",
            b =>
            {
                b.HasData(
                    new SystemSettings
                    {
                        Id = Domain.Entities.PersonalSpace.SystemSettings.SingletonId,
                        ArchivedItemRetentionDays = 730,
                        AuditLogRetentionDays = 90,
                        NotificationRetentionDays = 30,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    }
                );
            }
        );
```

Update it to add a property configuration before `HasData`:

```csharp
        ConfigureEntity<SystemSettings>(
            modelBuilder,
            "system_settings",
            b =>
            {
                b.Property(s => s.AiIdentityPrompt).HasColumnType("text");
                b.HasData(
                    new SystemSettings
                    {
                        Id = Domain.Entities.PersonalSpace.SystemSettings.SingletonId,
                        ArchivedItemRetentionDays = 730,
                        AuditLogRetentionDays = 90,
                        NotificationRetentionDays = 30,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    }
                );
            }
        );
```

- [ ] **Step 2: Generate the migration**

Run:
```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations add AddAiIdentityPrompt --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```
Expected: Migration file created under `src/HydraForge.Infrastructure/Migrations/`. Verify the `Up` method adds an `ai_identity_prompt` column of type `text` (nullable) to the `system_settings` table.

- [ ] **Step 3: Verify model is clean (no pending changes)**

Run:
```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```
Expected: "No pending model changes." (If it reports pending changes, the migration missed something — fix before continuing.)

- [ ] **Step 4: Build to verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
git add src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs src/HydraForge.Infrastructure/Migrations/
git commit -m "feat(settings): map AiIdentityPrompt + migration"
```

---

## Task 3: Expose `AiIdentityPrompt` on the admin settings API

**Files:**
- Modify: `src/HydraForge.Server/Controllers/Admin/AdminController.cs:159-178` (GET settings) and `:180-248` (PUT settings) and `:279-289` (request record)

- [ ] **Step 1: Add `AiIdentityPrompt` to the GET response**

In `src/HydraForge.Server/Controllers/Admin/AdminController.cs`, the `GetSettings` method (around line 159-178) returns an anonymous object. Add `AiIdentityPrompt` to it:

```csharp
        return Ok(
            new
            {
                settings.ArchivedItemRetentionDays,
                settings.AuditLogRetentionDays,
                settings.NotificationRetentionDays,
                settings.NtfyServerUrl,
                settings.SearXngUrl,
                settings.BrandName,
                settings.BrandLogoUrl,
                settings.AiNarrativeGenerationTimeUtc,
                settings.HousekeepingRunTimeUtc,
                settings.AiIdentityPrompt,
            }
        );
```

- [ ] **Step 2: Add `AiIdentityPrompt` to the `UpdateSystemSettingsRequest` record**

At the bottom of the same file (around line 279-289), add the field:

```csharp
public record UpdateSystemSettingsRequest(
    int? ArchivedItemRetentionDays,
    int? AuditLogRetentionDays,
    int? NotificationRetentionDays,
    string? NtfyServerUrl,
    string? SearXngUrl,
    string? BrandName,
    string? BrandLogoUrl,
    TimeSpan? AiNarrativeGenerationTimeUtc,
    TimeSpan? HousekeepingRunTimeUtc,
    string? AiIdentityPrompt
);
```

- [ ] **Step 3: Handle `AiIdentityPrompt` in the PUT handler**

In the `UpdateSettings` method (around line 180-248), after the existing `hasHousekeepingTime` check (line 221-224), add a similar presence check for `aiIdentityPrompt`:

```csharp
        bool hasAiIdentityPrompt;
        try
        {
            using var jsonDoc = JsonDocument.Parse(body);
            hasAiNarrativeTime = jsonDoc.RootElement.TryGetProperty(
                "aiNarrativeGenerationTimeUtc",
                out _
            );
            hasHousekeepingTime = jsonDoc.RootElement.TryGetProperty(
                "housekeepingRunTimeUtc",
                out _
            );
            hasAiIdentityPrompt = jsonDoc.RootElement.TryGetProperty(
                "aiIdentityPrompt",
                out _
            );
        }
```

Then after the `SetHousekeepingRunTime` call (line 244), add:

```csharp
        if (hasAiIdentityPrompt)
            settings.SetAiIdentityPrompt(request.AiIdentityPrompt);
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
git add src/HydraForge.Server/Controllers/Admin/AdminController.cs
git commit -m "feat(admin): expose AiIdentityPrompt on settings API"
```

---

## Task 4: Persist identity `System` message on session creation

**Files:**
- Modify: `src/HydraForge.Application/Chat/ChatSessionService.cs` (constructor + `CreateAsync` + `CreateForkedAsync`)
- Test: `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`

This is the core backend change. `ChatSessionService` gets `ISettingsProvider` injected, reads `AiIdentityPrompt` (null → hardcoded default), and persists a `System`-role `ChatMessage` on every new session (both regular and forked paths).

- [ ] **Step 1: Write the failing test**

In `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`, add a test that asserts a `System`-role message is persisted on session creation. The test fakes need an `ISettingsProvider` stub. First, check the existing test constructor — find where `ChatSessionService` is instantiated in the test file and add the `ISettingsProvider` stub.

Add a field and stub setup near the other fakes (top of the test class or in a helper). The `ISettingsProvider` interface (from `src/HydraForge.Application/Settings/ISettingsProvider.cs`):
```csharp
public interface ISettingsProvider
{
    Task<SystemSettings> GetAsync(CancellationToken ct = default);
    void Invalidate();
}
```

Add this test method (adapt the existing `CreateAsync_NoProjectNoFork_CreatesPersonalSession` test pattern — find it around line 931 and mirror its setup):

```csharp
    [Fact]
    public async Task CreateAsync_PersistsIdentitySystemMessage()
    {
        var fakes = new TestFakes();
        fakes.SettingsProvider
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SystemSettings { AiIdentityPrompt = null });

        var service = fakes.BuildService();
        var request = new CreateChatSessionRequest(Title: "test", FolderId: null, ProjectId: null, OpenCardId: null, PersonalityId: null, AiEditMode: null, SearchAllMyDocs: false, ForkedFromSessionId: null);

        var result = await service.CreateAsync(request, NewId());

        Assert.True(result.IsSuccess);
        // The System message should be the first persisted message
        var systemMessage = fakes.Messages.FirstOrDefault(m => m.Role == MessageRole.System);
        Assert.NotNull(systemMessage);
        Assert.Contains("HydraForge", systemMessage!.Content);
    }
```

Note: the exact `TestFakes` structure depends on the existing test file. Read `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs` to find the existing fake-collection pattern (look for a `TestFakes` class or similar, and where `ChatSessionService` is constructed). Add `SettingsProvider` to that collection. The `Messages` list should be the fake `IChatMessageRepository`'s captured messages — find the existing fake message repo and expose its captured list.

If the test file uses a different pattern (e.g. inline fakes per test), follow that pattern. The key assertion: after `CreateAsync`, a `System`-role `ChatMessage` with content containing "HydraForge" was added to the message repo.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~CreateAsync_PersistsIdentitySystemMessage`
Expected: FAIL — `ChatSessionService` constructor doesn't accept `ISettingsProvider` yet, or no System message is persisted.

- [ ] **Step 3: Inject `ISettingsProvider` into `ChatSessionService`**

In `src/HydraForge.Application/Chat/ChatSessionService.cs`, add `ISettingsProvider` to the primary constructor (line 13-25) and store it:

```csharp
public class ChatSessionService(
    IChatSessionRepository sessionRepo,
    IChatMessageRepository messageRepo,
    IChatSessionDocumentRepository sessionDocRepo,
    ICardRepository cardRepo,
    IUserRepository userRepo,
    IProjectMemberRepository memberRepo,
    IAgentPersonalityRepository personalityRepo,
    IDocumentRepository documentRepo,
    IChatSummaryGenerator summaryGenerator,
    IBackgroundTaskQueue backgroundTaskQueue,
    ISettingsProvider settingsProvider,
    ILogger<ChatSessionService> logger
) : IChatSessionService
```

Add the field assignment in the body (after line 37):
```csharp
    private readonly ISettingsProvider _settingsProvider = settingsProvider;
```

Add the `using` at the top:
```csharp
using HydraForge.Application.Settings;
```

- [ ] **Step 4: Add the identity prompt constant + helper**

Add a hardcoded default constant near the top of the class (after the field declarations, around line 38):

```csharp
    private const string DefaultIdentityPrompt =
        "You are HydraForge's built-in assistant. HydraForge is a project management tool: " +
        "users organize work into Projects, Boards (columns + cards), Specs, Plans, and Chat " +
        "sessions. You help users think through their work, draft content, and answer questions " +
        "about their projects. Be concise and direct. If a user asks about something outside " +
        "HydraForge's scope, say so.";
```

Add a private helper method (after `CreateAsync`, before `CreateForkedAsync`):

```csharp
    private async Task PersistIdentityMessageAsync(ChatSession session, CancellationToken ct)
    {
        var settings = await _settingsProvider.GetAsync(ct);
        var prompt = string.IsNullOrWhiteSpace(settings.AiIdentityPrompt)
            ? DefaultIdentityPrompt
            : settings.AiIdentityPrompt;

        var identityMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.System,
            Content = prompt,
            CreatedAt = DateTime.UtcNow,
        };
        await _messageRepo.AddAsync(identityMessage, ct);
    }
```

- [ ] **Step 5: Call the helper in `CreateAsync` (regular path)**

In `CreateAsync`, after `await _sessionRepo.AddAsync(session, ct);` (line 124), add:

```csharp
        await _sessionRepo.AddAsync(session, ct);
        await PersistIdentityMessageAsync(session, ct);
```

- [ ] **Step 6: Call the helper in `CreateForkedAsync` (forked path)**

In `CreateForkedAsync`, after `await _sessionRepo.AddAsync(forked, ct);` (line 192), add:

```csharp
        await _sessionRepo.AddAsync(forked, ct);
        await PersistIdentityMessageAsync(forked, ct);
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~CreateAsync_PersistsIdentitySystemMessage`
Expected: PASS

- [ ] **Step 8: Run the full ChatSessionService test suite to check for regressions**

Run: `dotnet test --filter FullyQualifiedName~ChatSessionServiceTests`
Expected: All PASS. If any existing test fails because the constructor signature changed (missing `ISettingsProvider` stub), update the test fakes to provide an `ISettingsProvider` stub returning a default `SystemSettings`.

- [ ] **Step 9: Commit**

```bash
git add src/HydraForge.Application/Chat/ChatSessionService.cs tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs
git commit -m "feat(chat): persist Hydra identity System message on session creation"
```

---

## Task 5: Verify `ChatReplyGenerator` flows identity from history (no per-reply injection needed)

**Files:**
- Test: `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs`

The identity prompt is now a `System`-role message in history. `ChatReplyGenerator` already maps all history messages (including `System` role — see line 209-210 of `ChatReplyGenerator.cs`) into the LLM call. So no code change is needed in `ChatReplyGenerator` — the identity flows naturally from history. We just need a test confirming a `System` message in history reaches the LLM call.

- [ ] **Step 1: Write the test**

In `tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs`, add a test that seeds history with a `System`-role message and asserts it appears in the `ChatRequest.Messages` passed to the LLM client. Find an existing happy-path test (one that reaches `StreamChatAsync`) and mirror its setup. The fake `ILlmClient` captures the `ChatRequest` — find how existing tests inspect it.

```csharp
    [Fact]
    public async Task GenerateAsync_SystemMessageInHistory_IsIncludedInLlmCall()
    {
        var session = new ChatSession
        {
            Id = SessionId,
            OwnerId = UserId,
            Status = ChatSessionStatus.Active,
        };
        var systemMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = SessionId,
            Role = MessageRole.System,
            Content = "You are HydraForge's assistant.",
        };
        var userMessage = new ChatMessage
        {
            Id = MessageId,
            SessionId = SessionId,
            Role = MessageRole.User,
            Content = "hello",
        };
        _sessionRepo.GetByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(session);
        _messageRepo.GetByIdAsync(MessageId, Arg.Any<CancellationToken>()).Returns(userMessage);
        _messageRepo
            .GetBySessionAsync(SessionId, Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessage> { systemMessage, userMessage });

        // Set up a minimal successful stream (mirror an existing happy-path test)
        var capturedRequests = new List<ChatRequest>();
        var fakeClient = Substitute.For<ILlmClient>();
        fakeClient.SupportsToolCalling(Arg.Any<ProviderModelConfigDto>()).Returns(false);
        fakeClient
            .StreamChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(async c =>
            {
                capturedRequests.Add(c.Arg<ChatRequest>());
                await Task.CompletedTask;
                yield return new ChatChunk(Delta: "ok", FinishReason: ChatChunkFinishReason.Stop);
            });
        _llmClientFactory.For(Arg.Any<LlmProvider>()).Returns(fakeClient);
        // Wire up a valid route (mirror existing happy-path test setup)

        await _generator.GenerateAsync(SessionId, MessageId, UserId, null, null);

        Assert.NotEmpty(capturedRequests);
        Assert.Contains(capturedRequests[0].Messages, m => m.Role == ChatRole.System && m.Content.Contains("HydraForge"));
    }
```

Note: the exact setup for a happy-path test (route resolution, budget guard, stream registry) is non-trivial. Read an existing passing test in `ChatReplyGeneratorTests.cs` that reaches `StreamChatAsync` (search for `StreamChatAsync` calls or `FinishReason.Stop` in the test file) and copy its setup. The key assertion is that the captured `ChatRequest.Messages` includes a `System`-role message with "HydraForge" content.

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~GenerateAsync_SystemMessageInHistory_IsIncludedInLlmCall`
Expected: PASS (no code change needed — `ChatReplyGenerator` already maps `System` role from history at line 209-210).

- [ ] **Step 3: Commit**

```bash
git add tests/HydraForge.Application.Tests/Chat/ChatReplyGeneratorTests.cs
git commit -m "test(chat): verify identity System message flows from history to LLM call"
```

---

## Task 6: Filter `System`-role messages from the rendered chat list

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue:136-152` (fetchSession)
- Test: `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts` (existing test file)

The identity `System` message is persisted in the DB and returned by `GET /api/chat/sessions/{id}`. It must NOT render in the UI. Filter `role === 'System'` from the messages array after fetch.

- [ ] **Step 1: Write the failing test**

In `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts`, add a test that asserts `System`-role messages are not rendered. Read the existing test file first to follow its mount + mock pattern. The test should:
1. Mock `useApi().GET` to return a `ChatSessionDetailDto` with one `System` message and one `User` message.
2. Mount `ChatSessionView`.
3. Assert the `System` message content does NOT appear in the rendered output, and the `User` message DOES.

```typescript
it('filters System-role messages from the rendered list', async () => {
  // Follow the existing mount pattern in this test file — stub useApi,
  // useChatStream, useAppToast as the other tests do.
  // Mock GET /api/chat/sessions/{id} to return:
  //   messages: [
  //     { id: 'sys', role: 'System', content: 'You are HydraForge assistant.', ... },
  //     { id: 'u1', role: 'User', content: 'hello', ... }
  //   ]
  // Assert: 'You are HydraForge assistant.' is NOT in document.
  // Assert: 'hello' IS in document.
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/web-ui && pnpm test -- ChatSessionView`
Expected: FAIL — System message currently renders (it's not filtered).

- [ ] **Step 3: Add the filter in `fetchSession`**

In `src/web-ui/app/components/chat/ChatSessionView.vue`, the `fetchSession` function (around line 136-152) assigns `session.value` then sorts. Add a filter after the assignment, before the sort. Change:

```typescript
    session.value = data as ChatSessionDetailDto
    // API returns newest-first; sort chronologically for display
    session.value.messages.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
```

to:

```typescript
    session.value = data as ChatSessionDetailDto
    // Hide System-role messages (e.g. the Hydra identity prompt) from the UI —
    // they flow to the LLM via history but aren't for the user to read.
    session.value.messages = session.value.messages.filter(m => m.role !== 'System')
    // API returns newest-first; sort chronologically for display
    session.value.messages.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/web-ui && pnpm test -- ChatSessionView`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/chat/ChatSessionView.vue src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts
git commit -m "fix(chat): hide System-role messages from chat UI"
```

---

## Task 7: Create the `chatDock` Pinia store

**Files:**
- Create: `src/web-ui/app/stores/chatDock.ts`
- Test: `src/web-ui/app/stores/__tests__/chatDock.test.ts`

The store owns the sticky session (`activeSessionId`), window open state (`isOpen`), creation state (`isCreating`), and window position (`position: { x, y }`). It detects page context (projectId from board route) and creates sessions via the API.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/stores/__tests__/chatDock.test.ts`. Test the store's core logic: initial state, `toggleDock`, `openDock`/`closeDock`, `startNewChat` creates a session with the right projectId context, page-context detection.

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useChatDockStore } from '~/stores/chatDock'

// Mock useApi and useRoute — follow the existing store test pattern in
// src/web-ui/app/stores/__tests__/ (read one for the mocking convention).

describe('chatDock store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts closed with no active session', () => {
    const store = useChatDockStore()
    expect(store.isOpen).toBe(false)
    expect(store.activeSessionId).toBe(null)
  })

  it('toggleDock flips isOpen', () => {
    const store = useChatDockStore()
    expect(store.isOpen).toBe(false)
    store.toggleDock()
    expect(store.isOpen).toBe(true)
    store.toggleDock()
    expect(store.isOpen).toBe(false)
  })

  it('closeDock sets isOpen false', () => {
    const store = useChatDockStore()
    store.isOpen = true
    store.closeDock()
    expect(store.isOpen).toBe(false)
  })

  it('detects projectId from board route', () => {
    // Mock useRoute to return { path: '/projects/abc/board', params: { id: 'abc' } }
    const store = useChatDockStore()
    expect(store.currentProjectId).toBe('abc')
  })

  it('returns null projectId when not on a board route', () => {
    // Mock useRoute to return { path: '/projects', params: {} }
    const store = useChatDockStore()
    expect(store.currentProjectId).toBe(null)
  })

  it('startNewChat creates a session with projectId when on board', async () => {
    // Mock useApi().POST to return { id: 'new-session' }
    // Mock useRoute to be on /projects/abc/board
    const store = useChatDockStore()
    await store.startNewChat()
    expect(store.activeSessionId).toBe('new-session')
    // Assert POST was called with body containing projectId: 'abc'
  })

  it('startNewChat creates a session without projectId when not on board', async () => {
    // Mock useApi().POST to return { id: 'new-session' }
    // Mock useRoute to be on /projects
    const store = useChatDockStore()
    await store.startNewChat()
    expect(store.activeSessionId).toBe('new-session')
    // Assert POST was called with body NOT containing projectId
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/web-ui && pnpm test -- chatDock`
Expected: FAIL — store module doesn't exist.

- [ ] **Step 3: Implement the store**

Create `src/web-ui/app/stores/chatDock.ts`:

```typescript
import { defineStore } from 'pinia'
import { ApiError } from '~/lib/api-error'
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'

export const useChatDockStore = defineStore('chatDock', () => {
  const isOpen = ref(false)
  const activeSessionId = ref<string | null>(null)
  const isCreating = ref(false)
  const position = ref({ x: 0, y: 0 }) // default set by ChatDock on mount

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
  }

  function closeDock() {
    isOpen.value = false
  }

  async function startNewChat() {
    if (isCreating.value) return
    isCreating.value = true
    try {
      const body: Record<string, unknown> = { title: 'New chat' }
      if (currentProjectId.value) body.projectId = currentProjectId.value
      const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), { body })
      if (data) {
        activeSessionId.value = data.id
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to create chat session')
    } finally {
      isCreating.value = false
    }
  }

  return {
    isOpen,
    activeSessionId,
    isCreating,
    position,
    currentProjectId,
    toggleDock,
    openDock,
    closeDock,
    startNewChat
  }
})
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/web-ui && pnpm test -- chatDock`
Expected: PASS. If `useRoute`/`useApi`/`useAppToast` auto-imports don't resolve in the test, stub them per the existing store test convention (check `src/web-ui/app/stores/__tests__/` for the mocking pattern).

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/stores/chatDock.ts src/web-ui/app/stores/__tests__/chatDock.test.ts
git commit -m "feat(chat): add chatDock Pinia store for global chat widget"
```

---

## Task 8: Create the `ChatDock.vue` component

**Files:**
- Create: `src/web-ui/app/components/chat/ChatDock.vue`
- Test: `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`

The component renders a FAB (collapsed) + draggable popup (expanded), reusing `ChatSessionView`. Hidden on `/chats` routes. Uses `@vueuse/core` `useDraggable` for the popup.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`. Test: FAB renders, clicking FAB opens the popup, popup contains `ChatSessionView` when a session is active, dock is hidden on `/chats` route.

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ChatDock from '~/components/chat/ChatDock.vue'

// Stub ChatSessionView to avoid mounting the full session view (it has its
// own tests). Stub useChatDockStore, useRoute, useApi per existing component
// test patterns in src/web-ui/app/components/chat/__tests__/.

describe('ChatDock', () => {
  it('renders the FAB when closed', async () => {
    // Mount with store.isOpen = false
    // Assert: a button with chat icon is in the document
  })

  it('opens the popup when FAB is clicked', async () => {
    // Mount with store.isOpen = false
    // Click the FAB
    // Assert: store.toggleDock was called (or isOpen becomes true)
  })

  it('renders ChatSessionView when a session is active', async () => {
    // Mount with store.isOpen = true, store.activeSessionId = 'abc'
    // Assert: ChatSessionView stub receives session-id='abc'
  })

  it('shows empty state when no session is active', async () => {
    // Mount with store.isOpen = true, store.activeSessionId = null
    // Assert: "New chat" button or empty-state text is visible
  })

  it('is hidden on /chats route', async () => {
    // Mock useRoute to return { path: '/chats' }
    // Assert: component renders nothing (v-if false)
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/web-ui && pnpm test -- ChatDock`
Expected: FAIL — component doesn't exist.

- [ ] **Step 3: Implement the component**

Create `src/web-ui/app/components/chat/ChatDock.vue`:

```vue
<script setup lang="ts">
import { useDraggable } from '@vueuse/core'
import { useChatDockStore } from '~/stores/chatDock'
import ChatSessionView from '~/components/chat/ChatSessionView.vue'

const dock = useChatDockStore()
const route = useRoute()

// Hidden on /chats pages (chat-in-chat avoidance)
const isHidden = computed(() => route.path.startsWith('/chats'))

// Drag handle ref — the header element drives the popup position
const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)

// Default position: bottom-right, above the FAB. useDraggable tracks
// the popup's position via the drag handle.
const { x, y, isDragging } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: { x: typeof window !== 'undefined' ? window.innerWidth - 400 : 0, y: typeof window !== 'undefined' ? window.innerHeight - 600 : 0 },
  preventDefault: true
})

// Reset to default position when opening (if never dragged)
watch(() => dock.isOpen, (open) => {
  if (open && popupRef.value && !isDragging.value) {
    // Keep default bottom-right; useDraggable manages x/y
  }
})

// ESC closes the popup
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

// First open: auto-create a session if none active
watch(() => dock.isOpen, async (open) => {
  if (open && !dock.activeSessionId && !dock.isCreating) {
    await dock.startNewChat()
  }
})
</script>

<template>
  <ClientOnly>
    <template v-if="!isHidden">
      <!-- FAB (collapsed) -->
      <UButton
        v-if="!dock.isOpen"
        icon="i-lucide-messages-square"
        size="lg"
        color="primary"
        rounded="full"
        class="fixed bottom-4 right-4 z-40 shadow-lg"
        title="Open chat"
        aria-label="Open chat"
        @click="dock.toggleDock()"
      />

      <!-- Popup window (expanded) -->
      <div
        v-if="dock.isOpen"
        ref="popupRef"
        class="fixed z-50 w-[380px] h-[560px] max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
        :style="{ left: `${x}px`, top: `${y}px` }"
      >
        <!-- Header (drag handle) -->
        <div
          ref="dragHandle"
          class="shrink-0 flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700 cursor-move select-none"
        >
          <h2 class="font-semibold text-sm truncate">
            {{ dock.currentProjectId ? 'Project Chat' : 'Chat' }}
          </h2>
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-plus"
              variant="ghost"
              size="xs"
              title="New chat"
              :disabled="dock.isCreating"
              @click="dock.startNewChat()"
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
          <ChatSessionView
            v-if="dock.activeSessionId"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
          />
          <div
            v-else
            class="flex-1 flex flex-col items-center justify-center gap-3 p-4"
          >
            <p class="text-sm text-muted text-center">
              Start a conversation.
            </p>
            <UButton
              size="sm"
              icon="i-lucide-plus"
              :disabled="dock.isCreating"
              @click="dock.startNewChat()"
            >
              New chat
            </UButton>
          </div>
        </div>
      </div>
    </template>
  </ClientOnly>
</template>
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/web-ui && pnpm test -- ChatDock`
Expected: PASS. Adjust stubs as needed — `ChatSessionView`, `useChatDockStore`, `useRoute` must be stubbed per the existing component test convention.

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/chat/ChatDock.vue src/web-ui/app/components/chat/__tests__/ChatDock.test.ts
git commit -m "feat(chat): add ChatDock global floating chat widget"
```

---

## Task 9: Render `ChatDock` in the default layout

**Files:**
- Modify: `src/web-ui/app/layouts/default.vue:49-79` (template)

- [ ] **Step 1: Add the import and render**

In `src/web-ui/app/layouts/default.vue`, add the import in the `<script setup>` block (after line 4):

```typescript
import ChatDock from '~/components/chat/ChatDock.vue'
```

In the `<template>`, add `<ChatDock />` inside `<UApp>` but after the `<UDashboardGroup>` (so it floats above everything). Place it right before the closing `</UApp>` tag (after the `<ClientOnly>` block, around line 78):

```vue
    <ClientOnly>
      <SessionExpiryModal
        :open="showSessionModal"
        :expired="isExpired"
        :time-remaining="timeRemaining"
        :remaining-formatted="remainingFormatted"
        :extending="isExtending"
        @extend="handleExtend"
        @logout="handleSessionLogout"
      />
    </ClientOnly>

    <ChatDock />
  </UApp>
```

- [ ] **Step 2: Verify typecheck + lint + build**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
Expected: All pass.

- [ ] **Step 3: Commit**

```bash
git add src/web-ui/app/layouts/default.vue
git commit -m "feat(chat): render ChatDock in default layout"
```

---

## Task 10: Revert Plan 18 `ChatPanel` integration from `board.vue`

**Files:**
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue`
- Delete: `src/web-ui/app/components/chat/ChatPanel.vue`

Remove the side-rail `ChatPanel` and all its wiring from the board page. The global `ChatDock` (layout-level) replaces it.

- [ ] **Step 1: Remove the `ChatPanel` import**

In `src/web-ui/app/pages/projects/[id]/board.vue`, remove line 11:
```typescript
import ChatPanel from '~/components/chat/ChatPanel.vue'
```

- [ ] **Step 2: Remove `showChatPanel` ref**

Remove line 43:
```typescript
const showChatPanel = ref(false)
```

- [ ] **Step 3: Remove `showChatPanel` from the `selectedCardId` watcher**

In the `watch(selectedCardId, ...)` block (around line 232-239), remove `showChatPanel.value = true`:

```typescript
watch(selectedCardId, (cardId) => {
  if (cardId) {
    presence.focusCard(projectId, cardId)
  } else {
    presence.unfocusCard(projectId)
  }
})
```

- [ ] **Step 4: Remove the chat toggle button from the board header**

Remove the `i-lucide-message-square` button (around line 353-359):
```vue
        <UButton
          variant="ghost"
          size="sm"
          icon="i-lucide-message-square"
          title="Chat"
          @click="showChatPanel = !showChatPanel"
        />
```

- [ ] **Step 5: Remove the `<ChatPanel>` render block**

Remove the block at the bottom of the template (around line 523-529):
```vue
    <ChatPanel
      v-if="showChatPanel"
      :project-id="projectId"
      :card-id="selectedCard?.id"
      :card-number="selectedCard?.cardNumber"
      :card-title="selectedCard?.title"
    />
```

- [ ] **Step 6: Delete `ChatPanel.vue`**

```bash
git rm src/web-ui/app/components/chat/ChatPanel.vue
```

- [ ] **Step 7: Verify typecheck + lint + build**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
Expected: All pass (no dangling references to `ChatPanel` or `showChatPanel`).

- [ ] **Step 8: Commit**

```bash
git add src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "refactor(chat): remove Plan 18 ChatPanel, replaced by global ChatDock"
```

---

## Task 11: Add AI Identity Prompt editor to the admin settings page

**Files:**
- Modify: `src/web-ui/app/pages/admin/settings.vue`

Add a new card for the AI Identity Prompt with a textarea + save button, following the existing section/save pattern.

- [ ] **Step 1: Add `aiIdentityPrompt` to the settings reactive state**

In `src/web-ui/app/pages/admin/settings.vue`, add to the `settings` reactive object (around line 16-26):

```typescript
const settings = reactive({
  archivedItemRetentionDays: 730,
  auditLogRetentionDays: 90,
  notificationRetentionDays: 30,
  ntfyServerUrl: '',
  searXngUrl: '',
  brandName: '',
  brandLogoUrl: '',
  aiNarrativeGenerationTimeUtc: null as string | null,
  housekeepingRunTimeUtc: null as string | null,
  aiIdentityPrompt: ''
})
```

- [ ] **Step 2: Add `aiIdentityPrompt` to the `SettingsResponse` interface**

In the same file (around line 35-45), add:

```typescript
interface SettingsResponse {
  archivedItemRetentionDays: number
  auditLogRetentionDays: number
  notificationRetentionDays: number
  ntfyServerUrl: string | null
  searXngUrl: string | null
  brandName: string | null
  brandLogoUrl: string | null
  aiNarrativeGenerationTimeUtc: string | null
  housekeepingRunTimeUtc: string | null
  aiIdentityPrompt: string | null
}
```

- [ ] **Step 3: Add an `ai` section to the `saving` reactive + `saveSettings`**

Add `ai` to the `saving` reactive (around line 28-33):

```typescript
const saving = reactive({
  retention: false,
  notifications: false,
  search: false,
  branding: false,
  ai: false
})
```

In `saveSettings` (around line 113-143), add a branch for the `ai` section:

```typescript
    } else if (section === 'ai') {
      body.aiIdentityPrompt = settings.aiIdentityPrompt
    }
```

- [ ] **Step 4: Add the AI Identity Prompt card to the template**

Add a new `<UCard>` in the grid (after the Branding card, around line 375 — before the closing `</div>` of the grid). It follows the same pattern as the other cards:

```vue
      <UCard class="lg:col-span-2">
        <template #header>
          <h2 class="font-semibold">
            AI Identity Prompt
          </h2>
          <p class="text-sm text-muted">
            System prompt injected into every chat session so models know they're running inside HydraForge. Leave blank to use the built-in default. Applies to new sessions only — existing sessions keep their original prompt.
          </p>
        </template>

        <UFormField label="Identity Prompt">
          <UTextarea
            v-model="settings.aiIdentityPrompt"
            :rows="6"
            placeholder="You are HydraForge's built-in assistant..."
            class="w-full"
          />
        </UFormField>

        <template #footer>
          <div class="flex justify-end">
            <UButton
              label="Save AI Identity"
              :loading="saving.ai"
              @click="saveSettings('ai')"
            />
          </div>
        </template>
      </UCard>
```

- [ ] **Step 5: Verify typecheck + lint + build**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
Expected: All pass.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/pages/admin/settings.vue
git commit -m "feat(admin): add AI Identity Prompt editor to settings page"
```

---

## Task 12: Full verification + manual validation matrix

**Files:**
- Create: `docs/manual-validation/2026-08-04-slice-a-global-chat-dock-matrix.md`

- [ ] **Step 1: Run the full .NET verification**

Run:
```bash
dotnet build
dotnet test
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```
Expected: All build, all tests pass, no pending model changes.

- [ ] **Step 2: Run the full Web UI verification**

Run:
```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```
Expected: All pass.

- [ ] **Step 3: Write the manual validation matrix**

Create `docs/manual-validation/2026-08-04-slice-a-global-chat-dock-matrix.md`:

```markdown
# Manual Validation Matrix — Slice A: Global Chat Dock + Identity Prompt

**Plan:** docs/superpowers/plans/2026-08-04-slice-a-global-chat-dock.md
**Spec:** docs/superpowers/specs/2026-08-04-slice-a-global-chat-dock-design.md
**Branch:** task/web-chat-panel

## TC-1: Open dock on project list
**Steps:**
1. Navigate to `/projects` (project list page).
2. Click the chat FAB (bottom-right).
**Expected:** Popup window opens. A new chat session is created (user-scoped, no projectId). ChatSessionView renders. Can send a message and receive a reply.

## TC-2: Navigate to board — same session persists
**Steps:**
1. From TC-1, with the chat open, click a project to navigate to `/projects/[id]/board`.
**Expected:** The chat popup stays open with the same session (messages preserved). No new session is auto-created. The session remains user-scoped (it was created on the list).

## TC-3: New chat on board — project-scoped
**Steps:**
1. On `/projects/[id]/board`, with chat open, click "New chat" (plus icon in popup header).
**Expected:** A new session is created with `projectId` set to the current board's project. F6 implicit-close of the prior session fires server-side (prior session gets closed in the background). The popup shows the fresh empty session.

## TC-4: Model switch mid-conversation — identity stays
**Steps:**
1. Open a chat, send a message, get a reply.
2. Switch the model in the chat input.
3. Send another message.
**Expected:** The reply is coherent. The model knows it's HydraForge's assistant (identity System message in history flows to the new model).

## TC-5: /chats page — dock hidden
**Steps:**
1. Navigate to `/chats`.
**Expected:** The chat FAB is NOT visible. No chat dock renders on `/chats` pages.

## TC-6: Open card modal — chat dock usable above modal
**Steps:**
1. On `/projects/[id]/board`, open a card (click a card to open the card modal).
2. With the card modal open, click the chat FAB.
**Expected:** The chat popup opens ON TOP of the card modal (higher z-index). Can interact with the chat while the card modal is open behind it. No backdrop blocks the chat.

## TC-7: Drag the popup window
**Steps:**
1. Open the chat popup.
2. Click and drag the header (title bar) to move the window.
**Expected:** The popup moves with the cursor. Released at the new position.

## TC-8: ESC closes the popup
**Steps:**
1. Open the chat popup.
2. Press Escape.
**Expected:** The popup closes. The FAB reappears. The session is NOT closed (stays Active, resumable on next open).

## TC-9: Identity prompt hidden from UI
**Steps:**
1. Open a new chat session.
2. Inspect the rendered messages.
**Expected:** No "You are HydraForge's built-in assistant..." system message is visible in the chat. Only User/Assistant messages render.

## TC-10: Admin-configurable identity prompt
**Steps:**
1. Navigate to `/admin/settings`.
2. Find the "AI Identity Prompt" card.
3. Enter a custom prompt (e.g., "You are a pirate. Arrr.").
4. Save.
5. Start a NEW chat session (not an existing one).
6. Ask "who are you?".
**Expected:** The reply uses the custom prompt (pirate persona). Existing sessions (created before the change) still use the old prompt.

## TC-11: Collapsing the dock does not close the session
**Steps:**
1. Open chat, send a message.
2. Close the popup (X button or ESC).
3. Reopen the chat (click FAB).
**Expected:** The same session resumes with all messages visible. No new session is created on reopen (only "New chat" button creates a new one).
```

- [ ] **Step 4: Commit**

```bash
git add docs/manual-validation/2026-08-04-slice-a-global-chat-dock-matrix.md
git commit -m "docs: manual validation matrix for Slice A (global chat dock)"
```

---

## Self-Review Notes

**Spec coverage:**
- A1 (global floating widget): Tasks 7, 8, 9 ✓
- A2 (Hydra identity prompt, persisted): Tasks 1-6 ✓
- A3 (admin-configurable): Tasks 1, 2, 3, 11 ✓
- Plan 18 cleanup: Task 10 ✓
- Testing: Tasks 4, 5, 6, 7, 8 (unit) + Task 12 (manual) ✓
- Draggable window: Task 8 (`useDraggable`) ✓
- System message hidden from UI: Task 6 ✓
- F6 implicit close on "New chat": Task 7 store calls `POST /api/chat/sessions` (server handles F6) ✓
- Page context detection: Task 7 (`currentProjectId` computed) ✓
- `/chats` hide: Task 8 (`isHidden` computed) ✓

**Placeholder scan:** No TBD/TODO. All steps have concrete code.

**Type consistency:** `useChatDockStore` used in Task 8 matches Task 7's export. `ChatSessionView` props match existing (`:session-id`, `:key`). `AiIdentityPrompt` property name consistent across Tasks 1-3, 11. `SetAiIdentityPrompt` method name consistent across Tasks 1, 3.
