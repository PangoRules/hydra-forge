# HydraForge — Design Decision Log

> **Purpose:** Record every architectural and functional decision made during requirements gathering, along with the rationale. This prevents re-litigating settled topics and preserves context for future contributors.
>
> **Date:** 2026-06-02 (updated 2026-06-09)
> **Status:** 10 original questions resolved + 21 new decisions added (D-13–D-34). D-3, D-7 revised/rejected. Ready for Phase 1.

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
| **Decision** | **CardType enum:** `Task`, `Bug`, `Epic`, `Spec`, `Idea`. Plans are a **separate entity**, not a card type. |
| **Rationale** | Epics are cards that group other cards (parent-child). Specs and Plans are rich markdown documents, distinct from cards. Keeping them as separate entities with links is cleaner than overloading Card. |
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
| **Impact** | Removed `Swashbuckle.AspNetCore` and `Swashbuckle.AspNetCore.Annotations` packages. Removed `using Swashbuckle.AspNetCore.Annotations` and all `[SwaggerTag]`, `[SwaggerOperation]`, `[SwaggerResponse]` attributes from all 5 controllers. Replaced `AddSwaggerGen()` / `UseSwagger()` / `UseSwaggerUI()` with `AddOpenApi()` / `MapOpenApi()` / `MapScalarApiReference()`. OpenAPI doc served at `/openapi/v1.json`. Scalar UI served at `/scalar/v1` (dev only). For customizing the OpenAPI doc (e.g. adding Bearer auth scheme), use `IOpenApiDocumentTransformer` / `IOpenApiOperationTransformer` instead of Swashbuckle filters. The `Microsoft.OpenApi.Models` namespace does not exist in OpenAPI.NET v2.x — all types live in root `Microsoft.OpenApi`. |

---

## Summary

| # | Topic | Decision | Status |
|---|---|---|---|
| D-1 | Brand | HydraForge | ✅ |
| D-2 | Dual interface | Full feature parity | ✅ |
| D-3 | Offline | Rejected — server connection required, TUI locks gracefully | ❌ |
| D-4 | Personas | 4 personas: Terminal, Team, Manager, AI | ✅ |
| D-5 | Card types | Enum `Task/Bug/Epic/Spec/Idea`. Plan = own entity | ✅ |
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
| D-25 | Presence indicators | SignalR green dot, Phase 2 | ✅ |
| D-26 | Error handling | Air Force standard — global middleware, Result pattern, ProblemDetails, correlationId | ✅ |
| D-27 | Card dependencies | Typed relationships (BlockedBy, Precedes, Relates), soft warnings, cycle detection | ✅ |
| D-28 | Keyboard navigation | Both TUI and Web UI fully keyboard-navigable, shortcut reference overlay | ✅ |
| D-29 | Model routing & token management | Tier-based routing, per-feature defaults, token tracking, prompt caching, fallback chain | ✅ |
| D-30 | Card human-readable number | Sequential `CardNumber` per project (1, 2, 3…) — like GitHub issues | ✅ |
| D-31 | RAG pipeline | DocumentChunk + pgvector; IEmbeddingClient abstraction; chunks regenerated on doc change | ✅ |
| D-32 | ProjectContextSnapshot strategy | TemplateContent instant on mutation; AiNarrative nightly scheduled job only | ✅ |
| D-33 | OpenAPI docs (Swagger replacement) | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` instead of Swashbuckle | ✅ |
| D-34 | File storage architecture | `IFileStore` abstraction (Local fallback + MinIO/AWS S3), `{userId}/{sourceType}/{sourceId}/{guid}` key hierarchy, `InitializeAsync` for bucket creation | ✅ |

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
| **Status** | ✅ Settled |
| **Decision** | **Simple green dot presence via SignalR. Implemented in Phase 2 (alongside board real-time).** |
| **Rationale** | Cheap to implement with SignalR already in-stack. Prevents two people unknowingly editing the same card simultaneously. High value, low cost. |
| **UX** | Green dot on card = someone else currently has it open. Board header shows online member avatars. |
| **Impact** | SignalR `PresenceHub`: broadcast join/leave events. Client tracks `onlineUsers` map. No DB writes — ephemeral only. |

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
