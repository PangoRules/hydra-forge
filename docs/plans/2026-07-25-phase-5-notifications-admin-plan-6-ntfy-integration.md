# Plan 6: ntfy Integration + Docker Compose

**Branch:** `task/ntfy-integration`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 6

## Task

Add `INtfyClient` port, `NtfyClient` implementation, `NtfyOptions`. Add `NtfyServerUrl` field to `SystemSettings` entity + migration. Add ntfy service to `docker-compose.yml`. Wire `NtfyClient` into `NotificationService`.

## Files to create

- `src/HydraForge.Application/Notifications/INtfyClient.cs`
- `src/HydraForge.Infrastructure/Notifications/NtfyClient.cs`
- `src/HydraForge.Infrastructure/Notifications/NtfyOptions.cs`

## Files to modify

- `src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs` — add `NtfyServerUrl` field + `UpdateSettings()` instance method
- `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` — update seed data for new fields
- `src/HydraForge.Application/Notifications/NotificationService.cs` — inject `INtfyClient?`, call `PublishAsync` after DB write
- `src/HydraForge.Infrastructure/Notifications/NotificationServiceCollectionExtensions.cs` — register `NtfyClient`
- `docker-compose.yml` — add ntfy service
- `.env.example` — add `NTFY_BASE_URL`

## Implementation steps

### Step 1: Add `NtfyServerUrl` to SystemSettings + `UpdateSettings()` method

In `src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs`, replace the property-bag class:

```csharp
namespace HydraForge.Domain.Entities.PersonalSpace;

public class SystemSettings
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int ArchivedItemRetentionDays { get; private set; } = 730;
    public int AuditLogRetentionDays { get; private set; } = 90;
    public int NotificationRetentionDays { get; private set; } = 30;
    public string? NtfyServerUrl { get; private set; }
    public string? SearXngUrl { get; private set; }
    public string? BrandName { get; private set; }
    public string? BrandLogoUrl { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private SystemSettings() { }

    public void UpdateSettings(
        int? archivedItemRetentionDays = null,
        int? auditLogRetentionDays = null,
        int? notificationRetentionDays = null,
        string? ntfyServerUrl = null,
        string? searXngUrl = null,
        string? brandName = null,
        string? brandLogoUrl = null)
    {
        if (archivedItemRetentionDays.HasValue)
            ArchivedItemRetentionDays = archivedItemRetentionDays.Value;
        if (auditLogRetentionDays.HasValue)
            AuditLogRetentionDays = auditLogRetentionDays.Value;
        if (notificationRetentionDays.HasValue)
            NotificationRetentionDays = notificationRetentionDays.Value;
        if (ntfyServerUrl is not null)
            NtfyServerUrl = ntfyServerUrl;
        if (searXngUrl is not null)
            SearXngUrl = searXngUrl;
        if (brandName is not null)
            BrandName = brandName;
        if (brandLogoUrl is not null)
            BrandLogoUrl = brandLogoUrl;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

### Step 2: Generate EF migration

```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations add AddSystemSettingsFields --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

### Step 3: Update DbContext seed data

In `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`, update the `SystemSettings` seed data to include null defaults for new fields (the `HasData` call on line 315-324). The new fields default to `null` so no explicit seed change needed — EF Core will use the CLR defaults. But verify the migration doesn't reset existing seed data.

### Step 4: Create `INtfyClient` port

Create `src/HydraForge.Application/Notifications/INtfyClient.cs`:

```csharp
namespace HydraForge.Application.Notifications;

public interface INtfyClient
{
    Task PublishAsync(Guid userId, string title, string? body, CancellationToken ct = default);
}
```

### Step 5: Create `NtfyOptions`

Create `src/HydraForge.Infrastructure/Notifications/NtfyOptions.cs`:

```csharp
namespace HydraForge.Infrastructure.Notifications;

public class NtfyOptions
{
    public string DefaultPriority { get; set; } = "default";
}
```

### Step 6: Create `NtfyClient`

Create `src/HydraForge.Infrastructure/Notifications/NtfyClient.cs`:

```csharp
using System.Net.Http.Json;
using HydraForge.Application.Notifications;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public class NtfyClient : INtfyClient
{
    private readonly HttpClient _http;
    private readonly NtfyOptions _options;
    private readonly string? _serverUrl;

    public NtfyClient(HttpClient http, IOptions<NtfyOptions> options, string? serverUrl)
    {
        _http = http;
        _options = options.Value;
        _serverUrl = serverUrl;
    }

    public async Task PublishAsync(Guid userId, string title, string? body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_serverUrl))
            return;

        var topic = $"hydraforge-{userId}";
        var url = $"{_serverUrl.TrimEnd('/')}/{topic}";

        var payload = new
        {
            topic,
            title,
            message = body ?? title,
            priority = _options.DefaultPriority,
            tags = new[] { "hydraforge" }
        };

        try
        {
            await _http.PostAsJsonAsync(url, payload, ct);
        }
        catch
        {
            // ntfy is best-effort — never throw on push failure
        }
    }
}
```

