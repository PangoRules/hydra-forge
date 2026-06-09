# HydraForge Agent Notes

Compact repo-specific guidance for OpenCode sessions. Prefer executable files over roadmap prose when they disagree.

## Current State

- Phase 1 foundation is complete on `feat/phase-1-foundation`. Docker Compose, EF Core + pgvector schema, auth, ProblemDetails/correlation, health, audit infrastructure, CI, and placeholder cleanup are implemented. All 50+ Domain entities are mapped by `HydraForgeDbContext`; pgvector `vector(1536)` columns are configured for `MemoryEntry.Embedding` and `DocumentChunk.Embedding`. Seven migrations are committed (latest `20260604050632_AddAuditLogScopeAndNullableProjectId`).
- Phase 2 (Project Space API & Domain) is in progress on `feat/phase-2-project-space-api-domain`. Tasks 1-3 complete: Project CRUD/membership/archive, Column CRUD/reorder, Card CRUD/move/assignees/blocked-move-warning/parent-epic-linking. Tasks 4-10 (checklists, comments, attachments, specs, plans, relationships, snapshot, hubs, hardening) remain.
- 196 xUnit tests across Domain (52), Application (55), Infrastructure (43), Server (46). Domain/Application are pure logic; Infrastructure tests assert EF model contract (`AssertProperties` on `IEntityType`).

## Read First

- `CLAUDE.md` for stack, commands, and conventions.
- `docs/DECISIONS.md` before changing architecture; do not re-litigate settled decisions.
- The monolithic `requirements-and-architecture.md` was split in `dc2e092`. It is now a 17-line index. Read by intent:
  - `docs/scope.md` — vision, personas, scope boundaries
  - `docs/functional-spec.md` — FRs, NFRs, phase checklists (live)
  - `docs/architecture.md` — Clean Architecture, real-time, LLM, error handling, tech stack
  - `docs/data-model.md` — entity tables and enums (authoritative for schema intent)
  - `docs/glossary.md` — terminology
  - `docs/agent-platform-vision.md` — agent platform direction
  - `docs/backlog.md` — uncommitted ideas and scope-creep candidates
- Check manifests/config before trusting prose: `HydraForge.slnx`, `*.csproj`, `src/web-ui/package.json`, `src/web-ui/nuxt.config.ts`.

## Repo Shape

- Solution file is `HydraForge.slnx`; projects target `net10.0` with nullable and implicit usings enabled.
- Backend boundaries: `Domain` -> `Application` -> `Infrastructure` -> `Server`/`Tui`. Keep Domain free of EF Core, HTTP, SignalR, and infrastructure concerns.
- `src/HydraForge.Server` is ASP.NET Core. `Program.cs` calls `builder.Services.AddPersistence(builder.Configuration)` and wires auth, ProblemDetails/correlation middleware, health probes, admin seeding, and controllers. Starter weather endpoints are removed.
- `src/HydraForge.Tui` is the terminal client; Spectre.Console is planned but not currently referenced.
- `src/web-ui` is a separate pnpm package using Nuxt `^4.4.6`, Nuxt UI `^4.8.1`, Tailwind `^4.3.0`, TypeScript `^6.0.3`, pnpm `^11.5.0`. Nuxt 4 source layout (`src/web-ui/app/`).

## Commands

- Build .NET: `dotnet build`
- Run all .NET tests: `dotnet test`
- Run one test project: `dotnet test tests/HydraForge.Domain.Tests/HydraForge.Domain.Tests.csproj`
- Filter a .NET test: `dotnet test --filter FullyQualifiedName~TestName`
- Run server: `dotnet run --project src/HydraForge.Server`
- Run TUI: `dotnet run --project src/HydraForge.Tui`
- EF migrations require the tool on PATH: `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef ...`
  - Add migration: `... dotnet ef migrations add <Name> --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`
  - Verify model is clean: `... dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`
  - Apply migrations: `... dotnet ef database update --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`
- Docker (full stack): `docker compose up`
- Docker (Postgres only): `docker compose up -d postgres` (host port 5433)
- Install web deps: `cd src/web-ui && pnpm install`
- Web dev server: `cd src/web-ui && pnpm dev`
- Web typecheck: `cd src/web-ui && pnpm typecheck`
- Web lint: `cd src/web-ui && pnpm lint`
- Web build: `cd src/web-ui && pnpm build`
- On a fresh checkout, run `pnpm install` or `pnpm exec nuxt prepare` before web lint/typecheck because `eslint.config.mjs` imports generated `.nuxt/eslint.config.mjs`.

## Testing Notes

