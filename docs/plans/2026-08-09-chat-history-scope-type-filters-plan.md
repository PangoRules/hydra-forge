# Chat History — Scope & Type Filters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix personal chat history (`/chats` page + FAB dock's History panel) leaking other project members' chats by default, and add a Type (Chat/Project/Card) filter alongside the existing Status filter, on both surfaces, via one shared filter-bar component.

**Architecture:** A new `scope` query param (`mine` default | `participated`) on `GET /api/chat/sessions` and `GET /api/chat/search` controls whether results are owner-only or the existing Plan-20 "participated in" (owner OR project member) behavior — `mine` becomes the default everywhere except `ProjectChatsTab.vue`, which explicitly opts into `participated` since that's its whole purpose. A new `types` query param (comma-separated `normal,project,card`) filters server-side in the same `WHERE` clause, so pagination/counts stay correct under any combination. One new shared Vue component (`ChatSessionFilters.vue`) renders Status+Type (top row) and the Mine/Participated segmented toggle (bottom row) identically on `/chats` and the FAB history panel.

**Tech Stack:** .NET 10 / EF Core / PostgreSQL (backend), Nuxt 4 / Nuxt UI v4 / Vitest (frontend). No new dependencies.

## Global Constraints

- xUnit + plain `Assert.*` only — no FluentAssertions. NSubstitute, not Moq (not needed in this plan — all touched repos are hand-rolled fakes already).
- `dotnet csharpier format .` before every backend commit; `dotnet csharpier check .` must be clean.
- Frontend: `pnpm lint` and `pnpm typecheck` must be clean before every frontend commit; `pnpm test` must pass.
- No `var` where the type isn't obvious from the right-hand side.
- Domain entities encapsulate state transitions via instance methods — not touched by this plan (no entity changes, this is pure query/filter logic).
- `UTable`/`USelectMenu` etc. are Nuxt UI **v4** components — `:data` not `:rows` (see D-40-adjacent gotcha already fixed elsewhere in this codebase).
- Every new/changed backend method signature must be mirrored in **all** existing fake implementations of that interface across the test suite, or the solution won't compile. Each task below names every file that needs updating — this is not optional cleanup, it's required for a green build.

---

### Task 1: Backend — `ChatSessionScope`/`ChatSessionKind` enums + `IChatSessionRepository`/`EfChatSessionRepository`

**Files:**
- Modify: `src/HydraForge.Application/Chat/ChatDtos.cs`
- Modify: `src/HydraForge.Application/Chat/IChatSessionRepository.cs`
- Modify: `src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs`
- Test: `tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs`

**Interfaces:**
- Produces: `ChatSessionScope { Mine, Participated }` (default `Mine`), `ChatSessionKind { Normal, Project, Card }` — both in `HydraForge.Application.Chat` namespace (`ChatDtos.cs`). New `IChatSessionRepository.ListAsync`/`CountAsync`/`SearchByTitleAsync` signatures (below) — every other task in this plan builds on these exact signatures.

**1a. Add the two enums to `ChatDtos.cs`**, directly below the existing `ChatSessionStatusFilter` enum (same file, `// ── Enums ──` section):

```csharp
public enum ChatSessionScope
{
    Mine,
    Participated,
}

public enum ChatSessionKind
{
    Normal,
    Project,
    Card,
}
```

**1b. Update `IChatSessionRepository.cs`** — replace the three method signatures with:

```csharp
Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
);

Task<int> CountAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
);

Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
    Guid actorId,
    string query,
    Guid? projectId,
    int limit,
    bool isAdmin = false,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
);
```

