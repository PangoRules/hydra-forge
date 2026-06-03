# HydraForge Agent Notes

Compact repo-specific guidance for OpenCode sessions. Prefer executable files over roadmap prose when they disagree.

## Current State

- This repo is mostly scaffold today, despite large roadmap docs: `src/HydraForge.Server/Program.cs` is the default weather endpoint, `src/HydraForge.Tui/Program.cs` is `Hello, World!`, and Domain/Application/Infrastructure have no real source files yet.
- `docker-compose.yml` and `.env.example` exist but are empty right now. Do not tell users `docker-compose up` or `.env` setup is working until those files are implemented.
- Docs describe intended architecture and settled decisions; use them as constraints for new work, not proof that code already exists.

## Read First

- `CLAUDE.md` for stack, commands, and conventions.
- `docs/DECISIONS.md` before changing architecture; do not re-litigate settled decisions.
- `docs/requirements-and-architecture.md` for roadmap, data model intent, and phase checklist.
- Check manifests/config before trusting prose: `HydraForge.slnx`, `*.csproj`, `src/web-ui/package.json`, `src/web-ui/nuxt.config.ts`.

## Repo Shape

- Solution file is `HydraForge.slnx`; projects target `net10.0` with nullable and implicit usings enabled.
- Backend boundaries: `Domain` -> `Application` -> `Infrastructure` -> `Server`/`Tui`. Keep Domain free of EF Core, HTTP, SignalR, and infrastructure concerns.
- `src/HydraForge.Server` is ASP.NET Core; current entrypoint is `Program.cs`.
- `src/HydraForge.Tui` is the terminal client; Spectre.Console is planned but not currently referenced.
- `src/web-ui` is a separate pnpm package. Although docs say Nuxt 3, `package.json` currently uses Nuxt `^4.4.6`, Nuxt UI `^4.8.1`, Tailwind `^4.3.0`, TypeScript `^6.0.3`, and `pnpm@11.5.0`.
- Web UI still contains the Nuxt UI starter (`app.vue`, `pages/index.vue`); do not assume HydraForge screens or composables exist.

## Commands

- Build .NET: `dotnet build`
- Run all .NET tests: `dotnet test`
- Run one test project: `dotnet test tests/HydraForge.Domain.Tests/HydraForge.Domain.Tests.csproj`
- Filter a .NET test: `dotnet test --filter FullyQualifiedName~TestName`
- Run server: `dotnet run --project src/HydraForge.Server`
- Run TUI: `dotnet run --project src/HydraForge.Tui`
- Install web deps: `cd src/web-ui && pnpm install`
- Web dev server: `cd src/web-ui && pnpm dev`
- Web typecheck: `cd src/web-ui && pnpm typecheck`
- Web lint: `cd src/web-ui && pnpm lint`
- Web build: `cd src/web-ui && pnpm build`
- On a fresh checkout, run `pnpm install` or `pnpm exec nuxt prepare` before web lint/typecheck because `eslint.config.mjs` imports generated `.nuxt/eslint.config.mjs`.

## Testing Notes

- Test projects are xUnit with plain `Assert.*`; do not add FluentAssertions.
- Existing tests are placeholder `UnitTest1` files. Add real tests with new Domain/Application business logic.
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

## Database And Migrations

- EF Core/Npgsql packages and DbContext are not present yet. Add infrastructure deliberately before generating migrations.
- All future schema changes should go through EF Core migrations under Infrastructure; do not hand-edit database schema.
- pgvector is part of the planned architecture; ensure the extension exists before adding vector columns.

## Web UI Conventions

- Nuxt config enables `@nuxt/eslint` and `@nuxt/ui`, uses `~/assets/css/main.css`, and prerenders `/`.
- ESLint stylistic settings use no trailing comma and `1tbs` brace style via Nuxt config.
- Mobile-first, keyboard-navigable UI is a settled requirement; preserve this when replacing starter screens.

## Commit/Docs Discipline

- Do not commit unless explicitly asked.
- If a change creates a real new architecture decision, update `docs/DECISIONS.md`, `docs/requirements-and-architecture.md`, and `CLAUDE.md` if commands or conventions change.
- Migrations, once the EF stack exists, should be committed with the entity/schema changes that require them.
