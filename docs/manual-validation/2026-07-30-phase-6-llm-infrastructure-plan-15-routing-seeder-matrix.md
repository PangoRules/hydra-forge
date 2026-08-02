## Validate: FeatureRoutingConfig startup seeder

### Setup
- [ ] Start PostgreSQL with schema migrations enabled (`Database:ApplyMigrationsOnStartup=true`).
- [ ] Ensure `feature_routing_configs` is empty before first startup.

### Happy Path
1. Start server → migration completes and startup succeeds.
2. Query `feature_routing_configs` → exactly 11 rows exist, one for each `AiFeature` enum value.
3. Inspect seeded tiers → values match Plan 15: chat/edit/review/image features use specified Standard/Premium pairs; research/pipeline use Premium/null; extraction/classification/gallery use Economy/Standard.

### Edge Cases
1. Restart server with all 11 rows present → startup succeeds and row count remains 11.
2. Query DeepResearch and AgentPipeline rows → `max_user_tier` is SQL NULL, preserving locked-to-default semantics.
3. Start server with a partially populated table → verify behavior is understood before release; current `AnyAsync()` guard skips insertion, so no missing rows are repaired.

### Regressions
1. Run admin seeding on startup → existing admin seed behavior remains successful.
2. Start server with development test-user seeding enabled → test-user seeding still runs after routing seeding.

### Cleanup
- [ ] Remove manually inserted routing rows only if test DB must be reset; otherwise retain seeded baseline.
