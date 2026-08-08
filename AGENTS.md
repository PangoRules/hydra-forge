# HydraForge Agent Notes

Compact repo-specific guidance for OpenCode sessions. Prefer executable files over roadmap prose when they disagree.

## Current State

- Phase 1 foundation is complete on `feat/phase-1-foundation`. Docker Compose, EF Core + pgvector schema, auth, ProblemDetails/correlation, health, audit infrastructure, CI, and placeholder cleanup are implemented. All 50+ Domain entities are mapped by `HydraForgeDbContext`; pgvector `vector(1536)` columns are configured for `MemoryEntry.Embedding` and `DocumentChunk.Embedding`. Seven migrations are committed (latest `20260604050632_AddAuditLogScopeAndNullableProjectId`).
- Phase 2 (Project Space API & Domain) is complete on `feat/phase-2-project-space-api-domain`. Tasks 1-10 complete: Project CRUD/membership/archive, Column CRUD/reorder, Card CRUD/move/assignees/blocked-move-warning/parent-epic-linking, checklists, comments, attachments (with `IFileStore` + MinIO), specs and plans (versioned markdown with ownership FK model, full document state snapshots, restore), relationships (cycle detection, archive impact), ProjectContextSnapshot (deterministic renderer, `IProjectSnapshotRefresher` port injected into all 9 mutation services, `GET /api/projects/{projectId}/ProjectSnapshot` endpoint), SignalR BoardHub + PresenceHub, hardening. Swashbuckle/Swagger replaced with built-in `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore`.
- Phase 3 (Project Space — Web UI) is **complete** on `feat/phase-3-web-ui`. All 7 tasks shipped: auth + scaffold, project list + board view, card modal core (desktop split + mobile tabs, Tiptap description), card modal hardening (useApi try/catch, version ownership on `CardModal`, shared `card-type.ts`/`date.ts`), card modal panels (checklist/comments/attachments/dependencies/metadata), board filtering + quick-add + filter redesign, card type redesign (Task/Issue/Goal/Idea), E2E Playwright foundation, Specs/Plans/Realtime (SignalR BoardHub + PresenceHub), doc model schema redesign (`DocType`, `PlanStatus`, multi-plan, `SpawnedFrom` — D-44) with UX-polish follow-up (collapsible plans, clickable status dropdown, fullscreen editor), editable columns + project templates (Software/General/Blank), Polish & Hardening (keyboard nav, bugfixes, roving tabindex), and Project List Redesign (server-paginated table with search/sort/role filter, mobile card grid, desktop UTable, filter bar). Per-plan E2E matrices consolidated into `docs/archive/specs/2026-06-23-phase-3-web-ui-matrix.md`. Spec archived.
- Phase 4 (Project Space — TUI) is **complete** on `feat/phase-4-tui`. All 17 tasks shipped: Spectre.Console scaffolding + NSwag codegen, `ConfigStore` (JWT at `.hydraforge/config.json`, 0600 — D-49), `ApiClientFactory` (token refresh), login screen, connection handling (`LockScreen` + `ConnectionManager`, backoff `[5s,10s,30s,60s]`), project list view (search/sort/role-filter/pagination), board view (column/card layout + keyboard nav), SignalR (BoardHub + PresenceHub), card detail view, card CRUD via keyboard, dependency panel, blocked card indicator, spec/plan viewer/editor (`$EDITOR`), comments, checklists, keyboard shortcut reference (`?` overlay, `Rendering/HelpOverlay.cs`), and status bar (connection/online/error counts + `ErrorPanelScreen` on `X`). Unread-notification count deferred to Phase 5 (no notification API surface yet). Per-plan matrices consolidated into `docs/archive/manual-validation/2026-07-24-phase-4-tui-matrix.md`. Spec archived to `docs/archive/specs/2026-07-24-phase-4-tui-design.md`.
- Phase 5 (Multi-User Notifications & Admin Dashboard) is **complete** (2026-07-29). All 13 plans shipped: JWT role claim + `Roles.Admin`, notification system + `NotificationHub` + Web UI bell + TUI unread count, ntfy integration, 7 notification trigger points, admin all-projects bypass, admin controller + user mgmt, system settings, audit log reader + Web UI page, admin dashboard home. Spec archived to `docs/archive/specs/phase-5-multi-user-notifications-admin.md`.
- Phase 6 (LLM Infrastructure) is **complete** (2026-08-02). All 24 tasks shipped: `IKeyVault` + `AesGcmKeyVault` + startup validation, Application ports (`ILlmClient`/`IImageClient`/`IEmbeddingClient`/`IModelRouter`/`IContextCompressor`/`IUsageRecorder`/`ILlmAdminService`/`ILlmClientFactory`) + DTOs, 5 text adapters + 3 image adapters (DallE/StabilityAi/ComfyUi, ComfyUi serves both `ComfyUi`+`Diffusers`), `LlmClientFactory`, `ModelRouter`, `ContextCompressor`, `FeatureRoutingConfig` startup seeder, usage recording, budget enforcement, `ILlmAdminService` + controller + tests, `/api/account/usage` self-service, Web UI admin pages (providers/routing/usage), Hangfire wiring + `SystemSettings.AiNarrativeGenerationTimeUtc` + admin settings time picker, `GenerateAiNarrativeForAllActiveProjectsAsync` recurring job + Web UI modal / TUI (`v` key) narrative viewer, `FeatureAllowedModel` per-feature model allowlist (opt-in, admin-managed on Routing page). Token estimation is `chars / 4`, no tokenizer dep (D-63); `StreamChatAsync` returns `IAsyncEnumerable<ChatChunk>`, transport to a live client deferred to Phase 7 (D-64). Spec archived to `docs/archive/specs/2026-07-30-phase-6-llm-infrastructure-design.md`.
- Phase 7 (Chat) is in progress — Tasks 1–18 + 19b complete on `feat/phase-7-chat`. Domain entities, EF migration, Application ports/DTOs, `ImageBlock` extension (F5), document ingestion + chunking + embedding, RAG retrieval (F1 scope toggle), ChatSession service (CRUD, F6 implicit close, close-with-summary, permission-state read), ChatMessage service (history pagination, user-message persist), ChatFolder service (max-depth-2, archive cascade), PromptPreset + PromptPresetGroup services, AgentPersonality service, CardChatLink service, chat search service (title + message content), ChatSummaryGenerator (one LLM call on close), `ChatHub` + `IChatHub` (streaming transport, one-active-stream, cancel, usage recording, context compression), REST controllers + `Program.cs` wiring, `useChatStream` composable + `ChatSessionView`/`ChatMessageList`/`ChatMessageBubble`/`ChatInput`, `ChatPanel` (project board rail) + F6 implicit close + auto-prompt pre-fill, and `CardPopup` multi-popup shell (`stores/cardPopup.ts` + `CardPopup.vue` + shared `usePopupZIndex` z-index/Escape-LIFO composable; `ChatDock` re-pointed at shared z-index; max 3 cards) are all shipped. Remaining: Tasks 19a, 20–24 (Web UI screens + TUI screens + integration tests + E2E). Spec is live at `docs/specs/2026-08-02-phase-7-chat-design.md`.