(`types` is intentionally not added to `SearchByTitleAsync` — search results don't render the type badge, only the list endpoint needs it.)

**1c. Update `EfChatSessionRepository.cs`.** Replace `ListAsync`, `CountAsync`, `SearchByTitleAsync`, and add two new private helpers. The existing `WhereParticipatedIn` method is unchanged and still used, just conditionally now.

```csharp
public async Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    var query = ApplyStatusFilter(ApplyScope(context.ChatSessions, actorId, isAdmin, scope), statusFilter);
    query = ApplyTypes(query, types);

    if (folderId.HasValue)
        query = query.Where(s => s.FolderId == folderId.Value);
    if (projectId.HasValue)
        query = query.Where(s => s.ProjectId == projectId.Value);
    if (before.HasValue)
        query = query.Where(s =>
            s.UpdatedAt < before.Value || (s.UpdatedAt == before.Value && s.Id < beforeId)
        );

    return await query
        .OrderByDescending(s => s.UpdatedAt)
        .ThenByDescending(s => s.Id)
        .Take(limit)
        .ToListAsync(ct);
}

public async Task<int> CountAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    var query = ApplyStatusFilter(ApplyScope(context.ChatSessions, actorId, isAdmin, scope), statusFilter);
    query = ApplyTypes(query, types);

    if (folderId.HasValue)
        query = query.Where(s => s.FolderId == folderId.Value);
    if (projectId.HasValue)
        query = query.Where(s => s.ProjectId == projectId.Value);

    return await query.CountAsync(ct);
}

public async Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
    Guid actorId,
    string query,
    Guid? projectId,
    int limit,
    bool isAdmin = false,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
)
{
    if (string.IsNullOrWhiteSpace(query))
        return [];

    var q = ApplyStatusFilter(
        ApplyScope(context.ChatSessions, actorId, isAdmin, scope),
        ChatSessionStatusFilter.NonArchived
    ).Where(s => EF.Functions.ILike(s.Title, $"%{query}%"));

    if (projectId.HasValue)
        q = q.Where(s => s.ProjectId == projectId.Value);

    return await q.Take(limit).ToListAsync(ct);
}

// scope == Mine: strictly the caller's own sessions, regardless of admin status —
// a personal history view is personal even for an admin. scope == Participated
// preserves Plan 20's existing behavior unchanged, admin bypass included.
private IQueryable<ChatSession> ApplyScope(
    IQueryable<ChatSession> q,
    Guid actorId,
    bool isAdmin,
    ChatSessionScope scope
) =>
    scope switch
    {
        ChatSessionScope.Participated => isAdmin ? q : WhereParticipatedIn(q, actorId),
        _ => q.Where(s => s.OwnerId == actorId),
    };

// Mirrors web-ui's lib/chat-type.ts getChatType() exactly: card implies project,
// project means projectId set with no card, normal means no projectId at all.
private static IQueryable<ChatSession> ApplyTypes(
    IQueryable<ChatSession> q,
    IReadOnlySet<ChatSessionKind>? types
)
{
    if (types == null || types.Count == 0)
        return q;

    return q.Where(s =>
        (types.Contains(ChatSessionKind.Normal) && s.ProjectId == null)
        || (types.Contains(ChatSessionKind.Project) && s.ProjectId != null && s.OpenCardId == null)
        || (types.Contains(ChatSessionKind.Card) && s.OpenCardId != null)
    );
}
```

- [ ] **Step 1: Make the changes above to `ChatDtos.cs`, `IChatSessionRepository.cs`, `EfChatSessionRepository.cs`.**

- [ ] **Step 2: Confirm the solution does not yet build** (expected — every fake `IChatSessionRepository` in the test suite is now missing the two new optional params... actually they aren't missing anything since the new params have defaults, so add-only signature changes to an *interface* don't break implementers with fewer optional params in C#. Verify this assumption directly:)

Run: `dotnet build HydraForge.slnx -nologo -v q`
Expected: **Build fails** — C# requires an explicit interface implementation to match the interface member's signature exactly (including optional parameters use the *implementation's* defaults, but the parameter list itself — types, count, order — must match). Every existing fake with a 6-parameter `ListAsync`/`CountAsync` (missing `scope`/`types`) and 5-parameter `SearchByTitleAsync` (missing `scope`) will fail to satisfy the interface. This is expected and fixed in Task 2.

- [ ] **Step 3: Add EF-level tests to `EfChatSessionRepositoryTests.cs`.** These follow the exact existing pattern in that file (gated on `HYDRAFORGE_TEST_CONNECTION_STRING`, no-op/return early if unset — see `ListAsync_WhereParticipatedIn_IncludesProjectMemberSessions` for the pattern to copy). Add after that method, before the closing `}` of the class:

```csharp
[Fact]
public async Task ListAsync_ScopeMine_ExcludesParticipatedOnlySessions()
{
    string? connectionString = Environment.GetEnvironmentVariable(
        "HYDRAFORGE_TEST_CONNECTION_STRING"
    );
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    var options = CreateOptions(connectionString);
    using var context = new HydraForgeDbContext(options);
    var repo = new EfChatSessionRepository(context);

    var ownerId = Guid.NewGuid();
    var memberId = Guid.NewGuid();
    var projectId = Guid.NewGuid();

    var projectSession = new ChatSession
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        ProjectId = projectId,
        Title = "Project Session",
        UpdatedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Status = ChatSessionStatus.Active,
    };

    var projectMember = new HydraForge.Domain.Entities.ProjectSpace.ProjectMember
    {
        ProjectId = projectId,
        UserId = memberId,
        Role = HydraForge.Domain.Enums.MemberRole.Member,
    };

    context.ChatSessions.Add(projectSession);
    context.ProjectMembers.Add(projectMember);
    await context.SaveChangesAsync();

    // Member is not the owner. Default scope (Mine) must exclude this session
    // even though scope=Participated (the pre-existing behavior) would include it.
    var mineResults = await repo.ListAsync(
        memberId,
        null,
        null,
        DateTime.MaxValue,
        null,
        10,
        isAdmin: false,
        ChatSessionStatusFilter.NonArchived,
        ChatSessionScope.Mine,
        null,
        CancellationToken.None
    );
    Assert.DoesNotContain(mineResults, s => s.Id == projectSession.Id);

    var participatedResults = await repo.ListAsync(
        memberId,
        null,
        null,
        DateTime.MaxValue,
        null,
        10,
        isAdmin: false,
        ChatSessionStatusFilter.NonArchived,
        ChatSessionScope.Participated,
        null,
        CancellationToken.None
    );
    Assert.Contains(participatedResults, s => s.Id == projectSession.Id);
}

[Fact]
public async Task ListAsync_TypesFilter_ReturnsOnlyMatchingKinds()
{
    string? connectionString = Environment.GetEnvironmentVariable(
        "HYDRAFORGE_TEST_CONNECTION_STRING"
    );
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    var options = CreateOptions(connectionString);
    using var context = new HydraForgeDbContext(options);
    var repo = new EfChatSessionRepository(context);

    var ownerId = Guid.NewGuid();
    var projectId = Guid.NewGuid();
    var cardId = Guid.NewGuid();

    var normalSession = new ChatSession
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        ProjectId = null,
        Title = "Personal",
        UpdatedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Status = ChatSessionStatus.Active,
    };
    var projectSession = new ChatSession
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        ProjectId = projectId,
        OpenCardId = null,
        Title = "Project",
        UpdatedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Status = ChatSessionStatus.Active,
    };
    var cardSession = new ChatSession
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        ProjectId = projectId,
        OpenCardId = cardId,
        Title = "Card",
        UpdatedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Status = ChatSessionStatus.Active,
    };
    context.ChatSessions.AddRange(normalSession, projectSession, cardSession);
    await context.SaveChangesAsync();

    var cardOnly = await repo.ListAsync(
        ownerId,
        null,
        null,
        DateTime.MaxValue,
        null,
        10,
        isAdmin: false,
        ChatSessionStatusFilter.NonArchived,
        ChatSessionScope.Mine,
        new HashSet<ChatSessionKind> { ChatSessionKind.Card },
        CancellationToken.None
    );
    Assert.Single(cardOnly);
    Assert.Equal(cardSession.Id, cardOnly[0].Id);

    var normalAndProject = await repo.ListAsync(
        ownerId,
        null,
        null,
        DateTime.MaxValue,
        null,
        10,
        isAdmin: false,
        ChatSessionStatusFilter.NonArchived,
        ChatSessionScope.Mine,
        new HashSet<ChatSessionKind> { ChatSessionKind.Normal, ChatSessionKind.Project },
        CancellationToken.None
    );
    Assert.Equal(2, normalAndProject.Count);
    Assert.DoesNotContain(normalAndProject, s => s.Id == cardSession.Id);
}
```

- [ ] **Step 4: Run the new tests** (they no-op without a real Postgres connection string — that's expected and matches every other test in this file):

Run: `dotnet test tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj --filter "FullyQualifiedName~EfChatSessionRepositoryTests"`
Expected: PASS (tests either run against real Postgres if `HYDRAFORGE_TEST_CONNECTION_STRING` is set, or short-circuit and pass trivially if not — do not skip this step assuming it's a no-op, confirm it actually reports PASS)

- [ ] **Step 5: Do not commit yet** — the solution doesn't build until Task 2 fixes every fake. Proceed directly to Task 2 in the same working session (these two tasks land in one commit).

---

### Task 2: Backend — fix every trivial fake `IChatSessionRepository`/`IChatMessageRepository` to match the new interfaces

**Files:**
- Modify: `tests/HydraForge.Application.Tests/Chat/ChatFolderServiceTests.cs`
- Modify: `tests/HydraForge.Application.Tests/Chat/ChatMessageServiceTests.cs`
- Modify: `tests/HydraForge.Application.Tests/Chat/LlmChatSummaryGeneratorTests.cs`
- Modify: `tests/HydraForge.Server.Tests/Chat/ChatSessionsControllerTests.cs`

**Interfaces:**
- Consumes: `ChatSessionScope`, `ChatSessionKind` from Task 1.
- Produces: nothing new — this task only restores compilation. `IChatMessageRepository.SearchByContentAsync` is *not* touched here (that's Task 5) — these four files' `FakeMessageRepo`/`TestChatMessageRepository` classes are untouched in this task.

None of these four fakes implement real scope/type filtering logic (they're trivial stubs used by services that don't test list-filtering behavior) — just add the two new parameters to each signature, keep the body unchanged.

**2a. `ChatFolderServiceTests.cs`** — its `FakeSessionRepo.ListAsync`/`CountAsync` *does* have real filtering logic (used by `ChatArchiveService` via folder cascade-archive tests), but none of it exercises scope/types. Add the params, ignore them in the body (the existing `isAdmin ? Sessions.AsEnumerable() : Sessions.Where(s => s.OwnerId == ownerId)` line already matches what `scope=Mine` would do — leave it exactly as-is, just accept and ignore the two new params):

```csharp
public Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    var query = isAdmin
        ? Sessions.AsEnumerable()
        : Sessions.Where(s => s.OwnerId == ownerId);
    query = statusFilter switch
    {
        ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
        ChatSessionStatusFilter.Active => query.Where(s =>
            s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
        ),
        ChatSessionStatusFilter.Closed => query.Where(s =>
            s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
        ),
        _ => query.Where(s => s.ArchivedAt == null),
    };
    if (folderId.HasValue)
        query = query.Where(s => s.FolderId == folderId.Value);
    if (projectId.HasValue)
        query = query.Where(s => s.ProjectId == projectId.Value);
    return Task.FromResult<IReadOnlyList<ChatSession>>(
        query.OrderByDescending(s => s.UpdatedAt).Take(limit).ToList()
    );
}

public Task<int> CountAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    var query = isAdmin
        ? Sessions.AsEnumerable()
        : Sessions.Where(s => s.OwnerId == ownerId);
    query = statusFilter switch
    {
        ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
        ChatSessionStatusFilter.Active => query.Where(s =>
            s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
        ),
        ChatSessionStatusFilter.Closed => query.Where(s =>
            s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
        ),
        _ => query.Where(s => s.ArchivedAt == null),
    };
    if (folderId.HasValue)
        query = query.Where(s => s.FolderId == folderId.Value);
    if (projectId.HasValue)
        query = query.Where(s => s.ProjectId == projectId.Value);
    return Task.FromResult(query.Count());
}

public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
    Guid ownerId,
    string query,
    Guid? projectId,
    int limit,
    bool isAdmin = false,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
) => Task.FromResult<IReadOnlyList<ChatSession>>([]);
```

**2b. `ChatMessageServiceTests.cs`** — its `FakeSessionRepo.ListAsync`/`CountAsync`/`SearchByTitleAsync` are pure stubs returning empty/0. Replace the three method signatures (bodies unchanged — `Task.FromResult<IReadOnlyList<ChatSession>>([])` / `Task.FromResult(0)`):

```csharp
public Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

public Task<int> CountAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
) => Task.FromResult(0);

public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
    Guid ownerId,
    string query,
    Guid? projectId,
    int limit,
    bool isAdmin = false,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
) => Task.FromResult<IReadOnlyList<ChatSession>>([]);
```

**2c. `LlmChatSummaryGeneratorTests.cs`** — identical pure-stub shape to 2b; apply the exact same three signatures verbatim (its `FakeSessionRepo` has the same 6/6/5-param stub methods returning empty/0).

**2d. `ChatSessionsControllerTests.cs`** — `TestChatSessionRepository`'s `ListAsync`/`CountAsync`/`SearchByTitleAsync` are also pure stubs (`Task.FromResult<IReadOnlyList<ChatSession>>([])` / `Task.FromResult(0)`). Apply the same three signatures verbatim as 2b (this class is `internal`, not `private sealed`, but the method bodies are identical stubs — only the signatures change).

- [ ] **Step 1: Apply 2a–2d exactly as shown.**

- [ ] **Step 2: Build the whole solution.**

Run: `dotnet build HydraForge.slnx -nologo -v q`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Run the full test suite (Tasks 1+2 combined change nothing behaviorally for these four files — this just confirms nothing broke).**

Run: `dotnet test HydraForge.slnx -nologo -v q`
Expected: All test projects report `Passed!` — same counts as before this plan started (verify none of the counts dropped, which would mean a test silently stopped compiling/running).

- [ ] **Step 4: Format and commit.**

```bash
dotnet csharpier format .
git add src/HydraForge.Application/Chat/ChatDtos.cs src/HydraForge.Application/Chat/IChatSessionRepository.cs src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs tests/HydraForge.Application.Tests/Chat/ChatFolderServiceTests.cs tests/HydraForge.Application.Tests/Chat/ChatMessageServiceTests.cs tests/HydraForge.Application.Tests/Chat/LlmChatSummaryGeneratorTests.cs tests/HydraForge.Server.Tests/Chat/ChatSessionsControllerTests.cs
git commit -m "feat(chat): add scope + type filtering to IChatSessionRepository

ChatSessionScope (Mine default | Participated) and ChatSessionKind
(Normal/Project/Card) are now query-level filters on ListAsync/
CountAsync/SearchByTitleAsync, applied in the same WHERE clause as the
existing status filter. No caller is wired to the new params yet —
this task only lands the repository contract and its EF-level tests."
```

---

### Task 3: Backend — `ChatSessionService.ListAsync` scope/types passthrough + the actual bug-fix tests

**Files:**
- Modify: `src/HydraForge.Application/Chat/ChatSessionService.cs`
- Modify: `src/HydraForge.Application/Chat/IChatSessionService.cs`
- Modify: `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`

**Interfaces:**
- Consumes: `ChatSessionScope`, `ChatSessionKind`, `IChatSessionRepository.ListAsync`/`CountAsync` (Task 1).
- Produces: `IChatSessionService.ListAsync(Guid actorId, Guid? folderId, Guid? projectId, DateTime? before, Guid? beforeId, int limit, ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived, ChatSessionScope scope = ChatSessionScope.Mine, IReadOnlySet<ChatSessionKind>? types = null, CancellationToken ct = default)` — Task 4 (controller) calls this exact signature.

**3a. Update `IChatSessionService.cs`** — `ListAsync` signature gains the same two trailing optional params (before `CancellationToken ct = default`):

```csharp
Task<Result<ChatSessionPageDto>> ListAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
);
```

**3b. Update `ChatSessionService.ListAsync`** — thread `scope`/`types` straight through to the repository (the method already resolves `isAdmin` internally):

```csharp
public async Task<Result<ChatSessionPageDto>> ListAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    var user = await _userRepo.FindByIdAsync(actorId, ct);
    var isAdmin = user?.IsAdmin == true;

    var sessions = await _sessionRepo.ListAsync(
        actorId,
        folderId,
        projectId,
        before,
        beforeId,
        limit,
        isAdmin,
        statusFilter,
        scope,
        types,
        ct
    );
    var dtos = new List<ChatSessionDto>();
    foreach (var session in sessions)
        dtos.Add(await MapToDtoAsync(session, ct));

    var totalCount = await _sessionRepo.CountAsync(
        actorId,
        folderId,
        projectId,
        isAdmin,
        statusFilter,
        scope,
        types,
        ct
    );

    return Result<ChatSessionPageDto>.Success(new ChatSessionPageDto(dtos, totalCount));
}
```

(This replaces the entire existing method body — the two `_sessionRepo` calls just gain `scope`/`types` arguments in the same position they're declared in the interface.)

**3c. Update `ChatSessionServiceTests.cs`'s `FakeSessionRepo`** so it *actually* implements scope/type filtering — the new tests in 3d need real behavior to assert against, not a stub. Replace the existing `ListAsync`/`CountAsync` bodies (the ones with the `isAdmin ? Sessions.AsQueryable() : Sessions.AsQueryable().Where(...)` participated-in logic) with:

```csharp
public Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    var baseQuery = ApplyScope(ownerId, isAdmin, scope);
    var query = ApplyStatusFilter(baseQuery, statusFilter);
    query = ApplyTypes(query, types);
    if (folderId.HasValue)
        query = query.Where(s => s.FolderId == folderId.Value);
    if (projectId.HasValue)
        query = query.Where(s => s.ProjectId == projectId.Value);
    if (before.HasValue)
        query = query.Where(s => s.CreatedAt < before.Value);

    return Task.FromResult<IReadOnlyList<ChatSession>>(
        query.OrderByDescending(s => s.UpdatedAt).Take(limit).ToList()
    );
}

public Task<int> CountAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    var baseQuery = ApplyScope(ownerId, isAdmin, scope);
    var query = ApplyStatusFilter(baseQuery, statusFilter);
    query = ApplyTypes(query, types);
    if (folderId.HasValue)
        query = query.Where(s => s.FolderId == folderId.Value);
    if (projectId.HasValue)
        query = query.Where(s => s.ProjectId == projectId.Value);
    return Task.FromResult(query.Count());
}

// Mirrors EfChatSessionRepository.ApplyScope — scope=Mine ignores isAdmin entirely.
private IQueryable<ChatSession> ApplyScope(Guid ownerId, bool isAdmin, ChatSessionScope scope) =>
    scope switch
    {
        ChatSessionScope.Participated => isAdmin
            ? Sessions.AsQueryable()
            : Sessions.AsQueryable().Where(s =>
                s.OwnerId == ownerId
                || (s.ProjectId != null && _memberRepo.IsMember(s.ProjectId.Value, ownerId))),
        _ => Sessions.AsQueryable().Where(s => s.OwnerId == ownerId),
    };

// Mirrors EfChatSessionRepository.ApplyTypes exactly.
private static IQueryable<ChatSession> ApplyTypes(
    IQueryable<ChatSession> q,
    IReadOnlySet<ChatSessionKind>? types
)
{
    if (types == null || types.Count == 0)
        return q;

    return q.Where(s =>
        (types.Contains(ChatSessionKind.Normal) && s.ProjectId == null)
        || (types.Contains(ChatSessionKind.Project) && s.ProjectId != null && s.OpenCardId == null)
        || (types.Contains(ChatSessionKind.Card) && s.OpenCardId != null)
    );
}
```

Also update `FakeSessionRepo.SearchByTitleAsync`'s signature to match Task 1 (5-param, no `types`) — keep its existing empty-list body:

```csharp
public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
    Guid ownerId,
    string query,
    Guid? projectId,
    int limit,
    bool isAdmin = false,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
) => Task.FromResult<IReadOnlyList<ChatSession>>([]);
```

**3d. Add new tests to `ChatSessionServiceTests.cs`**, in the `// ── Participation-aware ListAsync tests ──` region, after the existing `ListAsync_AdminSeesAll` test:

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact]
public async Task ListAsync_DefaultScope_ExcludesParticipatedOnlySession()
{
    var (service, sessionRepo, _, _, userRepo, memberRepo, _, _, _, _, _, _) = CreateSut();
    var ownerId = NewId();
    var memberId = NewId();
    var projectId = NewId();

    userRepo.Users[ownerId] = User.Create("owner", "Owner", "User", "owner@test.com", "hash", isAdmin: false, id: ownerId);
    userRepo.Users[memberId] = User.Create("member", "Member", "User", "member@test.com", "hash", isAdmin: false, id: memberId);
    memberRepo.AddMembership(projectId, memberId, MemberRole.Member);

    var projectSession = new ChatSession
    {
        Id = NewId(),
        OwnerId = ownerId,
        ProjectId = projectId,
        Status = ChatSessionStatus.Active,
    };
    sessionRepo.Sessions.Add(projectSession);

    // No scope passed — must default to Mine and exclude a session the caller
    // only participates in as a project member. This is the exact bug: a
    // member's own /chats page must not show another owner's project chat.
    var result = await service.ListAsync(
        memberId,
        folderId: null,
        projectId: null,
        before: null,
        beforeId: null,
        limit: 50
    );

    Assert.True(result.IsSuccess);
    Assert.Equal(0, result.Value.TotalCount);
}

[Fact]
public async Task ListAsync_ScopeParticipated_IncludesProjectMemberSession()
{
    var (service, sessionRepo, _, _, userRepo, memberRepo, _, _, _, _, _, _) = CreateSut();
    var ownerId = NewId();
    var memberId = NewId();
    var projectId = NewId();

    userRepo.Users[ownerId] = User.Create("owner", "Owner", "User", "owner@test.com", "hash", isAdmin: false, id: ownerId);
    userRepo.Users[memberId] = User.Create("member", "Member", "User", "member@test.com", "hash", isAdmin: false, id: memberId);
    memberRepo.AddMembership(projectId, memberId, MemberRole.Member);

    var projectSession = new ChatSession
    {
        Id = NewId(),
        OwnerId = ownerId,
        ProjectId = projectId,
        Status = ChatSessionStatus.Active,
    };
    sessionRepo.Sessions.Add(projectSession);

    var result = await service.ListAsync(
        memberId,
        folderId: null,
        projectId: null,
        before: null,
        beforeId: null,
        limit: 50,
        scope: ChatSessionScope.Participated
    );

    Assert.True(result.IsSuccess);
    Assert.Equal(1, result.Value.TotalCount);
    Assert.Equal(projectSession.Id, result.Value.Items[0].Id);
}

[Fact]
public async Task ListAsync_AdminWithScopeMine_SeesOnlyOwnSessions()
{
    var (service, sessionRepo, _, _, userRepo, _, _, _, _, _, _, _) = CreateSut();
    var adminId = NewId();
    var otherOwnerId = NewId();

    userRepo.Users[adminId] = User.Create("admin", "Admin", "User", "admin@test.com", "hash", isAdmin: true, id: adminId);
    userRepo.Users[otherOwnerId] = User.Create("other", "Other", "User", "other@test.com", "hash", isAdmin: false, id: otherOwnerId);

    sessionRepo.Sessions.Add(new ChatSession { Id = NewId(), OwnerId = adminId, Status = ChatSessionStatus.Active });
    sessionRepo.Sessions.Add(new ChatSession { Id = NewId(), OwnerId = otherOwnerId, Status = ChatSessionStatus.Active });

    // Admin bypass only applies under scope=Participated — an admin's own
    // /chats page (scope=Mine, the default) must not show every user's chats.
    var result = await service.ListAsync(
        adminId,
        folderId: null,
        projectId: null,
        before: null,
        beforeId: null,
        limit: 50
    );

    Assert.True(result.IsSuccess);
    Assert.Equal(1, result.Value.TotalCount);
}

[Fact]
public async Task ListAsync_TypesFilter_ReturnsOnlyRequestedKinds()
{
    var (service, sessionRepo, _, _, _, _, _, _, _, _, _, _) = CreateSut();
    var ownerId = NewId();
    var projectId = NewId();
    var cardId = NewId();

    sessionRepo.Sessions.Add(new ChatSession { Id = NewId(), OwnerId = ownerId, ProjectId = null, Status = ChatSessionStatus.Active });
    sessionRepo.Sessions.Add(new ChatSession { Id = NewId(), OwnerId = ownerId, ProjectId = projectId, OpenCardId = null, Status = ChatSessionStatus.Active });
    var cardSession = new ChatSession { Id = NewId(), OwnerId = ownerId, ProjectId = projectId, OpenCardId = cardId, Status = ChatSessionStatus.Active };
    sessionRepo.Sessions.Add(cardSession);

    var result = await service.ListAsync(
        ownerId,
        folderId: null,
        projectId: null,
        before: null,
        beforeId: null,
        limit: 50,
        types: new HashSet<ChatSessionKind> { ChatSessionKind.Card }
    );

    Assert.True(result.IsSuccess);
    Assert.Equal(1, result.Value.TotalCount);
    Assert.Equal(cardSession.Id, result.Value.Items[0].Id);
}
```

- [ ] **Step 2: Run the new tests to verify they fail before 3a/3b are applied** (if you're implementing 3a–3d in one pass rather than strict TDD ping-pong, instead just confirm they fail against the *old* `ChatSessionService.ListAsync` signature by temporarily checking out `HEAD` for `ChatSessionService.cs`/`IChatSessionService.cs` — or, simpler: apply 3a/3b first, then write+run these tests, since 3c's fake update is a prerequisite for the tests to even compile).

Practical order: apply 3a, 3b, 3c first (all three are needed just to compile), *then* add the 3d tests, run once, confirm PASS.

Run: `dotnet test tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj --filter "FullyQualifiedName~ChatSessionServiceTests"`
Expected: All tests PASS, including the 4 new ones.

- [ ] **Step 3: Run the full backend test suite.**

Run: `dotnet test HydraForge.slnx -nologo -v q`
Expected: All projects `Passed!`.

- [ ] **Step 4: Format and commit.**

```bash
dotnet csharpier format .
git add src/HydraForge.Application/Chat/ChatSessionService.cs src/HydraForge.Application/Chat/IChatSessionService.cs tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs
git commit -m "fix(chat): ChatSessionService.ListAsync defaults to owner-only scope

Personal chat history was showing every project a user is a member of,
not just their own chats, because the service unconditionally called
the repository's participated-in query. scope now defaults to Mine
(owner-only, admin bypass no longer applies) with an explicit
Participated opt-in preserving the prior behavior."
```

---

### Task 4: Backend — `ChatSessionsController.List` reads `scope`/`types` from the query string

**Files:**
- Modify: `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs`
- Modify: `tests/HydraForge.Server.Tests/Chat/ChatSessionsControllerTests.cs`

**Interfaces:**
- Consumes: `IChatSessionService.ListAsync` (Task 3), `ChatSessionScope`/`ChatSessionKind` (Task 1).
- Produces: `GET /api/chat/sessions?...&scope=mine|participated&types=normal,project,card` — Task 7 (`ProjectChatsTab.vue`) depends on `scope=participated` being accepted here.

**4a. Update the `List` action** in `ChatSessionsController.cs`. ASP.NET Core binds `ChatSessionScope` from a query string by name case-insensitively already (no custom binder needed) — but `types` needs manual parsing since it's a comma-joined string, not a native list-bindable shape:

```csharp
[HttpGet]
[ProducesResponseType(typeof(ChatSessionPageDto), StatusCodes.Status200OK)]
public async Task<IActionResult> List(
    [FromQuery] Guid? folderId,
    [FromQuery] Guid? projectId,
    [FromQuery] DateTime? before = null,
    [FromQuery] Guid? beforeId = null,
    [FromQuery] int limit = 20,
    [FromQuery] ChatSessionStatusFilter status = ChatSessionStatusFilter.NonArchived,
    [FromQuery] ChatSessionScope scope = ChatSessionScope.Mine,
    [FromQuery] string? types = null
)
{
    var userId = User.GetRequiredUserId();
    var typeSet = ParseTypes(types);
    var result = await sessionService.ListAsync(
        userId,
        folderId,
        projectId,
        before,
        beforeId,
        limit,
        statusFilter: status,
        scope: scope,
        types: typeSet
    );

    if (result.IsFailure)
        return this.ToProblemResult(result.Error);

    return Ok(result.Value);
}

private static IReadOnlySet<ChatSessionKind>? ParseTypes(string? types)
{
    if (string.IsNullOrWhiteSpace(types))
        return null;

    var set = new HashSet<ChatSessionKind>();
    foreach (var part in types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (Enum.TryParse<ChatSessionKind>(part, ignoreCase: true, out var kind))
            set.Add(kind);
    }

    return set.Count > 0 ? set : null;
}
```

(Only the `List` action and the new private `ParseTypes` helper change — every other action in this controller is untouched.)

**4b. `TestChatSessionRepository` in `ChatSessionsControllerTests.cs` is a pure stub** — `ListAsync`/`CountAsync` always return empty/0 regardless of args (Task 2d). That means a test asserting on the *response body* can't actually prove `scope`/`types` were parsed and forwarded correctly — the body would look identical whether or not 4a's binding code even exists. Make it a spy instead: capture the last call's `scope`/`types` so the test can assert on *what the controller passed down*, not on filtered results (that behavior is already covered by Task 1's EF-level tests and Task 3's service-level tests against the real-filtering `FakeSessionRepo` — this class only needs to prove the controller-to-service wiring is correct). Add two capture fields and set them at the top of `ListAsync`:

```csharp
public ChatSessionScope? LastScope { get; private set; }
public IReadOnlySet<ChatSessionKind>? LastTypes { get; private set; }

public Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid actorId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
)
{
    LastScope = scope;
    LastTypes = types;
    return Task.FromResult<IReadOnlyList<ChatSession>>([]);
}
```

(`CountAsync`/`SearchByTitleAsync` keep the plain stub bodies from Task 2d — only `ListAsync` needs the spy, since that's the only method this task's tests exercise.)

- [ ] **Step 1: Write the failing tests** — add to `ChatSessionsControllerTests.cs`, in a new test class alongside the existing `ChatSessionsControllerLinkCardTests` (same file, same `ChatSessionsTestWebApplicationFactory` infra already in that file). This needs a way to reach into the factory's `TestChatSessionRepository` instance after the request completes — add a public accessor to `ChatSessionsTestWebApplicationFactory` alongside its existing `AddProject`/`AddChatSession`/etc. methods:

```csharp
public TestChatSessionRepository SessionRepository { get; } = new([]);
```

and change the factory's `services.AddScoped<IChatSessionRepository>(...)` registration from `_ => new TestChatSessionRepository(_sessions)` to `_ => SessionRepository` (reusing the same instance — `TestChatSessionRepository`'s constructor already takes the `_sessions` list; adjust it to take no constructor arg and read from a field set via `AddChatSession`, or simplest: keep the constructor as-is and change the field initializer to `new(_sessions)` instead of a parameterless `new([])`, whichever compiles — the goal is only that `SessionRepository` is the *same instance* the DI container hands to the controller, so its captured `LastScope`/`LastTypes` are visible to the test after the request).

```csharp
public class ChatSessionsControllerListTests
{
    [Fact]
    public async Task List_NoScopeQueryParam_DefaultsToMineWhenCallingTheService()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var callerId = Guid.NewGuid();
        var token = ChatSessionsTestWebApplicationFactory.IssueToken(callerId, "caller");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/chat/sessions");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ChatSessionScope.Mine, factory.SessionRepository.LastScope);
        Assert.Null(factory.SessionRepository.LastTypes);
    }

    [Fact]
    public async Task List_ScopeParticipatedQueryParam_ForwardsToTheService()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var callerId = Guid.NewGuid();
        var token = ChatSessionsTestWebApplicationFactory.IssueToken(callerId, "caller");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/chat/sessions?scope=participated");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ChatSessionScope.Participated, factory.SessionRepository.LastScope);
    }

    [Fact]
    public async Task List_TypesQueryParam_ParsesCommaSeparatedValues()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var callerId = Guid.NewGuid();
        var token = ChatSessionsTestWebApplicationFactory.IssueToken(callerId, "caller");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/chat/sessions?types=project,card");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.SessionRepository.LastTypes);
        Assert.Equal(2, factory.SessionRepository.LastTypes!.Count);
        Assert.Contains(ChatSessionKind.Project, factory.SessionRepository.LastTypes);
        Assert.Contains(ChatSessionKind.Card, factory.SessionRepository.LastTypes);
    }

    [Fact]
    public async Task List_NoAuth_Returns401()
    {
        var factory = new ChatSessionsTestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/chat/sessions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run to verify they fail** (before 4a/4b are applied, `factory.SessionRepository` and `LastScope`/`LastTypes` don't exist yet):

Run: `dotnet build HydraForge.slnx -nologo -v q`
Expected: build FAILS — the new tests reference members that don't exist yet.

- [ ] **Step 3: Apply 4a and 4b, then run the new tests.**

Run: `dotnet test tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj --filter "FullyQualifiedName~ChatSessionsControllerListTests"`
Expected: PASS — all 4 tests, including confirming `LastScope == ChatSessionScope.Mine` with no query param at all (proving the default is wired correctly, not just accepted).

- [ ] **Step 4: Run the full test suite.**

Run: `dotnet test HydraForge.slnx -nologo -v q`
Expected: All projects `Passed!`.

- [ ] **Step 5: Format and commit.**

```bash
dotnet csharpier format .
git add src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs tests/HydraForge.Server.Tests/Chat/ChatSessionsControllerTests.cs
git commit -m "feat(chat): GET /api/chat/sessions accepts scope + types query params"
```

---

### Task 5: Backend — `IChatMessageRepository.SearchByContentAsync` gets a `scope` param

**Files:**
- Modify: `src/HydraForge.Application/Chat/IChatMessageRepository.cs`
- Modify: `src/HydraForge.Infrastructure/Chat/EfChatMessageRepository.cs`
- Modify: `tests/HydraForge.Application.Tests/Chat/ChatMessageServiceTests.cs`
- Modify: `tests/HydraForge.Application.Tests/Chat/LlmChatSummaryGeneratorTests.cs`
- Modify: `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`
- Modify: `tests/HydraForge.Server.Tests/Chat/ChatSessionsControllerTests.cs`
- Test: `tests/HydraForge.Infrastructure.Tests/Chat/EfChatMessageRepositoryTests.cs` (create if it doesn't already exist — check first)

**Interfaces:**
- Consumes: `ChatSessionScope` (Task 1).
- Produces: `IChatMessageRepository.SearchByContentAsync(Guid actorId, string query, Guid? projectId, int limit, ChatSessionScope scope = ChatSessionScope.Mine, CancellationToken ct = default)` — Task 6 (`ChatSearchService`) calls this exact signature.

`EfChatMessageRepository.SearchByContentAsync` today hardcodes `s.OwnerId == ownerId` (already owner-only — this is *not* part of the reported leak, content search was already safe). This task adds the ability to opt into `Participated` for symmetry with title search, per the approved spec.

**5a. `IChatMessageRepository.cs`** — update the signature:

```csharp
Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
    Guid actorId,
    string query,
    Guid? projectId,
    int limit,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
);
```

(Add `using HydraForge.Application.Chat;` is not needed — `ChatSessionScope` is already in this same namespace, `HydraForge.Application.Chat`.)

**5b. `EfChatMessageRepository.cs`** — replace the method body. It needs access to `ProjectMembers` the same way `EfChatSessionRepository.WhereParticipatedIn` does:

```csharp
public async Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
    Guid actorId,
    string query,
    Guid? projectId,
    int limit,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
)
{
    if (string.IsNullOrWhiteSpace(query))
        return [];

    var sessionIds = context
        .ChatSessions.Where(s => s.ArchivedAt == null)
        .Where(s =>
            scope == ChatSessionScope.Participated
                ? s.OwnerId == actorId
                    || (s.ProjectId != null
                        && context.ProjectMembers.Any(m =>
                            m.ProjectId == s.ProjectId && m.UserId == actorId))
                : s.OwnerId == actorId
        )
        .Where(s => !projectId.HasValue || s.ProjectId == projectId.Value)
        .Select(s => s.Id);

    return await context
        .ChatMessages.Where(m => sessionIds.Contains(m.SessionId))
        .Where(m => EF.Functions.ILike(m.Content, $"%{query}%"))
        .Take(limit)
        .ToListAsync(ct);
}
```

**5c. Update the four trivial fakes.** Each of `ChatMessageServiceTests.cs`, `LlmChatSummaryGeneratorTests.cs`, `ChatSessionServiceTests.cs`, `ChatSessionsControllerTests.cs`'s `FakeMessageRepo`/`TestChatMessageRepository` has a `SearchByContentAsync` stub returning `Task.FromResult<IReadOnlyList<ChatMessage>>([])`. Update each signature to:

```csharp
public Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
    Guid ownerId,
    string query,
    Guid? projectId,
    int limit,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);
```

(parameter name `ownerId` vs `actorId` doesn't matter for interface satisfaction — keep whatever each file already uses to minimize the diff.)

- [ ] **Step 1: Check whether `tests/HydraForge.Infrastructure.Tests/Chat/EfChatMessageRepositoryTests.cs` already exists.**

Run: `find tests/HydraForge.Infrastructure.Tests/Chat -iname "EfChatMessageRepositoryTests.cs"`

If it exists, read it fully and follow its existing test/gating pattern for the new test below. If it doesn't exist, create it following the exact same shape as `EfChatSessionRepositoryTests.cs` (`CreateOptions` helper, `HYDRAFORGE_TEST_CONNECTION_STRING` gate, `Implements_I...Repository` smoke test).

- [ ] **Step 2: Add (or create-and-add) this test:**

```csharp
[Fact]
public async Task SearchByContentAsync_ScopeParticipated_IncludesProjectMemberSessionMatch()
{
    string? connectionString = Environment.GetEnvironmentVariable(
        "HYDRAFORGE_TEST_CONNECTION_STRING"
    );
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    var options = CreateOptions(connectionString);
    using var context = new HydraForgeDbContext(options);
    var repo = new EfChatMessageRepository(context);

    var ownerId = Guid.NewGuid();
    var memberId = Guid.NewGuid();
    var projectId = Guid.NewGuid();

    var session = new ChatSession
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        ProjectId = projectId,
        Title = "Project chat",
        Status = ChatSessionStatus.Active,
    };
    var message = new ChatMessage
    {
        Id = Guid.NewGuid(),
        SessionId = session.Id,
        Role = MessageRole.User,
        Content = "Let's discuss the widget rollout.",
    };
    var projectMember = new HydraForge.Domain.Entities.ProjectSpace.ProjectMember
    {
        ProjectId = projectId,
        UserId = memberId,
        Role = HydraForge.Domain.Enums.MemberRole.Member,
    };

    context.ChatSessions.Add(session);
    context.ChatMessages.Add(message);
    context.ProjectMembers.Add(projectMember);
    await context.SaveChangesAsync();

    var mineResults = await repo.SearchByContentAsync(memberId, "widget", null, 20);
    Assert.Empty(mineResults);

    var participatedResults = await repo.SearchByContentAsync(
        memberId,
        "widget",
        null,
        20,
        ChatSessionScope.Participated
    );
    Assert.Single(participatedResults);
    Assert.Equal(message.Id, participatedResults[0].Id);
}
```

- [ ] **Step 3: Run it.**

Run: `dotnet test tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SearchByContentAsync"`
Expected: PASS (trivially, if no real Postgres connection string is set — same caveat as Task 1's EF tests)

- [ ] **Step 4: Build and run the full suite.**

Run: `dotnet build HydraForge.slnx -nologo -v q && dotnet test HydraForge.slnx -nologo -v q`
Expected: Build succeeds, all projects `Passed!`.

- [ ] **Step 5: Format and commit.**

```bash
dotnet csharpier format .
git add src/HydraForge.Application/Chat/IChatMessageRepository.cs src/HydraForge.Infrastructure/Chat/EfChatMessageRepository.cs tests/HydraForge.Application.Tests/Chat/ChatMessageServiceTests.cs tests/HydraForge.Application.Tests/Chat/LlmChatSummaryGeneratorTests.cs tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs tests/HydraForge.Server.Tests/Chat/ChatSessionsControllerTests.cs tests/HydraForge.Infrastructure.Tests/Chat/EfChatMessageRepositoryTests.cs
git commit -m "feat(chat): IChatMessageRepository.SearchByContentAsync accepts scope"
```

---

### Task 6: Backend — `ChatSearchService`/`IChatSearchService`/`ChatSearchController` scope wiring

**Files:**
- Modify: `src/HydraForge.Application/Chat/IChatSearchService.cs`
- Modify: `src/HydraForge.Application/Chat/ChatSearchService.cs`
- Modify: `src/HydraForge.Server/Controllers/Chat/ChatSearchController.cs`
- Modify: `tests/HydraForge.Application.Tests/Chat/ChatSearchServiceTests.cs`

**Interfaces:**
- Consumes: `IChatSessionRepository.SearchByTitleAsync`, `IChatMessageRepository.SearchByContentAsync` (Tasks 1, 5).
- Produces: `GET /api/chat/search?q=&projectId=&scope=mine|participated`.

**6a. `IChatSearchService.cs`**:

```csharp
Task<IReadOnlyList<ChatSearchResultDto>> SearchAsync(
    Guid userId,
    string query,
    Guid? projectId = null,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
);
```

**6b. `ChatSearchService.SearchAsync`** — replace the two hardcoded-`isAdmin`/no-scope repo calls at the top of the method:

```csharp
public async Task<IReadOnlyList<ChatSearchResultDto>> SearchAsync(
    Guid userId,
    string query,
    Guid? projectId = null,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
)
{
    var titleResults = await _sessionRepo.SearchByTitleAsync(
        userId,
        query,
        projectId,
        MaxResults,
        isAdmin: false,
        scope,
        ct
    );
    var contentResults = await _messageRepo.SearchByContentAsync(
        userId,
        query,
        projectId,
        MaxResults,
        scope,
        ct
    );

    // ... rest of the method (seen/results/sessionTitles/BuildSnippet loops) is unchanged
```

(Everything from `var seen = new HashSet<Guid>();` onward in the current method stays exactly as-is — only the two repository calls at the top change.)

**6c. `ChatSearchController.cs`** — add the query param:

```csharp
[HttpGet]
[ProducesResponseType(typeof(IReadOnlyList<ChatSearchResultDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> Search(
    [FromQuery] string q,
    [FromQuery] Guid? projectId = null,
    [FromQuery] ChatSessionScope scope = ChatSessionScope.Mine
)
{
    var userId = User.GetRequiredUserId();
    var results = await searchService.SearchAsync(userId, q, projectId, scope);
    return Ok(results);
}
```

- [ ] **Step 1: Write the failing tests.** Add to `ChatSearchServiceTests.cs`, after `SearchAsync_NoResults_ReturnsEmptyList`. These require `FakeSessionRepo`/`FakeMessageRepo` in this file to become scope-aware first (Step 2 below) — write both together since the test won't compile otherwise:

```csharp
[Fact]
public async Task SearchAsync_DefaultScope_ExcludesParticipatedOnlyTitleMatch()
{
    var (service, sessionRepo, _) = CreateSut();
    var ownerId = NewId();
    var memberId = NewId();
    var projectId = NewId();

    sessionRepo.Sessions.Add(new ChatSession
    {
        Id = NewId(),
        OwnerId = ownerId,
        ProjectId = projectId,
        Title = "Widget rollout plan",
        Status = ChatSessionStatus.Active,
    });
    sessionRepo.Memberships.Add((projectId, memberId));

    var results = await service.SearchAsync(memberId, "Widget");

    Assert.Empty(results);
}

[Fact]
public async Task SearchAsync_ScopeParticipated_IncludesProjectMemberTitleMatch()
{
    var (service, sessionRepo, _) = CreateSut();
    var ownerId = NewId();
    var memberId = NewId();
    var projectId = NewId();

    var session = new ChatSession
    {
        Id = NewId(),
        OwnerId = ownerId,
        ProjectId = projectId,
        Title = "Widget rollout plan",
        Status = ChatSessionStatus.Active,
    };
    sessionRepo.Sessions.Add(session);
    sessionRepo.Memberships.Add((projectId, memberId));

    var results = await service.SearchAsync(memberId, "Widget", scope: ChatSessionScope.Participated);

    Assert.Single(results);
    Assert.Equal(session.Id, results[0].SessionId);
}
```

- [ ] **Step 2: Make `FakeSessionRepo`/`FakeMessageRepo` in `ChatSearchServiceTests.cs` scope-aware.** Add a `Memberships` list to `FakeSessionRepo` and update `SearchByTitleAsync`'s signature/body:

```csharp
private sealed class FakeSessionRepo : IChatSessionRepository
{
    public List<ChatSession> Sessions { get; } = [];
    public List<(Guid ProjectId, Guid UserId)> Memberships { get; } = [];

    // ... GetByIdAsync, GetActiveByPanelAsync, ListAsync, CountAsync, AddAsync, UpdateAsync, AddCardChatLinkAsync unchanged ...

    public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
        Guid ownerId,
        string query,
        Guid? projectId,
        int limit,
        bool isAdmin = false,
        ChatSessionScope scope = ChatSessionScope.Mine,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<ChatSession>>([]);

        var results = Sessions
            .Where(s =>
                (scope == ChatSessionScope.Participated
                    ? s.OwnerId == ownerId
                        || (s.ProjectId.HasValue && Memberships.Contains((s.ProjectId.Value, ownerId)))
                    : s.OwnerId == ownerId)
                && s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                && s.ArchivedAt == null
                && (!projectId.HasValue || s.ProjectId == projectId)
            )
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<ChatSession>>(results);
    }
}
```

Also update `ListAsync`/`CountAsync` in this same `FakeSessionRepo` to accept the two new Task-1 params (they're unused stubs here, same trivial-fake treatment as Task 2 — return `[]`/`0` regardless):

```csharp
public Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

public Task<int> CountAsync(
    Guid ownerId,
    Guid? folderId,
    Guid? projectId,
    bool isAdmin = false,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    ChatSessionScope scope = ChatSessionScope.Mine,
    IReadOnlySet<ChatSessionKind>? types = null,
    CancellationToken ct = default
) => Task.FromResult(0);
```

`FakeMessageRepo.SearchByContentAsync` in this same file already has real owner-only filtering logic (it queries `_sessionRepo.Sessions` directly) — update its signature to accept `scope` and branch the same way `EfChatMessageRepository` does in Task 5b:

```csharp
public Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
    Guid ownerId,
    string query,
    Guid? projectId,
    int limit,
    ChatSessionScope scope = ChatSessionScope.Mine,
    CancellationToken ct = default
)
{
    if (string.IsNullOrWhiteSpace(query))
        return Task.FromResult<IReadOnlyList<ChatMessage>>([]);

    var sessionIds = _sessionRepo
        .Sessions.Where(s =>
            (scope == ChatSessionScope.Participated
                ? s.OwnerId == ownerId
                    || (s.ProjectId.HasValue && _sessionRepo.Memberships.Contains((s.ProjectId.Value, ownerId)))
                : s.OwnerId == ownerId)
            && s.ArchivedAt == null
            && (!projectId.HasValue || s.ProjectId == projectId)
        )
        .Select(s => s.Id)
        .ToHashSet();

    var results = Messages
        .Where(m =>
            sessionIds.Contains(m.SessionId)
            && m.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
        )
        .ToList();

    return Task.FromResult<IReadOnlyList<ChatMessage>>(results);
}
```

- [ ] **Step 3: Apply 6a and 6b, then run the new tests.**

Run: `dotnet test tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj --filter "FullyQualifiedName~ChatSearchServiceTests"`
Expected: All tests PASS, including the 2 new ones.

- [ ] **Step 4: Apply 6c. Build and run the full suite.**

Run: `dotnet build HydraForge.slnx -nologo -v q && dotnet test HydraForge.slnx -nologo -v q`
Expected: Build succeeds, all projects `Passed!`.

- [ ] **Step 5: Format and commit.**

```bash
dotnet csharpier format .
git add src/HydraForge.Application/Chat/IChatSearchService.cs src/HydraForge.Application/Chat/ChatSearchService.cs src/HydraForge.Server/Controllers/Chat/ChatSearchController.cs tests/HydraForge.Application.Tests/Chat/ChatSearchServiceTests.cs
git commit -m "fix(chat): GET /api/chat/search defaults to owner-only scope"
```

**Backend is now complete and independently deployable/testable** — the API defaults to safe behavior with no frontend changes required. Tasks 7–11 build the UI on top of it.

---

### Task 7: Frontend — `routes.ts` gains `scope`/`types` params

**Files:**
- Modify: `src/web-ui/app/lib/routes.ts`
- Modify: `src/web-ui/app/components/project/ProjectChatsTab.vue`

**Interfaces:**
- Produces: `ApiRoutes.Chat.sessions.list(folderId?, projectId?, before?, beforeId?, limit?, status?, scope?, types?): string` and `ApiRoutes.Chat.search(q, projectId?, scope?): string` — Task 8 (`useChatSessionList`) calls the `sessions.list` signature; this same task's own 7c uses it too for `ProjectChatsTab.vue`.

**7a. Update `ApiRoutes.Chat.sessions.list`** (inside the `Chat.sessions` object in `routes.ts`):

```typescript
list: (folderId?: string, projectId?: string, before?: string, beforeId?: string, limit = 20, status?: string, scope?: string, types?: string) =>
  `/api/chat/sessions?${folderId ? `folderId=${folderId}&` : ''}${projectId ? `projectId=${projectId}&` : ''}${before ? `before=${before}&` : ''}${beforeId ? `beforeId=${beforeId}&` : ''}${status ? `status=${status}&` : ''}${scope ? `scope=${scope}&` : ''}${types ? `types=${types}&` : ''}limit=${limit}`,
```

**7b. Update `ApiRoutes.Chat.search`** (top-level `Chat.search`, not nested under `sessions`):

```typescript
search: (q: string, projectId?: string, scope?: string) =>
  `/api/chat/search?q=${encodeURIComponent(q)}${projectId ? `&projectId=${projectId}` : ''}${scope ? `&scope=${scope}` : ''}`,
```

**7c. `ProjectChatsTab.vue`'s `fetchSessions()`** already calls `ApiRoutes.Chat.sessions.list(undefined, props.projectId, undefined, undefined, 50)` directly (5 positional args) — its whole purpose is showing every project member's chats, which is exactly what `scope=participated` means. Add the 6th positional arg (skip `status`, pass `undefined`) and 7th (`scope`):

```typescript
const url = ApiRoutes.Chat.sessions.list(undefined, props.projectId, undefined, undefined, 50, undefined, 'participated')
```

This is the **only** change to this file in this task — without it, `ProjectChatsTab.vue` would silently start showing only the caller's own chats once Task 3/4 ship, which is a regression of already-working behavior. (No other task touches this file.)

- [ ] **Step 1: Apply 7a, 7b, 7c.**

- [ ] **Step 2: Typecheck and lint.**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: Both clean, no errors.

- [ ] **Step 3: Run the full frontend test suite** (no behavior changed yet for anything other than `ProjectChatsTab`'s URL — confirm nothing broke):

Run: `cd src/web-ui && pnpm test`
Expected: All test files pass, same counts as before this task.

- [ ] **Step 4: Commit.**

```bash
cd src/web-ui
git add app/lib/routes.ts app/components/project/ProjectChatsTab.vue
git commit -m "feat(chat): ApiRoutes gain scope/types params; ProjectChatsTab opts into participated scope"
```

---

### Task 8: Frontend — `useChatSessionList.ts` scope + types state, and the hand-rolled status query fix

**Files:**
- Modify: `src/web-ui/app/composables/useChatSessionList.ts`
- Modify: `src/web-ui/app/composables/__tests__/useChatSessionList.test.ts`

**Interfaces:**
- Consumes: `ApiRoutes.Chat.sessions.list` (Task 7).
- Produces: `useChatSessionList(options?)` now also returns `scope: Ref<'mine' | 'participated'>` and `types: Ref<Set<ChatType>>` alongside the existing `sessions`/`loading`/`hasMore`/`statusFilter`/`loadMore`/`refresh`/`patchSession`/`prependSession`/`removeSession`. Task 9 (`ChatSessionFilters.vue`) binds directly to `scope`/`types`/`statusFilter`. Task 10/11 (`pages/chats/index.vue`, `ChatDockHistory.vue`) both destructure the new `scope`/`types` return values and pass them into `ChatSessionFilters`.

Replace the whole file:

```typescript
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'
import type { ChatType } from '~/lib/chat-type'

interface ChatSessionPageDto {
  items: ChatSessionDto[]
  totalCount: number
}

export function useChatSessionList(options?: { folderId?: string, projectId?: string }) {
  const sessions = ref<ChatSessionDto[]>([])
  const loading = ref(false)
  const hasMore = ref(true)
  // 'NonArchived' is deliberately NOT offered here — it stays in the
  // backend enum for internal use (folder cascade-archive), but the UI only
  // presents the linear lifecycle: Active (default) → Closed → Archived.
  const statusFilter = ref<'Active' | 'Closed' | 'Archived'>('Active')
  // Personal history defaults to the caller's own chats — 'participated' is
  // an explicit opt-in to also see project chats owned by other members
  // (see docs/specs/2026-08-09-chat-history-scope-type-filters-design.md).
  const scope = ref<'mine' | 'participated'>('mine')
  // All three types selected by default (no filtering). A Set so
  // ChatSessionFilters.vue can toggle membership directly.
  const types = ref<Set<ChatType>>(new Set(['normal', 'project', 'card']))
  const api = useApi()

  async function loadMore() {
    if (loading.value || !hasMore.value) return
    loading.value = true
    try {
      const last = sessions.value[sessions.value.length - 1]
      const typesParam = types.value.size < 3 ? [...types.value].join(',') : undefined
      const url = ApiRoutes.Chat.sessions.list(
        options?.folderId,
        options?.projectId,
        last?.updatedAt,
        last?.id,
        20,
        statusFilter.value,
        scope.value,
        typesParam
      )
      const { data } = await api.GET<ChatSessionPageDto>(url)
      if (data) {
        sessions.value.push(...data.items)
        hasMore.value = sessions.value.length < data.totalCount
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

  // In-place update for a single session already in the loaded list — avoids
  // dropping the caller back to page 1 (which a full refresh() would do) just
  // to reflect a title/status change on an item that's already visible.
  function patchSession(id: string, patch: Partial<ChatSessionDto>) {
    const index = sessions.value.findIndex(s => s.id === id)
    if (index !== -1) sessions.value[index] = { ...sessions.value[index]!, ...patch }
  }

  // A brand-new session always sorts newest-first — prepend it locally instead
  // of refresh()ing. refresh() resets to page 1, which visibly "disappears"
  // any already-loaded pages 2+ if the user had scrolled into older history —
  // this keeps everything they'd already loaded intact.
  function prependSession(session: ChatSessionDto) {
    sessions.value.unshift(session)
  }

  // Same reasoning as prependSession — removing an archived item locally
  // avoids collapsing back to page 1 for anyone who'd scrolled further.
  function removeSession(id: string) {
    const index = sessions.value.findIndex(s => s.id === id)
    if (index !== -1) sessions.value.splice(index, 1)
  }

  // Load initial page
  loadMore()

  return {
    sessions: readonly(sessions),
    loading: readonly(loading),
    hasMore: readonly(hasMore),
    statusFilter,
    scope,
    types,
    loadMore,
    refresh,
    patchSession,
    prependSession,
    removeSession
  }
}
```

Key behavior notes for whoever reviews this:
- `typesParam` is only sent when fewer than all 3 types are selected — omitting it entirely when everything's checked matches the backend's "empty/omitted = no filtering" contract from Task 1/4, and keeps the common case's URL shorter.
- The old hand-appended `` `${base}&status=${statusFilter.value}` `` string is gone — `status` now goes through `ApiRoutes.Chat.sessions.list`'s own param, matching how `scope`/`types` are passed. This is the D4 bug fix from the spec.

- [ ] **Step 1: Write the failing tests.** Add to `useChatSessionList.test.ts`, after the existing `'defaults to the Active filter and sends it as a query param'` test:

```typescript
it('defaults to mine scope and does not send a types param when all three are selected', async () => {
  mockGET.mockResolvedValue({
    data: { items: [], totalCount: 0 },
    error: undefined
  })
  const { useChatSessionList } = await import('~/composables/useChatSessionList')
  const { scope, types } = useChatSessionList()
  await flushMicrotasks()
  expect(scope.value).toBe('mine')
  expect(types.value.size).toBe(3)
  const calledUrl = mockGET.mock.calls[0]?.[0] as string
  expect(calledUrl).toContain('scope=mine')
  expect(calledUrl).not.toContain('types=')
})

it('sends types param when a subset of types is selected', async () => {
  mockGET.mockResolvedValue({
    data: { items: [], totalCount: 0 },
    error: undefined
  })
  const { useChatSessionList } = await import('~/composables/useChatSessionList')
  const { types, refresh } = useChatSessionList()
  await flushMicrotasks()
  types.value = new Set(['project', 'card'])
  await refresh()
  const calledUrl = mockGET.mock.calls.at(-1)?.[0] as string
  expect(calledUrl).toContain('types=project,card')
})

it('refetches when scope changes to participated', async () => {
  mockGET.mockResolvedValue({
    data: { items: [], totalCount: 0 },
    error: undefined
  })
  const { useChatSessionList } = await import('~/composables/useChatSessionList')
  const { scope, refresh } = useChatSessionList()
  await flushMicrotasks()
  scope.value = 'participated'
  await refresh()
  const calledUrl = mockGET.mock.calls.at(-1)?.[0] as string
  expect(calledUrl).toContain('scope=participated')
})
```

- [ ] **Step 2: Run to verify they fail** (the composable doesn't export `scope`/`types` yet):

Run: `cd src/web-ui && pnpm test -- useChatSessionList`
Expected: FAIL — `scope`/`types` are `undefined` on the returned object.

- [ ] **Step 3: Apply the full file replacement above.**

- [ ] **Step 4: Run again.**

Run: `cd src/web-ui && pnpm test -- useChatSessionList`
Expected: All tests PASS, including the 3 new ones and all 7 pre-existing ones.

- [ ] **Step 5: Typecheck, lint, full test suite.**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test`
Expected: All clean/passing.

- [ ] **Step 6: Commit.**

```bash
cd src/web-ui
git add app/composables/useChatSessionList.ts app/composables/__tests__/useChatSessionList.test.ts
git commit -m "feat(chat): useChatSessionList gains scope + types filter state

Also fixes a pre-existing bug where this composable hand-built its own
&status= query fragment instead of using ApiRoutes.Chat.sessions.list's
existing status param — status/scope/types now all go through the same
route helper."
```

---

### Task 9: Frontend — new shared `ChatSessionFilters.vue`

**Files:**
- Create: `src/web-ui/app/components/chat/ChatSessionFilters.vue`
- Create: `src/web-ui/app/components/chat/__tests__/ChatSessionFilters.test.ts`

**Interfaces:**
- Consumes: `ChatType`/`CHAT_TYPE_BADGE` from `~/lib/chat-type` (existing, unchanged).
- Produces: a component with props `status: 'Active' | 'Closed' | 'Archived'`, `types: Set<ChatType>`, `scope: 'mine' | 'participated'`, and emits `update:status`, `update:types`, `update:scope` — standard Vue `v-model` triple, used as `v-model:status="statusFilter" v-model:types="types" v-model:scope="scope"` by Tasks 10 and 11 directly against `useChatSessionList`'s refs.

Layout (approved in the design conversation): Status + Type selects side by side on top, Mine/Participated segmented control full-width below.

```vue
<script setup lang="ts">
import { CHAT_TYPE_BADGE } from '~/lib/chat-type'
import type { ChatType } from '~/lib/chat-type'

const props = defineProps<{
  status: 'Active' | 'Closed' | 'Archived'
  types: Set<ChatType>
  scope: 'mine' | 'participated'
}>()

const emit = defineEmits<{
  'update:status': [value: 'Active' | 'Closed' | 'Archived']
  'update:types': [value: Set<ChatType>]
  'update:scope': [value: 'mine' | 'participated']
}>()

// Linear lifecycle only — 'NonArchived' stays in the backend enum for
// internal use but is not offered as a UI filter.
const statusItems = [
  { label: 'Active', value: 'Active' },
  { label: 'Closed', value: 'Closed' },
  { label: 'Archived', value: 'Archived' }
]

const typeItems = (Object.keys(CHAT_TYPE_BADGE) as ChatType[]).map(value => ({
  label: CHAT_TYPE_BADGE[value].label,
  value
}))

// USelectMenu's v-model wants the array of selected values directly.
const selectedTypes = computed({
  get: () => [...props.types],
  set: (value: ChatType[]) => {
    // Never allow zero types selected — that's ambiguous (show nothing vs
    // show everything) and surprising either way. Ignore the change instead
    // of silently falling back to "all", so the UI doesn't visibly snap back.
    if (value.length === 0) return
    emit('update:types', new Set(value))
  }
})

const typeLabel = computed(() => {
  if (props.types.size === typeItems.length) return `All (${typeItems.length})`
  if (props.types.size === 1) {
    const only = [...props.types][0]!
    return CHAT_TYPE_BADGE[only].label
  }
  return `${props.types.size} selected`
})
</script>

<template>
  <div class="space-y-2">
    <div class="grid grid-cols-2 gap-2">
      <USelect
        :model-value="status"
        :items="statusItems"
        size="xs"
        @update:model-value="(v: unknown) => emit('update:status', v as 'Active' | 'Closed' | 'Archived')"
      />
      <USelectMenu
        :model-value="selectedTypes"
        :items="typeItems"
        multiple
        size="xs"
        @update:model-value="(v: unknown) => { selectedTypes = v as ChatType[] }"
      >
        <template #default>
          {{ typeLabel }}
        </template>
      </USelectMenu>
    </div>
    <div class="grid grid-cols-2 gap-0 rounded-md overflow-hidden border border-gray-200 dark:border-gray-700">
      <button
        type="button"
        class="text-xs py-1 transition-colors"
        :class="scope === 'mine' ? 'bg-primary text-white' : 'bg-transparent hover:bg-gray-50 dark:hover:bg-gray-800'"
        @click="emit('update:scope', 'mine')"
      >
        Mine
      </button>
      <button
        type="button"
        class="text-xs py-1 transition-colors border-l border-gray-200 dark:border-gray-700"
        :class="scope === 'participated' ? 'bg-primary text-white' : 'bg-transparent hover:bg-gray-50 dark:hover:bg-gray-800'"
        @click="emit('update:scope', 'participated')"
      >
        Participated
      </button>
    </div>
  </div>
</template>
```

- [ ] **Step 1: Write the failing test.** Create `ChatSessionFilters.test.ts`:

```typescript
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ChatSessionFilters from '~/components/chat/ChatSessionFilters.vue'

describe('ChatSessionFilters', () => {
  it('renders Mine as active when scope is mine', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set(['normal', 'project', 'card']), scope: 'mine' }
    })
    const buttons = wrapper.findAll('button')
    const mineButton = buttons.find(b => b.text() === 'Mine')!
    expect(mineButton.classes()).toContain('bg-primary')
  })

  it('emits update:scope when Participated is clicked', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set(['normal', 'project', 'card']), scope: 'mine' }
    })
    const buttons = wrapper.findAll('button')
    const participatedButton = buttons.find(b => b.text() === 'Participated')!
    await participatedButton.trigger('click')
    expect(wrapper.emitted('update:scope')).toEqual([['participated']])
  })

  it('shows "All (3)" when every type is selected', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set(['normal', 'project', 'card']), scope: 'mine' }
    })
    expect(wrapper.text()).toContain('All (3)')
  })

  it('shows the single type label when only one type is selected', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set(['card']), scope: 'mine' }
    })
    expect(wrapper.text()).toContain('Card')
  })

  it('emits update:status when the status select changes', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set(['normal', 'project', 'card']), scope: 'mine' }
    })
    await wrapper.findComponent({ name: 'USelect' }).vm.$emit('update:model-value', 'Closed')
    expect(wrapper.emitted('update:status')).toEqual([['Closed']])
  })
})
```

- [ ] **Step 2: Run to verify they fail** (component doesn't exist yet):

Run: `cd src/web-ui && pnpm test -- ChatSessionFilters`
Expected: FAIL — cannot resolve `~/components/chat/ChatSessionFilters.vue`.

- [ ] **Step 3: Create the component as shown above.**

- [ ] **Step 4: Run again.**

Run: `cd src/web-ui && pnpm test -- ChatSessionFilters`
Expected: PASS. If the `USelect`/`USelectMenu` component-name lookup in the last test doesn't resolve the way this plan assumes (Nuxt UI internals sometimes name components differently than their public tag), adjust the test to target `wrapper.find('select')` or whatever DOM element `pnpm test`'s failure output points at — the assertion's *intent* (status change emits `update:status`) is what matters, not the exact selector.

- [ ] **Step 5: Typecheck, lint, full test suite.**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test`
Expected: All clean/passing.

