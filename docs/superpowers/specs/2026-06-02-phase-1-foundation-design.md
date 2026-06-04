# Phase 1 Foundation Completion Design

**Branch:** `feat/phase-1-foundation`

## Purpose

Finish HydraForge Phase 1 after monorepo scaffold. Phase 1 creates runnable foundation only: server, database, auth, error handling, observability, health, audit infrastructure, and CI. It does not build project-board user features, chat, LLM routing, notifications, or UI screens beyond scaffold compatibility.

## Current State

- Monorepo scaffold exists: `HydraForge.Domain`, `HydraForge.Application`, `HydraForge.Infrastructure`, `HydraForge.Server`, `HydraForge.Tui`, and `src/web-ui`.
- `HydraForge.slnx` references source projects and all three test projects.
- **Task 1 (Docker Compose + env template) — done.** `docker-compose.yml` runs `pgvector/pgvector:pg16` on host port 5433 (container 5432) with health checks, plus a server profile and optional `search` profile for SearXNG. `.env.example` ships with placeholders for Postgres creds, JWT settings, admin seed, and `SearXng__BaseUrl`. Full stack starts with `docker compose up`.
- **Task 2 (Domain result/error foundation) — done.** `Result<T, Error>` and `Error` live in `src/HydraForge.Domain/Common/Result.cs`. Named error code constants are in place on entities that raise them.
- **Task 3 (EF Core, Npgsql, entities, pgvector, initial migration) — done.** `HydraForgeDbContext` is wired in `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` with snake_case naming, FK cascade configuration for Document→Version, Note→Reminder, Note→ImageAttachment, ChatSession→Message, and `HasPostgresExtension("vector")`. All 50+ Domain entities are mapped; `MemoryEntry.Embedding` and `DocumentChunk.Embedding` are `vector(1536)`. Six migrations are committed (latest `20260603210352_RenameCardDueDateToDueAt`); all applied successfully against the dev Postgres. The `HousekeepingBackgroundService` and cascading-archive services are deferred to later phases per the archive design spec. Archive + housekeeping schema foundation is in place: `ArchivedAt?` on nine entities, `SystemSettings` singleton.
- Persistence DI is registered via `src/HydraForge.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs` (`AddPersistence`), which chains `o => o.UseVector()` on `UseNpgsql`. `DesignTimeHydraForgeDbContextFactory` does the same so `dotnet ef` works.
- Server still exposes default `/weatherforecast` endpoint (Task 4 auth replaces it). TUI still prints `Hello, World!` (out of scope per design).
- **Starting Task 4 Done: Auth, password hashing, JWT, and admin seed.**
- `AGENTS.md` and `CLAUDE.md` have been synced to the current state.

## Scope

### In Scope

- Docker Compose stack for server, PostgreSQL 16, and optional SearXNG profile.
- Environment template for local development and first-run admin seed.
- EF Core/Npgsql infrastructure with DbContext, entity mappings, initial migration, and startup migration application.
- pgvector extension migration and vector columns for planned memory/RAG entities.
- Foundational Domain model entities from the architecture blueprint, enough for EF schema and Phase 1 tests.
- `Result<T, Error>` and typed error code foundation in Domain.
- Basic username/password auth with password hashing, JWT issuance/validation, disabled-user check, and admin seed on first run.
- Global exception middleware returning RFC 7807 `ProblemDetails` with `correlationId`.
- Structured logging using Microsoft logging + Serilog with per-request correlationId.
- `/health` endpoint reporting server, database, and configured LLM-provider connectivity status.
- Audit log infrastructure service capable of recording mutations when future phases call it.
- CI/CD workflow for .NET build/tests and web lint/typecheck/build where dependencies are available.

### Out of Scope

- Project/card/column CRUD endpoints.
- SignalR board updates and presence.
- TUI login UX or lock screen.
- Web UI auth pages or board UI.
- Real LLM provider management, ModelRouter, token usage dashboards, or chat.
- File attachment storage implementation.
- ntfy notifications.
- PostgreSQL test container orchestration beyond what Phase 1 tests need to validate infrastructure wiring.

## Design Approach