## Read First

- `CLAUDE.md` for stack, commands, and conventions.
- `docs/DECISIONS.md` before changing architecture; do not re-litigate settled decisions.
- Read by intent:
  - `docs/scope.md` — vision, personas, scope boundaries
  - `docs/functional-spec.md` — FRs, NFRs, phase checklists (live)
  - `docs/architecture.md` — Clean Architecture, real-time, LLM, error handling, tech stack
  - `docs/data-model.md` — entity tables and enums (authoritative for schema intent)
  - `docs/glossary.md` — terminology
  - `docs/agent-platform-vision.md` — agent platform direction
  - `docs/backlog.md` — uncommitted ideas and scope-creep candidates
  - `docs/admin-llm-providers.md` — how-to: registering LLM providers (OpenRouter, Ollama, etc.) via the admin UI
- Specs/plans always go to `docs/specs/`+`docs/plans/` (archived to `docs/archive/`) — never `docs/superpowers/`, see CLAUDE.md's "Spec/plan location" note.
- Check manifests/config before trusting prose: `HydraForge.slnx`, `*.csproj`, `src/web-ui/package.json`, `src/web-ui/nuxt.config.ts`.

## Repo Shape

- Solution file is `HydraForge.slnx`; projects target `net10.0` with nullable and implicit usings enabled.
- Backend boundaries: `Domain` -> `Application` -> `Infrastructure` -> `Server`/`Tui`. Keep Domain and Application free of EF Core, HTTP, SignalR, and infrastructure concerns. Application services must never reference `HydraForgeDbContext` or other Infrastructure types directly — use Application-layer repository ports even for simple read-only queries.
- `src/HydraForge.Server` is ASP.NET Core. `Program.cs` calls `builder.Services.AddPersistence(builder.Configuration)` and wires auth, ProblemDetails/correlation middleware, health probes, admin seeding, and controllers. Starter weather endpoints are removed.
- `src/HydraForge.Tui` is the terminal client — Spectre.Console for rendering, NSwag-generated `HydraForgeApiClient` for HTTP, `Microsoft.AspNetCore.SignalR.Client` for BoardHub/PresenceHub. No `ProjectReference` to Domain/Application/Infrastructure — pure HTTP client over the same API the Web UI uses.
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
- Docker (Postgres + MinIO only): `docker compose up -d postgres minio` (host ports 5433, 9000, 9001)
- MinIO console: `http://localhost:9001` (default: minioadmin / minioadmin)
- API docs (OpenAPI JSON): `http://localhost:5000/openapi/v1.json`
- API reference (Scalar UI): `http://localhost:5000/scalar/v1`
- Install web deps: `cd src/web-ui && pnpm install`
- Web dev server: `cd src/web-ui && pnpm dev`
- Web typecheck: `cd src/web-ui && pnpm typecheck`
- Web lint: `cd src/web-ui && pnpm lint`
- Web build: `cd src/web-ui && pnpm build`
- Web E2E tests (Playwright, D-42): `cd src/web-ui && pnpm test:e2e` — requires the API server running with `ASPNETCORE_ENVIRONMENT=Development` and `pnpm dev` already up; specs live in `src/web-ui/e2e/` and seed their own data via the API.
- On a fresh checkout, run `pnpm install` or `pnpm exec nuxt prepare` before web lint/typecheck because `eslint.config.mjs` imports generated `.nuxt/eslint.config.mjs`.

