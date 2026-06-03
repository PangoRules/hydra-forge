# HydraForge

> Self-hosted AI workspace and project management platform. Two interfaces, one brain.

HydraForge is a team tool that combines a Trello-style project board with an AI workspace — chat, memory, documents, calendar, deep research, and more — all running on your own infrastructure. Work from a browser or stay in the terminal; both interfaces have full feature parity.

## What it is

- **Project board** — Kanban pipeline with cards, specs, plans, dependencies, and an agent pipeline (Planner → Developer → Reviewer → Git Agent)
- **AI workspace** — Per-user chat (with project context awareness), brain/memory, documents, gallery, deep research, calendar, notes
- **Dual interface** — Full-featured TUI (Spectre.Console) for terminal power users + mobile-first Web UI (Nuxt 3)
- **Self-hosted** — Your data, your infrastructure, one `docker-compose up`

## Stack

| Layer | Technology |
|---|---|
| Server | .NET 10 / C# (Clean Architecture) |
| TUI | Spectre.Console |
| Web UI | Nuxt 3 + Nuxt UI + Tailwind CSS |
| Database | PostgreSQL 16 + pgvector |
| Real-time | SignalR |

## Quick start

```bash
cp .env.example .env
# Fill in required values in .env

docker-compose up
```

Server starts at `http://localhost:5000`. First boot creates an admin account — credentials printed to server logs.

## Development setup

**Prerequisites:** .NET 10 SDK, Node 22+, pnpm, Docker

```bash
# Install web UI dependencies
cd src/web-ui && pnpm install && cd ../..

# Apply database migrations
dotnet ef database update --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server

# Run server
dotnet run --project src/HydraForge.Server

# Run TUI (separate terminal)
dotnet run --project src/HydraForge.Tui

# Run web UI dev server (separate terminal)
cd src/web-ui && pnpm dev

# Run tests
dotnet test
```

## Repository structure

```
src/
  HydraForge.Domain/        Entities, enums, Result<T,Error>
  HydraForge.Application/   Business logic, services
  HydraForge.Infrastructure/ EF Core, LLM clients, file storage
  HydraForge.Server/        ASP.NET Core API + SignalR
  HydraForge.Tui/           Terminal interface
  web-ui/                   Nuxt 3 browser interface
tests/                      xUnit test projects
docs/                       Architecture & decision documents
```

## Docs

- [`docs/requirements-and-architecture.md`](docs/requirements-and-architecture.md) — Full requirements, data model, 12-phase roadmap
- [`docs/DECISIONS.md`](docs/DECISIONS.md) — Every architectural decision with rationale (D-1–D-32)
- [`docs/agent-platform-vision.md`](docs/agent-platform-vision.md) — Vision, agent pipeline, feature parity

## Contributing / AI agents

See [`CLAUDE.md`](CLAUDE.md) for stack, commands, and conventions.
See [`AGENTS.md`](AGENTS.md) for agent-specific rules and codebase navigation.
