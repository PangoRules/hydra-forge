# Plan 22: GenerateAiNarrative job

**Branch:** `task/ai-narrative-job`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 22

## Steps

### 1. Add method to `ProjectContextSnapshotService`
- File: `src/HydraForge.Application/ProjectSnapshots/ProjectContextSnapshotService.cs`
- Add `GenerateAiNarrativeForAllActiveProjectsAsync(CancellationToken ct)`.
- Query all active projects (non-archived) with existing `ProjectContextSnapshot`.
- For each project:
  1. Get snapshot → `TemplateContent`.
  2. Call `IModelRouter.ResolveAsync(AiFeature.ProjectChat, ...)` → get route.
  3. Call `ILlmClient.StreamChatAsync` with system prompt ("Generate a concise project narrative...") and `TemplateContent` as a `CacheBlock(CacheBlockType.ProjectSnapshot, templateContent)`.
  4. Collect full response from stream.
  5. Update snapshot: `AiNarrative = response`, `AiNarrativeGeneratedAt = DateTime.UtcNow`.
  6. Save via `snapshotRepo.UpdateAsync`.
- Per-project failures: catch, log error, continue to next project. One failure does not abort batch.
- Inject `IModelRouter` and `ILlmClientFactory` into service (new constructor dependencies).

### 2. Register recurring job
- File: `src/HydraForge.Server/Program.cs` (or dedicated `HangfireJobsConfig.cs`)
- After Hangfire server setup:
  ```csharp
  RecurringJob.AddOrUpdate<ProjectContextSnapshotService>(
      "ai-narrative-gen",
      svc => svc.GenerateAiNarrativeForAllActiveProjectsAsync(default),
      () => Cron.Daily(settings.AiNarrativeGenerationTimeUtc?.Hours ?? 0, settings.AiNarrativeGenerationTimeUtc?.Minutes ?? 0)
  );
  ```
- Read `SystemSettings.AiNarrativeGenerationTimeUtc` at registration time (or use `Cron.Daily(hour, minute)`).

### 3. Update DI registration
- `ProjectContextSnapshotService` now depends on `IModelRouter` + `ILlmClientFactory`. Update constructor.
- Ensure these are registered before `ProjectContextSnapshotService`.

### 4. Unit tests
- File: `tests/HydraForge.Application.Tests/ProjectSnapshots/GenerateAiNarrativeTests.cs`
- Use stub `IModelRouter` and `ILlmClient` (fake stream returning test narrative).
- Test: active projects get narrative generated.
- Test: archived projects skipped.
- Test: per-project failure doesn't abort batch.
- Test: `AiNarrativeGeneratedAt` set on success.

## Verification
- [x] `dotnet build`
- [x] `dotnet test --filter "GenerateAiNarrative"`
- [x] Manual: trigger job from Hangfire dashboard → verify narratives populated.