- [ ] **Step 6: Commit.**

```bash
cd src/web-ui
git add app/components/chat/ChatSessionFilters.vue app/components/chat/__tests__/ChatSessionFilters.test.ts
git commit -m "feat(chat): add shared ChatSessionFilters component (status/type/scope)"
```

---

### Task 10: Frontend — wire `ChatSessionFilters` into `/chats`

**Files:**
- Modify: `src/web-ui/app/pages/chats/index.vue`

**Interfaces:**
- Consumes: `ChatSessionFilters` (Task 9), `useChatSessionList`'s `scope`/`types` (Task 8).

**10a.** Import the new component:

```typescript
import ChatSessionFilters from '~/components/chat/ChatSessionFilters.vue'
```

**10b.** Destructure `scope`/`types` from `useChatSessionList()` alongside the existing values:

```typescript
const { sessions, loading, hasMore, statusFilter, scope, types, loadMore, refresh, patchSession, prependSession, removeSession } = useChatSessionList()
```

**10c.** Delete the now-unused `statusFilterItems` constant (lines 26-30 in the current file) — that array moves into `ChatSessionFilters.vue` (Task 9).

**10d.** Update the `watch` that triggers refetch — it currently only watches `statusFilter`:

```typescript
watch([statusFilter, scope, types], () => {
  refresh()
}, { deep: true })
```

