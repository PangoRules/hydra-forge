# HydraForge — Design Decision Log

> **Purpose:** Record every architectural and functional decision made during requirements gathering, along with the rationale. This prevents re-litigating settled topics and preserves context for future contributors.
>
> **Date:** 2026-06-02 (updated 2026-06-25)
> **Status:** 10 original questions resolved + 24 new decisions added (D-13–D-39). D-3, D-7 revised/rejected. Phase 3 Web UI complete. Ready for Phase 4 TUI.

---

## How to Read This

Each entry has:
- **Decision #** — unique ID
- **Topic** — what was decided
- **Date** — when it was settled
- **Status** — ✅ Settled / ❌ Rejected / 🔜 Future
- **Decision** — the concrete choice
- **Rationale** — why this path was chosen
- **Alternatives considered** — what was rejected and why
- **Impact** — architectural / UX implications

---

## D-1: Brand & Project Name

| Field | Value |
|---|---|
| **Topic** | Project name, folder structure, namespaces |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **HydraForge** — monorepo at `Projects/hydra-forge/`, namespaces `HydraForge.*` |
| **Rationale** | Evocative of multi-headed hydra — multiple interfaces (TUI, Web UI, AI agents) connected to one body (the server). "Forge" captures building/making. Memorable, unique, available. |
| **Impact** | All `.sln` files, project namespaces, Docker images, and docs use this name. |

---

## D-2: Dual Interface — Feature Parity

| Field | Value |
|---|---|
| **Topic** | TUI vs Web UI relationship |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Full feature parity** — whatever you can do in one interface, you can do in the other. The TUI is not a stripped-down version. |
| **Rationale** | The Terminal Power User persona must never be forced to open a browser to complete a task. Parity ensures no interface is a second-class citizen. |
| **Impact** | Every feature has both a TUI command and a Web UI panel. Build both simultaneously or in close succession. |

---

## D-3: Offline Capabilities

| Field | Value |
|---|---|
| **Topic** | What works offline |
| **Date** | 2026-06-02 |
| **Status** | ❌ Rejected |
| **Decision** | **No offline mode.** HydraForge requires an active connection to the server at all times. |
| **Rationale** | Developers always have internet access — whether in the office or remote, they connect via VPN to the server where HydraForge is deployed. Offline-first adds a local SQLite cache, a sync engine, a pending changes queue, and a conflict resolution system — all significant complexity with negligible real-world benefit. Cutting this keeps the architecture lean and focused. |
| **Alternatives considered** | Full offline with local SQLite + sync (rejected — high complexity, low real benefit given VPN requirement). |
| **Impact** | No local SQLite on TUI. No sync engine. No pending changes queue. D-7 (conflict resolution) is also dropped — no offline = no sync conflicts. Significant scope reduction. |

---

## D-4: User Personas

| Field | Value |
|---|---|
| **Topic** | Who HydraForge serves |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Four personas:** Terminal Power User (TUI), Team Member (Web UI), Manager (Web UI overview), AI Agent (API). |
| **Rationale** | Covers all stakeholders without over-engineering. Maps cleanly to the dual-interface design. AI Agent is a first-class actor with its own identity. |
| **Impact** | API surface must support agent operations. Manager views are a subset of Web UI (no separate build). |

---

## D-5: Card Types

| Field | Value |
|---|---|
| **Topic** | CardType enum values |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **CardType enum:** `Task`, `Issue`, `Goal`, `Idea`. Plans are a **separate entity**, not a card type. `Spec` card type retired (rows migrated to `Goal`); `Bug` renamed `Issue`; `Epic` renamed `Goal`; parent restriction removed — any card can parent any other card. |
| **Rationale** | Original types were software-specific. Universal replacements (Goal/Issue/Idea) work across any domain. Epics-only parent restriction was unnecessary — cycle detection already prevents bad hierarchies. |
| **Impact** | Domain model: `Card` has `CardType`. `Spec` and `Plan` are project-level entities owned by cards via `Spec.CardId` and `Plan.CardId` FKs (ownership — one card creates and owns its spec/plan, other cards can read but not edit). `Plan.SpecId` is an optional FK linking a plan to its parent specification. Each has version snapshot entities (`SpecVersion`, `PlanVersion`) storing full document state (title, description, content) for history + restore. |

---

## D-6: Authentication

| Field | Value |
|---|---|
| **Topic** | How users log in |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Basic authentication** (username + password, self-contained user store). **No SSO/OAuth/Entra/Okta.** |
| **Rationale** | HydraForge is self-hosted. SSO requires paid services or complex IdP setup. Basic auth is free, portable, and works with a single `docker-compose up`. The goal is maximum openness — zero external dependencies. |
| **Alternatives considered** | SSO/OAuth (rejected — costs money, adds complexity, requires internet). |
| **Impact** | Server needs a local user store (in PostgreSQL — `User` entity). No integration with auth providers. Passwords hashed with bcrypt/Argon2. Admin seeded on first run. |

---

## D-7: Conflict Resolution

| Field | Value |
|---|---|
| **Topic** | How sync conflicts are handled |
| **Date** | 2026-06-02 |
| **Status** | ❌ Rejected (superseded by D-3 rejection) |
| **Decision** | **No conflict resolution needed.** D-3 (offline mode) was rejected. No offline = no divergent local states = no sync conflicts. |
| **Rationale** | Conflict resolution only exists to reconcile offline changes made while disconnected. Since HydraForge has no offline mode, all mutations go directly through the server and SignalR pushes updates to all clients in real-time. Concurrent edits are handled by last-write-wins at the database level (standard behavior), which is sufficient for a team tool on a LAN. |
| **Impact** | No conflict detection logic. No ⚠️ badge. No audit LWW strategy. Audit log still records all mutations (for history), but conflict resolution as a feature does not exist. |

---

## D-8: Multi-Tenant

| Field | Value |
|---|---|
| **Topic** | One server serving many companies |
| **Date** | 2026-06-02 |
| **Status** | ❌ Rejected |
| **Decision** | **No multi-tenant.** Each team/organization runs a fresh installation. |
| **Rationale** | Multi-tenant adds enormous complexity (org isolation, billing, provisioning). Odysseus isn't multi-tenant either. Fresh installs are simpler, more secure, and align with the "self-hosted" philosophy. If another team wants HydraForge, they `docker-compose up` their own instance. |
| **Impact** | No `Organization`/`TenantId` in the data model. No provisioning UI. Single PostgreSQL per install. Simpler auth — just a local `User` table. |

---

## D-9: File Attachments

