# Plan 10: System Settings API + Cache + Web UI Page

**Branch:** `task/system-settings`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 10

## Task

Add `SystemSettings.UpdateSettings()` instance method (partial-update). Create `ISettingsProvider`/`ISettingsRepository` ports, `CachedSettingsProvider`, `EfSettingsRepository`. Add settings endpoints to `AdminController`. Create Web UI settings page. Wire `NtfyServerUrl` into `NtfyClient`.

## Files to create

- `src/HydraForge.Application/Settings/ISettingsProvider.cs`
- `src/HydraForge.Application/Settings/ISettingsRepository.cs`
- `src/HydraForge.Infrastructure/Settings/CachedSettingsProvider.cs`
- `src/HydraForge.Infrastructure/Settings/EfSettingsRepository.cs`
- `src/HydraForge.Infrastructure/Settings/SettingsServiceCollectionExtensions.cs`
- `src/web-ui/app/pages/admin/settings.vue`

## Files to modify

- `src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs` — `UpdateSettings()` already added in Task 6
- `src/HydraForge.Server/Controllers/Admin/AdminController.cs` — add settings endpoints
- `src/HydraForge.Server/Program.cs` — register settings services
- `src/HydraForge.Infrastructure/Notifications/NotificationServiceCollectionExtensions.cs` — wire `NtfyServerUrl` from `ISettingsProvider`
- `src/web-ui/app/lib/routes.ts` — `ApiRoutes.Admin.settingsGet/settingsUpdate` already added in Task 9

## Implementation steps

### Step 1: Create ISettingsProvider port

Create `src/HydraForge.Application/Settings/ISettingsProvider.cs`:

```csharp
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Settings;

public interface ISettingsProvider
{
    Task<SystemSettings> GetAsync(CancellationToken ct = default);
}
```

### Step 2: Create ISettingsRepository port

Create `src/HydraForge.Application/Settings/ISettingsRepository.cs`:

```csharp
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Settings;

public interface ISettingsRepository
{
    Task<SystemSettings> GetSingletonAsync(CancellationToken ct = default);
    Task UpdateAsync(SystemSettings settings, CancellationToken ct = default);
}
```

### Step 3: Create EfSettingsRepository

Create `src/HydraForge.Infrastructure/Settings/EfSettingsRepository.cs`:

```csharp
using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Settings;

public class EfSettingsRepository(HydraForgeDbContext db) : ISettingsRepository
{
    public async Task<SystemSettings> GetSingletonAsync(CancellationToken ct = default)
    {
        return await db.SystemSettings
            .FirstOrDefaultAsync(ct)
            ?? new SystemSettings();
    }

    public async Task UpdateAsync(SystemSettings settings, CancellationToken ct = default)
    {
        db.SystemSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
```

### Step 4: Create CachedSettingsProvider

Create `src/HydraForge.Infrastructure/Settings/CachedSettingsProvider.cs`:

```csharp
using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.PersonalSpace;
using Microsoft.Extensions.Caching.Memory;

namespace HydraForge.Infrastructure.Settings;

public class CachedSettingsProvider(ISettingsRepository repo, IMemoryCache cache) : ISettingsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CacheKey = "system_settings";

    public async Task<SystemSettings> GetAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await repo.GetSingletonAsync(ct);
        }) ?? new SystemSettings();
    }

    public void Invalidate()
    {
        cache.Remove(CacheKey);
    }
}
```

### Step 5: Create DI extension

Create `src/HydraForge.Infrastructure/Settings/SettingsServiceCollectionExtensions.cs`:

```csharp
using HydraForge.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Settings;

public static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsServices(this IServiceCollection services)
    {
        services.AddScoped<ISettingsRepository, EfSettingsRepository>();
        services.AddSingleton<ISettingsProvider, CachedSettingsProvider>();
        services.AddMemoryCache();
        return services;
    }
}
```

### Step 6: Register in Program.cs