(`{ deep: true }` is required because `types` is a `Set` — a `.add()`/`.delete()`-style mutation wouldn't otherwise trigger the watcher, though `ChatSessionFilters.vue`'s `update:types` always emits a brand-new `Set` via `new Set(value)`, so a shallow watch would technically also work here; `deep: true` is the safer, more obviously-correct choice and costs nothing at this data size.)

**10e.** Replace the filter markup (currently a bare `<USelect v-model="statusFilter" :items="statusFilterItems" size="xs" />` inside a `<div class="shrink-0 px-4 pb-3">`) with:

```vue
<div class="shrink-0 px-4 pb-3">
  <ChatSessionFilters
    :status="statusFilter"
    :types="types"
    :scope="scope"
    @update:status="statusFilter = $event"
    @update:types="types = $event"
    @update:scope="scope = $event"
  />
</div>
```

- [ ] **Step 1: Apply 10a–10e.**

- [ ] **Step 2: Typecheck and lint.**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: Clean.

- [ ] **Step 3: Manual verification** (this page has no existing dedicated test file — check first):

Run: `find src/web-ui/app/pages/chats -iname "*.test.ts"`

If none exists, this step is manual: start the dev server (`pnpm dev`), navigate to `/chats`, confirm the filter bar renders as two rows (Status+Type, then Mine/Participated), and that toggling each control refetches the list (watch the Network tab for a new `GET /api/chat/sessions?...` request with the expected `scope`/`types`/`status` values). If a test file *does* exist, extend it with an equivalent assertion instead of skipping this step.

