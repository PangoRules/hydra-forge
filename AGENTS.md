# HydraForge Agent Notes

Compact repo-specific guidance for OpenCode sessions. Prefer executable files over roadmap prose when they disagree.

## Current State

- Phase 1 (EF Core + pgvector schema foundation) is in progress on `task/phase-1-ef-pgvector`. All 50+ Domain entities are implemented; `HydraForgeDbContext` is wired with `OnModelCreating`, snake_case naming, FK cascade configuration, and pgvector `vector(1536)` columns for `MemoryEntry.Embedding` and `DocumentChunk.Embedding`. Six migrations are committed (latest `20260603210352_RenameCardDueDateToDueAt`).
- Archive + housekeeping foundation is committed: nine entities carry `ArchivedAt?` (Card, Document, Note, MemoryEntry, CalendarEvent, CalendarSource, PersonalTask, CardChatLink, Comment). `Note.IsArchived` and `Document.IsArchived` (bool) were replaced with `ArchivedAt?` (DateTime). `SystemSettings` singleton (id `00000000-0000-0000-0000-000000000001`) holds `ArchivedItemRetentionDays=730`, `AuditLogRetentionDays=90`, `NotificationRetentionDays=30`. Explicit `OnDelete: Cascade` is configured for `Document→DocumentVersion`, `Note→NoteReminder`, `Note→NoteImageAttachment`, `ChatSession→ChatMessage`. The `HousekeepingBackgroundService` and per-entity archive services are deferred (distributed across phases 1, 2, 5, 7, 9 in `docs/functional-spec.md`); design spec is at `docs/superpowers/specs/2026-06-03-archive-and-housekeeping-design.md`.
- Persistence DI lives at `src/HydraForge.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs` (method `AddPersistence`). It chains `o => o.UseVector()` on `UseNpgsql` so the runtime `Vector` CLR type resolves to a `vector(1536)` PostgreSQL column. `DesignTimeHydraForgeDbContextFactory` does the same for `dotnet ef`.
- `docker-compose.yml` and `.env.example` are implemented (not empty as older AGENTS.md revisions claimed). Postgres is `pgvector/pgvector:pg16`, host port **5433** mapped to container 5432 (5433 was chosen to avoid colliding with any local Postgres install or Odysseus on the same host). `appsettings.Development.json` already has a `ConnectionStrings:Default` with the dev password — local devs override the host to `localhost` and port to `5433` when running against docker-compose.
- Web UI still ships the Nuxt UI starter under `src/web-ui/app/app.vue` and `src/web-ui/app/pages/index.vue`; do not assume HydraForge screens or composables exist.
- 29 xUnit tests across 3 projects (9 Domain, 1 Application, 19 Infrastructure). Domain/Application are pure logic; Infrastructure tests assert EF model contract (`AssertProperties` on `IEntityType`).

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
- Check manifests/config before trusting prose: `HydraForge.slnx`, `*.csproj`, `src/web-ui/package.json`, `src/web-ui/nuxt.config.ts`.

## Repo Shape

- Solution file is `HydraForge.slnx`; projects target `net10.0` with nullable and implicit usings enabled.
- Backend boundaries: `Domain` -> `Application` -> `Infrastructure` -> `Server`/`Tui`. Keep Domain free of EF Core, HTTP, SignalR, and infrastructure concerns.
- `src/HydraForge.Server` is ASP.NET Core. `Program.cs` calls `builder.Services.AddPersistence(builder.Configuration)` (the only DI registration at the moment). Weather endpoint is still the default scaffold; no real controllers yet.
- `src/HydraForge.Tui` is the terminal client; Spectre.Console is planned but not currently referenced.
- `src/web-ui` is a separate pnpm package. Although `docs/` say Nuxt 3, `package.json` uses Nuxt `^4.4.6`, Nuxt UI `^4.8.1`, Tailwind `^4.3.0`, TypeScript `^6.0.3`, pnpm `^11.5.0`. Nuxt 4 source layout (`src/web-ui/app/`).

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
- No PostgreSQL test infrastructure exists yet. The architecture requires real PostgreSQL for DB tests, not SQLite or mocked DB behavior.

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
