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

# Format C# code (CSharpier — same formatter/version as the nvim setup's format-on-save,
# pinned via .config/dotnet-tools.json so CI and any clone stay in sync)
dotnet csharpier format .

# Check formatting without writing (what CI runs)
dotnet csharpier check .

# Run server (from repo root)
dotnet run --project src/HydraForge.Server

# Run TUI (from repo root)
dotnet run --project src/HydraForge.Tui

# Web UI dev server
cd src/web-ui && pnpm dev

# Web UI E2E tests (Playwright) — requires the API server (Development env) and `pnpm dev` already running
cd src/web-ui && pnpm test:e2e

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

# API docs (OpenAPI JSON): http://localhost:5000/openapi/v1.json
# API reference (Scalar UI): http://localhost:5000/scalar/v1

# Docker — full stack (Postgres + MinIO + Server)
docker compose up

# Docker — Postgres + MinIO only (no server)
docker compose up -d postgres minio

# MinIO console: http://localhost:9001 (user: minioadmin / pass: minioadmin)

# Docker `server`/`web` services COPY source at image build time (no volume mount).
# A code change is invisible in the running container until rebuilt:
docker compose up -d --build server web
# `pnpm dev` (Web UI) and `dotnet run` (Server) hot-reload as normal — this only
# bites the Docker Compose path. If a feature "isn't showing up" in Docker but
# the code is clearly there, rebuild before debugging further.