- [ ] **Step 4: Run the full frontend test suite.**

Run: `cd src/web-ui && pnpm test`
Expected: All passing.

- [ ] **Step 5: Commit.**

```bash
cd src/web-ui
git add app/pages/chats/index.vue
git commit -m "feat(chat): /chats page uses the shared ChatSessionFilters bar"
```

---

### Task 11: Frontend — wire `ChatSessionFilters` into the FAB's `ChatDockHistory.vue`

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatDockHistory.vue`

**Interfaces:**
- Consumes: `ChatSessionFilters` (Task 9), `useChatSessionList`'s `scope`/`types`/`statusFilter` (Task 8).

This panel currently has **zero** filter controls — it destructures only `{ sessions, loading, hasMore, loadMore }` from `useChatSessionList()`. Replace the whole file:

```vue
<script setup lang="ts">
import { useChatSessionList } from '~/composables/useChatSessionList'
import ChatSessionList from '~/components/shared/ChatSessionList.vue'
import ChatSessionFilters from '~/components/chat/ChatSessionFilters.vue'
import { getChatType, CHAT_TYPE_BADGE } from '~/lib/chat-type'

const emit = defineEmits<{
  select: [sessionId: string]
  back: []
}>()

const { sessions, loading, hasMore, statusFilter, scope, types, loadMore, refresh } = useChatSessionList()

