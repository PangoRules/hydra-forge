# Plan 11: Audit Log Reader + Controller

**Branch:** `task/audit-log-reader`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 11

## Task

Create `IAuditLogReader` port, `EfAuditLogReader` implementation, `AuditLogQuery`/`AuditLogQueryResult`/`AuditLogEntryDto` records. Add audit log endpoint to `AdminController`.

## Files to create

- `src/HydraForge.Application/Audit/IAuditLogReader.cs`
- `src/HydraForge.Application/Audit/AuditLogQuery.cs`
- `src/HydraForge.Infrastructure/Audit/EfAuditLogReader.cs`

## Files to modify

- `src/HydraForge.Server/Controllers/Admin/AdminController.cs` — add audit log endpoint
- `src/HydraForge.Server/Program.cs` — register `IAuditLogReader`

## Implementation steps

### Step 1: Create query/result DTOs

Create `src/HydraForge.Application/Audit/AuditLogQuery.cs`:

```csharp
namespace HydraForge.Application.Audit;

public record AuditLogQuery(
    Guid? ProjectId = null,
    Guid? ActorId = null,
    string? EntityType = null,
    string? Action = null,
    DateTime? From = null,
    DateTime? To = null,
    int Skip = 0,
    int Take = 50
);

public record AuditLogQueryResult(
    IReadOnlyList<AuditLogEntryDto> Items,
    int TotalCount
);

public record AuditLogEntryDto(
    Guid Id,
    Guid? ProjectId,
    Guid ActorId,
    string ActorName,
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    string Scope
);
```

### Step 2: Create IAuditLogReader port

Create `src/HydraForge.Application/Audit/IAuditLogReader.cs`:

```csharp
namespace HydraForge.Application.Audit;

public interface IAuditLogReader
{
    Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}
```

### Step 3: Create EfAuditLogReader

Create `src/HydraForge.Infrastructure/Audit/EfAuditLogReader.cs`:

```csharp
using HydraForge.Application.Audit;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Audit;

public class EfAuditLogReader(HydraForgeDbContext db) : IAuditLogReader
{
    public async Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var q = db.AuditLogEntries.AsQueryable();

        if (query.ProjectId.HasValue)
            q = q.Where(e => e.ProjectId == query.ProjectId.Value);
        if (query.ActorId.HasValue)
            q = q.Where(e => e.ActorId == query.ActorId.Value);
        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(e => e.EntityType == query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(e => e.Action == query.Action);
        if (query.From.HasValue)
            q = q.Where(e => e.Timestamp >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(e => e.Timestamp <= query.To.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(e => e.Timestamp)
            .Skip(query.Skip)
            .Take(query.Take)
            .Join(db.Users,
                entry => entry.ActorId,
                user => user.Id,
                (entry, user) => new AuditLogEntryDto(
                    entry.Id,
                    entry.ProjectId,
                    entry.ActorId,
                    user.Username,
                    entry.EntityType,
                    entry.EntityId,
                    entry.Action,
                    entry.OldValue,
                    entry.NewValue,
                    entry.Timestamp,
                    entry.Scope.ToString()
                ))
            .ToListAsync(ct);

        return new AuditLogQueryResult(items, totalCount);
    }
}
```

### Step 4: Register in DI

In `src/HydraForge.Server/Program.cs`, add:

```csharp
using HydraForge.Application.Audit;
using HydraForge.Infrastructure.Audit;
// ...
builder.Services.AddScoped<IAuditLogReader, EfAuditLogReader>();
```

### Step 5: Add audit log endpoint to AdminController

In `src/HydraForge.Server/Controllers/Admin/AdminController.cs`, add:

```csharp
using HydraForge.Application.Audit;

// Inject IAuditLogReader:
public class AdminController(
    IAdminService adminService,
    ISettingsRepository settingsRepo,
    CachedSettingsProvider settingsProvider,
    IAuditLogReader auditLogReader
) : ControllerBase
{
    [HttpGet("audit-log")]
    public async Task<IActionResult> QueryAuditLog(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? actorId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var query = new AuditLogQuery(projectId, actorId, entityType, action, from, to, skip, take);
        var result = await auditLogReader.QueryAsync(query, ct);
        return Ok(result);
    }
}
```

### Step 6: Write tests

Create `tests/HydraForge.Infrastructure.Tests/Audit/EfAuditLogReaderTests.cs`:

```csharp
using HydraForge.Application.Audit;
using HydraForge.Infrastructure.Audit;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HydraForge.Infrastructure.Tests.Audit;

public class EfAuditLogReaderTests
{
    private static HydraForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HydraForgeDbContext(options);
    }

    [Fact]
    public async Task QueryAsync_EmptyDb_ReturnsEmptyResult()
    {
        using var db = CreateContext();
        var reader = new EfAuditLogReader(db);

        var result = await reader.QueryAsync(new AuditLogQuery());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task QueryAsync_FilterByEntityType_ReturnsMatching()
    {
        using var db = CreateContext();
        db.AuditLogEntries.Add(Domain.Entities.ProjectSpace.AuditLogEntry.Create(
            Guid.NewGuid(), Domain.Enums.AuditLogScope.Project, "Card", Guid.NewGuid(), "Created"));
        db.AuditLogEntries.Add(Domain.Entities.ProjectSpace.AuditLogEntry.Create(
            Guid.NewGuid(), Domain.Enums.AuditLogScope.Project, "Column", Guid.NewGuid(), "Created"));
        await db.SaveChangesAsync();

        var reader = new EfAuditLogReader(db);
        var result = await reader.QueryAsync(new AuditLogQuery { EntityType = "Card" });

        Assert.Single(result.Items);
        Assert.Equal("Card", result.Items[0].EntityType);
    }
}
```

### Step 7: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Infrastructure.Tests/ --filter "FullyQualifiedName~EfAuditLogReaderTests"
dotnet test
```

## Verification

- `dotnet build` — no errors
- `dotnet test` — all tests pass
- Manual: login as admin, `GET /api/admin/audit-log?take=10` → returns audit log entries with actor names

## Dependencies

- Task 9 (AdminController must exist)
- Audit log write path already exists (Phase 2)