| Field | Value |
|---|---|
| **Topic** | Can files be attached to cards? |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Yes — file attachments on cards.** Store on local filesystem or S3-compatible storage (user's choice). |
| **Rationale** | File attachments are a strong selling point — many project management tools limit attachment size or charge for storage. Being able to attach screenshots, logs, PDFs, and reference files to a card is a concrete quality-of-life win. |
| **Impact** | New entity `Attachment` (CardId, FileName, Size, ContentType, StoragePath). Storage abstraction (IFileStore) with LocalFileStore + S3FileStore implementations. Each card can list attachments in TUI and Web UI. |

---

## D-10: Notifications

| Field | Value |
|---|---|
| **Topic** | How users are notified of changes |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **ntfy (open-source self-hosted push) + SignalR in-app bell. No email.** Per-user ntfy topic: `hydraforge-{userId}`. ntfy delivers to browser, mobile, and desktop — zero cost, self-hosted. |
| **Rationale** | Email requires SMTP/paid services. ntfy is open-source, self-hosted, and covers all notification surfaces (browser, mobile, desktop) without cost or external dependency. Same approach as Odysseus. SignalR bell provides in-app real-time count. |
| **Alternatives considered** | Email (rejected — costs money, SMTP complexity). Mobile push via FCM/APNs (rejected — requires paid developer accounts). |
| **Impact** | `Notification` entity (UserId, Message, CardId, ProjectId, IsRead, CreatedAt). SignalR hub pushes to connected clients. Bell icon in Web UI. TUI shows unread count in status bar. Admin configures ntfy server URL in system settings. No email daemon, no SMTP config. |

---

## D-11: LLM Providers

| Field | Value |
|---|---|
| **Topic** | Which LLM providers to support, who configures them |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Admin-only configuration. Pluggable via `ILlmClient`.** Ship with OpenAI-compatible, Anthropic, and Ollama adapters. Users select from admin-provided providers only — no personal API keys. |
| **Rationale** | HydraForge is a team tool deployed by an org. Centralizing LLM config prevents key sprawl, billing surprises, and security risks. The `ILlmClient` abstraction keeps it extensible — any OpenAI-compatible endpoint works without code changes. |
| **Impact** | `ILlmClient` interface with `StreamChatAsync()`, `GetModelsAsync()`, `SupportsToolCalling()`, cache block placement. `IImageClient` for image generation. `IEmbeddingClient` for RAG/Brain embeddings. Ship adapters: `OpenAiCompatClient`, `AnthropicClient`, `OllamaClient`. Provider + API key + tier + fallback configurable by admin only. |

---

## D-12: Data Architecture

| Field | Value |
|---|---|
| **Topic** | Database, storage, setup |
| **Date** | 2026-06-02 (revised 2026-06-02) |
| **Status** | ✅ Settled |
| **Decision** | **PostgreSQL per install.** Server uses EF Core + Npgsql. Docker Compose for one-command setup (server + postgres containers). |
| **Rationale** | HydraForge is multi-user and real-time — multiple users simultaneously move cards, post comments, trigger SignalR events, and create notifications. SQLite uses file-level locking (even in WAL mode) and causes write contention under concurrent load. PostgreSQL uses MVCC (multi-version concurrency control) — built for exactly this pattern. Additional benefits: `pgvector` (active — used for Brain/Memory semantic search and RAG document chunks), native full-text search (`tsvector`) for card/spec search, mature tooling. The docker-compose overhead is one extra service (5 lines) — trivial cost for the concurrency gain. |
| **Alternatives considered** | SQLite (rejected — write contention under multi-user real-time load). MySQL/MariaDB (rejected — PostgreSQL has better EF Core support, pgvector, and JSON). |
| **Impact** | `docker-compose.yml` includes a `pgvector/pgvector:pg16` PostgreSQL service with a named volume. EF Core uses `Npgsql.EntityFrameworkCore.PostgreSQL` plus pgvector mapping. Backup via `pg_dump`. Connection strings use standard .NET configuration, usually `ConnectionStrings__Default` in environment variables. |

---

## D-33: OpenAPI Documentation (Swagger Replacement)

| Field | Value |
|---|---|
| **Topic** | How the REST API is documented and tested interactively |
| **Date** | 2026-06-09 |
| **Status** | ✅ Settled |
| **Decision** | **Replace Swashbuckle/Swagger with built-in `Microsoft.AspNetCore.OpenApi` for doc generation + `Scalar.AspNetCore` for the interactive UI.** |
| **Rationale** | Microsoft deprecated Swashbuckle in default templates starting .NET 9, and in .NET 10 the `Microsoft.OpenApi` library had a major v2 breaking change. Swashbuckle 10.x depends on `Microsoft.OpenApi` 2.x which removed the `Microsoft.OpenApi.Models` namespace, broke all type references (`OpenApiInfo`, `OpenApiSecurityScheme`, etc.), and restructured the public API. Rather than fight the breaking changes and maintain compatibility with a deprecated library, we switch to Microsoft's recommended path: `Microsoft.AspNetCore.OpenApi` (already in the project) generates the OpenAPI 3.1 document, and Scalar provides a modern dark-mode interactive reference UI. Scalar is actively maintained, has no legacy compatibility burden, and is the de-facto standard in the .NET ecosystem for replacing Swagger UI. |
| **Alternatives considered** | 1. Fix Swashbuckle 10.x references to use root `Microsoft.OpenApi` namespace + new v2 API (rejected — the `Reference` property on security schemes was removed, security requirement API changed, and migration path is poorly documented / unstable). 2. Use `Microsoft.AspNetCore.OpenApi` for docs + `Swashbuckle.AspNetCore.SwaggerUI` for UI (rejected — adds complexity of mixing two systems with different transformer/filter models for no benefit over Scalar). |
| **Impact** | Removed `Swashbuckle.AspNetCore` and `Swashbuckle.AspNetCore.Annotations` packages. Replaced `AddSwaggerGen()` / `UseSwagger()` / `UseSwaggerUI()` with `AddOpenApi()` / `MapOpenApi()` / `MapScalarApiReference()`. OpenAPI doc served at `/openapi/v1.json`. Scalar UI served at `/scalar/v1` (dev only). For customizing the OpenAPI doc (e.g. adding Bearer auth scheme), use `IOpenApiDocumentTransformer` / `IOpenApiOperationTransformer` instead of Swashbuckle filters. The `Microsoft.OpenApi.Models` namespace does not exist in OpenAPI.NET v2.x — all types live in root `Microsoft.OpenApi`. **Note:** `[ProducesResponseType]` attributes (built-in ASP.NET Core, distinct from Swashbuckle's `[SwaggerResponse]`) were later added to all controller actions to improve OpenAPI spec accuracy — these are standard MVC attributes and do not reintroduce Swashbuckle dependency. |

---

## Summary

| # | Topic | Decision | Status |
|---|---|---|---|
| D-1 | Brand | HydraForge | ✅ |
| D-2 | Dual interface | Full feature parity | ✅ |
| D-3 | Offline | Rejected — server connection required, TUI locks gracefully | ❌ |
| D-4 | Personas | 4 personas: Terminal, Team, Manager, AI | ✅ |
| D-5 | Card types | Enum `Task/Issue/Goal/Idea`. Plan = own entity. Any card can parent any card. | ✅ |
| D-6 | Auth | Basic auth, no SSO | ✅ |
| D-7 | Conflict resolution | Rejected — no offline = no sync conflicts | ❌ |
| D-8 | Multi-tenant | No — fresh install per team | ✅ |
| D-9 | File attachments | `IFileStore` abstraction — Local (fallback) or S3-compatible (MinIO/AWS S3) | ✅ |
| D-10 | Notifications | In-app (ntfy) + SignalR bell | ✅ |
| D-11 | LLM providers | Pluggable, admin-configured only | ✅ |
| D-12 | Data architecture | PostgreSQL per install, Docker Compose | ✅ |
| D-13 | Offline mode | Rejected — VPN always available | ❌ |
| D-14 | Chat folder structure | Max 2 levels, project auto-creates folder | ✅ |
| D-15 | Chat ↔ project integration | Fresh + smart context injection | ✅ |
| D-16 | AI edit permissions | Chat-session-scoped, user confirmation gate | ✅ |
| D-17 | Card chat summaries | Collapsible table on card, owner-clickable | ✅ |
| D-18 | Shared project chats | View-only, summarize-to-continue | ✅ |
| D-19 | Project visibility | Members-only (Option B) | ✅ |
| D-20 | Card edit permissions | Member = full edit, admin = all, non-member = invisible | ✅ |
| D-21 | Project creation | Any user, becomes owner, managers see all | ✅ |
| D-22 | Notification rules | Card move → assignees, comment → watchers, project events → all members | ✅ |
| D-23 | LLM configuration | Admin-only, no personal provider by users | ✅ |
| D-24 | AI agent personality | User-definable system prompt per account | ✅ |
| D-25 | Presence indicators | Ephemeral SignalR presence via PresenceHub (/hubs/presence) — UserJoined, UserLeft, CardFocused; ConcurrentDictionary in-memory, no DB writes | ✅ |
| D-26 | Error handling | Air Force standard — global middleware, Result pattern, ProblemDetails, correlationId | ✅ |
| D-27 | Card dependencies | Typed relationships (BlockedBy, Precedes, Relates), soft warnings, cycle detection | ✅ |
| D-28 | Keyboard navigation | Both TUI and Web UI fully keyboard-navigable, shortcut reference overlay | ✅ |
| D-29 | Model routing & token management | Tier-based routing, per-feature defaults, token tracking, prompt caching, fallback chain | ✅ |
| D-30 | Card human-readable number | Sequential `CardNumber` per project (1, 2, 3…) — like GitHub issues | ✅ |
| D-31 | RAG pipeline | DocumentChunk + pgvector; IEmbeddingClient abstraction; chunks regenerated on doc change | ✅ |
| D-32 | ProjectContextSnapshot strategy | TemplateContent instant on mutation; AiNarrative nightly scheduled job only | ✅ |
| D-33 | OpenAPI docs (Swagger replacement) | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` instead of Swashbuckle | ✅ |
| D-34 | File storage architecture | `IFileStore` abstraction (Local fallback + MinIO/AWS S3), `{userId}/{sourceType}/{sourceId}/{guid}` key hierarchy, `InitializeAsync` for bucket creation | ✅ |
| D-35 | ProjectContextSnapshot implementation | Pure deterministic renderer (`ProjectContextSnapshotRenderer`) — no LLM, no side effects; `IProjectSnapshotRefresher` port injected into 9 mutation services; `ProjectSnapshotRefresher` EF impl in Infrastructure; `GET /api/projects/{projectId}/ProjectSnapshot` members-only endpoint | ✅ |
| D-36 | Board event publishing | `IProjectBoardEventPublisher` port in Application layer; `SignalRProjectBoardEventPublisher` impl; `BoardHub` (/hubs/board) fans out to project groups; `ProjectBoardEventEnvelope` with `BoardEntityType` + `BoardAction`; `RealtimeServiceCollectionExtensions.AddRealtimeServices()` wires DI | ✅ |
| D-37 | vue-draggable-plus removal | Removed SortableJS wrapper; plain `v-for` for board lists; native HTML5 drag planned | ✅ |
| D-38 | Nuxt UI v4 UModal usage | `v-model:open`, named slots (`#body`/`#header`/`#footer`), no `UOverlay` | ✅ |
| D-39 | USelect clearable workaround | Wrap `USelect` in `relative` container with ghost `UButton` (X icon) | ✅ |
| D-40 | `useApi()` throws | `try/catch` required on every call site; `{ error }` destructuring without try/catch is dead code | ✅ |
| D-41 | Card detail panel version ownership | `CardModal.vue` owns `card` ref + `version`; panels emit `update:card` on save, never cache their own copy | ✅ |
| D-42 | Playwright E2E testing | `@playwright/test` adopted; specs seed their own data via API; run serially (`workers: 1`) | ✅ |
| D-43 | Shared filter state via composables | Extract to `useBoardFilters.ts`; desktop + mobile both read/write Pinia store directly | ✅ |
| D-44 | Doc model redesign | DocType on Spec; PlanStatus on Plan; multi-plan; Idea→Goal `SpawnedFrom` | ✅ |
| D-45 | Plan SetStatus endpoint | Single `PATCH` replaces Activate/Complete/Reactivate; UI shows status dropdown | ✅ |
| D-46 | Blocked card move UI | Web UI never sends `confirmBlockedMove=true`; 409 → rollback + toast, no confirm modal | ✅ |
| D-47 | Keyboard nav composable split | `useRovingFocus` + `useBoardKeyboardNav`; `activate()`/`deactivate()` pattern for composable lifecycle | ✅ |
| D-48 | TUI API client + SignalR + credential storage | NSwag codegen; `Microsoft.AspNetCore.SignalR.Client`; plain JSON file at `.hydraforge/config.json` | ✅ |
| D-49 | Credential storage location | Repo-root `.hydraforge/config.json` (found via `HydraForge.slnx` walk), not `~/.config` | ✅ |
| D-50 | OpenAPI enum schemas | EnumSchemaTransformer corrects `type: integer` → `type: string` in schema; NSwag generates real C# enums | ✅ |
| D-51 | Web UI NotificationHub deferred | `useNotifications.ts` has `onNotificationReceived` handler; wired to a live SignalR connection via `useNotificationHub.ts`, connected from `layouts/default.vue` on mount (D-51 follow-up, 2026-07-27) | ✅ |
| D-52 | ntfy client implementation | `INtfyClient` port in Application; `NtfyClient` + `NtfyOptions` in Infrastructure; `SystemSettings.NtfyServerUrl`; docker-compose `ntfy` service; best-effort push (null URL = no-op) | ✅ |
| D-53 | SignalR JsonStringEnumConverter | Hub payload enums must serialize as strings, not ints — TUI silently drops int-encoded board events | ✅ |
| D-54 | Targeted realtime board patching | `useRealtime.ts` routes by entity type for per-entity re-fetch instead of full board refresh; `CardId` on envelope for card-scoped sub-entities | ✅ |
| D-55 | Notification triggers never block business op | `try/catch` + `IWarnLogger` at every `NotifyAsync` call site | ✅ |
| D-56 | Dependency-resolved notification fires on Archive | Moved from `MoveAsync` to `ArchiveAsync` — only place `ArchivedAt` is set | ✅ |
| D-57 | Nightly job scheduler | Hangfire + `Hangfire.PostgreSql`, not `BackgroundService` or Quartz.NET | ✅ |
| D-58 | AiNarrative display surface | Web UI: "View Narrative" button next to project title → modal. TUI: same viewer pattern as spec/plan, bound to board screen's help overlay if bar has no room | ✅ |

---

## D-27: Card Dependencies

| Field | Value |
|---|---|
| **Topic** | How cards express dependency relationships |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Typed relationships: `BlockedBy`, `Precedes`, `Relates`. Soft warning on column move. Circular dependency detection at application layer. Scoped to same project only.** |
| **Dependency model** | Option B — `CardRelationship` entity with directed typed edges. Same underlying data structure that can scale to full DAG (Option C) later without schema changes. |
| **Enforcement** | Soft warning only — never hard-block a column move. User sees "This card is blocked by #42 (Auth Middleware). Move anyway?" and decides. Board always shows lock icon + blocked-by count badge on blocked cards. |
| **Circular detection** | Application layer validates acyclic graph on every `CardRelationship` insert. Returns `DEPENDENCY_CYCLE_DETECTED` error code if cycle found. Never written to DB. |
| **Archive behavior** | Archiving a card with dependents: user warned + shown list of affected cards. On confirm — `CardRelationship.ArchivedAt` set (soft delete, invisible in UI). Relationship record retained in audit log. Dependency history auditable but not shown in active board. |
| **Cross-project** | Explicitly out of scope. Both `SourceCardId` and `TargetCardId` must belong to the same project. Enforced at application layer. |
| **AI + dependencies** | AI can propose dependencies when creating or editing cards. Uses `ProjectContextSnapshot` card index (id → title → column) — never fabricates IDs. Bulk card creation: all proposals confirmed as a list, not one-by-one. |
| **Notification** | When a blocking card moves to Done: assignees of all `BlockedBy` dependent cards receive ntfy ping: "{blocker title} is done — your card {title} is now unblocked." |
| **Impact** | `CardRelationship` entity added to domain. `CardDependencyService` in application layer with `ValidateAcyclic()` check. Board client renders lock badge. Column move emits `BlockedCardMoveWarning` before proceeding. |

---

## D-28: Keyboard Navigation

| Field | Value |
|---|---|
| **Topic** | Keyboard navigability of TUI and Web UI |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Both TUI and Web UI are fully keyboard-navigable. No mouse required for any action in either interface.** |
| **Rationale** | Power users live in the keyboard. Forcing mouse for common actions in Web UI creates a two-tier experience where TUI users feel penalized when switching to browser. Feature parity extends to input method. |
| **TUI** | Vim-style movement (`h/j/k/l`), `d` for dependencies, `?` for shortcut reference, `Enter` to open card detail, `n` for new card, standard modal navigation. |
| **Web UI** | Full keyboard shortcut system: navigate columns/cards with arrow keys, `Enter` to open card, `n` for new card, `m` to move card, `/` to search, `?` for shortcut overlay. All modals closable with `Escape`. Focus trapped correctly in modals. |
| **Shortcut reference** | Both interfaces expose a shortcut reference: Web UI shows overlay modal on `?`, TUI shows inline help on `?`. Consistent mental model across interfaces. |
| **Impact** | Web UI keyboard event system built from Phase 2. Not retrofitted. All interactive components (board, cards, modals, panels) implement `onKeyDown` handlers. Focus management tested. |

## D-26: Error Handling Strategy

| Field | Value |
|---|---|
| **Topic** | How errors are caught, surfaced, and logged |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Air Force standard error handling.** Every error is accounted for. Nothing is silently swallowed. Nothing raw is exposed to clients. |
| **Principles** | 1. Global exception middleware catches ALL unhandled exceptions — no error escapes to the framework default handler. 2. Business logic uses `Result<T, Error>` pattern — exceptions are for truly exceptional conditions only, not expected failures. 3. All errors return structured `ProblemDetails` (RFC 7807) with a named error code and `correlationId`. 4. Stack traces never sent to clients — server-side logs only. 5. External service failures (LLM, Git, ntfy) never crash core board functionality. 6. TUI locks visibly on connection loss; never crashes silently. |
| **Error codes** | Typed, exhaustive enum — every failure condition has a named code (e.g. `CARD_NOT_FOUND`, `COLUMN_LIMIT_EXCEEDED`, `LLM_RATE_LIMITED`, `GIT_PUSH_REJECTED`). No generic "something went wrong" without a code. |
| **Logging** | Structured JSON logging. 4xx → Warning. 5xx → Error. Every log entry includes: `correlationId`, `userId`, `endpoint`, `durationMs`, error context. |
| **Resilience** | PostgreSQL down → 503, fail loudly (board cannot work). LLM/Git/ntfy down → surfaced in relevant UI panel, board continues working. |
| **Impact** | `GlobalExceptionMiddleware` in `HydraForge.Server`. `Result<T, Error>` and `Error` types in `HydraForge.Domain`. `ProblemDetailsExtensions` mapper in `HydraForge.Server`. TUI error panel component. Web UI toast system. Structured logging via `Microsoft.Extensions.Logging` + provider (Serilog recommended). |

---

## D-13: Offline Mode

| Field | Value |
|---|---|
| **Topic** | Offline-first architecture |
| **Date** | 2026-06-02 |
| **Status** | ❌ Rejected |
| **Decision** | See D-3. |

---

## D-14: Chat Folder Structure

| Field | Value |
|---|---|
| **Topic** | How chats are organized |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Max 2 levels of folder nesting. Project creation auto-creates a matching chat folder.** |
| **Rationale** | Two levels covers all real-world organization needs (e.g. `KenanAdvantage/OrdersApi`). Deeper nesting is theoretical overkill and creates UX problems in TUI tree views. Auto-creating a project folder keeps chat and project namespaces in sync without manual setup. |
| **Impact** | `ChatFolder` entity: `ParentFolderId` nullable, depth enforced at application layer (reject if depth > 2). Project creation triggers folder creation. Project archive triggers folder archive. |

```
Chats
├── General/                     ← free-form, no depth limit enforced here
│   └── SubFolder/               ← max 2 levels: General/SubFolder
└── Projects/
    ├── KenanAdvantage/          ← auto-created on project creation
    │   ├── OrdersApi/           ← sub-project folder
    │   └── [chat sessions]
    └── PersonalProject/
        └── [chat sessions]
```

---

## D-15: Chat ↔ Project Integration

| Field | Value |
|---|---|
| **Topic** | How chat and project boards connect |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Fresh chat every time the project panel opens, with smart context injection. A persistent project context snapshot (board summary) is maintained and injected automatically.** |
| **Rationale** | Persistent conversation threads accumulate context window debt on long projects. Fresh-per-session with auto-injected project state gives AI awareness without the bloat. A maintained summary file (regenerated on board state changes) is the "memory" — cheap to inject, always current. |
| **Context payload injected at chat start** | 1. `ProjectContextSnapshot.TemplateContent` (template-rendered board state: columns, card index with numbers/titles/columns/types, open blockers). 2. Open card context if a card is currently open: title, description, type, assignees, checklist, comments. 3. User's AI personality prompt (see D-24). The `AiNarrative` (nightly AI summary) is shown on the project dashboard but not injected into chat — keeps context lean. |
| **Impact** | `ProjectContextSnapshot` entity: `ProjectId`, `TemplateContent` (regenerated on every board mutation — instant, no LLM), `AiNarrative` (nullable, generated by nightly scheduled job), `TemplateGeneratedAt`, `AiNarrativeGeneratedAt`. Chat panel always opens a new `ChatSession` with `ProjectId` FK. Closed chats land in project's chat folder. |

---

## D-16: AI Edit Permissions

| Field | Value |
|---|---|
| **Topic** | Can the AI mutate board state from chat? |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **AI proposes changes, user confirms before any mutation. Permission to allow AI edits is chat-session-scoped and non-persistent.** |
| **Rationale** | Human must always be the gate on board mutations — AI should assist, not act unilaterally. Session-scoped permission (not persisted, not carried to new chats) ensures the user consciously re-grants access each time, preventing accidental runaway edits. |
| **UX flow** | 1. AI proposes: "Move card #42 to In Review?" 2. Confirmation dialog appears. 3. User approves → mutation applied + audit logged. OR: User grants session-level permission ("allow edits this session") → AI can mutate without per-action confirmation for duration of that chat. |
| **Session boundary** | Permission granted in chat A does NOT carry to chat B. Reopening a closed chat starts a new session — permission must be re-granted. Login session is irrelevant to this boundary. |
| **Impact** | `AiEditPermission` is ephemeral — tracked in-memory on the server per `ChatSessionId`, never stored in DB. |

---

## D-17: Card-Level Chat Summaries

| Field | Value |
|---|---|
| **Topic** | Chat history visibility at card level |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Each card shows a collapsible table of all chats that referenced it. Each row: user avatar, summary, date. Owner's row is clickable (reopens that chat). Others' rows are read-only.** |
| **Rationale** | Cards accumulate decisions and context over time. Surfacing chat summaries at card level creates a lightweight audit trail of AI-assisted work without requiring users to dig through chat folders. |
| **Impact** | `CardChatLink` entity: `CardId`, `ChatSessionId`, `OwnerId`, `Summary` (auto-generated on chat close or when card is first referenced). Auto-link created when: user opens a card while in project chat panel, or AI references a specific card by number during conversation. |

---

## D-18: Shared Project Chats

| Field | Value |
|---|---|
| **Topic** | Can team members see each other's project chats? |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Project chats are owned by one user but visible to all project members. Non-owners see the conversation read-only. A "Summarize this → start my own" button lets any member fork the context into a new personal chat.** |
| **Rationale** | Transparency is valuable on a team — seeing how a teammate designed a module with AI is useful context. But non-owners should not be able to interject into someone else's conversation thread. The fork mechanism lets them benefit from prior work without polluting the original. |
| **Impact** | `ChatSession.IsShared` bool (default `true` for project-folder chats, `false` for general chats). Shared chats: all project members can read, only owner can send messages. Fork action: creates new `ChatSession` in user's project folder, injects summary of forked chat as first system message. |

---

## D-19: Project Visibility

| Field | Value |
|---|---|
| **Topic** | Which users can see a project |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Members-only. Non-members cannot see a project exists. Admin can see all projects.** |
| **Rationale** | Avoids noise — developers only see projects they're working on. Clean separation between teams working on different things on the same install. |
| **Alternatives considered** | Public-by-default (rejected — noisy). Public/private flag (rejected — adds settings complexity for MVP). |
| **Future** | View-only permission level (read board, no edits) deferred to post-MVP. |
| **Impact** | All project queries filter by `ProjectMember.UserId = currentUser` unless `currentUser.IsAdmin`. |

---

## D-20: Card Edit Permissions

| Field | Value |
|---|---|
| **Topic** | Who can create, edit, move, delete cards |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Any project member can create, edit, move, and delete cards. Admin can do everything across all projects. Non-members cannot see the project.** |
| **Rationale** | Lightweight — no per-card ownership friction. Team trust model: if you're on the project, you can work freely. |
| **Future** | View-only role deferred. Card ownership/lock (only assignee can edit) deferred. |
| **Impact** | Authorization check: `IsMember(projectId, userId) || IsAdmin(userId)`. |

---

## D-21: Project Creation & Ownership

| Field | Value |
|---|---|
| **Topic** | Who creates projects and manages members |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Any authenticated user can create a project and becomes its owner. Adding members is optional (single-person projects are valid). Admin can see and manage all projects.** |
| **Rationale** | No forced ceremony for solo/small projects. Owner has full control. Admin is the escape hatch for organizational oversight. |
| **Impact** | `Project.OwnerId` FK. `ProjectMember` junction table: `ProjectId`, `UserId`, `Role` (`Owner` / `Member`). Owner is auto-inserted as `Owner` on project creation. |

---

## D-22: Notification Rules

| Field | Value |
|---|---|
| **Topic** | Who gets notified of what |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | Specific trigger → specific audience, delivered via ntfy + SignalR in-app bell. |
| **Rules** | |

| Trigger | Notified |
|---|---|
| Card moved to new column | Card assignees |
| Card assigned to user | That user |
| Comment added to card | All card watchers (anyone who commented or is assigned) |
| @mention in comment | Mentioned user |
| Card checklist item completed | Card assignees |
| Blocking card moves to Done | Assignees of all cards that were `BlockedBy` it |
| Project archived | All project members |
| Project settings edited | All project members |
| AI proposes a card mutation | Card assignees + acting user |
| PR created (Git agent) | All project members |

| **Impact** | `Notification` entity: `UserId`, `Message`, `CardId?`, `ProjectId?`, `IsRead`, `CreatedAt`. `CardWatcher` junction: auto-add user as watcher when they comment or get assigned. ntfy topic per user: `hydraforge-{userId}`. SignalR hub pushes to connected client simultaneously. |

---

## D-23: LLM Configuration

| Field | Value |
|---|---|
| **Topic** | Who configures LLM providers |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Admin-only configuration. Users select from admin-provided providers. No personal provider setup by regular users.** |
| **Rationale** | HydraForge is a team tool deployed by an org. Centralizing LLM config prevents key sprawl, billing surprises, and security risks from users storing personal API keys in a shared system. |
| **Pluggability** | `ILlmClient` interface: `StreamChatAsync()`, `GetModelsAsync()`, `SupportsToolCalling()`. Ship with: `OpenAiClient` (covers OpenAI, Groq, DeepSeek, OpenRouter, vLLM, llama.cpp — all speak OpenAI protocol), `AnthropicClient`, `OllamaClient`. Admin adds any custom endpoint: base URL + API key + model list probe. |
| **Impact** | `LlmProvider` entity: `Name`, `BaseUrl`, `ApiKeyEncrypted`, `Models[]`, `IsEnabled`. Managed only via admin settings panel. Users see provider display names, never credentials. |

---

## D-24: AI Agent Personality

| Field | Value |
|---|---|
| **Topic** | Can users customize how the AI communicates |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Each user can define a personal AI personality — a custom system prompt appended to every chat. Multiple personalities per user, one marked default.** |
| **Rationale** | Different users work differently — some want concise responses, some want step-by-step explanations, some want a specific tone or persona. Personality is injected at chat start as part of the context payload (see D-15), so it naturally varies per user without affecting project context. |
| **Impact** | `AgentPersonality` entity: `UserId`, `Name`, `SystemPrompt`, `IsDefault`. Injected as last element of context payload in D-15. User manages personalities in their account settings. |

---

## D-25: Presence Indicators

| Field | Value |
|---|---|
| **Topic** | Can users see who else is active on a card/project |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled (implemented 2026-06-22) |
| **Decision** | **Ephemeral presence via SignalR `PresenceHub`. Green dot on card/board. `FocusCard` for card-level presence.** |
| **Rationale** | Cheap to implement with SignalR already in-stack. Prevents two people unknowingly editing the same card simultaneously. High value, low cost. |
| **Hub route** | `/hubs/presence` |
| **Events** | `UserJoined`, `UserLeft`, `CardFocused` |
| **Storage** | `ConcurrentDictionary` in-memory — no DB writes. Lost on server restart. |
| **Impact** | `PresenceHub` in `HydraForge.Server.Hubs`. `FocusCard(projectId, cardId)` broadcasts to project group. `OnDisconnectedAsync` cleans up all group memberships. |

---

## D-29: Model Routing & Token Management

| Field | Value |
|---|---|
| **Topic** | Which model runs which feature, how costs are controlled |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Tier-based routing. Admin maps models to tiers (Economy / Standard / Premium). Admin assigns default tier per feature. Per-user token budgets. Prompt caching. Model fallback chain.** |
| **Rationale** | Assigning specific models per feature is brittle — models change, providers add better cheaper options. Tiers are stable abstractions. Admin can swap the Economy model from `gpt-4o-mini` to `gemini-flash` without touching feature config. This also naturally separates cost concerns: personal space features default to Economy, mission-critical agent pipelines default to Premium. |
| **Tier definitions** | Economy — fast, cheap (haiku, gpt-4o-mini, gemini-flash). Standard — balanced (sonnet, gpt-4o, gemini-pro). Premium — most capable (opus, o3, gemini-ultra). |
| **Feature defaults** | Economy: personal chat, notes classify, memory extract, cookbook. Standard: project chat, card AI review, document editing. Premium: deep research, agent pipeline. Compare: user selects per run. |
| **User overrides** | Admin sets ceiling per feature. E.g. personal chat allows up to Standard, agent pipeline locked to Premium. User can pick any tier at or below ceiling. |
| **Context window guard** | Before routing, `ModelRouter` checks compressed context token count against model's window limit. If Economy model window insufficient, auto-bumps to Standard. Logged as a bump event. |
| **Context compression** | Configurable threshold (default: 80% of target model's window). `ContextCompressor` auto-summarizes injected context before sending. ProjectContextSnapshot is the primary compression target. |
| **Prompt caching** | `ILlmClient` abstraction exposes `cache_control` block placement. `ProjectContextSnapshot` and user memory formatted as cache-eligible prefix. Anthropic and OpenAI cache hits tracked in `TokenUsageRecord.CachedTokens`. |
| **Fallback chain** | Admin defines `LlmProvider.FallbackProviderId`. On rate-limit or 5xx, `ModelRouter` retries with fallback before surfacing error. Max 1 fallback hop to avoid cascading delays. |
| **Token tracking** | Every LLM call writes a `TokenUsageRecord`: user, `AiFeature`, model, input/output/cached tokens, project, pipeline run. Budget enforcement at call time — reject if daily/monthly limit exceeded, return `TOKEN_BUDGET_EXCEEDED` error. |
| **Cost visibility** | Admin: usage dashboard by user / feature / model / period. User: self-service view of own usage. Deep Research and agent pipeline show input token estimate before run. |
| **Image generation** | Image providers configured in same `LlmProvider` table with `ProviderType: Image`. Separate image tiers (Economy = local SD/Flux-schnell, Standard = DALL-E 2/SD3, Premium = DALL-E 3/Flux-pro). Image usage tracked in `ImageUsageRecord` (not `TokenUsageRecord` — images are per-image priced, not per-token). Surfaces: in-chat generation, gallery editor (inpaint/upscale/style), document insert. |
| **Impact** | New domain services: `ModelRouter`, `ContextCompressor`. New entities: `ModelTier` enum, `ProviderType` enum, `AdapterType` enum, `AiFeature` enum, `ProviderModelConfig`, planned `FeatureRoutingConfig`, `UserTokenBudget`, `TokenUsageRecord`, `ImageUsageRecord`, `LlmProvider.Tier` + `FallbackProviderId` + `ProviderType` + `AdapterType`. Admin settings panel gains model routing + image provider sections. `ILlmClient` updated with cache block support. `IImageClient` abstraction added for image providers. |

---

## D-30: Card Human-Readable Number

| Field | Value |
|---|---|
| **Topic** | How cards are referenced by humans and AI |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Sequential `CardNumber int` per project.** Cards are referenced as `#1`, `#42`, etc. — unique within a project, like GitHub issues. |
| **Rationale** | Cards use GUIDs as primary keys, which are unreadable. The dependency system, AI card proposals, chat summaries, board display, and `ProjectContextSnapshot` card index all need a short human-readable identifier. Sequential per-project numbers are familiar (GitHub, Linear, Jira all use this pattern) and easy to type. |
| **Implementation** | Application layer assigns `CardNumber` on creation: `MAX(CardNumber) + 1` within the project. Never reused after deletion. `CardNumber` is unique per `ProjectId` — enforced by DB unique constraint. |
| **Impact** | `Card.CardNumber int` field. Unique index on `(ProjectId, CardNumber)`. API supports card lookup by number (`GET /projects/{projectId}/cards/{cardNumber}`). TUI and Web UI display `#CardNumber` not GUID. `ProjectContextSnapshot.TemplateContent` card index uses CardNumber. |

---

## D-31: RAG Pipeline

| Field | Value |
|---|---|
| **Topic** | Retrieval-Augmented Generation for chat context |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **`DocumentChunk` entity + pgvector + `IEmbeddingClient` abstraction.** User uploads a file → text extracted → chunked (~500 tokens) → embedded → stored in pgvector. At query time, user message is embedded → top-K similar chunks retrieved → injected as context. |
| **Rationale** | RAG allows chat to reference uploaded documents without stuffing the entire file into every request. pgvector keeps it in the same PostgreSQL instance — no separate ChromaDB or Pinecone. `IEmbeddingClient` abstraction keeps the embedding model swappable (OpenAI, nomic-embed via Ollama, etc.). |
| **Scope** | Personal chats: user's own document/note chunks. Project chats: project specs and plans chunked for reference. Chunks are user-scoped — a user can only retrieve their own chunks. |
| **Chunk regeneration** | When a document or note is updated, its chunks are deleted and regenerated. Async background job, not blocking the save operation. |
| **Impact** | `DocumentChunk` entity: `UserId`, `SourceType`, `SourceId`, `ChunkIndex`, `Content`, `Embedding vector(1536)`. `IEmbeddingClient`: `EmbedAsync(text) → vector`. Admin configures embedding model (separate from chat model — usually a cheaper dedicated embedding model). Phase 6 (LLM Infra) includes `IEmbeddingClient`. Phase 7 (Chat) wires RAG into general chats. |

---

## D-32: ProjectContextSnapshot Generation Strategy

| Field | Value |
|---|---|
| **Topic** | When and how ProjectContextSnapshot is generated |
| **Date** | 2026-06-02 |
| **Status** | ✅ Settled |
| **Decision** | **Two-track generation: `TemplateContent` (instant, template-rendered, no LLM) + `AiNarrative` (nightly scheduled job, AI-generated summary).** |
| **Rationale** | Regenerating an AI summary on every board mutation is expensive and slow — a busy project board could trigger dozens of LLM calls per hour. Template rendering is instant and free. The AI narrative adds value (readable, contextual summary of the day's work) but only needs to be generated once daily. This gives AI chat the freshest data cheaply, while the nightly narrative gives humans a readable project digest. |
| **TemplateContent** | Rendered on every board mutation. Format: structured markdown listing columns, cards (by `#CardNumber`), blocked cards, recent moves. No LLM call. Injected into project chat context at session start. |
| **AiNarrative** | Generated by nightly scheduled job (configurable time, default: midnight server time). AI reads the `TemplateContent` diff since last narrative and writes a natural-language summary. Stored in `AiNarrative` field. Displayed on project dashboard as a digest. NOT injected into chat context (too narrative, less structured). Null until first nightly run. |
| **Impact** | `ProjectContextSnapshot`: `TemplateContent`, `AiNarrative?`, `TemplateGeneratedAt`, `AiNarrativeGeneratedAt?`. Board mutation handler calls `ProjectContextSnapshotService.RegenerateTemplate()` synchronously (fast). Nightly job calls `ProjectContextSnapshotService.GenerateAiNarrative()` for all active projects. |

---

## D-34: File Storage Architecture

| Field | Value |
|---|---|
| **Topic** | How file attachments are stored — provider choice, key hierarchy, initialization |
| **Date** | 2026-06-09 |
| **Status** | ✅ Settled |
| **Decision** | **`IFileStore` abstraction in Application layer. Two implementations: `LocalFileStore` (bare-metal fallback) and `S3FileStore` (MinIO/AWS S3, recommended). MinIO is the default in `docker-compose.yml` as a co-equal service alongside Postgres. Storage key hierarchy: `{userId}/{sourceType}/{sourceId}/{guid}`.** |
| **Rationale** | **Why `IFileStore` abstraction:** Controllers and services are decoupled from storage topology — swapping Local ↔ S3 is a config change, not a code change. **Why MinIO over Local as recommended path:** MinIO is 100% S3 API compatible, runs as a lightweight Docker container, provides a web console, and scales to cluster mode if needed. LocalFileStore remains for bare-metal dev where Docker isn't available. **Why `InitializeAsync` as default-interface-method:** S3/MinIO needs bucket creation on startup — `InitializeAsync` lets us do this without forcing every implementation to implement it. Default no-op keeps stubs/fakes simple. **Why `{userId}/{sourceType}/{sourceId}/{guid}` key hierarchy:** Single flat namespace is messy when multiple sources (cards, chat, notes) store files. User-prefix isolates data per-user, sourceType prefix (`cards/`, `chat/`, `notes/`) prevents collision between different subsystems, and the random guid prevents enumeration attacks (no sequential IDs, no filename in storage path). |
| **Alternatives considered** | 1. Single flat bucket with sequential IDs (rejected — enumeration risk, harder to debug). 2. No MinIO, just LocalFileStore + S3 config (rejected — MinIO gives us local S3-compatible testing without cloud costs). 3. `IsArchived` flag for attachment meta (rejected — follows `ArchivedAt: DateTime?` pattern, handled by housekeeping service with same retention as cards). |
| **Impact** | `docker-compose.yml` gains a `minio` service (ports 9000/9001, healthcheck, volume). Server `depends_on: minio` with Docker-prefilled S3 env vars. `.env.example` has commented MinIO section. `IFileStore` has `InitializeAsync()` with default no-op — `S3FileStore` creates bucket on startup. `LocalFileStore` unchanged. Storage keys are `{userId}/cards/{cardId}/{guid}` — no user filename in path, no date, no project. Supported content types: images (PNG/JPEG/GIF/WebP), PDF, text, JSON/XML/HTML/CSV, ZIP, Office docs. Default max size: 10 MB. |

---

## D-35: ProjectContextSnapshot Implementation

| Field | Value |
|---|---|
| **Topic** | How the snapshot is rendered, refreshed on mutations, and exposed via API |
| **Date** | 2026-06-22 |
| **Status** | ✅ Settled |
| **Decision** | **Pure deterministic renderer + `IProjectSnapshotRefresher` port injected into all 9 mutation services. `ProjectSnapshotRefresher` EF implementation. `GET /api/projects/{projectId}/ProjectSnapshot` members-only endpoint.** |
| **Renderer** | `ProjectContextSnapshotRenderer` (Application layer, `static`, no LLM, no side effects). Inputs: columns, cards, relationships. Output: structured JSON with column names, card index (`#CardNumber` + title + column + type), blockers list, recent-moved list. JSON serialized with `System.Text.Json`. |
| **Port (`IProjectSnapshotRefresher`)** | `RefreshAsync(projectId)` — reads current board state from repos, renders, upserts snapshot. `GetSnapshotAsync(projectId)` — returns snapshot or null. Injected into: `ProjectService`, `ColumnService`, `CardService`, `ChecklistService`, `CommentService`, `AttachmentService`, `SpecService`, `PlanService`, `CardRelationshipService`. |
| **Infrastructure impl** | `ProjectSnapshotRefresher` reads directly from `HydraForgeDbContext` (columns, active cards, active relationships), renders, upserts via EF. `IProjectContextSnapshotRepository.UpdateAsync` added for the upsert path. |
| **API** | `GET /api/projects/{projectId}/ProjectSnapshot` — membership check via `IProjectMemberRepository`, returns `ProjectSnapshotResponse` (id, projectId, templateContent, templateGeneratedAt, aiNarrative, aiNarrativeGeneratedAt). |
| **Repository additions** | `IProjectContextSnapshotRepository.UpdateAsync` (upsert existing snapshot), `ICardRelationshipRepository.ListActiveByProjectAsync` (active relationships only, used by renderer). |
| **Why upsert not replace** | Snapshot row is created once per project on first mutation and updated thereafter. Avoids race conditions from concurrent mutation services trying to create duplicates. Unique constraint on `ProjectId` enforces singleton-per-project at DB level. |
| **Alternatives considered** | 1. Snapshot as a cached view materialised in DB on mutation (rejected — same upsert logic needed, more complex). 2. Snapshot rendered lazily on first chat read (rejected — latency on first chat open; D-32 intent is instant template rendering). |
| **Impact** | All board mutations (cards, columns, checklists, comments, attachments, specs, plans, relationships, project edits) now trigger snapshot refresh synchronously before returning. Snapshot is ready before the client receives the mutation response. |


---

## D-36: Board Event Publishing — IProjectBoardEventPublisher Port + SignalR Broadcast

| Field | Value |
|---|---|
| **Topic** | How board mutations are broadcast to all connected project members in real-time |
| **Date** | 2026-06-22 |
| **Status** | ✅ Settled |
| **Decision** | **IProjectBoardEventPublisher port in Application layer. SignalRProjectBoardEventPublisher impl in Infrastructure. Board mutations call publisher after DB commit. BoardHub (/hubs/board) fans out to project SignalR groups.** |
| **Port** | IProjectBoardEventPublisher.PublishAsync(ProjectBoardEventEnvelope) — Application layer, injected into mutation services |
| **Envelope model** | ProjectBoardEventEnvelope: EventId, ProjectId, BoardEntityType (Project/Column/Card/ChecklistItem/Comment/Attachment/Spec/Plan/CardRelationship), BoardAction (Created/Updated/Moved/Deleted/Archived/Restored/Assigned/Unassigned), Version, OccurredAt, Payload |
| **Hub route** | /hubs/board |
| **Client method** | IBoardHub.OnBoardEvent(ProjectBoardEventEnvelope) — strongly typed hub client interface |
| **Grouping** | BoardHub.ProjectGroup(projectId) = project-{projectId} — clients join per-project groups |
| **Auth** | JoinProject(projectId) checks membership (or admin role) before adding to group |
| **Implementation** | SignalRProjectBoardEventPublisher uses IHubContext<BoardHub, IBoardHub> to fan out to the project group after mutation services commit to DB |
| **DI** | RealtimeServiceCollectionExtensions.AddRealtimeServices() in Infrastructure registers IProjectBoardEventPublisher as SignalRProjectBoardEventPublisher |
| **Alternatives considered** | 1. Each service publishes directly to SignalR internally (rejected — couples services to SignalR, breaks Clean Architecture). 2. Domain events with a separate subscriber (rejected — over-engineered for current scale). |
| **Impact** | All board mutations (cards, columns, checklists, comments, attachments, specs, plans, relationships) call IProjectBoardEventPublisher.PublishAsync after successful commit. Clients receive typed OnBoardEvent with the full envelope. |


---

## D-37: vue-draggable-plus Removal — Plain v-for for Board Lists

| Field | Value |
|---|---|
| **Topic** | Drag-and-drop library for board columns and cards in the Web UI |
| **Date** | 2026-06-23 |
| **Status** | ✅ Settled |
| **Decision** | **Remove `vue-draggable-plus` (SortableJS wrapper). Use plain `v-for` for column and card lists. Re-implement with native HTML5 drag-and-drop later.** |
| **Rationale** | `vue-draggable-plus` is SSR-incompatible with Nuxt 4. SortableJS requires browser APIs (DOM measurements, event listeners) that fail during SSR. Wrapping in `ClientOnly` causes hydration mismatches because the server-rendered static list differs from the client-rendered draggable list. The `v-model` reactivity pattern also causes hydration warnings. Plain `v-for` is stable, SSR-safe, and sufficient for the current board view. Native HTML5 drag-and-drop (no library dependency) is planned for re-implementation in a follow-up task. |
| **Alternatives considered** | 1. `@vueuse/core` `useDraggable` (rejected — free positioning, not list reordering). 2. `vue-smooth-dnd` (rejected — unmaintained, no Nuxt 4 support). 3. `sortablejs` directly with manual Vue integration (rejected — same SSR issues, more boilerplate). |
| **Impact** | Board columns and cards render as static `v-for` lists. Card moves between columns are not yet implemented via drag-and-drop — they work via curl/API only. Column reorder also not yet implemented via drag. Both will be re-added with native HTML5 drag-and-drop. |

---

## D-38: Nuxt UI v4 UModal Usage Patterns

| Field | Value |
|---|---|
| **Topic** | How to use UModal in Nuxt UI v4 for dialogs and overlays |
| **Date** | 2026-06-24 |
| **Status** | ✅ Settled |
| **Decision** | **UModal uses `v-model:open` for two-way binding. Content goes in named slots (`#body`, `#header`, `#footer`). The default slot is a `DialogTrigger`, not modal content. No `UOverlay` component exists — the overlay is built into `UModal` via the `overlay` prop (defaults to `true`).** |
| **Rationale** | Nuxt UI v4's `UModal` is built on `reka-ui` (Radix Vue) `DialogRoot`. The default slot renders as a `DialogTrigger` with `as-child` — putting content directly inside `<UModal>` makes it a trigger button rendered in-page, not an overlay modal. Named slots (`#body`, `#header`, `#footer`) are the correct way to provide modal content. The `overlay` prop (default `true`) controls the backdrop. `dismissible` (default `true`) controls ESC/click-outside close. `portal` (default `true`) renders content at the document root via `DialogPortal`. |
| **Alternatives considered** | 1. `UOverlay` component (rejected — does not exist in Nuxt UI v4). 2. Custom overlay with Tailwind fixed positioning (rejected — `UModal` already handles portal, backdrop, focus trap, and accessibility). |
| **Impact** | All modals in the Web UI must use `v-model:open` + named slots. The `close` prop enables the built-in X button. `dismissible` controls ESC/backdrop behavior. `transition` controls open/close animation. Component tests using `@vue/test-utils` `mount` cannot assert on portal-rendered content — use TypeScript compilation and lint to verify prop contracts instead. |

---

## D-39: USelect Clearable Workaround in Nuxt UI v4

| Field | Value |
|---|---|
| **Topic** | How to provide a clear/reset button for USelect since v4 has no `clearable` prop |
| **Date** | 2026-06-24 |
| **Status** | ✅ Settled |
| **Decision** | **USelect in Nuxt UI v4 has no `clearable` prop. Wrap the select in a `relative` container with an absolute-positioned ghost `UButton` (X icon, `right-6` to avoid overlapping the chevron) that sets the model to `undefined`. Only show the button when the model has a value.** |
| **Rationale** | The `clearable` prop exists in some UI libraries but was not included in Nuxt UI v4's `USelect` component (built on `reka-ui` `SelectRoot`). The component's props list (lines 23-60 of `Select.vue`) does not include `clearable`. The trailing slot renders the chevron icon only. A custom clear button is the simplest workaround without forking the component. |
| **Alternatives considered** | 1. Fork `USelect` to add `clearable` (rejected — maintenance burden). 2. Use a different select component (rejected — `USelect` is the standard Nuxt UI v4 select). 3. Require users to close and reopen the modal to clear (rejected — poor UX). |
| **Impact** | Any `USelect` that needs a clear/reset option must use the wrapper pattern. The button uses `v-if="model"` to only appear when a value is selected. `right-6` positioning avoids overlapping the built-in chevron-down icon. |

---

## D-40: `useApi()` Throws — Call Sites Must try/catch, Never Destructure `{ error }` From an Unguarded Call

| Field | Value |
|---|---|
| **Topic** | Why several "Failed to X" error toasts in the Web UI never fired on real API failures |
| **Date** | 2026-06-25 |
| **Status** | ✅ Settled |
| **Decision** | **`useApi()`'s `onResponse` middleware (`app/composables/useApi.ts`) throws `ApiError` for any non-2xx, non-401 response — it never resolves with a populated `error` field. Every call site must wrap `await api.X(...)` in try/catch. Writing `const { error } = await api.X(...); if (error) { ... }` with no surrounding try/catch is a bug: the `await` throws before that line's destructuring runs, the function's promise rejects unhandled, and the `else`/`if (error)` branch (and its toast) is dead code.** |
| **Rationale** | `CardModal.vue`'s `confirmArchive`/`handleRestore`, `BoardCard.vue`'s `confirmArchive`/`handleRestore`, and `CardCreateModal.vue`'s `handleCreate` were all written assuming a Result-style resolve (`{ data, error }`) — the pattern openapi-fetch uses by default. This project's `useApi()` deliberately overrides that with a throw-based middleware (so `ApiError` carries `status`/`code`/`detail`/`correlationId` from the RFC 7807 body) — documented in `CLAUDE.md`'s "Error types" bullet, but not enforced anywhere, so five call sites silently regressed to "click Archive, nothing happens, no toast, no error" on real failures. Found and fixed in `2026-06-25-phase-3-card-modal-hardening.md` Task 1. `CardDescription.vue` happened to handle this correctly already, because its `if (error) throw error` line is inside a `try { ... } catch (e) { saveError.value = e.message }` — the outer `await`'s rejection lands in that `catch` regardless of the dead `if` line. |
| **Alternatives considered** | 1. Change `useApi()` to resolve with `{ error }` instead of throwing, matching openapi-fetch's default and every call site's assumption (rejected — `CLAUDE.md` already documents throw-based error handling as intentional, and `ApiError`'s richer shape, e.g. `correlationId`, is more useful caught at the call site than buried in a generic `error` field). 2. Add a global Vue error handler to catch unhandled rejections from template event handlers and toast generically (rejected — loses the specific "Failed to archive card" vs. "Failed to restore card" messaging the manual test matrix expects per action). |
| **Impact** | Any new component that calls `useApi()` and wants to show a failure toast MUST wrap the call in try/catch — destructuring `{ error }` without one will compile and pass code review but silently does nothing on a real failure. Code review for new `useApi()` call sites should specifically check for this. |

---

## D-41: Card Detail Panel Version Ownership Lives on `CardModal`, Not on Each Panel

| Field | Value |
|---|---|
| **Topic** | Where the optimistic-concurrency `version` for a `Card` is cached while its detail modal is open across multiple editable panels |
| **Date** | 2026-06-25 |
| **Status** | ✅ Settled |
| **Decision** | **`CardModal.vue` owns the single `card` ref (and therefore `card.value.version`) for the lifetime of the open modal. Every panel below it that mutates a `Card` field (`CardDescription`, and `CardMetadata` once Plan 4's Task 16 lands) reads `props.card.version` at call time and emits `'update:card': [CardResponse]` with the server's response on success — it never keeps its own cached copy of `version`.** |
| **Rationale** | `CardDescription.vue` originally seeded a local `currentVersion` ref from `props.card.version` once at mount and only updated it from its own save responses. Plan 4 adds a second panel (`CardMetadata`'s type/due-date/assignee editor) that also calls `PUT Cards/{cardId}` with a version. Two independently-cached copies of the same token desync the moment a user edits the description and then the type (or vice versa) in one modal session — the second save sends a stale version and gets a spurious `409 CARD_CONCURRENCY_MISMATCH` even though no other user or tab touched the card. Centralizing on `CardModal.vue`'s `card` ref, with children emitting `update:card` instead of caching, makes this structurally impossible: there is exactly one `version` in memory per open modal. |
| **Alternatives considered** | 1. Have each panel re-fetch the full card before every save (rejected — extra round-trip per save, and still racy between the fetch and the save). 2. Pass `version` down as a separate prop, updated by the parent on every child's success (rejected — `update:card` replacing the whole `card.value` is simpler and also keeps `description`/`assignees`/etc. in sync for sibling panels, e.g. the metadata sidebar reflecting a description-triggered `updatedAt` change). |
| **Impact** | Implemented in `2026-06-25-phase-3-card-modal-hardening.md` Task 2 for `CardDescription`; Plan 4's Task 16 (rewritten in `2026-06-23-phase-3-plan-4-card-modal-panels.md`) wires `CardMetadata` onto the same `update:card` contract. Any future card-detail panel that edits a `Card` field (not a sub-resource like checklist items or comments, which have their own concurrency tokens) must follow this pattern. |

---

## D-42: Playwright Adopted for End-to-End Testing

| Field | Value |
|---|---|
| **Topic** | Which framework drives automated end-to-end tests against the real Web UI + API + Postgres stack |
| **Date** | 2026-06-25 |
| **Status** | ✅ Settled |
| **Decision** | **`@playwright/test` (MIT license), paired with the project's existing `@nuxt/test-utils` devDependency family. No E2E framework existed before this — `docs/superpowers/manual-validation/*.md` matrices were 100% manual checkbox lists.** |
| **Rationale** | Open-source-only constraint ruled out paid tiers, but Playwright's free local tier already covers everything needed here: multi-browser (matters less today, useful later), first-class network interception/waiting (needed to assert on the description editor's debounced auto-save and the two-tab version-conflict scenario), and a free trace viewer for diagnosing flaky CI runs without a paid dashboard. Cypress's strongest free-tier differentiator (component testing DX) is not a gap here — Vitest + `@nuxt/test-utils` already covers component tests; what was missing was real-browser, real-stack flows, which is Playwright's core strength. `@nuxt/test-utils` is already a devDependency, so no new framework family was introduced, only a sibling package. |
| **Alternatives considered** | 1. Cypress (rejected — Chromium-first, WebKit support experimental; richer replay/debugging is behind the paid Cypress Cloud tier, while Playwright's trace viewer is free). 2. `@nuxt/test-utils/e2e`'s own `setup()` helper alone, without Playwright (rejected — `setup()` boots an isolated Nuxt instance per test file; this project's E2E goal is to exercise the real running API + Postgres + MinIO stack, which `setup()` does not model). |
| **Impact** | `src/web-ui/e2e/` holds Playwright specs; `playwright.config.ts` lives alongside `vitest.config.ts`. New end-to-end coverage goes here, not into Vitest component tests. CI runs E2E specs in a dedicated `pull_request`-only job (slower than unit tests) per `2026-06-25-phase-3-e2e-testing-foundation.md` Task 6. There is no per-test database reset yet — specs seed their own randomly-suffixed data via the API and run serially (`workers: 1`) until that exists. |

---

## D-43: Unify Shared Filter State and Logic via Composables

| Field | Value |
|---|---|
| **Topic** | How to handle filter state and logic shared between desktop and mobile views in the Web UI |
| **Date** | 2026-06-25 |
| **Status** | ✅ Settled |
| **Decision** | **Extract shared filter state and logic into a dedicated composable (`useBoardFilters.ts`) that reads/writes the Pinia store directly. Both `BoardFilterBar.vue` (desktop) and `BoardMobileList.vue` (mobile) consume this composable instead of duplicating state or using watchers to sync local refs.** |
| **Rationale** | Desktop and mobile views often render different components but need to share the same global filter state (search, type, assignee, archived, hide empty). Originally, `BoardFilterBar.vue` used `defineModel` connected to the store, while `BoardMobileList.vue` maintained its own duplicate local refs (`mobileSearch`, `mobileType`, etc.) and used watchers to sync them with the store. This duplication caused significant issues: 1. Double-fetching (both the store watcher and local watcher triggered API calls on change). 2. Synchronization lag and race conditions. 3. Multi-statement inline event handlers that introduced bugs (like the `"null"` string → `NaN` conversion bug on the per-column type filter). Unifying on a single composable that directly wraps the Pinia store's `boardFilters` with reactive computed getters/setters ensures identical behavior, zero state duplication, and a cleaner API. |
| **Alternatives considered** | 1. Keep duplicate state and fix the sync watchers (rejected — high maintenance burden, prone to regression, still suffers from double-fetching). 2. Pass filters as props and emit events from both components (rejected — introduces immense prop-drilling and event-bubbling boilerplate across the board page hierarchy). |
| **Impact** | Created `app/composables/useBoardFilters.ts`. Refactored `BoardFilterBar.vue` and `BoardMobileList.vue` to use it, deleting 4 duplicate refs and 5 watchers. Updated `BoardFilterBar.test.ts` to assert against the Pinia store instead of emitted events. All future shared filter state or complex multi-view logic must use this composable pattern. |

---

## D-44: Doc Model — Spec/Plan Redesign (DocType, Multi-Plan, PlanStatus, Idea→Goal)

| Field | Value |
|---|---|
| **Topic** | How Spec and Plan documents relate to card types; plan lifecycle states; Idea-to-Goal lineage |
| **Date** | 2026-06-29 |
| **Status** | ✅ Settled |
| **Decision** | **Specs are typed by card: Goal→Specification, Idea→Concept, Issue→Report. Each Spec owns 0..N Plans (via `Plan.SpecId`). Task and Issue cards may also carry Plans directly (`Plan.CardId` only, no Spec). Plans have a `PlanStatus` lifecycle: `Pending → Active → Done` (read-only when Done; reactivation required to edit). Plans carry a `Position` field for ordering. Creating a Goal from an Idea is a UX action that optionally wires a `SpawnedFrom` CardRelationship (Source=Goal, Target=Idea).** |
| **Rationale** | The previous model assumed 1 Spec and 1 Plan per Card, which did not match actual delivery workflow: a single spec (milestone) naturally decomposes into multiple sequential plans (tasks), each handed to an agent or developer one at a time. Idea cards need a freeform exploration document (Concept) but no execution plans — they are still in the thinking phase; when ready, they become Goals. Issue cards need a problem description (Report) plus one or more mitigation/research plans. Task cards are lightweight — no formal spec needed, but structured plans help for non-trivial tasks. The DocType discriminator keeps a single `Spec` entity while allowing the UI to show domain-appropriate labels (Specification / Concept / Report), keeping the system approachable for non-developer use cases (finance, education, libraries, etc.). PlanStatus makes agent orchestration state machine explicit: an orchestrator picks up a Pending plan, sets it Active, marks it Done when the PR merges. Done plans are read-only to prevent accidental edits after delivery — reactivation requires intent. |
| **Alternatives considered** | 1. Separate `Concept` and `Report` entities instead of DocType discriminator (rejected — same structure, needless schema duplication; a discriminator field costs one column and zero extra tables). 2. Idea cards can have Plans (rejected — Ideas are conceptual; premature planning before a Goal is created creates orphaned work). 3. `Relates` CardRelationship for Idea→Goal lineage (considered — sufficient for MVP since `Relates` already exists; `SpawnedFrom` added instead because the direction and intent are specific enough to warrant a named type, enabling future queries like "all Goals spawned from this Idea"). 4. Plan status tracked externally (e.g. by branch existence or PR state) (rejected — external state is ephemeral and not domain-owned; HydraForge should be the source of truth for work state). |
| **Impact** | Schema: add `Spec.DocType` (DocType enum), `Plan.Status` (PlanStatus enum, default Pending), `Plan.Position` (int). Add `SpawnedFrom = 4` to `RelationshipType`. Card→doc rules: Goal card creates a Specification; Idea card creates a Concept; Issue card creates a Report; Task card has no primary doc. UI: Docs tab label adapts to card type. Plan list replaces the single-plan panel; each plan shows status badge. **Superseded on the "Reactivate" mechanism only — see D-45**: the dedicated Activate/Complete/Reactivate actions described here were consolidated into a single status dropdown before this Impact line was updated. New migration required. |

---

## D-45: Plan Status Transitions — Single `SetStatus` Endpoint Replaces Activate/Complete/Reactivate

| Field | Value |
|---|---|
| **Topic** | How a Plan's `PlanStatus` (Pending/Active/Done) is changed from the UI and API, superseding the dedicated-action mechanism described in D-44's Impact line |
| **Date** | 2026-07-07 (documented retroactively — the change shipped earlier in this branch's history, undocumented) |
| **Status** | ✅ Settled |
| **Decision** | **One `PATCH` endpoint (`SetStatus` on `PlansController`, backed by `PlanService.SetStatusAsync` → `Plan.SetStatus(PlanStatus)`) accepts any target status and applies it unconditionally — there is no enforced `Pending → Active → Done` ordering and no separate Activate/Complete/Reactivate/Reopen actions. The UI (`CardPlan.vue`) shows the current status as a clickable badge; clicking opens a dropdown listing the other two statuses, and picking one calls `SetStatus` directly.** |
| **Rationale** | The original three-action design (Activate/Complete/Reactivate) mapped cleanly to a strict linear lifecycle but added UI and API surface for what is, in practice, a single "change this plan's status" operation — and real usage needed to jump backward (Done → Pending) without a dedicated "un-complete" action. Collapsing to one endpoint plus one dropdown removed three controller actions and three button states for one select control, with no loss of capability. |
| **Alternatives considered** | 1. Keep D-44's three dedicated actions and add a fourth for arbitrary reopen (rejected — grows the action surface further for a state machine that turned out not to need enforced ordering). 2. Enforce valid transitions server-side (e.g. reject Done→Pending without going through Active) (rejected — no product requirement surfaced for this, and it would need a real reason to justify the extra validation code and error states). |
| **Impact** | `Plan.Reopen()` (added under D-44, sets status back to `Pending`) is now dead code — nothing calls it; `SetStatus` bypasses it entirely. Confirm before deleting it, or fold its intent into `SetStatus` if a distinct "reopen" semantic (vs. arbitrary status assignment) still turns out to matter. Any future plan/task lifecycle work should default to this single-setter pattern rather than reintroducing per-transition actions unless a concrete need for enforced ordering appears. |

---

## D-46: Blocked Card Move — No UI Override, Toast-and-Stop Is Final

| Field | Value |
|---|---|
| **Topic** | Whether the Web UI should expose a way to force a card move past a `409 Conflict` blocked-move rejection, given the API supports `confirmBlockedMove=true` |
| **Date** | 2026-07-07 |
| **Status** | ✅ Settled |
| **Decision** | **The Web UI never sends `confirmBlockedMove=true`. On `409`, `useCardMove.ts` rolls back the optimistic move and toasts "Cannot move blocked card" — full stop, no confirm-and-retry modal.** |
| **Rationale** | Resolving a blocked card means resolving the actual blocking dependency (finish/unblock the card it's waiting on), not force-moving past the block. `confirmBlockedMove` exists as an API-level capability — any client could use it — but the Web UI intentionally chooses not to expose an override, keeping the blocked-move rule meaningful rather than a dismissible warning. |
| **Alternatives considered** | 1. Build a confirm-and-retry modal (Plan 6's original Task 23 design) that resends the move with `confirmBlockedMove=true` after user confirmation (rejected — the whole point of a blocked-move rule is undermined if the UI trains users to just click through it). |
| **Impact** | Plan 6 Task 23's "move warning modal" sub-scope is dropped — only the `BoardCard.vue` visual blocked indicator (lock icon) remains from that task. Do not read CLAUDE.md's `confirmBlockedMove=true` API-contract line as a requirement to build UI for it. |


---

## D-47: Keyboard Navigation — Roving-Focus Composable Split

| Field | Value |
|---|---|
| **Topic** | How board keyboard navigation (hjkl, selection highlight, roving tabindex) is structured to stay reusable as more sections of the app gain keyboard nav |
| **Date** | 2026-07-19 |
| **Status** | ✅ Settled |
| **Decision** | **Keyboard-nav selection state and dispatch live in `app/composables/keyboard/`: `useRovingFocus.ts` (generic single-axis index/selection primitive, no DOM opinion) and `useBoardKeyboardNav.ts` (board-specific composable that wires two `useRovingFocus` instances — columns axis, cards-within-column axis — to `Board`-scope `keyboard.register` calls). `useKeyboard.ts` gained an `enabled?: () => boolean` predicate per shortcut, replacing a manual `if (isModalOpen()) return` guard duplicated in every Board handler. `useBoardKeyboardNav` exposes `activate()`/`deactivate()` rather than self-managing `onMounted`/`onBeforeUnmount` — Vue no-ops `onMounted` when there is no active component instance on the call stack, so the composable cannot register its own lifecycle hook; the caller (`board.vue`) invokes `activate()`/`deactivate()` from its own `onMounted`/`onBeforeUnmount`, the same way it already drives `realtime.connect`/`disconnect`.** |
| **Rationale** | The five keyboard-nav commits that shipped hjkl navigation, roving tabindex, and card move/reorder left `board.vue` holding ~270 lines of inline selection state, sync handlers, and shortcut registrations — the same shape of duplication D-43 already flagged for filter state. Along the way, a visual bug surfaced: `BoardView`'s own `tabindex=0` container carried its own `focus:ring` class, a second highlight mechanism entirely disconnected from the `selectedColumnIndex`/`selectedCardId` ring that actually reflects keyboard selection. Tab-ing into the board painted a ring shaped like the whole column row, which read as misaligned next to the real per-column/per-card ring. Splitting into a generic primitive + a board-specific wrapper fixes the duplication (one composable owns selection state, `syncToCard`/`syncToColumn` are its only write path) and gives the next keyboard-nav consumer (Projects page, later chat) a primitive to plug into directly instead of re-inlining the pattern. |
| **Alternatives considered** | 1. Single board-specific composable only, no generic primitive (rejected — YAGNI-adjacent but the columns/cards split is genuinely two instances of the same index logic; extracting it costs nothing and is exactly what a flat-list nav elsewhere would need). 2. True per-item ARIA roving-tabindex (tabindex physically moving between individual cards/columns as selection changes) (deferred, not rejected — bigger a11y rework, belongs to the still-open Task 25 ARIA pass, not this fix). 3. A scope-priority stack in `useKeyboard.ts` instead of a per-shortcut `enabled` predicate (rejected — only two scopes exist today (`Board`, `Card`); a predicate is the minimal mechanism that removes the duplicated guard without adding a new registry concept). 4. Composable self-registers via its own internal `onMounted`/`onBeforeUnmount` (tried first, caught during plan-writing before implementation — `onMounted` with no active component instance on the stack is a silent no-op in Vue, so shortcuts would never register; corrected to caller-driven `activate()`/`deactivate()`). |
| **Impact** | New folder `app/composables/keyboard/` holds `useKeyboard.ts` (moved), `useRovingFocus.ts`, `useBoardKeyboardNav.ts`. `board.vue` calls `useBoardKeyboardNav` once and drives `nav.activate()`/`nav.deactivate()` from its own lifecycle hooks instead of registering 9+ shortcuts inline. `BoardView.vue` dropped its own focus ring in favor of the existing selection ring (`aria-label` added for screen readers). Any future keyboard-nav surface should start from `useRovingFocus` rather than re-inlining index/wraparound logic. |

---

## D-48: Phase 4 TUI Infrastructure — API Client, Realtime Client, Credential Storage

| Field | Value |
|---|---|
| **Topic** | Resolving the three pre-phase decisions flagged in `docs/functional-spec.md` §25 Phase 4: how the TUI calls the API, how it consumes SignalR, and where it stores the JWT between runs |
| **Date** | 2026-07-24 |
| **Status** | ✅ Settled |
| **Decision** | **1. API client:** NSwag-generated typed C# client from `/openapi/v1.json`, regenerated as a build step — mirrors the Web UI's openapi-typescript + `routes.ts` convention (CLAUDE.md "Web UI API routes") on the .NET side. **2. Realtime client:** `Microsoft.AspNetCore.SignalR.Client` (official Microsoft package) for `BoardHub`/`PresenceHub` consumption. **3. Credential storage:** plain JSON file at `~/.config/hydraforge/config.json` (Linux/macOS) / `%APPDATA%\hydraforge\config.json` (Windows), written with owner-only permissions (`0600` via `File.SetUnixFileMode` on POSIX; default user-profile ACL on Windows) immediately after creation. No OS keychain integration for Phase 4 MVP. |
| **Rationale** | NSwag keeps the TUI's API surface generated from the same source of truth as the server's OpenAPI spec, avoiding hand-drift the same way codegen already prevents it on the Web UI side; MIT licensed, no alternative seriously considered beyond Kiota (functionally equivalent, NSwag has a longer track record against ASP.NET Core minimal-API output). `Microsoft.AspNetCore.SignalR.Client` is the only real option — official, MIT, already the hub implementation's counterpart package. Credential storage is the one with real trade-offs: `docs/scope.md`'s TUI persona works on a **remote server over VPN/SSH**, where a desktop OS keychain (macOS Keychain, Windows Credential Manager, Linux Secret Service/libsecret) is frequently unavailable — headless SSH sessions have no Secret Service bus running. A keychain-first design would still need a plain-file fallback for exactly this primary use case, so shipping only the fallback avoids building a per-OS abstraction (three shell-out implementations, one per OS) that a large fraction of real usage would fall through anyway. This is the same shape `gh`, `docker login`, and `aws configure` default to — an owner-only-permissioned file, with OS keyring as an optional upgrade some of them layer on later, not a Day 1 requirement. Risk is further bounded by `Jwt:AccessTokenMinutes = 60` in `appsettings.json` — a stolen token file has a one-hour blast radius regardless of storage mechanism. |
| **Alternatives considered** | 1. Hand-rolled `HttpClient` + `System.Text.Json` calls instead of NSwag (rejected — reintroduces the exact hand-drift risk codegen exists to prevent). 2. Kiota instead of NSwag (viable alternative, not rejected on merits — NSwag picked for track record, revisit only if NSwag friction appears during implementation). 3. OS-native keychain integration for JWT storage (rejected for MVP — no first-party cross-platform .NET library; per-OS shell-outs are meaningful surface to carry before a single TUI screen exists; primary usage mode largely can't use it anyway per rationale above). 4. `Microsoft.AspNetCore.DataProtection` to encrypt the token file at rest instead of relying on file permissions alone (deferred, not rejected — the server already uses Data Protection for its own keys; revisit if a config file with permissions alone is judged insufficient after real usage, but adds a machine-bound-key management question — e.g. what happens on key rotation — that doesn't need solving before Phase 4 ships). |
| **Impact** | Phase 4 planning can proceed on this basis — see `docs/functional-spec.md` §25 Phase 4. NSwag codegen needs wiring into `HydraForge.Tui`'s build (MSBuild target or a checked-in generation script, exact mechanism decided at implementation time). Config file read/write/permission-setting belongs in a small config-store helper inside `HydraForge.Tui` — it is a TUI-process concern, not something that belongs in Domain/Application/Infrastructure per the Clean Architecture dependency direction. Revisit OS-keychain support only if desktop (non-remote) TUI usage turns out to matter more than the VPN/SSH case this decision assumes. **Credential storage location superseded by D-49** — the file lives in the repo/checkout, not the user's home directory. |

## D-49: Credential Storage Location — Repo-Relative `.hydraforge/config.json`, Not `~/.config`

| Field | Value |
|---|---|
| **Topic** | Amends D-48 clause 3 only (credential storage). Everything else in D-48 — NSwag, SignalR.Client, plain-file-with-0600, no OS keychain — stands. |
| **Date** | 2026-07-24 |
| **Status** | ✅ Settled |
| **Decision** | `ConfigStore`'s default path is `.hydraforge/config.json` at the repo root (found by walking up from the running assembly's directory looking for `HydraForge.slnx`, falling back to the assembly directory if no repo root is found — e.g. a published single-file exe run outside a checkout). **Not** `~/.config/hydraforge/` (Linux/macOS) or `%APPDATA%\hydraforge\` (Windows) as D-48 originally specified. Already `.gitignore`d, still written with owner-only permissions (`0600` POSIX). |
| **Rationale** | Explicit user preference: config/state belongs inside the project directory, not scattered into the home directory — same principle already applied elsewhere in this codebase (e.g. `.env`-driven local config). D-48's home-dir choice was reasoned from a remote-SSH-persona angle, but that angle doesn't actually require the home directory — an owner-only-permissioned file inside the checkout satisfies the same "no OS keychain available over SSH" constraint just as well, since the checkout itself is already on that machine. |
| **Alternatives considered** | 1. Keep `~/.config/hydraforge/` per original D-48 (rejected — user override, and the security rationale doesn't actually depend on home-dir vs. project-dir). 2. `AppContext.BaseDirectory` (next to the built DLL) directly, no repo-root search (rejected — lands inside `bin/Debug/net10.0/`, gets wiped on `dotnet clean` and differs per build configuration, meaning the user re-logs in on every rebuild). |
| **Impact** | `ConfigStore()` default ctor changed to resolve the repo root via `HydraForge.slnx` and store there. One real trade-off to be aware of: two separate checkouts/worktrees of this repo no longer share a login — each gets its own `.hydraforge/config.json` and its own login prompt. Acceptable given the explicit preference. |

## D-50: OpenAPI Enum Schemas Corrected to Match Runtime Serialization

| Field | Value |
|---|---|
| **Topic** | `/openapi/v1.json` documented every C# enum as `type: integer`, but `Program.cs` registers a global `JsonStringEnumConverter` — the server has always serialized enums as their string name (e.g. `"myRole":"Owner"`). NSwag trusted the (wrong) schema and generated `int MyRole` on the TUI's DTOs; the real string response then failed strict deserialization, silently killing the Task 6 project list (0 projects shown, error swallowed with no panel to surface it). |
| **Date** | 2026-07-24 |
| **Status** | ✅ Settled |
| **Decision** | Added `HydraForge.Server.OpenApi.EnumSchemaTransformer` (`IOpenApiSchemaTransformer`), registered via `builder.Services.AddOpenApi(options => options.AddSchemaTransformer<EnumSchemaTransformer>())` in `Program.cs`. For any enum-typed schema it rewrites `type` to `string`, sets `enum` to the C# member names, and fixes up any stale numeric `default`. Confirmed via `/openapi/v1.json`: all 7 affected enums (`CardType`, `ColumnTemplate`, `DocType`, `MemberRole`, `PlanStatus`, `ProjectSortField`, `RelationshipType`) now correctly show `type: string`. |
| **Rationale** | The bug is in the schema generator, not the runtime — `AddOpenApi()` has no built-in awareness of custom `JsonSerializerOptions` converters. Fixing it here (once, server-side) is correct for every current and future NSwag/openapi-typescript consumer, versus patching each generated client's DTOs by hand per field, which is exactly the hand-drift risk D-48 chose codegen to avoid in the first place. This is also the same root cause behind the Web UI's pre-existing "openapi-typescript enum typing" workaround (CLAUDE.md) — that workaround stays defensively in place but is no longer strictly necessary once the Web UI regenerates its types. |
| **Alternatives considered** | 1. Patch each generated TUI DTO field to the correct type by hand after every NSwag regen (rejected — codegen is regenerated on every build, hand-patches would be silently overwritten). 2. Change `ProjectListScreen.cs` to parse the raw string manually, bypassing the typed field (rejected — treats a schema bug as a client-side problem, doesn't fix the other 6 enums or future screens touching them). |
| **Impact** | Zero runtime change — `JsonStringEnumConverter` already controlled the actual wire format; this only corrects what the schema *documents*. Web UI unaffected: its `api.d.ts` is generated by a manual `pnpm generate:api-types` script, not run automatically, so nothing regenerates unless someone runs it — and when they do, it becomes more accurate, not less. TUI's NSwag client now generates real C# enums (`MemberRole`, `ColumnTemplate`, etc.) instead of raw `int`, which is what let `ProjectListScreen.cs` drop its hand-rolled int↔string mapping entirely.

---

## D-51: Web UI NotificationHub Connection Deferred

| Field | Value |
|---|---|
| **Topic** | Why the Web UI notification bell shows a static count instead of real-time updates |
| **Date** | 2026-07-25 |
| **Status** | 🔜 Future |
| **Decision** | **`useNotifications.ts` has an `onNotificationReceived` handler but no live SignalR `NotificationHub` connection. The bell count is fetched via REST (`GET /api/notifications/unread-count`) and periodically refreshed — it is not pushed in real-time.** |
| **Rationale** | `NotificationHub` in the server is fully implemented and used by the TUI (`SignalRConnectionManager.ConnectAsync` wires the NotificationHub alongside BoardHub and PresenceHub). The Web UI's `useNotifications.ts` composable was scaffolded with the `onNotificationReceived` handler placeholder but the SignalR connection establishment (`new HubConnectionBuilder().WithUrl(...)...StartAsync()`) was not wired into the composable or the app startup. Completing this requires `HubConnection` lifecycle management in the composable (connect on app init, reconnect logic, cleanup on unmount) and is a Phase 5 follow-up task. |
| **Alternatives considered** | 1. Keep Web UI polling-only indefinitely (rejected — Phase 5 follow-up exists specifically to complete real-time notifications in Web UI). 2. Use `EventSource` instead of SignalR (rejected — `NotificationHub` uses SignalR's bidirectional channel, not server-sent events). |
| **Impact** | Web UI notification bell does not update in real-time. TUI does receive real-time updates via `SignalRConnectionManager`. The TUI implementation is the reference for what the Web UI will eventually replicate. |

## D-52: ntfy Client Implementation

| Field | Value |
|---|---|
| **Topic** | ntfy push client — INtfyClient port, NtfyClient impl, NtfyOptions, SystemSettings.NtfyServerUrl, docker-compose service |
| **Date** | 2026-07-25, corrected 2026-07-27 |
| **Status** | ✅ Settled |
| **Decision** | `INtfyClient` port in Application layer; `NtfyClient` in Infrastructure; `NtfyOptions` for priority/defaults; `SystemSettings.NtfyServerUrl` field stores admin-configured URL; URL wired via `CachedSettingsProvider` (Task 10 — not yet built, so `NtfyClient`'s `serverUrl` ctor param currently resolves to `null` through the DI container with no explicit factory needed); docker-compose `ntfy` service with `profiles: ["notifications"]`; `.env.example` has `NTFY_BASE_URL`. |
| **Rationale** | Decouples push logic from Application layer — `NotificationService` holds `INtfyClient?` and calls `PublishAsync` after DB write. Null client = graceful no-op. Makes ntfy fully opt-in: no URL configured = no push, in-app SignalR still works. Best-effort: HTTP failures are caught and swallowed, never propagate. |
| **Topic naming** | Per-user topic: `hydraforge-{userId}` — deterministic, no per-notification topic creation. |
| **Alternatives considered** | 1. Topic-per-notification (rejected — creates topics dynamically on ntfy server, harder to manage ACLs). 2. Throw on ntfy failure (rejected — push is best-effort, server/ntfy down must not crash board). |
| **Impact** | `NotificationService` now optionally pushes to ntfy after every `NotifyAsync` call. `AddSystemSettingsFields` EF migration adds `NtfyServerUrl`, `SearXngUrl`, `BrandName`, `BrandLogoUrl` as nullable text columns. `SystemSettingsSingletonId` moved from `HydraForgeDbContext` to `SystemSettings.SingletonId`. `NtfyClient` registered via `AddHttpClient<INtfyClient, NtfyClient>()` with `NtfyOptions` configured. |
| **2026-07-27 correction** | The original 2026-07-25 close-out marked this ✅ without the branch ever building — `INtfyClient.cs` and `NtfyOptions.cs` were referenced but never committed (CS0246 on `dotnet build`), and Step 1 (`SystemSettings` fields, `UpdateSettings()`, singleton-id move, migration) was never written at all. `docker-compose.yml`'s `ntfy` service also had no `command`, so the container exited immediately instead of serving (fixed: added `command: serve`). All gaps closed on `task/ntfy-integration` 2026-07-27; see the corrected Plan 6 section in `docs/manual-validation/2026-07-25-phase-5-notifications-admin-matrix.md` for what was actually re-verified. |

---

## D-53: SignalR Hub Protocol Must Use JsonStringEnumConverter

| Field | Value |
|---|---|
| **Topic** | Hub payload enums must serialize as strings, not ints |
| **Date** | 2026-07-27 |
| **Status** | ✅ Settled |
| **Decision** | `builder.Services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))` in `Program.cs`. |
| **Rationale** | Without this, hub payload enums (`BoardEntityType`, `BoardAction`, etc.) serialize as ints. MVC's `JsonStringEnumConverter` (via `builder.Services.ConfigureHttpJsonOptions`) only covers REST responses, not the SignalR hub protocol. The TUI `SignalRConnectionManager` deserializes these fields as strings (from the NSwag-generated contracts), so a mismatched int throws inside the client's message handler and is silently swallowed by the `HubConnection` — making board-event pushes a silent no-op. The TUI sees no board updates, no error, no reconnect. |
| **Impact** | `Program.cs` now has `AddJsonProtocol` with `JsonStringEnumConverter`. All future SignalR hubs inherit this serializer. Found and fixed in `9b26412`. |

## D-54: Targeted Realtime Board Patching

| Field | Value |
|---|---|
| **Topic** | Per-entity-type realtime event routing instead of full board refresh |
| **Date** | 2026-07-27 |
| **Status** | ✅ Settled |
| **Decision** | `useRealtime.ts` routes `BoardEntityEvent` by `envelope.entityType` to targeted handlers: `Card`/`Column` → single-entity re-fetch via `applyRealtimeCardEvent`/`applyRealtimeColumnEvent`; `CardRelationship` → full `board.fetchBoard`; others → `signalCardContentEvent` for open CardModals. `ProjectBoardEventEnvelope` gains `Guid? CardId` for card-scoped sub-entities. |
| **Rationale** | The original code called `board.fetchBoard(projectId)` on every `BoardEntityEvent` — a full board re-fetch that flickers the UI and wastes bandwidth, especially during rapid edits (checklist toggles, comment posts). Per-entity patching: no board blink, lower latency, no wasted data. `CardRelationship` still does full refresh because the event affects badges on both the source and target card (neither is the relationship's own `entityId`), and this event is rare enough that full refresh is acceptable. The `CardId` field lets the client route card-scoped sub-entity events (comment posted, checklist toggled) to the right open CardModal without guessing. |
| **Impact** | `useRealtime.ts` event handler restructured; `BoardStore` gains `applyRealtimeCardEvent`, `applyRealtimeColumnEvent`, `signalCardContentEvent`; `ProjectBoardEventEnvelope` has new `CardId` field (all sub-entity services now pass it). Added in `72afc95`.

---

## D-55: Notification Triggers Must Never Block the Business Operation They're Attached To

| Field | Value |
|---|---|
| **Topic** | `NotifyAsync` call sites at every trigger point (card move/assign/comment/@mention/dependency-resolved/project archive/update) |
| **Date** | 2026-07-28 |
| **Status** | ✅ Settled |
| **Decision** | Every trigger call site wraps `await _notifService.NotifyAsync(...)` in `try/catch`. A new `IWarnLogger` abstraction (`LogWarning(string)`) logs the failure instead of letting it propagate — optional constructor param defaulting to `NullWarnLogger` so existing DI registrations keep working unchanged. `ConsoleWarnLogger` (writes to `stderr`) is the real implementation registered in `Program.cs`. |
| **Rationale** | A card move, comment, or project update must succeed even if the notification pipeline (DB write, SignalR push, ntfy publish) fails for any reason — a transient DB hiccup or a downed ntfy server must never turn a routine board action into a 500. This generalizes the existing "external services are best-effort" pattern (ntfy failures already swallowed inside `NtfyClient`, D-52) up one level, to the trigger call sites themselves. |
| **Alternatives considered** | 1. Let `NotifyAsync` failures propagate and rely on global exception middleware (rejected — turns a notification hiccup into a failed card move, which the user experiences as data loss). 2. Fire-and-forget (`_ = NotifyAsync(...)` without awaiting) (rejected — loses the ability to log failures at all, and risks the notification running after the request/DbContext scope is disposed). |
| **Impact** | New `IWarnLogger`/`NullWarnLogger`/`ConsoleWarnLogger` in Application/Infrastructure. Every notification trigger call site in `CardService`, `CommentService`, `ProjectService` follows this pattern — any new trigger must too. |

---

## D-56: Dependency-Resolved Notification Fires on Card Archive, Not Card Move

| Field | Value |
|---|---|
| **Topic** | `NotifyResolvedDependenciesAsync` placement in `CardService` |
| **Date** | 2026-07-28 |
| **Status** | ✅ Settled |
| **Decision** | The "blocker resolved" notification call lives in `CardService.ArchiveAsync`, not `MoveAsync`. `CardRelationship`'s "is this blocker still active" check requires `ArchivedAt != null`. |
| **Rationale** | Plan 7 originally placed the call in `MoveAsync` on the assumption that moving a blocking card to a terminal column resolves it. But this codebase has no "Archived" pseudo-column — `ArchivedAt` is the only signal a card is done, and only `ArchiveAsync` sets it. With the call in `MoveAsync`, the "last active blocker" check always saw the just-moved card as still active (since `ArchivedAt` was still null) and the notification never fired, regardless of which column the card moved to. Caught during Plan 7's manual validation pass — zero automated coverage existed for this path beforehand, which is why the wiring bug shipped unnoticed. |
| **Impact** | `NotifyResolvedDependenciesAsync` call moved to `ArchiveAsync`. Two regression tests added: `ArchiveAsync_ArchivingLastActiveBlocker_NotifiesBlockedCardAssignees`, `ArchiveAsync_OtherActiveBlockerRemains_DoesNotNotify`. See `CLAUDE.md` "Notification trigger patterns (Plan 7 lessons)". |

---

## D-57: Nightly Job Scheduler — Hangfire + PostgreSQL

| Field | Value |
|---|---|
| **Topic** | Pre-phase decision blocking Phase 6 — how `ProjectContextSnapshot.AiNarrative` and any future scheduled work actually run |
| **Date** | 2026-07-30 |
| **Status** | ✅ Settled — supersedes the `BackgroundService`-for-MVP recommendation in `docs/functional-spec.md` §Phase 6 (that line was a suggestion, never a ratified decision) |
| **Decision** | **Hangfire, with `Hangfire.PostgreSql` as the job store** (same Postgres instance the app already runs — no new infra service). One recurring job (`RecurringJob.AddOrUpdate`) calls `ProjectContextSnapshotService.GenerateAiNarrative()` for every active project, on the admin-configurable time from D-52-adjacent settings (default midnight server time, per D-32/570s). Hangfire's built-in dashboard mounted admin-only in the admin space, alongside the existing audit-log/settings/users pages (Phase 5). |
| **Rationale** | User explicitly asked for **robust**. `BackgroundService` is an in-memory timer — a server restart mid-run silently drops the job, no retry, no history, no visibility. LLM calls are already known-flaky (Phase 6's own `ModelRouter` spec calls out rate-limit/5xx fallback) — narrative generation needs the same resilience, not less. Hangfire gives persistent job storage (survives restarts), automatic retry on failure, and run history/dashboard — for free, on infra we already run. Quartz.NET was rejected as solving a problem this app doesn't have: multiple cron schedules, misfire policies, per-job triggers. HydraForge needs exactly one global recurring job with one configurable time — Quartz's flexibility is pure overhead here. |
| **Alternatives considered** | 1. `BackgroundService` (rejected — no persistence/retry/visibility, wrong tradeoff given "robust" requirement). 2. Quartz.NET (rejected — cron-engine flexibility unused, heavier setup for zero benefit over Hangfire here). |
| **Impact** | New NuGet deps: `Hangfire.AspNetCore`, `Hangfire.PostgreSql`. `Program.cs` registers `AddHangfire(...).UsePostgreSqlStorage(...)` + `AddHangfireServer()`, mounts `/hangfire` dashboard behind admin auth filter. `docs/architecture.md` Technology Stack table updated. `docs/functional-spec.md` §Phase 6 pre-phase note marked resolved, points here. |

---

## D-58: AiNarrative Display Surface — Web UI Modal + TUI Viewer

| Field | Value |
|---|---|
| **Topic** | Where a user actually reads the nightly `AiNarrative` digest (D-32/D-570s settled that it's generated and stored, but no UI task existed anywhere in the spec to display it — gap found while reviewing Phase 6 readiness) |
| **Date** | 2026-07-30 |
| **Status** | ✅ Settled |
| **Decision** | **Web UI:** a "View Narrative" button next to the project title (board view header) opens a modal (`UModal`) showing `AiNarrative` + `AiNarrativeGeneratedAt` (fetched via existing `GET /api/projects/{projectId}/ProjectSnapshot`). **TUI:** same pattern as the existing spec/plan viewer screen (overlay push/pop, per D-4x TUI conventions) — a keybinding on the board screen opens a read-only narrative view. If the board status bar has no room for a new binding hint (it's already carrying connection/online/error-count per Phase 4), the binding lives in that screen's `?` help overlay only, not forced onto the bar. |
| **Rationale** | Directly requested. Closes a real spec gap: `AiNarrative` had a producer (nightly job) and an API (`ProjectSnapshotController`) but zero consumer task in either client — it would have shipped generated and unreadable. Reusing the spec/plan viewer overlay pattern on TUI and the modal pattern already established for card details on Web UI avoids inventing a new UI primitive for a read-only text panel. |
| **Impact** | Web UI: new button in board header component, new modal component (or reuse `AppModal` per its `#body` slot convention), no new store needed beyond the existing snapshot fetch. TUI: new `NarrativeViewerScreen` (or extends existing spec/plan viewer), overlay-launched from Board screen per the `PreviousScreen`/`CurrentScreen` pattern, help-table entry added to Board's `ShowHelp()` binding list. `docs/functional-spec.md` Phase 7 checklist gets an explicit display task (previously only had "generate," never "display"). |
