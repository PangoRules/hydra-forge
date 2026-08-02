# Plan 21: Hangfire + SystemSettings field + migration

**Branch:** `task/hangfire-wiring`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 21

## Steps

### 1. Add Hangfire NuGet packages
- `Hangfire.AspNetCore` and `Hangfire.PostgreSql` to `src/HydraForge.Server/HydraForge.Server.csproj`.

### 2. Wire Hangfire in Program.cs
- File: `src/HydraForge.Server/Program.cs`
- `services.AddHangfire(c => c.UsePostgreSqlStorage(connectionString))` — reuse same Postgres `ConnectionStrings:Default`.
- `services.AddHangfireServer()`.
- Dashboard: `app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new AdminRequiredAuthFilter()] })`.
- Create `AdminRequiredAuthFilter` implementing `IDashboardAuthorizationFilter` — checks `HttpContext.User.IsInRole("Admin")`.

### 3. Add `AiNarrativeGenerationTimeUtc` to SystemSettings
- File: `src/HydraForge.Domain/Entities/PersonalSpace/SystemSettings.cs`
- Add property: `public TimeSpan? AiNarrativeGenerationTimeUtc { get; set; }` (default `TimeSpan.Zero` = midnight UTC).
- Update `UpdateSettings` method: add optional `TimeSpan? aiNarrativeGenerationTimeUtc` parameter.

### 4. Add EF migration
- Run: `dotnet ef migrations add AddAiNarrativeGenerationTime --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`
- Verify no pending model changes.

### 5. Update admin settings UI
- File: `src/web-ui/app/pages/admin/settings.vue`
- Add time picker for "AI Narrative Generation Time (UTC)".
- Send `aiNarrativeGenerationTimeUtc` in PUT body.

### 6. Update settings endpoint
- File: `src/HydraForge.Server/Controllers/Admin/AdminController.cs`
- Add `AiNarrativeGenerationTimeUtc` to `UpdateSystemSettingsRequest` and `GetSettings` response.

## Verification
- `dotnet build`
- `dotnet test` — existing tests pass.
- `dotnet ef migrations has-pending-model-changes` — clean.
- Manual: start server, visit `/hangfire` as admin → dashboard loads.
- Manual: update AI narrative time in settings → persisted.