## Testing Notes

- Test projects are xUnit with plain `Assert.*`; do not add FluentAssertions.
- Domain/Application tests are pure logic. Infrastructure tests assert the EF model contract via `AssertProperties(IEntityType, params string[])` — these run without a database because they inspect `context.Model`, not `context.Database`.
- Most tests run without PostgreSQL. Optional PostgreSQL-backed tests use `HYDRAFORGE_TEST_CONNECTION_STRING`; the architecture requires real PostgreSQL for DB behavior tests, not SQLite or mocked DB behavior.
- **New Application-layer port checklist** — required whenever a new port (e.g. `IProjectSnapshotRefresher`) gets injected into an existing service's constructor:
  1. `grep -rl "ConfigureServices" tests/HydraForge.Server.Tests/` — list every factory.
  2. Create a shared `Test<PortName>` stub in `tests/HydraForge.Server.Tests/` and register it in every factory's `ConfigureServices`.
  3. Skipping this resolves the real Infrastructure implementation in tests — it fails as a `500` from the endpoint, not a DI exception, so it's easy to misdiagnose as an application bug instead of missing test wiring.
- HTTP `.http` smoke test files must be self-contained: auth → setup (create project, add members, add data) → test cases → cleanup. Never depend on variable values from other `.http` files. Each `.http` file runs in isolation.
- **Fake repo dictionary pattern** — When a test fake repo needs to look up entities by ID (e.g. `FakeCardRepo`), use a `Dictionary<Guid, T>` field instead of a single nullable `T?` field. This supports multiple entities, lookup by ID from the service under test, and avoids `null`-checking boilerplate. Register test entities via `repo.Cards[cardId] = card` before calling the service.

## API Documentation

