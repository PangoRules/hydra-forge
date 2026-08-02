## Validate: AiNarrative recurring job (Plan 22)

### Setup
- [ ] Server running via `docker compose up -d postgres minio` and `dotnet run --project src/HydraForge.Server` with `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Hangfire dashboard reachable at `/hangfire` with admin credentials
- [ ] At least one active project exists with a populated `ProjectContextSnapshot.TemplateContent` (state changes trigger snapshot refresh)
- [ ] Admin → AI page shows an enabled LLM provider + project-chat route

### Happy Path
1. Trigger `ai-narrative-gen` from Hangfire dashboard → job executes, completes successfully (no red entries) within ~30s for one project.
2. Query DB: `SELECT "AiNarrative", "AiNarrativeGeneratedAt" FROM project_context_snapshots WHERE "ProjectId" = <id>` → `AiNarrative` is 3–5 sentence narrative describing the project; `AiNarrativeGeneratedAt` within last few minutes (UTC).
3. Open Web UI on that project's board page → narrative appears in any narrative display surface (or `GET /api/projects/{id}/snapshot` returns it).
4. Wait for cron time matching `SystemSettings.AiNarrativeGenerationTimeUtc`, OR restart server to register cron at different time → Hangfire shows `ai-narrative-gen` with the configured `Cron.Daily(h, m)` schedule.

### Edge Cases
1. Project exists with no `ProjectContextSnapshot` row → job iterates active projects, skips projects missing snapshot (no narrative written). No exception.
2. Archived project (`ArchivedAt IS NOT NULL`) → skipped; `AiNarrative` unchanged for that project.
3. LLM provider unreachable / `StreamChatAsync` throws → job logs error for that project, continues to next project, completes overall.
4. `StreamChatAsync` returns empty stream (no deltas) → warning logged for that project, snapshot not updated (`AiNarrative` not overwritten with empty).
5. `ModelRouter.ResolveAsync` returns `Failure` → warning logged, project skipped, no crash.

### Regressions
1. Existing mutation-triggered `ProjectSnapshotRefresher.RefreshAsync` still populates `TemplateContent` instantly on card/column changes (unchanged).
2. `GET /api/projects/{projectId}/ProjectSnapshot` still returns the full snapshot including any updated `AiNarrative`.
3. Admin settings page (Phase 5/6) still saves `AiNarrativeGenerationTimeUtc` without breaking the field schema.
4. Other Hangfire recurring jobs (audit, housekeeping) still registered and run.
5. All 7 application tests + 4 new `GenerateAiNarrative` tests still pass with `dotnet test`.

### Cleanup
- [ ] No test data needs removal; the job only writes `AiNarrative` and timestamp on real snapshots.