- Test projects are xUnit with plain `Assert.*`; do not add FluentAssertions.
- Domain/Application tests are pure logic. Infrastructure tests assert the EF model contract via `AssertProperties(IEntityType, params string[])` — these run without a database because they inspect `context.Model`, not `context.Database`.
- Most tests run without PostgreSQL. Optional PostgreSQL-backed tests use `HYDRAFORGE_TEST_CONNECTION_STRING`; the architecture requires real PostgreSQL for DB behavior tests, not SQLite or mocked DB behavior.

## Architecture Constraints To Preserve

- Server is authoritative. Do not add offline mode, local state sync, SQLite fallback, pending-change queues, or conflict-resolution features.
- TUI and Web UI must remain feature-parity capable; do not design browser-only APIs or cookie-only auth flows.
- Expected Application-layer failures should return `Result<T, Error>` with named error-code constants in Domain; reserve thrown exceptions for unexpected failures.
- When LLM features are implemented, calls must go through Application-level routing (`ModelRouter` per docs), not directly from controllers or clients.
- Admin configures LLM providers; users must not store personal provider API keys.
- Admin can manage system/project scope but must not access user personal-space data.
- Card identifiers shown to users should be per-project `CardNumber` values, not raw GUIDs.
- If implementing card relationships, validate acyclic dependencies before persisting them; circular relationships are rejected in Application logic.
- `ProjectContextSnapshot.TemplateContent` is intended for instant board-mutation updates; `AiNarrative` is intended for nightly scheduled generation only.
- Domain entities encapsulate state transitions via instance methods (e.g. `card.MoveTo(columnId, position)`, `project.Archive()`, `column.UpdateDetails(name, color, wipLimit)`, `member.ChangeRole(role)`). Services MUST call these methods — never set entity properties directly in Application layer. This keeps all mutation logic on the entity itself.
- Blocked card move: API returns `409 Conflict` with warning payload when `confirmBlockedMove=false`. 200 OK must not be used for blocked moves — the move was not executed.
- Housekeeping cascade: DbContext `OnDelete: Cascade` for Document→Version, Note→Reminder, Note→ImageAttachment, ChatSession→Message. `DocumentChunk` is polymorphic (`SourceType`+`SourceId`); its cascade must be handled manually in `HousekeepingBackgroundService`, not via FK.
- One global admin-configurable retention period for all archived ownable content (via `SystemSettings.ArchivedItemRetentionDays`). Notifications and audit logs have their own (shorter) retention knobs.
- Archive is `ArchivedAt: DateTime?`, not `IsArchived: bool`. Archived-by-default queries use manual `.Where(x => x.ArchivedAt == null)` filters (no global query filter), so admin and audit views can see archived rows.
- Housekeeping cascade: DbContext `OnDelete: Cascade` for Document→Version, Note→Reminder, Note→ImageAttachment, ChatSession→Message. `DocumentChunk` is polymorphic (`SourceType`+`SourceId`); its cascade must be handled manually in `HousekeepingBackgroundService`, not via FK.
- One global admin-configurable retention period for all archived ownable content (via `SystemSettings.ArchivedItemRetentionDays`). Notifications and audit logs have their own (shorter) retention knobs.

## Database And Migrations

- EF Core 10 + Npgsql + `Pgvector.EntityFrameworkCore 0.3.0` are wired. DbContext lives at `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`. The `UseVector()` call is required on the options builder — both `PersistenceServiceCollectionExtensions.AddPersistence` and `DesignTimeHydraForgeDbContextFactory` chain it.
- All schema changes go through EF Core migrations under `src/HydraForge.Infrastructure/Migrations/`; do not hand-edit database schema.
- pgvector extension is added via `modelBuilder.HasPostgresExtension("vector")` in `OnModelCreating`.
- Table naming convention is snake_case plural (e.g. `card_chat_links`, `audit_log_entries`).
- Local dev DBs typically run on host port **5433** (per `docker-compose.yml`); update `appsettings.Development.json` if your local Postgres uses a different port.
- `dotnet ef` warns "Unable to check if the migration has been applied" when no DB is reachable — migrations still regenerate locally; use `has-pending-model-changes` to verify model state.

## Web UI Conventions

- Nuxt config enables `@nuxt/eslint` and `@nuxt/ui`, uses `~/assets/css/main.css`, and prerenders `/`.
- ESLint stylistic settings use no trailing comma and `1tbs` brace style via Nuxt config.
- Mobile-first, keyboard-navigable UI is a settled requirement; preserve this when replacing starter screens.

## Commit/Docs Discipline

- Do not commit unless explicitly asked.
- If a change creates a real new architecture decision, update `docs/DECISIONS.md`, the relevant file under `docs/` (scope/functional-spec/architecture/data-model/glossary), and `CLAUDE.md` if commands or conventions change.
- Migrations should be committed with the entity/schema changes that require them in the same commit, not separately.
- Doc-only changes that fix parity between `data-model.md` and existing entities are fine in a single commit; new fields on entities must be TDD (failing test first).