In `src/HydraForge.Server/Program.cs`, add:

```csharp
using HydraForge.Infrastructure.Settings;
// ...
builder.Services.AddSettingsServices();
```

### Step 7: Add settings endpoints to AdminController

In `src/HydraForge.Server/Controllers/Admin/AdminController.cs`, add:

```csharp
using HydraForge.Application.Settings;
using HydraForge.Infrastructure.Settings;

// Inject into constructor:
public class AdminController(
    IAdminService adminService,
    ISettingsRepository settingsRepo,
    CachedSettingsProvider settingsProvider
) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await settingsProvider.GetAsync(ct);
        return Ok(new
        {
            settings.ArchivedItemRetentionDays,
            settings.AuditLogRetentionDays,
            settings.NotificationRetentionDays,
            settings.NtfyServerUrl,
            settings.SearXngUrl,
            settings.BrandName,
            settings.BrandLogoUrl,
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSystemSettingsRequest request, CancellationToken ct)
    {
        var settings = await settingsRepo.GetSingletonAsync(ct);
        settings.UpdateSettings(
            request.ArchivedItemRetentionDays,
            request.AuditLogRetentionDays,
            request.NotificationRetentionDays,
            request.NtfyServerUrl,
            request.SearXngUrl,
            request.BrandName,
            request.BrandLogoUrl
        );
        await settingsRepo.UpdateAsync(settings, ct);
        settingsProvider.Invalidate();
        return Ok(new { message = "Settings updated. Changes apply within 5 minutes." });
    }
}

public record UpdateSystemSettingsRequest(
    int? ArchivedItemRetentionDays,
    int? AuditLogRetentionDays,
    int? NotificationRetentionDays,
    string? NtfyServerUrl,
    string? SearXngUrl,
    string? BrandName,
    string? BrandLogoUrl
);
```

### Step 8: Wire NtfyServerUrl into NtfyClient

Update `src/HydraForge.Infrastructure/Notifications/NotificationServiceCollectionExtensions.cs`:

```csharp
using HydraForge.Application.Notifications;
using HydraForge.Application.Settings;
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
            var settingsProvider = sp.GetRequiredService<ISettingsProvider>();
            var settings = settingsProvider.GetAsync().GetAwaiter().GetResult();
            return new NtfyClient(client, sp.GetRequiredService<IOptions<NtfyOptions>>(), settings.NtfyServerUrl);
        });

        return services;
    }
}
```

### Step 9: Create Web UI settings page

Create `src/web-ui/app/pages/admin/settings.vue`:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