# File storage toggle (Local ↔ S3):
#   Set FILE_STORAGE_PROVIDER=S3 in .env to use MinIO instead of local FS
```

### File storage

- `IFileStore` in Application layer — `LocalFileStore` (bare-metal fallback) and `S3FileStore` (MinIO/AWS S3, recommended)
- MinIO is a core Docker Compose service alongside Postgres — ports 9000 (S3 API) and 9001 (console)
- Storage key hierarchy: `{userId}/{sourceType}/{sourceId}/{guid}` — e.g. `a1b2c3d4/cards/e5f6g7h8/abc123`
- Switch provider by changing `FileStorage:Provider` in config (or env `FILE_STORAGE_PROVIDER`)
- S3/MinIO bucket auto-created on startup via `IFileStore.InitializeAsync()`
- Default max file size: 10 MB. Supported types: PNG/JPEG/GIF/WebP, PDF, text, JSON/XML/HTML/CSV, ZIP, Office docs
- `.env.example` has commented MinIO config block — uncomment `FileStorage__Provider=S3` and related vars to enable

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

**File storage — key rules:**
- `IFileStore.StoreAsync` returns `Result<string>` (the storage key), never throws for expected failures
- Storage keys are opaque: `{userId}/{sourceType}/{sourceId}/{guid}` — no user filenames, dates, or projects
- Metadata (filename, content-type, size) lives in DB `Attachment` entity, never in the storage path
- Attachment metadata is committed **after** file-store success — if store fails, no orphaned DB rows
- On delete: metadata removed first, then file-store delete attempted (non-fatal if file-store fails)
- `S3FileStore.InitializeAsync()` auto-creates bucket — called from `Program.cs` during startup, logs warning on failure (doesn't crash)
- `LocalFileStore` is bare-metal fallback only — MinIO is the recommended default

**Error handling — non-negotiable:**
- Business logic returns `Result<T, Error>` — never throw exceptions for expected failures
- All errors have a typed error code (e.g. `CARD_NOT_FOUND`, `DEPENDENCY_CYCLE_DETECTED`)
- Controllers map expected `Result<T, Error>` failures to ProblemDetails RFC 7807 with `correlationId` and named `code`; global exception middleware catches everything else
- Stack traces never reach clients
### Notification trigger patterns (Plan 7 lessons)

- **`NotifyAsync` calls must be wrapped in `try/catch`** — a notification failure must not block the business operation (card move, comment, project update). Each trigger call site wraps `await _notifService.NotifyAsync(...)` in try/catch. The `IWarnLogger` abstraction (`IWarnLogger.LogWarning`) logs failures instead of letting them propagate — optional constructor param with `NullWarnLogger` default so existing DI registrations don't break. `ConsoleWarnLogger` writes to `stderr`. External service failures (LLM, Git, ntfy, notification) must never crash the board.
- **Test fakes shared across test files** — `FakeNotificationRepository` and `FakeNotificationHubBus` in `NotificationServiceTests` changed from `private` to `public` (with `[assembly: InternalsVisibleTo]`) so `NotificationTriggerTests` can reuse them. When a new test file needs the same fake, make it `public` instead of duplicating.
- **Dependency resolution notification fires on archive, not move:** Plan 7 originally placed `NotifyResolvedDependenciesAsync` in `MoveAsync`, but the implementation moved it to `ArchiveAsync` — moving a card never sets `ArchivedAt`, so a blocker only stops counting as active when archived. The `CardRelationship.ArchivedAt` check in the notification helper requires `ArchivedAt != null` to consider a blocker resolved. Notify on the action that actually changes archive state, not on a move that doesn't.

### SignalR conventions

- **SignalR `AddJsonProtocol` + `JsonStringEnumConverter` is required.** Without `options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())` on `builder.Services.AddSignalR()`, hub payload enums (`BoardEntityType`, `BoardAction`, etc.) serialize as ints. The TUI client deserializes these fields as strings, so a mismatched int throws inside the client's message handler and is silently swallowed by the SignalR client — making board-event pushes a silent no-op. MVC's `JsonStringEnumConverter` (via `builder.Services.ConfigureHttpJsonOptions`) only covers REST responses, not the SignalR hub protocol. This was fixed in `9b26412` (commit message details the root cause).
- **`ProjectBoardEventEnvelope` has a `CardId` field** (`Guid?`, default `null`) — populated for card-scoped sub-entities (Comment, ChecklistItem, Attachment, Spec, Plan, CardRelationship) so a client with a specific card open can tell "does this event belong to what I'm looking at" without relying on `EntityId` alone (which is always the sub-entity's own id, not the card it's on). Null for entity types that don't hang off a single card (Project, Column) or where `EntityId` already IS the card id (Card itself). Added in `72afc95`.
- **Web UI `useRealtime.ts` routes by entity type for targeted patching:**
  - `Card` / `Column` → `board.applyRealtimeCardEvent` / `applyRealtimeColumnEvent` (single-entity re-fetch, no full board blink)
  - `CardRelationship` → full `board.fetchBoard` (affects badges on both source and target card — neither is the relationship's own entityId; this event is rare enough that full refresh is fine)
  - `Comment` / `ChecklistItem` / `Attachment` / `Spec` / `Plan` → `board.signalCardContentEvent` (CardModal picks it up if open for that card; nothing to patch on the board tile itself)
  - This was added in `72afc95` as part of realtime UX polish — the previous code did a full `fetchBoard` on every event, causing board flicker.

### LLM adapter conventions

- **SSE stream reading: use `ReadLineAsync` null-check, not `EndOfStream`.** .NET 10 analyzer CA2024 flags `StreamReader.EndOfStream` as inefficient. Read lines in a loop: `while ((line = await reader.ReadLineAsync()) != null)`.
- **Non-2xx HTTP on streaming endpoints: yield error chunk, don't throw.** `HttpResponseMessage.EnsureSuccessStatusCode()` throws, which breaks the `IAsyncEnumerable<ChatChunk>` streaming contract. Instead, check `IsSuccessStatusCode` and yield a single `ChatChunk(null, ChatChunkFinishReason.Error, null)` then stop.
- **`ChatChunkFinishReason.Error` enum value** — added for SSE error chunks. Must be present on the enum for adapters to signal transport-level failures mid-stream.
- **Cache block prefix hashing:** OpenAI-compatible adapters render cache blocks as system messages with `[cache:{hex}]` prefix where hex is a stable 16-char lowercase SHA256 prefix. This enables OpenAI's automatic prompt caching by prefix match.
- **Anthropic system field is an array of blocks, not a string.** `system` = `[{type:"text", text:"...", cache_control:{type:"ephemeral"}}]`. SystemContext blocks get `cache_control`; Memory blocks go in the same array without `cache_control`. `ChatRole.System` messages also go here (without `cache_control`). Omit the `system` field entirely (null) when no system blocks exist — `JsonIgnore(Condition = WhenWritingNull)`.
- **Anthropic tool `input_schema` must be a JSON-Schema object.** Shape: `{type:"object", properties:{...}, required:[...]}`. Convert from `ToolDefinition.Parameters` via `AnthropicInputSchema.FromParameters()`. Never serialize as a flat `input` array — Anthropic rejects it.
- **Project snapshot injected into first user message only (4-cache-breakpoint limit).** Only one `cache_control` on a user message is allowed. Use a `snapshotInjected` bool flag to skip subsequent user messages. Join multiple snapshot blocks with `\n` into one prefix.
- **`x-api-key` header guarded on non-empty key.** Guard with `if (!string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted))` before adding the header. Empty key = no `x-api-key` header (still sends `anthropic-version`).
- **Non-2xx Anthropic responses: log status code + response body.** Use `logger.LogError("Anthropic API error {StatusCode}: {ResponseBody}", statusCode, responseBody)` before yielding error chunk. Critical for debugging provider-side errors.

- **`useNotificationHub.ts`** composable connects to the NotificationHub (user-scoped, not project-scoped — unlike `useRealtime`'s BoardHub). Connected once per session from `layouts/default.vue` on mount. Uses `useAuthToken().getToken()` for the JWT access token factory. Disconnects on unmount. Falls back silently if the connection fails — the notification bell already fetches the live list on click regardless of realtime state.
- **`useRealtime.ts`** (BoardHub) connects per-project from `board.vue` — the NotificationHub is separate and persists across project navigation.

**Domain entity patterns — non-negotiable:**
- Domain entities encapsulate state transitions via instance methods — services orchestrate but NEVER set entity properties directly
- On Card: `UpdateDetails`, `MoveTo(columnId, position)`, `ShiftPosition(delta)`, `Archive()`
- On Project: `UpdateDetails`, `Archive()`
- On Column: `UpdateDetails`, `AssignPosition(position)`
- On ProjectMember: `ChangeRole(role)`
- Services call these methods instead of `entity.Property = value`
- **Spec/Plan ownership:** `Spec.CardId` owns the Spec (max 1 per Card; Goal/Idea/Issue only — Task has none). `Plan.CardId` owns the Plan; `Plan.SpecId` (nullable) groups Goal plans under their Spec. Card-type rules: Goal→Spec(DocType=Specification)+Plans via SpecId, Idea→Spec(DocType=Concept) only, Issue→Spec(DocType=Report)+Plans direct, Task→Plans direct only. No link/unlink. See D-44.
- **Plan lifecycle:** `PlanStatus` — `Pending → Active → Done`. Done plans are read-only; `Reactivate` transitions Done→Active. Services must reject edits on Done plans.
- **Version snapshots:** `SpecVersion` and `PlanVersion` store `Title`, `Description`, `Content` — full document state at each snapshot. Restore reverts all three.

**Controller routing:**
- Resource sub-controllers use `[Route("api/projects/{projectId:guid}/[controller]")]` — no `~` overrides
- `[controller]` token resolves to the controller class name minus "Controller" suffix — e.g. `ProjectSnapshotController` → `/ProjectSnapshot`. **Always verify the resolved URL when writing `.http` smoke tests.**
- Card-scoped actions get `"cards/{cardId:guid}"` prefix: `[HttpPost("cards/{cardId:guid}")]`
- Standalone actions by resource ID: `[HttpGet("{specId:guid}")]`, `[HttpPut("{specId:guid}")]`
- Never use `~/api/...` absolute route overrides

**Testing:**
- `xUnit` only — no FluentAssertions (deprecation risk), use plain `Assert.*`
- > 90% coverage on Application and Domain layers
- Infrastructure tests assert the EF model contract via `AssertProperties(IEntityType, ...)` — they inspect `context.Model` and do not need a database
- Never mock the database — use a real test PostgreSQL instance once that infrastructure exists (not in place yet)
- **`ISettingsProvider` in endpoint tests:** When a controller injects `ISettingsProvider` but the test registers a fake `ISettingsRepository`, the real `CachedSettingsProvider` wraps `IMemoryCache` — the cache TTL means writes via the fake repo are invisible on subsequent reads. Register a `TestCachedSettingsProvider` that delegates directly to the fake repo without caching (same pattern as `TestCachedSettingsProvider` in `AdminControllerTests`).
- **`Llm:EncryptionKey` in test factories:** When `AddLlmInfrastructure` is called from `PersistenceServiceCollectionExtensions.AddPersistence`, the `Llm:EncryptionKey` config key is validated at startup (missing/invalid → `InvalidOperationException`). Every `WebApplicationFactory` fixture must set `Llm:EncryptionKey` in its `ConfigureWebHost` override — same pattern as the "new Application-layer port checklist" in AGENTS.md but for required config keys, not ports. Caught in `cce0d18` when 14 test factories all failed with "Llm:EncryptionKey is required."

**Database:**
- PostgreSQL only — no SQLite fallback
- All schema changes via EF Core migrations
- pgvector extension required (`CREATE EXTENSION IF NOT EXISTS vector`, declared via `modelBuilder.HasPostgresExtension("vector")`)
- Card numbers are sequential per project (`CardNumber int`, unique per ProjectId) — never expose raw GUIDs to users
- Archive is `ArchivedAt: DateTime?`, not `IsArchived: bool`. Default query filters use `.Where(x => x.ArchivedAt == null)` manually — no global query filter, so admin/audit views see archived rows by default
- **EF Core 10 `HasSentinel()` for enum defaults:** When using `HasConversion<int>().HasDefaultValue(SomeEnum.Value)` on an enum property, also chain `.HasSentinel(default(SomeEnum))`. EF 10 treats the 0-value sentinel as the default unless explicitly overridden — without `HasSentinel`, the default value is silently overwritten by the sentinel. This bit `DocType` and `PlanStatus` in `HydraForgeDbContext` (fixed in `12f1f2a`).

**Code style:**
- Readable like a newspaper — method names explain intent, no clever tricks
- No unnecessary abstractions — three similar lines beats a premature helper
- Comments only when the WHY is non-obvious
- No `var` where the type isn't obvious from the right-hand side
- No `console.log`/`console.error`/`console.warn` in production code — use `useToast().add()` for user-facing feedback instead

**Web UI API routes:**
- All API endpoint paths are centralized in `src/web-ui/app/lib/routes.ts` — `UiRoutes` for page paths, `ApiRoutes` for HTTP endpoints.
- Use `ApiRoutes.<Resource>.<action>(id)` instead of inline strings: `api.GET(ApiRoutes.Projects.list())`, `api.POST(ApiRoutes.Cards.move(projectId, cardId), { body: {...} })`.
- Never write inline API path strings in components, stores, or composables. If a route is not in `routes.ts`, add it there first.
- `useApi()` wraps openapi-fetch with auth middleware (attaches JWT, handles 401 redirect). Always use `useApi()` instead of importing openapi-fetch directly.
- **`useApi()` throws — it never resolves with a populated `error` field.** Every call site MUST wrap `await api.X(...)` in try/catch. `const { error } = await api.X(...); if (error) { ... }` with no surrounding try/catch is a bug: the `await` itself throws first, the destructuring never runs, and the function's promise rejects unhandled — silently skipping whatever the `if (error)` branch was supposed to do (see D-40). This broke archive/restore/create error toasts in three components before being caught. **PATCH support added to `useApi.ts` composable.**
- **`useApi()` must NOT use a module-level singleton.** The app is SSR (no `ssr: false` in nuxt.config), and a cached client + store at module scope freezes onto whichever request's Pinia store happens to call it first — every subsequent user's request silently reuses that stale store's token for the lifetime of the server process. `useAuthStore()` and `createClient()` are both cheap; build a fresh client per call (fixed in `12f1f2a`).

**Auth:**
- JWT — admin seeded on first boot
- No SSO, no OAuth, no external auth providers
- Admin cannot access user personal data (chats, memory, notes, calendar, gallery)
- **BroadcastChannel cross-tab auth sync:** `useAuth.ts` posts `{ type: 'login' | 'logout' }` to a `BroadcastChannel('hydraforge-auth')` so other tabs on the same origin sync immediately. `listenForAuthChanges()` is called once per tab from `layouts/default.vue`. Module-level `BroadcastChannel` is fine (per-tab, holds no per-request/user state) — unlike the old `useApi.ts` singleton which was removed for the same pattern (fixed in `12f1f2a`).

**LLM:**
- Server is the only component that calls LLMs — TUI and Web UI never call LLMs directly
- All LLM calls go through `ModelRouter` which selects provider based on feature tier
- Admin configures providers — users cannot add personal API keys
- `ILlmClient`, `IImageClient`, `IEmbeddingClient`, `IKeyVault` — always code to the interface
- **Admin DTOs separate from core DTOs:** `LlmAdminDtos.cs` holds admin CRUD DTOs (`ProviderDto`, `FeatureRoutingDto`, `UsagePageDto`, `BudgetDto`); `LlmDtos.cs` holds core DTOs (`ChatRequest`, `ChatChunk`, `CacheBlock`, `RouteDecision`, `CompressedContext`). Never mix admin concerns into core DTOs — keeps admin surface decoupled from API contract.
- **API keys encrypted at rest** via `IKeyVault`/`AesGcmKeyVault` (AES-256-GCM, `Llm:EncryptionKey` config). `AddLlmInfrastructure` validates key at startup — server fails to start if missing/invalid. Migration `20260731000000_ReencryptLlmProviderApiKeys` backfills existing placeholder rows. Ciphertext format: `v1:{nonceB64}:{ciphertextB64}:{tagB64}`. `LlmOptions.SectionName = "Llm"`.
- **`byte[]` parameter guarding in adapters:** methods accepting `byte[]` params (e.g. `InpaintAsync`'s `ImageBytes`/`MaskBytes`) must guard null/empty with `Result.Failure` before any HTTP work — cheap to reject, prevents sending malformed multipart requests. Found during DallEAdapter review.

### API smoke tests

- `.http` files in `src/HydraForge.Server/HttpTests/` — each resource has its own file (auth, setup, full flow, cleanup)
- Requires dev server running with seeded users: `testadmin`/`TestAdmin123!` (admin), `nonmember`/`NonMember123!` (regular), `testuser1`/`TestUser123!` (regular)
- Test users seeded only in `Development` environment — controlled by `TestUserSeeder`

## Key domain rules

- `Card.CardNumber` is sequential per project — assigned at creation, never reused after deletion
- Blocked card move: returns `409 Conflict` with warning payload when `confirmBlockedMove=false`. `confirmBlockedMove=true` is a capability of the API contract, available to any client — it is not a requirement that every client exposes an override. The Web UI deliberately does not: on `409` it toasts and stops, full stop (D-46) — the intended path is to resolve the actual blocking card, not force past it. 200 OK is wrong — the move was not executed.
- `CardRelationship` forms a DAG — `CardDependencyService.ValidateAcyclic()` must be called on every insert
- **`CardRelationshipBadge` model** replaces `IsBlocked`/`PrimaryRelatedCard` on `CardDto`. Each badge has `Type` + `IsSource` so both clients derive the same directional verb ("blocks"/"blocked by", "precedes"/"preceded by", "spawned"/"spawned from", "relates"). Ordering: `BlockedBy` first, `Relates` last. Capped at 5 per card (`MaxRelationshipBadges`). `RelationshipCount` on the parent DTO carries the true total for "+N more" overflow. Built by `CardService.BuildRelationshipBadges()`; `ListAsync` fetches all project relationships once and maps via `ILookup` instead of per-card queries. Added in `8193745`.
- `ProjectContextSnapshot.TemplateContent` regenerated on every board mutation (instant, no LLM)
- `ProjectContextSnapshot.AiNarrative` generated by nightly scheduled job only (never on mutation)
- AI proposes board mutations — human confirms — never mutate board state from AI without explicit user approval
- AI edit permission is chat-session-scoped — revoked when session ends or new chat opens

## Project spaces

- **Project space** — shared, members-only visibility (`ProjectMember` table gates access)
- **Personal space** — private per user (chats, memory, notes, tasks, calendar, gallery, documents)
- **Admin space** — users, all projects, LLM providers, system health, audit logs only

## Current Phase — Phase 6 in progress (Task 5: AnthropicAdapter)

Phase 3 (Web UI) is **complete** — see `docs/functional-spec.md` §25 Phase 3 checklist (all items checked) and `docs/archive/specs/2026-06-23-phase-3-web-ui-design.md` for the full task history. That includes Task 6 (Polish & Hardening: keyboard shortcuts, error toasts, blocked-card indicator, archive-with-dependents warning, ARIA pass, tablet pass, PWA manifest) and Task 7 (Project Management UI, superseded by `docs/specs/2026-07-07-project-list-redesign-design.md` — server-paginated table, search/sort/role-filter). Both archived plans carry a 2026-07-07 pre-execution note confirming what shipped vs. what the original plan text assumed.

Phase 4 (TUI) is **complete** — see `docs/functional-spec.md` §25 Phase 4 checklist (all items checked), `docs/archive/specs/2026-07-24-phase-4-tui-design.md` for the full task history, and `docs/archive/manual-validation/2026-07-24-phase-4-tui-matrix.md` for the consolidated validation matrix. All 17 tasks shipped: Spectre.Console scaffolding + NSwag codegen, `ConfigStore` (JWT at `.hydraforge/config.json` in the repo root, 0600 — see D-49), `ApiClientFactory` (NSwag client + token refresh), login screen + startup auth flow, connection handling (`LockScreen` + `ConnectionManager`, auto-retry backoff `[5s,10s,30s,60s]`), project list view (search/sort/role-filter/pagination), board view (column/card layout + keyboard nav), SignalR integration (BoardHub + PresenceHub), card detail view (sections, `$EDITOR`, checklist, comments), create/edit/move cards via keyboard, dependency panel, blocked card indicator, spec/plan viewer/editor, comments, checklists, keyboard shortcut reference (`?` — `Rendering/HelpOverlay.cs`, per-screen `ShowHelp()` binding tables), and status bar (connection/online/error counts, `ErrorPanelScreen` on `X`). Unread-notification count in the status bar is deferred to Phase 5 — no notification API surface exists in the generated client until ntfy integration lands. Infra pre-phase decisions (API client strategy, SignalR client wiring, JWT config storage) are resolved — see D-48/D-49 in `docs/DECISIONS.md`.

Phase 5 (Multi-User, Notifications & Admin) is **complete** (2026-07-29) — see `docs/functional-spec.md` §25 Phase 5 checklist (all items checked except self-service change-password and PR-created notifications, both explicitly deferred), `docs/archive/specs/phase-5-multi-user-notifications-admin.md` for the full design spec, and `docs/archive/manual-validation/2026-07-25-phase-5-notifications-admin-matrix.md` for the consolidated validation matrix. All 13 plans shipped: JWT role claim + `Roles.Admin` (Plan 1), notification domain/app/infra (Plan 2), `NotificationHub` + SignalR push (Plan 3), Web UI bell icon + panel (Plan 4), TUI status-bar unread count + `U` key list (Plan 5), ntfy integration (Plan 6 — initial close-out was incomplete, see D-52's 2026-07-27 correction), notification triggers wiring 7 triggers across 4 services (Plan 7 — see D-55/D-56 for the `IWarnLogger`/try-catch pattern and the dependency-resolved-on-Archive-not-Move fix), admin all-projects bypass (Plan 8), admin controller + user management (Plan 9), system settings API + cache + Web UI (Plan 10), audit log reader + controller (Plan 11), audit log Web UI page (Plan 12), and admin dashboard home page (Plan 13). Out of scope, correctly deferred: self-service `POST /api/Auth/change-password` (admin-initiated reset-password shipped instead) and "PR created → all members" (no Git/PR event source exists yet).

Phase 6 (LLM Infrastructure) is **in progress**. Pre-phase blocking decisions resolved: nightly job scheduler is **Hangfire + `Hangfire.PostgreSql`** (D-57), `AiNarrative` display surface is Web UI modal + TUI viewer (D-58). Task 2 **complete** (2026-07-30): `IKeyVault` port in Application, `AesGcmKeyVault` impl in Infrastructure (AES-256-GCM, `Llm:EncryptionKey` config, startup validation), migration `20260731000000_ReencryptLlmProviderApiKeys` re-encrypts placeholder rows idempotently, `LlmServiceCollectionExtensions.AddLlmInfrastructure` registered via `PersistenceServiceCollectionExtensions`. See D-59 in `docs/DECISIONS.md`. Task 3 **complete** (2026-07-30): All Application LLM ports defined — `ILlmClient`, `IImageClient`, `IEmbeddingClient`, `IModelRouter`, `IContextCompressor`, `IUsageRecorder`, `ILlmAdminService`, `ILlmClientFactory` + DTOs in `HydraForge.Application/Llm/`. Task 4 **complete** (2026-07-30): `OpenAiCompatibleAdapter` (SSE stream parse, cache-block prefix hashing, `UsageSnapshot` from final chunk, `GetModelsAsync`, `SupportsToolCalling`). Task 5 **complete** (2026-07-30): `AnthropicAdapter` (`cache_control` ephemeral blocks, `/v1/messages` stream, `message_delta` usage mapping, `input_schema` JSON-Schema tool shape, 4-cache-breakpoint limit). Task 7 **complete** (2026-08-01): `DallEAdapter` (`/images/generations` + `/images/edits` inpaint, `InpaintRequest` DTO refined to `ImageBytes`/`MaskBytes`/`Count`).

### Nuxt UI v4 patterns

- **UModal:** `v-model:open` for two-way binding. Content in named slots (`#body`, `#header`, `#footer`). Default slot is `DialogTrigger`, not modal content. No `UOverlay` component — overlay built into `UModal` via `overlay` prop (default `true`).
- **USelect:** No `clearable` prop. Wrap in relative container with absolute ghost `UButton` (X icon) to clear. Additionally, USelect v4's internal `SelectItem` component rejects empty-string values — never use `value: ''` in items. Use a non-empty sentinel (e.g. `'all'`) and translate to `''`/`null` at the emit site.
- **UTable v4:** Use `:data` (array of row objects) + `:columns` (array of `TableColumn` with `accessorKey`/`header`) — NOT `:rows` (v3 API). Slot names: `#field-cell` (not `#field-data`). Expandable rows: `v-model:expanded` (Map of rowId → boolean), `:get-row-id` function, `row.toggleExpanded()` in cell template. Expanded content goes in `#expanded` slot. Example: `src/web-ui/app/pages/admin/audit-log.vue`.
- **DataTable (shared wrapper):** Wraps `UTable` + pagination + loading/empty states + optional `#card` slot (mobile card grid). Two props added for the audit log page:
  - `fillHeight` — boolean. When `true`, table scrolls rows internally with a sticky header and fixed pagination footer. Use in full-height dashboard layouts.
  - `expanded` / `update:expanded` — `Record<string, boolean>` for expandable rows. Passed through to UTable's native expandable-row API. Example: `src/web-ui/app/pages/admin/audit-log.vue`.