### Step 7: Wire into NotificationService

Update `src/HydraForge.Application/Notifications/NotificationService.cs` constructor:

```csharp
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifRepo;
    private readonly INotificationHubBus _hubBus;
    private readonly INtfyClient? _ntfyClient;

    public NotificationService(
        INotificationRepository notifRepo,
        INotificationHubBus hubBus,
        INtfyClient? ntfyClient = null)
    {
        _notifRepo = notifRepo;
        _hubBus = hubBus;
        _ntfyClient = ntfyClient;
    }

    public async Task NotifyAsync(NotifyRequest request, CancellationToken ct = default)
    {
        if (request.UserId == request.ActorId)
            return;

        var notif = Notification.Create(
            request.UserId,
            request.Title,
            request.Body,
            request.Message ?? request.Title,
            request.CardId,
            request.ProjectId,
            request.ActionUrl);

        await _notifRepo.AddAsync(notif, ct);

        if (_ntfyClient != null)
            await _ntfyClient.PublishAsync(request.UserId, request.Title, request.Body, ct);

        await _hubBus.SendNotificationAsync(request.UserId, notif, ct);
    }
}
```

### Step 8: Register NtfyClient in DI

Update `src/HydraForge.Infrastructure/Notifications/NotificationServiceCollectionExtensions.cs`:

```csharp
using HydraForge.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        services.Configure<NtfyOptions>(_ => { });

        services.AddHttpClient<INtfyClient, NtfyClient>((sp, client) =>
        {
            // Server URL resolved at resolution time from cached settings
            // For now, use a placeholder — Task 10 adds CachedSettingsProvider
            var settingsUrl = (string?)null; // Will be wired in Task 10
            return new NtfyClient(client, sp.GetRequiredService<IOptions<NtfyOptions>>(), settingsUrl);
        });

        return services;
    }
}
```

Note: The `NtfyServerUrl` wiring from `SystemSettings` will be completed in Task 10 when `CachedSettingsProvider` is built. For now, `NtfyClient` receives `null` URL and gracefully no-ops.

### Step 9: Add ntfy to docker-compose.yml

In `docker-compose.yml`, add under `services:`:

```yaml
  ntfy:
    image: binwiederhier/ntfy:v2.11.0
    profiles: ["notifications"]
    environment:
      TZ: UTC
      NTFY_BASE_URL: http://ntfy:80
      NTFY_CACHE_FILE: /var/lib/ntfy/cache.db
      NTFY_AUTH_FILE: /var/lib/ntfy/auth.db
      NTFY_AUTH_DEFAULT_ACCESS: deny-all
    volumes:
      - ntfy-data:/var/lib/ntfy
    ports:
      - "8083:80"
    healthcheck:
      test: ["CMD", "wget", "-q", "http://localhost:80/v1/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

Add volume under `volumes:`:

```yaml
  ntfy-data:
```

### Step 10: Update .env.example

Add to `.env.example`:

```
NTFY_BASE_URL=http://localhost:8083
```

### Step 11: Write tests

Create `tests/HydraForge.Application.Tests/Notifications/NtfyClientTests.cs`:

```csharp
using HydraForge.Application.Notifications;
using Xunit;

namespace HydraForge.Application.Tests.Notifications;

public class NtfyClientTests
{
    [Fact]
    public async Task NotificationService_WithNullNtfyClient_DoesNotThrow()
    {
        var repo = new NotificationServiceTests.FakeNotificationRepository();
        var hubBus = new NotificationServiceTests.FakeNotificationHubBus();
        var service = new NotificationService(repo, hubBus, ntfyClient: null);

        // Should not throw
        await service.NotifyAsync(new NotifyRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Body", null, null, null, null));

        Assert.Single(repo.Added);
    }
}
```

### Step 12: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Application.Tests/ --filter "FullyQualifiedName~NtfyClientTests"
dotnet test
```

```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

Expected: "No pending model changes found."

## Verification

- `dotnet build` — no errors
- `dotnet test` — all tests pass
- EF migration drift check — no pending changes
- `docker compose --profile notifications up -d ntfy` — ntfy starts, healthcheck passes
- `curl http://localhost:8083/v1/health` — returns 200

## Dependencies

- Task 2 (NotificationService must exist)
- Task 3 (NotificationHub bus must be injectable)
- Task 10 (CachedSettingsProvider completes the NtfyServerUrl wiring)