watch([statusFilter, scope, types], () => {
  refresh()
}, { deep: true })
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
    <div class="px-4 py-2 border-b border-gray-200 dark:border-gray-700">
      <ChatSessionFilters
        :status="statusFilter"
        :types="types"
        :scope="scope"
        @update:status="statusFilter = $event"
        @update:types="types = $event"
        @update:scope="scope = $event"
      />
    </div>
    <ChatSessionList
      :sessions="sessions"
      :loading="loading"
      :has-more="hasMore"
      @select="emit('select', $event)"
      @load-more="loadMore"
    >
      <template #item="{ session }">
        <div
          class="px-4 py-3 border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800 cursor-pointer"
          @click="emit('select', session.id)"
        >
          <div class="flex items-center gap-1.5 min-w-0">
            <span class="text-sm font-medium truncate">
              {{ session.title || 'New Chat' }}
            </span>
            <UBadge
              :color="CHAT_TYPE_BADGE[getChatType(session)].color"
              variant="subtle"
              size="xs"
              class="shrink-0"
            >
              {{ CHAT_TYPE_BADGE[getChatType(session)].label }}
            </UBadge>
          </div>
          <div class="text-xs text-muted truncate mt-0.5">
            {{ session.summary || 'No messages' }}
          </div>
        </div>
      </template>
    </ChatSessionList>
  </div>