- **openapi-typescript enum typing:** `openapi-typescript` may type string-valued enum fields (serialized by `JsonStringEnumConverter`) as `number` in `api.d.ts`. Handle both types in cell templates (`displayRole` function checking `typeof`). Cast test fixtures with `as any` to satisfy typecheck; the actual API response at runtime is the string name.
- **vue-draggable-plus** removed — SSR-incompatible with Nuxt 4. Use plain `v-for`; native HTML5 drag-and-drop planned.
- **`import.meta.client`** not usable in Vue template expressions — define as `const isClient = import.meta.client` in `<script>`.
- **Card detail panel version ownership** — `CardModal.vue` owns the single `card` ref (and `card.value.version`) for the lifetime of the open modal. Panels under it that mutate a `Card` field (`CardDescription`, `CardMetadata`) read `props.card.version` at call time and emit `'update:card': [CardResponse]` with the server's response on success — never cache `version` locally inside a panel (D-41).
- **Shared card-type / due-date utilities** — `app/lib/card-type.ts` (`CARD_TYPE_OPTIONS`, `cardTypeToApiString`, `cardTypeOption`) and `app/lib/date.ts` (`formatDueDate`, `isOverdue`) are the single source of truth for the numeric-`CardType` → API-string-enum map and due-date formatting. These were hand-copied into three components before being consolidated — import from `~/lib/card-type` / `~/lib/date`, never redefine the map.
- **Shared filter state & logic via composables** (D-43) — Any filter state or logic shared between desktop and mobile views (like global board filters) MUST be extracted into a shared composable (e.g. `useBoardFilters.ts`) that directly reads/writes the Pinia store. Do not duplicate state with local refs or use watchers to sync them — this causes double-fetching, race conditions, and synchronization bugs.
- **Keyboard navigation via composables** (D-47) — Any keyboard-nav surface (selection highlight + shortcut dispatch) follows the pattern in `app/composables/keyboard/`: `useRovingFocus.ts` is a generic single-axis selection primitive (index + wraparound next/prev + select/selectById), and `useBoardKeyboardNav.ts` is the board-specific composable built on top of it (2D column/card selection + all `Board`-scope `keyboard.register` calls). `useKeyboard.ts`'s `register()` takes an `enabled?: () => boolean` predicate — use it instead of an inline `if (modalOpen) return` guard duplicated per handler. The composable exposes `activate()`/`deactivate()` rather than self-managing `onMounted`/`onBeforeUnmount` — Vue no-ops `onMounted` with no active component instance on the stack, so the consuming component's own lifecycle hooks must drive it (same pattern as `useRealtime.connect`/`disconnect`). Start any new keyboard-nav surface (Projects page, chat) from `useRovingFocus` rather than re-inlining index/selection logic.
- **E2E tests** — `src/web-ui/e2e/` (Playwright, D-42). Specs drive a real browser against the real running stack (`pnpm dev` + the .NET API in `Development`) and seed their own data via the API with random-suffixed titles — there is no per-test database reset. Run with `pnpm test:e2e`.
- **Test what a component actually does, not a copy of its logic** — `CardModal.docs.test.ts` originally hardcoded its own `DOCS_CARD_TYPES`/`PLAN_CARD_TYPES` constants instead of mounting `CardModal.vue`, so it silently drifted from the real `hasSpec`/`hasPlan` computed values (which already supported Issue and Task) and never caught it — the test always passed by checking itself, not the component. When a test needs to assert on a component's conditional-rendering logic, mount the component and assert on its actual output/props, not a hand-copied duplicate of the condition.
- **Vue Test Utils stub gotchas** (found writing the fix above): (1) `global.stubs: { AppModal: { render() { return h('div', {}, this.$slots.default?.()) } } }` renders nothing if the real usage puts content in a named slot — `AppModal.vue` uses `#body`, so the stub must render `this.$slots.body?.()`, not `default`. (2) Auto-stubbed components (`ComponentName: true`) serialize props to attributes in all-lowercase with no hyphen — a `:doc-type="..."` prop shows up as `wrapper.find('component-name-stub').attributes('doctype')`, not `'doc-type'`.