Use one Phase 1 milestone branch with independent task branches for each foundation slice. This keeps a shared spec and allows later architect/planner work to split implementation cleanly.

Rejected alternatives:

1. **Single giant implementation branch:** faster to start, but auth, DB, logging, and CI changes become tangled.
2. **Only next-task spec:** less up-front work, but weak for Phase 1 because EF entities, auth, errors, audit, and health share contracts.

Recommended approach: **milestone spec + task branches**. Each task ships independently where possible, with explicit integration points.

## Architecture

### Layer Boundaries

- `HydraForge.Domain`: entities, enums, value objects, `Result<T, Error>`, named error codes, domain interfaces only. No EF Core, HTTP, JWT, SignalR, or logging dependencies.
- `HydraForge.Application`: use cases and service interfaces for auth, audit, health, and persistence abstractions. Expected failures return `Result<T, Error>`.
- `HydraForge.Infrastructure`: EF Core DbContext, Npgsql mappings, migrations, repositories, password hashing implementation, admin seed, external health probes.
- `HydraForge.Server`: ASP.NET Core composition root, auth endpoints, middleware, ProblemDetails mapping, health endpoint, logging pipeline.
- `HydraForge.Tui` and `src/web-ui`: remain scaffold-compatible; no user-facing Phase 1 feature work.

### Data Foundation

Implement the architecture blueprint entities needed for Phase 1 schema, including project space, auth, audit, chat skeleton, LLM provider config, token/image usage, notifications, and personal-space entities. This is schema foundation, not feature implementation.

Key constraints:

- PostgreSQL only; no SQLite fallback.
- All schema changes via EF Core migrations under Infrastructure.
- `CREATE EXTENSION IF NOT EXISTS vector` in migration before vector columns.
- `MemoryEntry.Embedding` and `DocumentChunk.Embedding` use `vector(1536)`.
- User-facing card identity remains per-project `CardNumber`, unique with `ProjectId`.
- Admin cannot access user personal data; schema must preserve owner/user scoping.
- Relationship cycles are not enforced in DB in Phase 1; Application validation comes in Phase 2.

### Auth Foundation

Add local user auth:

- `User` persisted in PostgreSQL.
- Passwords hashed with bcrypt or Argon2. Prefer Argon2id if dependency maturity is acceptable; otherwise bcrypt.
- JWT access token issued by server auth endpoint.
- JWT settings read from config/env.
- Admin user seeded on first run from env vars.
- Disabled users cannot receive tokens.
- No OAuth, SSO, external identity provider, or cookie-only flow.

### Error Handling

Error handling follows D-26:

- Domain expected failures use `Result<T, Error>`.
- Each error has a stable named code constant.
- Server middleware catches unhandled exceptions and returns RFC 7807 `ProblemDetails`.
- Every error response includes `correlationId`.
- Stack traces are logged server-side only, never sent to clients.
- 4xx logs at Warning, 5xx logs at Error.

### Logging and Correlation

- Generate or accept a correlation ID per request.
- Include correlation ID in response headers, ProblemDetails, and logs.
- Configure Serilog for structured logs.
- Default production logging emits warnings/errors; debug level configurable.
- Logs include endpoint, duration, userId when authenticated, status code, error code, and correlationId.

### Health

Expose `/health` with:

- server status;
- database connectivity and migration status;
- LLM provider connectivity placeholder/probe status.

If no LLM providers are configured, health should report LLM status as configured-but-empty or not-configured, not fail the whole service. PostgreSQL down returns unhealthy because the server cannot function without DB.

### Audit Infrastructure

Create `AuditLogEntry` schema and service abstraction:

- records actor, project, entity type/id, action, old/new values, timestamp;
- no automatic broad mutation interception required in Phase 1;
- future Application services call the audit service explicitly;
- audit write failures should be visible in server logs and represented as errors where audit is required.

### Docker and Config

Docker Compose should support:

- PostgreSQL 16 with named volume;
- server container wired to DB;
- optional SearXNG service under `search` profile;
- env-driven connection strings, JWT config, admin seed, logging level, and optional SearXNG URL.

`.env.example` must be runnable after copying to `.env` and changing secrets. Do not claim `docker-compose up` is production-ready; Phase 1 target is local development foundation.

