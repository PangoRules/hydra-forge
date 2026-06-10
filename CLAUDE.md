# HydraForge — Claude Code Context

## What this is

Self-hosted AI workspace + project management platform. Dual interface: `.NET TUI` (Spectre.Console) and `Nuxt Web UI`. Server is the single source of truth — no offline mode, no local state.

## Stack

| Layer | Technology |
|---|---|
| Server | .NET 10 / C# — Clean Architecture |
| TUI | Spectre.Console (same process as server client) |
| Web UI | Nuxt 4 + Nuxt UI + Tailwind CSS (pnpm) |
| Database | PostgreSQL 16 + pgvector extension |
| Real-time | SignalR (WebSocket + SSE fallback) |
| Tests | xUnit — plain assertions only, no FluentAssertions |
| Container | Docker + docker-compose |

## Essential commands

```bash
# Build everything
dotnet build

# Run all tests
dotnet test

# Run server (from repo root)
dotnet run --project src/HydraForge.Server

# Run TUI (from repo root)
dotnet run --project src/HydraForge.Tui

# Web UI dev server
cd src/web-ui && pnpm dev

# Add a migration (from repo root)
# NOTE: dotnet ef must be on PATH on this box.
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations add <MigrationName> \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server

# Verify the model has no pending changes (CI-friendly):
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations has-pending-model-changes \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server

# Apply migrations
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef database update \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server

# Docker — full stack (Postgres + Server)
docker compose up

# Docker — Postgres only (port 5433 → 5432 in container)
docker compose up -d postgres
```

### Local dev database

- Docker compose exposes Postgres on host port **5433** (not 5432) to avoid collisions with local Postgres or other services on the host.
- `appsettings.Development.json` is ignored for local overrides. Use `.env.example` for Docker, or create a local dev appsettings file with `Host=localhost;Port=5433` when running `dotnet run` against Compose Postgres.
- `dotnet ef` operations warn "Unable to check if the migration has been applied" when the DB is unreachable; migrations still regenerate locally — this is expected, not a failure.

## Clean Architecture — what goes where

```
HydraForge.Domain          ← Entities, enums, interfaces, Result<T,Error>, error codes
HydraForge.Application     ← Use cases, services (CardService, ModelRouter, etc.), DTOs
HydraForge.Infrastructure  ← EF Core DbContext, migrations, LLM clients, file storage, SignalR
HydraForge.Server          ← ASP.NET Core controllers, SignalR hubs, middleware, Program.cs
HydraForge.Tui             ← Spectre.Console views, commands, screen rendering
src/web-ui                 ← Nuxt 4 app (pages, components, composables) under app/
```

**Dependency direction:** Domain ← Application ← Infrastructure ← Server/TUI. Domain knows nothing about EF Core, HTTP, or SignalR.

**DI registration:** Server's `Program.cs` calls `builder.Services.AddPersistence(builder.Configuration)` from `HydraForge.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`. That method registers `HydraForgeDbContext` with `UseNpgsql` chained to `o => o.UseVector()` (required for the `Vector` CLR type → `vector(1536)` PostgreSQL column mapping).

## Critical conventions

**Error handling — non-negotiable:**
- Business logic returns `Result<T, Error>` — never throw exceptions for expected failures
- All errors have a typed error code (e.g. `CARD_NOT_FOUND`, `DEPENDENCY_CYCLE_DETECTED`)
- Controllers map expected `Result<T, Error>` failures to ProblemDetails RFC 7807 with `correlationId` and named `code`; global exception middleware catches everything else
- Stack traces never reach clients
- External service failures (LLM, Git, ntfy) must never crash the board

**Domain entity patterns — non-negotiable:**
- Domain entities encapsulate state transitions via instance methods — services orchestrate but NEVER set entity properties directly
- On Card: `UpdateDetails`, `MoveTo(columnId, position)`, `ShiftPosition(delta)`, `Archive()`
- On Project: `UpdateDetails`, `Archive()`
- On Column: `UpdateDetails`, `AssignPosition(position)`
- On ProjectMember: `ChangeRole(role)`
- Services call these methods instead of `entity.Property = value`