</template>
```

(Only the `<script setup>` imports/destructure/new `watch`, and the new filter-bar `<div>` right after the header, are additions — the header markup and the `#item` slot template are byte-for-byte unchanged from the current file.)

- [ ] **Step 1: Apply the replacement above.**

- [ ] **Step 2: Typecheck and lint.**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: Clean.

- [ ] **Step 3: Check for an existing test file and extend or manually verify, same as Task 10 Step 3.**

Run: `find src/web-ui/app/components/chat/__tests__ -iname "ChatDockHistory*"`

If a test file exists, read it and add an equivalent filter-bar assertion. If not: start the dev server, open the FAB, click into History, confirm the same two-row filter bar now appears between the header and the session list, and that changing any control triggers a refetch.

- [ ] **Step 4: Run the full frontend test suite.**

Run: `cd src/web-ui && pnpm test`
Expected: All passing.

- [ ] **Step 5: Commit.**

```bash
cd src/web-ui
git add app/components/chat/ChatDockHistory.vue
git commit -m "feat(chat): FAB history panel gains the shared ChatSessionFilters bar

Previously had no filtering at all — not even the pre-existing Status
control that /chats already had."
```

---

## Post-plan verification (run once, after Task 11)

- [ ] `dotnet build HydraForge.slnx -nologo -v q` — 0 warnings, 0 errors
- [ ] `dotnet csharpier check .` — clean
- [ ] `dotnet test HydraForge.slnx -nologo -v q` — all projects `Passed!`
- [ ] `cd src/web-ui && pnpm typecheck` — clean
- [ ] `cd src/web-ui && pnpm lint` — clean
- [ ] `cd src/web-ui && pnpm test` — all passing
- [ ] Manual smoke test against a real running stack (`pnpm dev` + `dotnet run --project src/HydraForge.Server`), using two different logged-in users where one owns a project chat and the other is only a project member:
  1. As the member: `/chats` shows **no** trace of the owner's project chat by default.
  2. As the member: switching the filter bar to **Participated** makes it appear.
  3. As the member: the project's own **Board → Chats** tab shows it regardless of the Scope toggle (unaffected — always participated).
  4. Type filter: unchecking "Chat" hides personal chats from the list; the last remaining checked type cannot be unchecked.
  5. Same four checks, repeated inside the FAB's History panel.
