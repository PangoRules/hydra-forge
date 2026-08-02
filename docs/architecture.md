# HydraForge — Technical Architecture

> **Version:** 1.0
> **Date:** 2026-06-03

---

## Table of Contents

1. [Layered Architecture (Clean Architecture)](#1-layered-architecture-clean-architecture)
2. [Real-Time Architecture](#2-real-time-architecture)
3. [LLM Integration](#3-llm-integration)
4. [Error Handling Architecture](#4-error-handling-architecture)
5. [Technology Stack](#5-technology-stack)
6. [Repository Structure](#6-repository-structure)

---

## 1. Layered Architecture (Clean Architecture)

```
┌────────────────────────────────────────────┐
│         Presentation Layer                  │
│  ┌────────────┐      ┌────────────┐       │
│  │  TUI        │      │  Web UI    │       │
│  │ (Spectre)   │      │ (Nuxt+Vue) │       │
│  └──────┬─────┘      └──────┬─────┘       │
│         │                   │              │
│  ┌──────┴───────────────────┴─────┐        │
│  │       API Layer (Controllers)  │        │
│  │       + SignalR Hubs           │        │
│  └──────────────┬─────────────────┘        │
├─────────────────┼──────────────────────────┤
│  ┌──────────────▼─────────────────┐        │
│  │     Application Layer          │        │
│  │  (Use Cases / MediatR / CQRS)  │        │
│  │  - CardService                 │        │
│  │  - ColumnService               │        │
│  │  - SpecService / PlanService   │        │
│  │  - CardDependencyService       │        │
│  │  - ProjectContextSnapshotService│        │
│  │    + ProjectContextSnapshotRenderer (pure, no LLM)│
│  │    + IProjectSnapshotRefresher port│
│  │  - IProjectBoardEventPublisher  │        │
│  │    + ProjectBoardEventEnvelope  │        │
│  │  - ChatService                 │        │
│  │  - ModelRouter                 │        │
│  │  - ContextCompressor           │        │
│  │  - AuditService                │        │
│  │  - NotificationService         │
│  │  - INotificationHubBus (port)  │        │
│  │  - IKeyVault (port)            │        │
│  └──────────────┬─────────────────┘        │
├─────────────────┼──────────────────────────┤
│  ┌──────────────▼─────────────────┐        │
│  │     Domain Layer               │        │
│  │  (Entities, Value Objects,     │        │
│  │   Domain Events, Interfaces)   │        │
│  └──────────────┬─────────────────┘        │
├─────────────────┼──────────────────────────┤
│  ┌──────────────▼─────────────────┐        │
│  │     Infrastructure Layer       │        │
│  │  - EF Core / Npgsql provider   │        │
│  │  - Git service                 │        │
│  │  - LLM client (OpenAI/etc)     │        │
│  │    + AesGcmKeyVault (IKeyVault)│        │
│  │    + LlmServiceCollectionExtensions│        │
│  │  - SignalR messaging           │        │
│  │    + BoardHub, PresenceHub     │        │
│  │    + NotificationHub            │
│  │    + SignalRProjectBoardEventPublisher│        │
│  │    + SignalRNotificationHubBus  │
│  │    + RealtimeServiceCollectionExtensions│
│  │  - File storage (IFileStore:   │        │
│  │    LocalFileStore / S3FileStore)│        │
│  └────────────────────────────────┘        │
└────────────────────────────────────────────┘
```

**Dependency direction:** Domain ← Application ← Infrastructure ← Server/TUI. Domain knows nothing about EF Core, HTTP, or SignalR.

### Layer Responsibilities

| Layer | Project | Responsibility |
|---|---|---|
| Domain | `HydraForge.Domain` | Entities, enums, interfaces, `Result<T,Error>`, error codes. No external dependencies. |
| Application | `HydraForge.Application` | Use cases, services (CardService, ModelRouter, etc.), DTOs. Depends on Domain only. |
| Infrastructure | `HydraForge.Infrastructure` | EF Core DbContext, migrations, LLM clients, file storage (`IFileStore`: `LocalFileStore` fallback + `S3FileStore` for MinIO/AWS S3), SignalR. Implements Domain interfaces. |
| Server | `HydraForge.Server` | ASP.NET Core controllers, SignalR hubs, middleware, `Program.cs`. |
| TUI | `HydraForge.Tui` | Spectre.Console views, commands, screen rendering. |
| Web UI | `src/web-ui` | Nuxt 4 app under `app/` (pages, components, composables). Talks only to Server via HTTP + SignalR. |

---

## 2. Real-Time Architecture

Offline mode is rejected (see `DECISIONS.md` D-3). TUI connects directly to the server over the network (VPN when remote). All mutations go through the API; SignalR pushes updates to all connected clients.

```
TUI / Web UI
  │
  │  HTTP (mutations: create, edit, move, delete)
  ▼
┌──────────────────┐
│  Server          │
│  → Validate      │
│  → Apply to DB   │
│  → Broadcast     │
└────────┬─────────┘
         │
    SignalR hub
    (push to all
     connected clients)
         │
  ┌──────┴──────┐
  TUI          Web UI
  (receives    (receives
   update)      update)
```

### System Topology

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 LAN / VPN                                  │
│                                                                  │
│   ┌──────────────┐     ┌──────────────┐     ┌──────────────┐   │
│   │ Workstation A │     │ Workstation B │     │ Remote Dev   │   │
│   │ ┌──────────┐  │     │ ┌──────────┐  │     │ (via VPN)    │   │
│   │ │  TUI     │  │     │ │  TUI     │  │     │ ┌──────────┐ │   │
│   │ └──────────┘  │     │ └──────────┘  │     │ │  TUI     │ │   │
│   │ ┌──────────┐  │     │ ┌──────────┐  │     │ └──────────┘ │   │
│   │ │  Web UI  │  │     │ │  Web UI  │  │     │ ┌──────────┐ │   │
│   │ └──────────┘  │     │ └──────────┘  │     │ │  Web UI  │ │   │
│   └──────┬───────┘     └──────┬───────┘     │ └──────────┘ │   │
│          │                    │              └──────┬───────┘   │
│          └────────────┬───────┴─────────────────────┘           │
│                       │                                          │
  │              ┌────────▼────────┐  ┌──────────────┐              │
  │              │  Server         │  │  Git Remote  │              │
  │              │  .NET + SignalR  │  │  (GitHub/    │              │
  │              │  - REST API      │  │   GitLab/    │              │
  │              │  - SignalR hubs  │  │   self-host) │              │
  │              │  - LLM broker    │  └──────────────┘              │
  │              │  - IFileStore    │                                 │
  │              └────────┬────────┘                                 │
  │                       │                                          │
  │              ┌────────▼────────┐  ┌──────────────┐  ┌──────────┐│
  │              │  PostgreSQL 16  │  │  MinIO       │  │ SearXNG  ││
  │              │  (all state)    │  │  (S3 storage) │  │ (optional│
  │              └─────────────────┘  │  port 9000   │  │  profile)││
  │                                   │  console 9001│  └──────────┘│
  │                                   └──────────────┘              │
  └─────────────────────────────────────────────────────────────────┘
```

> All services run via `docker-compose up`. MinIO runs alongside Postgres as a core service. SearXNG enabled with `--profile search` or auto-detected if Deep Research is enabled.

### TUI Connectivity Behavior

- **Online:** Normal operation. JWT auth on startup, stored in user config.
- **Connection lost:** Immediate lock screen — `⚠ Server unreachable. Retrying... (correlationId: ...)` with exponential backoff `[5s, 10s, 30s, 60s]`. Uses `LockScreen` and `ConnectionManager` services.
- **Reconnected:** Auto-resumes. Re-fetches board state. No manual refresh required.

### SignalR Hubs

Three hubs handle real-time communication:

| Hub | Route | Purpose | Events |
|---|---|---|---|
| `BoardHub` | `/hubs/board` | Board mutation broadcasts | `OnBoardEvent(ProjectBoardEventEnvelope)` |
| `PresenceHub` | `/hubs/presence` | Ephemeral presence | `UserJoined`, `UserLeft`, `CardFocused` |
| `NotificationHub` | `/hubs/notifications` | In-app notification push | `OnNotificationReceived(NotificationReceivedEvent)` |

**Board mutation flow:**
1. HTTP mutation request → controller → Application service
2. Service applies to DB (via EF Core)
3. Service calls `IProjectBoardEventPublisher.PublishAsync(envelope)`
4. `SignalRProjectBoardEventPublisher` fans out to all clients in `BoardHub.ProjectGroup(projectId)`
5. Clients receive typed `OnBoardEvent` with full `ProjectBoardEventEnvelope`

**Presence flow:**
- `JoinProject(projectId)` → adds client to project group, broadcasts `UserJoined` to others
- `FocusCard(projectId, cardId)` → broadcasts `CardFocused` to project group
- `LeaveProject(projectId)` or disconnect → removes from group, broadcasts `UserLeft`
- All presence state is in-memory `ConcurrentDictionary` — no DB writes

**Notification push flow:**
1. Application service completes a mutating operation (e.g. card move, assignment, comment)
2. Service calls `INotificationService.NotifyAsync(request)` — single enforcement point, self-notification excluded internally
3. `NotificationService` writes `Notification` row to DB
4. `NotificationService` calls `INotificationHubBus.SendNotificationAsync(userId, notification, ct)`
5. `SignalRNotificationHubBus` sends `OnNotificationReceived(NotificationReceivedEvent)` to the `user-{userId}` SignalR group
6. Connected clients receive the typed event and update unread count / notification panel

**DI wiring:** `RealtimeServiceCollectionExtensions.AddRealtimeServices()` in Infrastructure registers `IProjectBoardEventPublisher → SignalRProjectBoardEventPublisher` and `INotificationHubBus → SignalRNotificationHubBus`.

---

## 3. LLM Integration

```
TUI User: ──▶ Server ──▶ LLM Provider (OpenAI, Claude, Ollama, etc.)
  "/agent write spec for card #42"     │
                                       │
Web UI User: ◀──────────────────────────┘
  (Result streamed back via SignalR in real-time)
```

**Rules:**
- Server is the **only** component that calls LLMs. TUI and Web UI never call LLMs directly.
- Admin configures API keys centrally. Users never touch credentials.
- **API keys encrypted at rest** via `IKeyVault`/`AesGcmKeyVault` (AES-256-GCM, key from `Llm:EncryptionKey` config). Migration `20260731000000_ReencryptLlmProviderApiKeys` backfills existing placeholder rows. Decrypted at call time in `ILlmClientFactory` — never cached in adapter state.
- All LLM calls go through `ModelRouter`, which resolves feature + user context → provider/model selection. Router returns `RouteDecision`; the calling service executes the call and handles fallback/retry.
- Always code to interfaces: `ILlmClient`, `IImageClient`, `IEmbeddingClient`, `IKeyVault`, `IRoutingConfigProvider`.

### ModelRouter

```
Feature Request (e.g. ProjectChat)
  │
  ▼
┌──────────────────────────────────────────────┐
│  ModelRouter                                 │
│  ┌────────────────────────────────────────┐  │
│  │  IRoutingConfigProvider                │  │
│  │  (Application port, EF-backed)        │  │
│  └────────────────────────────────────────┘  │
│  1. Look up FeatureRoutingConfig             │
│     → Default tier for this AiFeature       │
│  2. Apply user tier ceiling                  │
│     → Cap to MaxUserTier if exceeded.       │
│     null = locked to default.                │
│  3. Find model at resolved tier              │
│     → GetEnabledModelsAtTierAsync()          │
│  4. Context-window guard                     │
│     → estimatedTokens > MaxTokens?          │
│     → Auto-bump tier (Economy→Standard→Premium)│
│  5. Build fallback chain                     │
│     → Walk LlmProvider.FallbackProviderId    │
│     → Cycle detection via HashSet<Guid>      │
│  6. Return RouteDecision                     │
│     (Primary + Fallbacks)                    │
│  ════════════════════════════════════════    │
│  Calling service handles:                    │
│  → ILlmClient.StreamChatAsync()              │
│  → Retry fallback on rate-limit / 5xx       │
│  → Log TokenUsageRecord                      │
└──────────────────────────────────────────────┘
```

### Prompt Caching Strategy

`ProjectContextSnapshot.TemplateContent` and user memory blocks are formatted as cache-eligible blocks in `ILlmClient`. On repeated project chat calls with the same board state, the provider returns cached tokens — reducing cost. Tracked in `TokenUsageRecord.CachedTokens`.

### Context Compression

When injected context (board state + memory + card detail) exceeds the configurable token threshold, `ContextCompressor` auto-summarizes before sending. Keeps costs predictable on large projects.

### LLM Adapters

| Adapter | `AdapterType` value(s) | Covers |
|---|---|---|
| `OpenAiCompatibleAdapter` | `OpenAiCompatible` | OpenAI, Groq, DeepSeek, OpenRouter, vLLM, llama.cpp, any OpenAI-compat chat endpoint |
| `AnthropicAdapter` | `Anthropic` | Claude models — `cache_control` ephemeral prompt-caching blocks, `/v1/messages` streaming |
| `OllamaAdapter` | `Ollama` | Local Ollama server — no API key (`IKeyVault` not injected) |
| `DallEAdapter` | `DallE` | OpenAI image generation — `/images/generations` + `/images/edits` inpaint |
| `StabilityAiAdapter` | `StabilityAi` | Stability AI image generation |
| `ComfyUiAdapter` | `ComfyUi`, `Diffusers` | Local/self-hosted workflow-API image generation (`/prompt` submit + `/history` poll, 2s poll interval, 5-min timeout). One adapter class serves both enum values — `Diffusers` setups speak the same workflow-API wire shape as ComfyUI, so `LlmClientFactory` maps both to the same registered instance (only one named `HttpClient`, `"comfyui"`). See D-62. |

All adapters except `OllamaAdapter` decrypt their provider's API key via `IKeyVault` at call time (never cached in adapter state, see D-59).

### Scheduled Jobs (Hangfire)

Persistent recurring jobs run on Hangfire + `Hangfire.PostgreSql` (same Postgres instance the app already runs — no new infra service, see D-57). One recurring job exists today: `"ai-narrative-gen"`, calling `ProjectContextSnapshotService.GenerateAiNarrativeForAllActiveProjectsAsync` on the admin-configurable `SystemSettings.AiNarrativeGenerationTimeUtc` schedule (default midnight UTC). For every active (non-archived) project with an existing snapshot, it resolves a model via `IModelRouter`, streams a narrative from the project's `TemplateContent`, writes `ProjectContextSnapshot.AiNarrative` + `AiNarrativeGeneratedAt`, and records the call via `IUsageRecorder` (`UserId = Guid.Empty` — a system/batch identity, not a real user, so no `UserTokenBudget` is charged; only `TokenUsageRecord` is written for admin usage-dashboard visibility). Per-project failures are caught and logged; one project's failure never aborts the batch.

- **Dashboard**: `/hangfire`, admin-only. JWT bearer auth reads the `auth_token` cookie for `/hangfire` paths (dashboard is a browser UI, not an API client) — `AdminRequiredAuthFilter` then checks `IsInRole("Admin")`.
- **Registration timing**: the recurring job is registered from `app.Lifetime.ApplicationStarted.Register(...)`, reading `SystemSettings.AiNarrativeGenerationTimeUtc` once at startup — an admin changing the generation time takes effect on next server restart, not live.
- **Test env gating**: both `AddHangfire(...)` and `UseHangfireDashboard(...)` are skipped when `IsEnvironment("Test")` — `Hangfire.PostgreSql` requires a real connection string that `WebApplicationFactory` test fixtures don't provide.
- **Narrative display**: read-only, no edit path. Web UI "View Narrative" button (board header) → modal; TUI `v` key on the board screen → overlay viewer. See D-58.

---

## 4. Error Handling Architecture

**Principle: program like the Air Force. Every error is accounted for. Nothing silently swallowed. Nothing raw exposed to clients.**

```
Request
  │
  ▼
┌─────────────────────────────────────────┐
│  Global Exception Middleware            │
│  (catches ALL unhandled exceptions)     │
│  → maps to ProblemDetails (RFC 7807)    │
│  → assigns correlationId               │
│  → logs at Error level with context    │
│  → returns structured JSON, never HTML  │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│  Application Layer (Result Pattern)     │
│  Result<T, Error> — no exceptions for   │
│  expected failures (not found, invalid, │
│  permission denied, limit exceeded)     │
│  → typed Error with named code         │
│  → controller maps Result → HTTP status │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│  Infrastructure Layer                   │
│  DB errors → caught, wrapped in Error   │
│  LLM errors → caught, user-friendly msg │
│  Git errors → caught, explicit message  │
│  ntfy errors → caught, logged, non-fatal│
└─────────────────────────────────────────┘
```

### ProblemDetails Shape

Every error response follows RFC 7807:

```json
{
  "type": "https://hydraforge.io/errors/card-not-found",
  "title": "Card not found",
  "status": 404,
  "detail": "Card 'f3a1...' does not exist or you don't have access.",
  "correlationId": "req_7fKz9mXp"
}
```

### Client Error Behavior

**TUI:**
- Connection lost → lock screen: `⚠ Server unreachable. Retrying... (correlationId: ...)` with exponential backoff
- API error → status bar error panel: shows `title` + `correlationId`, dismissible
- Never crashes to unhandled exception — all paths return to a stable TUI state

**Web UI:**
- API error → typed toast: shows `detail` + `correlationId` copy button
- 401/403 → redirect to login / access denied page
- 5xx → "Something went wrong" banner with correlationId for support

### Structured Logging Format

```json
{
  "timestamp": "...",
  "level": "Error",
  "correlationId": "req_7fKz9mXp",
  "userId": "usr_...",
  "endpoint": "PUT /api/cards/f3a1/move",
  "durationMs": 12,
  "error": { "code": "CARD_NOT_FOUND", "message": "..." }
}
```

Log levels: 4xx → `Warning`, 5xx → `Error`. Production default: errors and warnings only. Debug configurable.

### File Storage Architecture

File attachments are stored via `IFileStore` (Application layer interface) with two implementations:

| Implementation | When used | Storage path |
|---|---|---|
| `LocalFileStore` | `FileStorage:Provider=Local` (default bare-metal fallback) | `{root}/{userId}/cards/{cardId}/{guid}` |
| `S3FileStore` | `FileStorage:Provider=S3` (MinIO/AWS S3, recommended) | `{bucket}/{userId}/cards/{cardId}/{guid}` |

**Key hierarchy:** `{userId}/{sourceType}/{sourceId}/{guid}` — user-prefixed, source-type namespaced (`cards/`, future `chat/`, `notes/`). No user filenames, dates, or project IDs in the storage path. GUID prevents enumeration and collisions.

**MinIO** runs as a core Docker Compose service (ports 9000/9001) — the server depends on its health. Switch between Local and S3 via `FileStorage:Provider` in config. `InitializeAsync()` on `S3FileStore` creates the bucket automatically on startup.

### External Service Resilience

Failures in these services must never bring down the core board:

| Service | Failure Behavior |
|---|---|
| LLM provider | Surface error in chat panel; board continues working |
| Git remote | Surface error in agent output; board continues working |
| ntfy | Log at Warning, skip notification; board continues working |
| PostgreSQL | 503 response; server cannot function without DB — fail loudly |
| MinIO / S3 | Upload returns 503 `FileStoreUnavailable`; download/delete also return 503. Board still functions read-only without file store — card metadata remains intact. |

---

## 5. Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| **Server** | .NET 10 / C# | Primary stack. Clean Architecture built-in. Great DI. Strong type system. |
| **TUI** | .NET + Spectre.Console | Same language as server. Rich terminal UI. Full keyboard support. |
| **Web UI** | Nuxt 4 + Vue 3 + Tailwind CSS + Nuxt UI | Familiar, fast DX. Mobile-first. SSR for fast first load. |
| **Database** | PostgreSQL 16 + pgvector | MVCC handles concurrent multi-user writes. pgvector powers RAG and Brain/Memory semantic search. Native full-text search. EF Core Npgsql provider. |
| **Real-time** | SignalR (WebSocket + SSE fallback) | Built into ASP.NET Core. Battle-tested. Auto-fallback. |
| **Tests** | xUnit | Default .NET testing. No FluentAssertions (prone to deprecation). Plain `Assert.*` only. |
| **API documentation** | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` | Built-in OpenAPI 3.1 doc generation at `/openapi/v1.json`. Scalar UI at `/scalar/v1`. Replaces deprecated Swashbuckle/Swagger (see D-33). |
| **Containerization** | Docker + docker-compose | Single-command setup. Portable. Reproducible. |
| **Architecture pattern** | Clean Architecture (DI, SOLID) | Testable, maintainable, readable. |
| **Web search** | SearXNG | Open-source, self-hosted metasearch. Bundled as optional Docker service. |
| **Push notifications** | ntfy | Open-source push. No email infrastructure. Per-user topic. |
| **Package manager (web)** | pnpm | Fast, efficient disk use. |
| **Scheduled jobs** | Hangfire + `Hangfire.PostgreSql` | Persistent recurring jobs on storage we already run. Survives restarts, auto-retries, admin-visible dashboard/history (see D-57). |

---

### 6.1. TUI Configuration Management

The TUI manages user-specific settings and preferences in `.hydraforge/config.json` at the repo root (D-49 — deliberately not the user's home directory). This configuration is handled by the `ConfigStore` service, which provides:

- **Persistence:** Reads and writes settings to a JSON file with `0600` permissions (read/write for owner only).
- **Default Values:** Returns a default `TuiConfig` if the file is missing; malformed JSON throws rather than silently falling back, so a corrupted file surfaces as an error instead of a silent re-login.

The `ConfigStore` is registered in `HydraForge.Tui`'s `Program.cs` and injected into services requiring access to user preferences.


```
hydra-forge/
├── .github/                  # CI/CD actions
├── docker-compose.yml        # One-command setup
├── Dockerfile                # Server container
├── .env.example              # Config template
├── README.md
│
├── src/
│   ├── HydraForge.Server/      # ASP.NET Core server
│   │   ├── Controllers/
│   │   │   └── Projects/
│   │   │       └── ProjectSnapshotController.cs  # GET /api/projects/{projectId}/ProjectSnapshot
│   │   ├── Hubs/              # PresenceHub (/hubs/presence)
│   │   ├── Errors/           # ProblemDetails mapping
│   │   ├── Middleware/
│   │   ├── HttpTests/        # *.http test files (Scalar-compatible)
│   │   ├── Program.cs        # AddOpenApi() + MapOpenApi() + MapScalarApiReference()
│   │   └── ...               # OpenAPI at /openapi/v1.json, Scalar UI at /scalar/v1
│   │
│   ├── HydraForge.Application/ # Use cases, services, DTOs
│   │   ├── Cards/
│   │   ├── Columns/
│   │   ├── Projects/
│   │   ├── ProjectSnapshots/   # IProjectSnapshotRefresher, ProjectContextSnapshotService, ProjectContextSnapshotRenderer
│   │   ├── Realtime/            # IBoardHub, IProjectBoardEventPublisher, ProjectBoardEventEnvelope
│   │   ├── Specs/
│   │   ├── Plans/
│   │   ├── Chat/
│   │   ├── Memory/
│   │   ├── Notifications/
│   │   ├── Audit/
│   │   ├── Llm/
│   │   └── Common/
│   │
│   ├── HydraForge.Domain/      # Entities, value objects, enums
│   │   ├── Entities/
│   │   ├── Enums/
│   │   └── Interfaces/
│   │
│   ├── HydraForge.Infrastructure/  # EF Core, PostgreSQL, LLM client, git, file storage
│   │   ├── Persistence/
│   │   ├── Auth/
│   │   ├── Audit/
│   │   ├── FileStorage/            # LocalFileStore, S3FileStore
│   │   ├── Llm/                    # AesGcmKeyVault, LlmServiceCollectionExtensions
│   │   ├── Attachments/            # EfAttachmentRepository, DI extensions
│   │   ├── Realtime/            # BoardHub, SignalRProjectBoardEventPublisher, RealtimeServiceCollectionExtensions
│   │   └── Health/
│   │
│   ├── HydraForge.Tui/         # Spectre.Console TUI
│   │   ├── Commands/         # CLI commands (move, create, edit)
│   │   ├── Views/            # Screen rendering
│   │   └── Program.cs
│   │
│   └── web-ui/               # Nuxt 4 + Vue + Tailwind
│       ├── app/
│       ├── public/
│       └── nuxt.config.ts
│
├── tests/
│   ├── HydraForge.Domain.Tests/
│   ├── HydraForge.Application.Tests/
│   ├── HydraForge.Infrastructure.Tests/
│   └── HydraForge.Server.Tests/
│
└── docs/
    ├── scope.md              # Scope / Statement of Work
    ├── functional-spec.md    # Functional Requirements (FR-1…FR-194)
    ├── architecture.md       # This document
    ├── data-model.md         # Data Model / ERD
    ├── glossary.md           # Terminology
    ├── DECISIONS.md          # Architecture decision records (D-1…)
    └── agent-platform-vision.md
```