**Testing:**
- `xUnit` only — no FluentAssertions (deprecation risk), use plain `Assert.*`
- > 90% coverage on Application and Domain layers
- Infrastructure tests assert the EF model contract via `AssertProperties(IEntityType, ...)` — they inspect `context.Model` and do not need a database
- Never mock the database — use a real test PostgreSQL instance once that infrastructure exists (not in place yet)

**Database:**
- PostgreSQL only — no SQLite fallback
- All schema changes via EF Core migrations
- pgvector extension required (`CREATE EXTENSION IF NOT EXISTS vector`, declared via `modelBuilder.HasPostgresExtension("vector")`)
- Card numbers are sequential per project (`CardNumber int`, unique per ProjectId) — never expose raw GUIDs to users
- Archive is `ArchivedAt: DateTime?`, not `IsArchived: bool`. Default query filters use `.Where(x => x.ArchivedAt == null)` manually — no global query filter, so admin/audit views see archived rows by default

**Code style:**
- Readable like a newspaper — method names explain intent, no clever tricks
- No unnecessary abstractions — three similar lines beats a premature helper
- Comments only when the WHY is non-obvious
- No `var` where the type isn't obvious from the right-hand side

**Auth:**
- JWT — admin seeded on first boot
- No SSO, no OAuth, no external auth providers
- Admin cannot access user personal data (chats, memory, notes, calendar, gallery)

**LLM:**
- Server is the only component that calls LLMs — TUI and Web UI never call LLMs directly
- All LLM calls go through `ModelRouter` which selects provider based on feature tier
- Admin configures providers — users cannot add personal API keys
- `ILlmClient`, `IImageClient`, `IEmbeddingClient` — always code to the interface

## Key domain rules

- `Card.CardNumber` is sequential per project — assigned at creation, never reused after deletion
- Blocked card move: returns `409 Conflict` with warning payload when `confirmBlockedMove=false`. Client retries with `confirmBlockedMove=true` after user confirms. Never hard-blocked. 200 OK is wrong — the move was not executed.
- `CardRelationship` forms a DAG — `CardDependencyService.ValidateAcyclic()` must be called on every insert
- `ProjectContextSnapshot.TemplateContent` regenerated on every board mutation (instant, no LLM)
- `ProjectContextSnapshot.AiNarrative` generated by nightly scheduled job only (never on mutation)
- AI proposes board mutations — human confirms — never mutate board state from AI without explicit user approval
- AI edit permission is chat-session-scoped — revoked when session ends or new chat opens

## Project spaces

- **Project space** — shared, members-only visibility (`ProjectMember` table gates access)
- **Personal space** — private per user (chats, memory, notes, tasks, calendar, gallery, documents)
- **Admin space** — users, all projects, LLM providers, system health, audit logs only

## Housekeeping & archive

- Soft-delete is `ArchivedAt: DateTime?`; hard-delete is the responsibility of the future `HousekeepingBackgroundService` (deferred across later phase work in `docs/functional-spec.md`).
- Retention periods are admin-configurable via the `SystemSettings` singleton: `ArchivedItemRetentionDays=730`, `AuditLogRetentionDays=90`, `NotificationRetentionDays=30`.
- DB-level cascades cover `Document→DocumentVersion`, `Note→NoteReminder`, `Note→NoteImageAttachment`, `ChatSession→ChatMessage`. Polymorphic `DocumentChunk` (`SourceType`+`SourceId`) is cascaded manually in the housekeeping service.
- Design spec: `docs/superpowers/specs/2026-06-03-archive-and-housekeeping-design.md`.

## Docs

The monolithic `requirements-and-architecture.md` was split in `dc2e092` into focused files. It is now a 17-line index pointing at:

- `docs/scope.md` — vision, personas, scope boundaries
- `docs/functional-spec.md` — FRs, NFRs, phase checklists (live)
- `docs/architecture.md` — Clean Architecture, real-time, LLM, error handling, tech stack
- `docs/data-model.md` — entity tables and enums (authoritative for schema intent)
- `docs/glossary.md` — terminology
- `docs/DECISIONS.md` — every design decision with rationale (D-1 through D-32)
- `docs/agent-platform-vision.md` — vision, pipeline, feature parity table

Read `docs/DECISIONS.md` before changing any architectural pattern — the rationale is there. Keep `docs/data-model.md` and entity code in sync when fields change.
