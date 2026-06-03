# HydraForge — Agent Guidelines

This file governs how AI agents operate in this repository. Read it fully before making any changes.

## Project orientation

HydraForge is a self-hosted AI workspace + project management platform. Two interfaces share one backend:
- `.NET TUI` (Spectre.Console) — full-featured terminal interface
- `Nuxt 3 Web UI` — browser interface with mobile-first responsive design

Server is always the authority. No offline mode. No local state outside the server.

## Before you touch anything

1. Read `CLAUDE.md` — stack, commands, conventions
2. Read `docs/DECISIONS.md` — every architectural decision with rationale. Do not re-litigate settled decisions.
3. Read `docs/requirements-and-architecture.md` — full data model, FRs, and phase plan

## Codebase map

```
src/HydraForge.Domain/          Entity classes, enums, Result<T,Error>, error codes, interfaces
src/HydraForge.Application/     Business logic services, use cases, DTOs — no EF Core, no HTTP
src/HydraForge.Infrastructure/  EF Core DbContext + migrations, LLM clients, file storage
src/HydraForge.Server/          ASP.NET Core — controllers, SignalR hubs, middleware, Program.cs
src/HydraForge.Tui/             Spectre.Console terminal app
src/web-ui/                     Nuxt 3 + Nuxt UI + Tailwind (pnpm)
tests/                          xUnit test projects
docs/                           Architecture, decisions, vision documents
docker-compose.yml              Full stack: server + PostgreSQL + SearXNG (optional)
.env.example                    All required environment variables
```

## How to find things

| Looking for | Where |
|---|---|
| Entity definitions | `src/HydraForge.Domain/Entities/` |
| Business logic | `src/HydraForge.Application/<Feature>/` |
| Database schema | `src/HydraForge.Infrastructure/Persistence/` |
| API endpoints | `src/HydraForge.Server/Controllers/` |
| Real-time hubs | `src/HydraForge.Server/Hubs/` |
| Error middleware | `src/HydraForge.Server/Middleware/` |
| EF Core migrations | `src/HydraForge.Infrastructure/Persistence/Migrations/` |
| Web UI pages | `src/web-ui/app/pages/` |
| Web UI components | `src/web-ui/app/components/` |
| Composables / API calls | `src/web-ui/app/composables/` |

## Verification commands

Always run these before reporting work complete:

```bash
# .NET — must pass with 0 errors, 0 warnings
dotnet build

# Tests — must all pass
dotnet test

# Web UI type check
cd src/web-ui && pnpm typecheck

# Web UI lint
cd src/web-ui && pnpm lint
```

Never claim a task is done without running the relevant verification. Show the output.

## Rules — must follow

**Architecture:**
- Domain layer has zero knowledge of EF Core, HTTP, or SignalR — never add those imports to Domain
- Application layer has zero knowledge of EF Core — it uses repository interfaces defined in Domain
- All LLM calls go through `ModelRouter` in Application — never call `ILlmClient` directly from a controller
- Never bypass `CardDependencyService.ValidateAcyclic()` when creating `CardRelationship` records

**Error handling:**
- Return `Result<T, Error>` from Application services — never throw for expected failures
- Every new error condition needs a named error code constant in Domain
- Never expose raw exception messages or stack traces to clients

**Database:**
- All schema changes via EF Core migration — never hand-edit the DB
- After adding a new entity, generate a migration and verify it applies cleanly
- pgvector extension must exist before any `vector` column is used

**Testing:**
- xUnit only — no FluentAssertions
- Never mock PostgreSQL — use a real test DB instance
- New business logic in Application/Domain needs a test

**Auth / security:**
- Never skip JWT validation
- Admin cannot access user personal data — enforce in every query that touches personal-space entities
- LLM API keys are server-side only — never log them, never return them to clients

**Feature parity:**
- Every feature implemented in Web UI must be implementable in TUI (even if TUI implementation is a later phase)
- Never design an API that only a browser can use (e.g. no cookie-only auth, no browser-only flows)

## Rules — must NOT do

- Do not add FluentAssertions
- Do not add offline/sync logic — D-3 explicitly rejects this
- Do not let users configure their own LLM providers — admin only (D-23)
- Do not write to `ProjectContextSnapshot.AiNarrative` from a board mutation handler — nightly job only (D-32)
- Do not expose raw GUIDs as card identifiers to users — use `Card.CardNumber` (#42 style)
- Do not create circular `CardRelationship` records — validate acyclic before every insert
- Do not add multi-tenant logic — each install is single-tenant (D-8)
- Do not add SSO/OAuth — basic auth only (D-6)

## Commit discipline

- Commits reference the phase they belong to: `feat(phase-2): add CardRelationship CRUD`
- Never commit with failing tests
- Never commit secrets or `.env` files (`.env.example` only)
- Migrations are committed alongside the entity changes that require them

## Docs updates

When you make a decision that changes the architecture, update:
1. `docs/DECISIONS.md` — add a new D-## entry
2. `docs/requirements-and-architecture.md` — update the relevant FR, data model, or phase checklist
3. `CLAUDE.md` — if a convention or command changes