### TUI conventions (Phase 4 lessons)

- **`IScreen` contract:** `OnEnterAsync` / `OnExitAsync` / `RenderAsync` / `HandleKeyAsync(ConsoleKeyInfo)`. No `ProjectReference` from `HydraForge.Tui` to Domain/Application/Infrastructure — it's a pure HTTP client against the NSwag-generated `HydraForgeApiClient` in `Generated/` (regenerated on every build via the `nswag.json` MSBuild step; codegen failure is non-fatal, build still succeeds off the last-good `Generated/` output).
- **Overlay push/pop pattern:** a screen becomes an overlay by setting `_appState.PreviousScreen = this; _appState.CurrentScreen = overlayScreen;` then rendering it directly. The overlay dismisses itself by setting `_appState.CurrentScreen = null` on `Esc`. `Program.cs`'s main loop detects `CurrentScreen == null && PreviousScreen != null`, restores it, calls `OnEnterAsync` + `RenderAsync` on the restored screen. Used by `DependencyPanel`, `ErrorPanelScreen`, and the spec/plan viewer — follow this pattern for any new modal-style screen rather than inventing a `ScreenStack`/dispatcher abstraction (an earlier written-ahead plan for the keyboard-reference overlay proposed exactly that; it was never needed).
- **Keyboard help overlay:** `Rendering/HelpOverlay.Show(title, bindings)` — blocks on `Console.ReadKey` until `?` or `Esc`, same blocking pattern as `AnsiConsole.Prompt`. Every screen with a real key-dispatch loop (Board, CardDetail, ProjectList, LockScreen) implements a private `ShowHelp()` that calls it with that screen's own binding table — the binding table lives with the screen, not in a separate central registry. `LoginScreen` deliberately has none: it's a sequential blocking-prompt flow with no key loop to hang `?` off, not a gap.
- **Key binding conflicts are per-screen, not global** — `E` means "Edit" on Board/CardDetail/SpecViewer; when a later feature needs a new global-feeling shortcut (e.g. the error panel), check each screen's existing `HandleKeyAsync` switch first. The error panel ended up on `X` for exactly this reason.
- **`ErrorCollector`** is a plain DI-registered service (capped ring buffer, 50 entries) fed from every screen's catch blocks (`_errorCollector.Add(correlationId, message)`). `AppState.Connection`/`OnlineCount` are the other two pieces of ambient status; all three render in `BoardRenderer`'s bottom status-bar row — extend that Grid, don't build a parallel status surface.
- **`ConfigStore`** — JWT lives at `.hydraforge/config.json` (repo-relative, not `~/.config/`), file mode `0600` on POSIX (D-49, amending an earlier home-directory default in D-48).
- **Manual validation matrices** — one file per plan under `docs/manual-validation/` while the phase is in flight, consolidated into a single `docs/archive/manual-validation/<date>-phase-N-<name>-matrix.md` when the phase ships, alongside the archived design spec in `docs/archive/specs/`. Don't leave the per-plan files lying around after consolidation — delete them in the same pass.
- **A full-screen `Spectre.Console.Layout` must contain everything the screen shows — nothing gets `AnsiConsole.Write`/`MarkupLine`'d after it.** `BoardRenderer` is currently the only screen using `Layout` (Title/Board/Status rows sized to exactly fill `AnsiConsole.Profile.Height`); any line printed after that `Write(layout)` call pushes the whole frame up and off the top of the terminal, forcing the user to scroll to see the title bar. Fold trailing content (key hints, mode banners) into a sized region of the *same* `Layout` instead — see `BoardRenderer.BuildLayout`'s `Status` region, which combines the connection line + reorder-mode banner + `KeyHintBar.WrapLines(...)` into one `Panel(Rows(...))` sized dynamically off their combined line count. If a future screen adopts `Layout`, follow this pattern rather than the print-after-Write shape.
- **`ConsoleSize.Sync()`** (`Rendering/ConsoleSize.cs`) re-stamps `AnsiConsole.Profile.Width/Height` from the live `Console.WindowWidth/Height` — call it right after every `AnsiConsole.Clear()`. Spectre's `Profile.Width/Height` are otherwise captured once and never re-queried, so every screen that sizes off them (all of them do) would render at a stale size after a mid-session terminal resize.
- **Small-terminal floor:** below `BoardRenderer`'s `minWidth`/`minBoardRows` threshold, Spectre's `Panel`/`Table` renderer throws `ArgumentOutOfRangeException` on the resulting negative/zero `Layout` region instead of clamping (`Segment.SplitLines`, confirmed via `Spectre.Console.Testing.TestConsole` at fixed sizes down to 5×5) — `BuildLayout` guards this by falling back to a plain "Terminal too small" message instead of attempting the board. `BoardRendererTests.BuildLayout_NeverThrows_AtAnyRealisticTerminalSize` is the regression test; extend the guard's thresholds there if `BuildLayout`'s status-bar content ever grows. Screens that don't use `Layout` (`ProjectListScreen`'s `Table`, etc.) don't have this failure mode — Spectre just squeezes/wraps them, verified down to 3×3 — so the guard is specific to `Layout` users, not a TUI-wide pattern.
- **`Spectre.Console.Testing.TestConsole` + `AnsiConsole.Console` in tests:** set `AnsiConsole.Console = testConsole` (with `Profile.Width`/`Height` configured) *before* calling code that reads `AnsiConsole.Profile` — not after building the renderable and only using the `TestConsole` for the final `Write`. And since `AnsiConsole.Console` is a shared global mutable singleton, any test touching it races with concurrently-running tests under xUnit's default cross-class parallelization (nondeterministic rendered output, not a clean failure) — `HydraForge.Tui.Tests` has `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `AssemblyInfo.cs` for exactly this reason.