- Swashbuckle/Swagger was replaced with built-in `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` (D-33).
- OpenAPI doc at `/openapi/v1.json`; Scalar UI at `/scalar/v1` (dev only).
- Do NOT add Swashbuckle back. Use `IOpenApiDocumentTransformer` / `IOpenApiOperationTransformer` for customizing the OpenAPI doc (e.g. adding Bearer auth scheme to Scalar's "Authorize" button).
- Controller endpoint metadata comes from `[ProducesResponseType]`, `[ApiExplorerSettings]`, and return type inference. The `[SwaggerOperation]`, `[SwaggerResponse]`, `[SwaggerTag]` attributes from Swashbuckle are gone.
- `Microsoft.OpenApi.Models` namespace DOES NOT EXIST in OpenAPI.NET v2.x. Types live in root `Microsoft.OpenApi` — don't try to add `using Microsoft.OpenApi.Models`.

## File Storage

- `IFileStore` abstraction in Application: `StoreAsync(Stream, contentType, storageKey)` → `Result<string>`, `OpenReadAsync(storageKey)` → `Result<Stream>`, `DeleteAsync(storageKey)` → `Result`. `InitializeAsync()` for bucket creation (default no-op).
- Two implementations: `LocalFileStore` (bare-metal fallback) and `S3FileStore` (MinIO/AWS S3, recommended). Switch via `FileStorage:Provider` config.
- MinIO runs as a core Docker Compose service alongside Postgres. Server `depends_on: minio`.
- Storage key hierarchy: `{userId}/{sourceType}/{sourceId}/{guid}` — never include user filenames, dates, or project IDs in the key.
- Metadata (filename, content-type, size) stored in `Attachment` entity — never extract from storage path.
- Flow: validate membership → validate card → validate size/content-type → sanitize filename → generate key → store file → store metadata → audit log.
- Delete: metadata first, then file (non-fatal if file-store delete fails).
- `S3FileStore` auto-creates bucket on startup. `InitializeAsync` failure logged as warning, server continues.

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
- Spec/Plan ownership: `Spec.CardId` and `Plan.CardId` are ownership FKs (the card that created the document owns it). Other cards can read but not edit. No link/unlink endpoints — ownership is set at creation and immutable.
- Controller route pattern for sub-resources: `[Route("api/projects/{projectId:guid}/[controller]")]` on class. Card-scoped actions use `[HttpPost("cards/{cardId:guid}")]` prefix. Standalone actions use `[HttpGet("{specId:guid}")]`. Never use `~/api/...` override routes.
- Version snapshots (`SpecVersion`, `PlanVersion`) store full document state: `Title`, `Description`, `Content`. Restore reverts all three fields.
- Blocked card move: API returns `409 Conflict` with warning payload when `confirmBlockedMove=false`. 200 OK must not be used for blocked moves — the move was not executed.
- Housekeeping cascade: DbContext `OnDelete: Cascade` for Document→Version, Note→Reminder, Note→ImageAttachment, ChatSession→Message. `DocumentChunk` is polymorphic (`SourceType`+`SourceId`); its cascade must be handled manually in `HousekeepingBackgroundService`, not via FK.
- **MembershipGuard NRE-guard** (D-55): After `MembershipGuard.HasAccessAsync` passes, re-fetched `ProjectMember` may still be null for admin users (they have no membership row). Every role check after guard must use `if (membership != null && ...)` — never access `membership.Role` without null-guard. RemoveMemberAsync specifically needs this guard: non-member admins should not be removable.
- One global admin-configurable retention period for all archived ownable content (via `SystemSettings.ArchivedItemRetentionDays`). Notifications and audit logs have their own (shorter) retention knobs.
- **`LlmOptions` binding**: use `AddOptions<LlmOptions>().Bind(configuration.GetSection("Llm"))` — never `services.Configure<LlmOptions>(name, section)`. Named overloads silently fail to resolve config values, leaving properties at their defaults.
- **`RouteDecision` forward-compat**: new optional fields on shared DTOs must use `= null` default to avoid breaking existing consumers at compile time.
- **`ILlmClientFactory.For()`** takes `LlmProvider` entity, not `ProviderDto` — the factory resolves adapter by entity's `AdapterType`.
- **ContextCompressor error-chunk passthrough**: when `StreamChatAsync` yields a chunk with `FinishReason.Error` or `ContentFilter`, return uncompressed blocks — never fail the compress call.
- **Index-based block exclusion**: use `HashSet<int>` of indices when removing blocks from `IReadOnlyList<CacheBlock>` — `List<T>.Remove` on record types matches by value, so duplicate-content blocks collide.
- **No circular DI in compressor chain**: `ContextCompressor` → `IModelRouter` → `ILlmClientFactory` → adapters. No back-edge from router/factory to compressor.

## Database And Migrations

- EF Core 10 + Npgsql + `Pgvector.EntityFrameworkCore 0.3.0` are wired. DbContext lives at `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`. The `UseVector()` call is required on the options builder — both `PersistenceServiceCollectionExtensions.AddPersistence` and `DesignTimeHydraForgeDbContextFactory` chain it.
- All schema changes go through EF Core migrations under `src/HydraForge.Infrastructure/Migrations/`; do not hand-edit database schema.
- pgvector extension is added via `modelBuilder.HasPostgresExtension("vector")` in `OnModelCreating`.
- Table naming convention is snake_case plural (e.g. `card_chat_links`, `audit_log_entries`).
- Local dev DBs typically run on host port **5433** (per `docker-compose.yml`); update `appsettings.Development.json` if your local Postgres uses a different port.
- `dotnet ef` warns "Unable to check if the migration has been applied" when no DB is reachable — migrations still regenerate locally; use `has-pending-model-changes` to verify model state.
- **`nvarchar(max)` → `text` for PostgreSQL:** EF Core defaults `string` properties to `nvarchar(max)` (SQL Server convention). On Npgsql, this generates `nvarchar(max)` in migrations — always chain `.HasColumnType("text")` for unbounded-length string columns to produce PostgreSQL-compatible DDL.

## Web UI Conventions

- Nuxt config enables `@nuxt/eslint` and `@nuxt/ui`, uses `~/assets/css/main.css`, and prerenders `/`.
- ESLint stylistic settings use no trailing comma and `1tbs` brace style via Nuxt config.
- Mobile-first, keyboard-navigable UI is a settled requirement; preserve this when replacing starter screens.
- **Nuxt UI v4 UModal**: Use `v-model:open` for two-way binding (not `:open`). Content goes in named slots (`#body`, `#header`, `#footer`) — the default slot is a `DialogTrigger`, not modal content. No `UOverlay` component exists in v4; the overlay is built into `UModal` via the `overlay` prop (defaults to `true`). Always use `AppModal.vue` wrapper instead of raw `UModal` — it standardizes open/close, loading, error, Escape key, and consistent sizing.
- **USelect** has no `clearable` prop in v4. For a clearable select, wrap in a relative container with an absolute-positioned ghost `UButton` (X icon) that sets the model to `undefined`.
- **vue-draggable-plus** is removed — SSR-incompatible with Nuxt 4 (SortableJS requires browser APIs, component fails inside `ClientOnly`, hydration mismatches with `v-model`). Use plain `v-for` for card/column lists; native HTML5 drag-and-drop planned for re-implementation.
- **`import.meta.client`** cannot be used in Vue template expressions — define it in `<script>` as `const isClient = import.meta.client` and use in template.
- **API route constants** — all API paths live in `app/lib/routes.ts` as `ApiRoutes` constants. Never write inline path strings in components/stores/composables. Use `ApiRoutes.<Resource>.<action>(id)` from `~/lib/routes`.
- **Toast system** — Nuxt UI v4 `useToast()` composable. Configure the toaster via `UApp`'s `toaster` prop: `<UApp :toaster="{ position: 'bottom-right' }">`. No separate `<Toaster />` component needed. Never use `console.log`/`console.error`/`console.warn` in production code — all user-facing feedback uses toasts.
- **Toast conventions** — `toast.add({ title: message, color: 'error' })` for failures; `toast.add({ title: '...', color: 'success' })` for significant user actions (create, archive, delete). Silent for optimistic updates (move, reorder, edits).
- **Error types** — `useApi()` throws `ApiError` (from `~/lib/api-error`) on non-2xx responses. Extract message via `error instanceof ApiError ? error.message : 'Unexpected error'`. `useAppToast.ts` is the established toast wrapper (`success`/`error` with baked-in durations) — do not build a second `useErrorToast` composable alongside it; Plan 6's original Task 22 predates `useAppToast` and is stale on this point.
- **`useApi()` throws — it never resolves with a populated `error` field.** Every call site MUST wrap `await api.X(...)` in try/catch. `const { error } = await api.X(...); if (error) { ... }` with no surrounding try/catch is a real bug, not a style nit: the `await` itself throws before the destructuring runs, the function's promise rejects unhandled, and the `if (error)` branch (and whatever toast it was meant to fire) silently never executes. This broke archive/restore/create error toasts in `CardModal.vue`, `BoardCard.vue`, and `CardCreateModal.vue` before it was caught and fixed (D-40 in `docs/DECISIONS.md`). Check for this pattern whenever reviewing a new `useApi()` call site.
- **Card detail panel version ownership** (D-41) — `CardModal.vue` owns the single `card` ref, including `card.value.version`, for the lifetime of the open modal. Panels under it that mutate a `Card` field (`CardDescription`, `CardMetadata`) read `props.card.version` at call time and emit `'update:card': [CardResponse]` with the server's response on success — they never cache their own copy of `version`. A panel caching its own version copy desyncs the moment a sibling panel saves first, producing a spurious `409 CARD_CONCURRENCY_MISMATCH`.
- **Shared card-type / due-date utilities** — `app/lib/card-type.ts` (`CARD_TYPE_OPTIONS`, `cardTypeToApiString`, `cardTypeOption`) and `app/lib/date.ts` (`formatDueDate`, `isOverdue`) replace three near-identical hand-copied maps that used to live in `CardDescription.vue`, `CardCreateModal.vue`, and `BoardCard.vue`. Import from these instead of redefining a `CardType` map or a due-date formatter anywhere new.
- **Shared filter state & logic via composables** (D-43) — Any filter state or logic shared between desktop and mobile views (like global board filters) MUST be extracted into a shared composable (e.g. `useBoardFilters.ts`) that directly reads/writes the Pinia store. Do not duplicate state with local refs or use watchers to sync them — this causes double-fetching, race conditions, and synchronization bugs.
- **Reusable component abstraction** — any component used in 2+ places MUST be abstracted into `components/shared/` with a clean props/emits API. Do not duplicate UModal boilerplate across modals — use `AppModal.vue` wrapper which handles `v-model:open`, `#body`/`#header`/`#footer` slots, loading spinner, error alert, Escape key close, and consistent width styling. Import shared components explicitly in their consumers (`import AppModal from '~/components/shared/AppModal.vue'`) to ensure test environments resolve them.
- **Component tests with Nuxt stubs** — Nuxt auto-imported child components may not resolve in `mountSuspended` tests. Use `global.stubs` to stub auto-imported children when testing a parent component that consumes them.
- **DataTable fillHeight** — `DataTable` supports a `fillHeight` boolean prop. When `true`, the table fills its parent's height via `flex-1 min-h-0 overflow-y-auto` and scrolls rows internally, with a sticky header and a shrink-0 pagination footer. Use this in full-height dashboard layouts (e.g. audit log page) — leave `false` (default) on normal pages that grow with content.
- **DataTable expanded rows** — `DataTable` supports `v-model:expanded` (a `Record<string, boolean>`) and emits `update:expanded`. Pass through to `UTable`'s native expandable-row API. The row key is set via `:row-key`. Expanded content goes in `#expanded` slot. Example: `src/web-ui/app/pages/admin/audit-log.vue`.
- **Composable register/unregister listener lifecycle** — composables exposing `register`/`unregister` functions MUST manage side effects (e.g. global keydown listeners) directly in those functions, not delegate to Vue watchers. A watcher on `stack.length` won't fire when a popup registers while the stack is already non-empty (length unchanged), leaving the listener detached prematurely when `unregisterPopup` sets length to 0 but the watcher's old value was already 0. Both paths call `ensureListener()`/`removeListener()` directly.
- **localStorage persist watcher flush mode** — Pinia store watchers that persist state to localStorage on UI-triggered changes (click, drag) must use `flush: 'sync'`. Default `'post'` flush can lose the last mutation during tab close or navigation because the flush fires after the next render tick, which may never arrive.

## Commit/Docs Discipline

- Do not commit unless explicitly asked.
- If a change creates a real new architecture decision, update `docs/DECISIONS.md`, the relevant file under `docs/` (scope/functional-spec/architecture/data-model/glossary), and `CLAUDE.md` if commands or conventions change.
- Migrations should be committed with the entity/schema changes that require them in the same commit, not separately.
- Doc-only changes that fix parity between `data-model.md` and existing entities are fine in a single commit; new fields on entities must be TDD (failing test first).