### CI/CD

Add GitHub Actions workflow:

- restore/build .NET solution;
- run .NET tests;
- install pnpm dependencies for web UI;
- run web lint, typecheck, and build;
- cache NuGet and pnpm where simple;
- avoid requiring Docker services unless tests explicitly need PostgreSQL.

If infrastructure tests require PostgreSQL, add a CI PostgreSQL service with pgvector support or split those tests to run only when service is present.

## Testing Strategy

- Domain tests cover `Result<T, Error>`, error codes, entity invariants that exist in Phase 1.
- Application tests cover auth use-case behavior, admin seed orchestration boundaries, and audit service contracts where implemented.
- Infrastructure tests validate EF model configuration and migration application against real PostgreSQL, not SQLite.
- Server tests validate ProblemDetails shape, correlation ID propagation, auth success/failure, disabled-user rejection, and `/health` behavior.
- No FluentAssertions; use plain `Assert.*`.
- Placeholder `UnitTest1` files should be replaced with meaningful tests as each task lands.

## Risks and Mitigations

- **Large schema surface:** implement entity mappings deliberately and test model creation before adding feature behavior.
- **pgvector CI friction:** use a PostgreSQL image with pgvector installed or isolate vector migration tests behind clear CI service setup.
- **Auth package choice drift:** pick one password hasher in plan; document rationale in implementation plan, not a new architecture decision unless it changes D-6.
- **Auto-run migrations at startup in production:** acceptable for Phase 1/local foundation, but keep behavior configurable for future production hardening.
- **LLM health before providers exist:** health must not fail because no provider is configured in Phase 1.

## Acceptance Criteria

- `dotnet build` succeeds.
- `dotnet test` succeeds.
- Web UI lint/typecheck/build commands are wired in CI and pass after dependencies are prepared.
- `docker-compose up` starts PostgreSQL and server locally with copied `.env`.
- Optional `docker-compose --profile search up` includes SearXNG.
- Initial migration creates schema, pgvector extension, and vector columns.
- Server startup applies pending migrations when configured.
- First run seeds exactly one admin from env config when no admin exists.
- Auth endpoint returns JWT for valid enabled user and denies invalid/disabled users.
- Unhandled server exception returns ProblemDetails with correlationId and no stack trace.
- Expected Application failure maps to ProblemDetails with named error code.
- Logs contain correlationId.
- `/health` reports server and DB status.
- Audit infrastructure can persist an `AuditLogEntry`.

## Tasks

> **Current task: 4** — Auth, password hashing, JWT, and admin seed.

- [x] Task 1: Docker Compose and environment template
- [x] Task 2: Domain result/error foundation
- [x] Task 3: EF Core, Npgsql, entities, pgvector, and initial migration
- [X] Task 4: Auth, password hashing, JWT, and admin seed
- [x] Task 5: Global exception middleware and ProblemDetails mapping
- [X] Task 6: Structured logging and correlation ID pipeline
- [X] Task 7: Health endpoint and service probes
- [X] Task 8: Audit log infrastructure (entity schema done; service abstraction pending)
- [ ] Task 9: CI/CD pipeline
- [ ] Task 10: Phase 1 verification and placeholder cleanup

## Suggested Branch Split

- `feat/phase-1-docker-env` → Task 1
- `feat/phase-1-result-errors` → Task 2
- `feat/phase-1-ef-pgvector` → Task 3
- `feat/phase-1-auth` → Task 4
- `feat/phase-1-problemdetails` → Task 5
- `feat/phase-1-logging-correlation` → Task 6
- `feat/phase-1-health` → Task 7
- `feat/phase-1-audit` → Task 8
- `feat/phase-1-ci` → Task 9
- `feat/phase-1-verification` → Task 10

## Self-Review

- No placeholders or TBDs remain.
- Scope is limited to Phase 1 foundation.
- Spec avoids implementation code.
- Architecture constraints from `AGENTS.md`, `CLAUDE.md`, `docs/DECISIONS.md`, and `docs/requirements-and-architecture.md` are preserved.
- `## Tasks` section contains discrete checkbox tasks for planning.
