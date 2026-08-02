# Plan 15: FeatureRoutingConfig seeder

**Branch:** `task/routing-seeder`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 15

## Steps

### 1. Create seeder class
- File: `src/HydraForge.Infrastructure/Llm/FeatureRoutingConfigSeeder.cs`
- Constructor takes `HydraForgeDbContext`.

### 2. Implement `SeedAsync`
- Check if `FeatureRoutingConfigs` table is empty.
- If empty, insert one row per `AiFeature` value per spec table:

| Feature | DefaultTier | MaxUserTier |
|---|---|---|
| PersonalChat | Standard | Premium |
| ProjectChat | Standard | Premium |
| DeepResearch | Premium | null |
| AgentPipeline | Premium | null |
| MemoryExtraction | Economy | Standard |
| NotesClassification | Economy | Standard |
| DocumentEditing | Standard | Premium |
| CardReview | Standard | Premium |
| ImageChat | Standard | Premium |
| ImageDocument | Standard | Premium |
| ImageGalleryEditor | Economy | Standard |

- `null` MaxUserTier = locked to default.
- `await db.SaveChangesAsync(ct)`.

### 3. Wire into startup
- File: `src/HydraForge.Server/Program.cs`
- After `adminSeeder.SeedIfNeededAsync()`, call `FeatureRoutingConfigSeeder.SeedAsync()`.
- Wrap in same migration scope.

### 4. Idempotency
- Check `AnyAsync()` before seeding — skip if rows exist. Safe to run on every startup.

## Verification
- `dotnet build`
- `dotnet test` — existing tests pass.
- Manual: start server, verify 11 rows in `feature_routing_configs` table.
- Re-run: no duplicates.