const { user } = useAuth()
if (!user.value?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()
const loading = ref(false)

const settings = reactive({
  archivedItemRetentionDays: 730,
  auditLogRetentionDays: 90,
  notificationRetentionDays: 30,
  ntfyServerUrl: '',
  searXngUrl: '',
  brandName: '',
  brandLogoUrl: '',
})

async function loadSettings() {
  loading.value = true
  try {
    const data = await api.GET<any>(ApiRoutes.Admin.settingsGet())
    Object.assign(settings, data)
  } catch (e: any) {
    toast.add({ title: e.message || 'Failed to load settings', color: 'error' })
  } finally {
    loading.value = false
  }
}

async function saveSettings(section: string) {
  try {
    const body: Record<string, any> = {}
    if (section === 'retention') {
      body.archivedItemRetentionDays = settings.archivedItemRetentionDays
      body.auditLogRetentionDays = settings.auditLogRetentionDays
      body.notificationRetentionDays = settings.notificationRetentionDays
    } else if (section === 'notifications') {
      body.ntfyServerUrl = settings.ntfyServerUrl || null
    } else if (section === 'search') {
      body.searXngUrl = settings.searXngUrl || null
    } else if (section === 'branding') {
      body.brandName = settings.brandName || null
      body.brandLogoUrl = settings.brandLogoUrl || null
    }
    await api.PUT(ApiRoutes.Admin.settingsUpdate(), { body })
    toast.add({ title: 'Settings saved. Changes apply within 5 minutes.', color: 'success' })
  } catch (e: any) {
    toast.add({ title: e.message || 'Save failed', color: 'error' })
  }
}

onMounted(() => loadSettings())
</script>

<template>
  <div class="p-6 max-w-2xl">
    <h1 class="text-2xl font-bold mb-6">System Settings</h1>

    <!-- Retention -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">Retention</h2>
      <div class="space-y-3">
        <UInput v-model.number="settings.archivedItemRetentionDays" label="Archived Item Retention (days)" type="number" />
        <UInput v-model.number="settings.auditLogRetentionDays" label="Audit Log Retention (days)" type="number" />
        <UInput v-model.number="settings.notificationRetentionDays" label="Notification Retention (days)" type="number" />
      </div>
      <UButton label="Save Retention" class="mt-3" @click="saveSettings('retention')" />
    </section>

    <!-- Notifications -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">Notifications</h2>
      <UInput v-model="settings.ntfyServerUrl" label="ntfy Server URL" placeholder="http://localhost:8083" />
      <UButton label="Save Notifications" class="mt-3" @click="saveSettings('notifications')" />
    </section>

    <!-- Search -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">Search</h2>
      <UInput v-model="settings.searXngUrl" label="SearXNG URL" placeholder="http://localhost:8080" />
      <UButton label="Save Search" class="mt-3" @click="saveSettings('search')" />
    </section>

    <!-- Branding -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">Branding</h2>
      <UInput v-model="settings.brandName" label="Brand Name" placeholder="HydraForge" />
      <UInput v-model="settings.brandLogoUrl" label="Brand Logo URL" placeholder="https://..." />
      <UButton label="Save Branding" class="mt-3" @click="saveSettings('branding')" />
    </section>
  </div>
</template>
```

### Step 10: Write tests

Create `tests/HydraForge.Domain.Tests/Entities/SystemSettingsTests.cs`:

```csharp
using HydraForge.Domain.Entities.PersonalSpace;
using Xunit;

namespace HydraForge.Domain.Tests.Entities;

public class SystemSettingsTests
{
    [Fact]
    public void UpdateSettings_OnlyUpdatesNonNullArgs()
    {
        var settings = new SystemSettings();
        var originalRetention = settings.ArchivedItemRetentionDays;

        settings.UpdateSettings(ntfyServerUrl: "http://ntfy:80");

        Assert.Equal(originalRetention, settings.ArchivedItemRetentionDays); // unchanged
        Assert.Equal("http://ntfy:80", settings.NtfyServerUrl); // updated
    }

    [Fact]
    public void UpdateSettings_UpdatesTimestamp()
    {
        var settings = new SystemSettings();
        var before = settings.UpdatedAt;

        System.Threading.Thread.Sleep(10);
        settings.UpdateSettings(brandName: "Test");

        Assert.True(settings.UpdatedAt > before);
    }

    [Fact]
    public void UpdateSettings_NullStringArg_DoesNotChange()
    {
        var settings = new SystemSettings();
        settings.UpdateSettings(ntfyServerUrl: "http://ntfy:80");
        settings.UpdateSettings(ntfyServerUrl: null);

        Assert.Equal("http://ntfy:80", settings.NtfyServerUrl);
    }
}
```

### Step 11: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Domain.Tests/ --filter "FullyQualifiedName~SystemSettingsTests"
dotnet test
```

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

## Verification

- `dotnet build` — no errors
- Domain tests: `UpdateSettings` partial-update works, timestamp updates, null args ignored
- `dotnet test` — all tests pass
- `pnpm typecheck && pnpm lint && pnpm build` — no errors
- Manual: login as admin, navigate to `/admin/settings`, change retention days, save, verify GET returns updated values

## Dependencies

- Task 6 (SystemSettings fields + migration already done)
- Task 9 (AdminController exists, routes defined)