## Housekeeping & archive

- Soft-delete is `ArchivedAt: DateTime?`; hard-delete is the responsibility of the future `HousekeepingBackgroundService` (deferred across later phase work in `docs/functional-spec.md`).
- Retention periods are admin-configurable via the `SystemSettings` singleton: `ArchivedItemRetentionDays=730`, `AuditLogRetentionDays=90`, `NotificationRetentionDays=30`.
- DB-level cascades cover `Document→DocumentVersion`, `Note→NoteReminder`, `Note→NoteImageAttachment`, `ChatSession→ChatMessage`. Polymorphic `DocumentChunk` (`SourceType`+`SourceId`) is cascaded manually in the housekeeping service.
- Design spec: `docs/archive/specs/2026-06-03-archive-and-housekeeping-design.md`.

## Docs

The monolithic `requirements-and-architecture.md` was split in `dc2e092` into focused files. It is now a 17-line index pointing at:

- `docs/scope.md` — vision, personas, scope boundaries
- `docs/functional-spec.md` — FRs, NFRs, phase checklists (live)
- `docs/architecture.md` — Clean Architecture, real-time, LLM, error handling, tech stack
- `docs/data-model.md` — entity tables and enums (authoritative for schema intent)
- `docs/glossary.md` — terminology
- `docs/DECISIONS.md` — every design decision with rationale (D-1 through D-50)
- `docs/agent-platform-vision.md` — vision, pipeline, feature parity table

Read `docs/DECISIONS.md` before changing any architectural pattern — the rationale is there. Keep `docs/data-model.md` and entity code in sync when fields change.
