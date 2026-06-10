# HydraForge — Functional Specification

> **Version:** 1.0
> **Date:** 2026-06-03

---

## Table of Contents

1. [Core Board](#1-core-board)
2. [Authentication](#2-authentication)
3. [Real-Time & Presence](#3-real-time--presence)
4. [Error Handling](#4-error-handling)
5. [General Chats (Personal)](#5-general-chats-personal)
6. [Accessibility & Responsive Design](#6-accessibility--responsive-design)
7. [Admin](#7-admin)
8. [Brain / Memory](#8-brain--memory)
9. [Calendar](#9-calendar)
10. [Compare](#10-compare)
11. [Cookbook](#11-cookbook)
12. [Deep Research](#12-deep-research)
13. [Gallery](#13-gallery)
14. [Documents / Library](#14-documents--library)
15. [Notes](#15-notes)
16. [Personal Tasks](#16-personal-tasks)
17. [Theme](#17-theme)
18. [Presets & Tools](#18-presets--tools)
19. [Card Dependencies](#19-card-dependencies)
20. [Chat → Card Creation](#20-chat--card-creation)
21. [Full Keyboard Navigation](#21-full-keyboard-navigation)
22. [Model Routing & Token Management](#22-model-routing--token-management)
23. [Image Generation](#23-image-generation)
24. [Non-Functional Requirements](#24-non-functional-requirements)
25. [Development Phase Checklists](#25-development-phase-checklists)

> ✅ = Confirmed | ❓ = Needs discussion | 🔜 = Future

---

## 1. Core Board

| # | Requirement | Status |
|---|---|---|
| FR-1 | Projects: Create, rename, archive, delete | ✅ |
| FR-2 | Projects: Configure git remote (any provider: GitHub, GitLab, Gitea, self-hosted) | ✅ |
| FR-3 | Columns: Renamable, reorderable, per-project columns | ✅ |
| FR-4 | Columns: Default set = Backlog → Spec-ing → Planned → In Dev → In Review → Done | ✅ |
| FR-5 | Cards: Create, edit, delete, move between columns | ✅ |
| FR-6 | Cards: Title, description, type (Task / Bug / Epic / Spec / Idea) | ✅ |
| FR-7 | Cards: Assignee(s) | ✅ |
| FR-8 | Cards: Checklist with subtasks | ✅ |
| FR-9 | Cards: Comments | ✅ |
| FR-10 | Cards: Link child cards to parent Epic | ✅ |
| FR-11 | Cards: Move by drag-and-drop (Web UI) and keyboard (TUI) | ✅ |
| FR-12 | Cards: Linked to Specs and Plans | ✅ |
| FR-13 | Specs: Long-form markdown documents linked to cards | ✅ |
| FR-14 | Plans: Step-by-step execution plans (markdown) linked to Specs/Cards | ✅ |
| FR-15 | Audit Log: Track all mutations — who did what, when | ✅ |
| FR-16 | TUI: Lock gracefully when server unreachable — show clear "not connected" message | ✅ |
| FR-17 | TUI: Reconnect automatically and resume when server becomes reachable again | ✅ |
| FR-22 | Multi-user: Multiple team members, same project | ✅ |
| FR-23 | Monorepo: Everything in one repo, one-command setup | ✅ |
| FR-24 | Dockerized: `docker-compose up` runs server + PostgreSQL + SearXNG (optional profile) — no manual setup | ✅ |
| FR-25 | Configuration: Easy to customize (company name, branding, defaults) | ✅ |
| FR-26 | Clean Architecture: Dependency injection, separation of concerns | ✅ |
| FR-27 | Testing: Heavy unit tests on business logic, xUnit | ✅ |
| FR-28 | Code style: Meaningful names, no unnecessary abstractions, readable like a newspaper | ✅ |
| FR-29 | File attachments on cards (local filesystem or S3-compatible storage) | ✅ |
| FR-30 | In-app notifications (SignalR push + ntfy, bell icon, no email) | ✅ |
| FR-31 | Chat folders: max 2 levels of nesting, free-form naming | ✅ |
| FR-32 | Project creation auto-creates matching chat folder | ✅ |
| FR-33 | Project archive auto-archives its chat folder (revivable) | ✅ |
| FR-34 | Project chat panel: collapsible, fresh session on open, does not obstruct board view | ✅ |
| FR-35 | Project chat auto-injects context: board summary + open card (if any) + user personality | ✅ |
| FR-36 | AI can propose card mutations from chat; user must confirm before any board state change | ✅ |
| FR-37 | AI edit permission is chat-session-scoped; revoked on session end or new chat | ✅ |
| FR-38 | Card-level chat summary table: collapsible, per-chat row with owner avatar + summary + date | ✅ |
| FR-39 | Card chat rows: owner's row clickable (reopens that chat), others read-only | ✅ |
| FR-40 | Project chats visible to all project members (read-only for non-owners) | ✅ |
| FR-41 | "Summarize → start my own" fork action on any shared project chat | ✅ |
| FR-42 | ProjectContextSnapshot: `TemplateContent` regenerated on every board mutation (instant, no LLM). `AiNarrative` generated nightly by scheduled job. Chat always injects TemplateContent. | ✅ |
| FR-43 | Project visibility: members-only; non-members cannot see project exists | ✅ |
| FR-44 | Admin sees all projects regardless of membership | ✅ |
| FR-45 | Any authenticated user can create a project; creator becomes owner | ✅ |
| FR-46 | ProjectMember roles: Owner, Member (view-only deferred to post-MVP) | ✅ |
| FR-47 | Notification rules: card move → assignees, comment → watchers, @mention → user, project events → all members | ✅ |
| FR-48 | CardWatcher auto-created when user comments on or is assigned to a card | ✅ |
| FR-52 | Presence indicators: green dot on card/board via SignalR PresenceHub (ephemeral) | ✅ |

---

## 2. Authentication

| # | Requirement | Status |
|---|---|---|
| FR-21 | Auth: Basic authentication (JWT) — no SSO, no OAuth, no external auth providers | ✅ |
| FR-49 | LLM providers: admin-configured only; users select from available providers | ✅ |
| FR-50 | LlmProvider pluggable via ILlmClient; ship OpenAI-compat, Anthropic, Ollama adapters | ✅ |
| FR-51 | AI agent personality: user-definable system prompt, multiple per user, one default | ✅ |

---

## 3. Real-Time & Presence

| # | Requirement | Status |
|---|---|---|
| FR-18 | Real-time: Server pushes updates via SignalR (WebSocket) | ✅ |
| FR-52 | Presence indicators: green dot on card/board via SignalR PresenceHub (ephemeral) | ✅ |
| FR-65 | SignalR disconnect handled gracefully — client shows reconnecting state, retries with backoff, resumes on reconnect | ✅ |

---

## 4. Error Handling

| # | Requirement | Status |
|---|---|---|
| FR-53 | All backend errors caught by global exception middleware — no unhandled exceptions reach clients | ✅ |
| FR-54 | All errors returned as structured `ProblemDetails` (RFC 7807): `type`, `title`, `status`, `detail`, `correlationId` | ✅ |
| FR-55 | Business logic errors use Result pattern (`Result<T, Error>`) — no exceptions for expected failure paths | ✅ |
| FR-56 | Error codes typed and exhaustive — every error condition has a named code (e.g. `CARD_NOT_FOUND`, `COLUMN_LIMIT_EXCEEDED`) | ✅ |
| FR-57 | Stack traces never sent to clients — logged server-side only | ✅ |
| FR-58 | All errors include a `correlationId` — logged server-side and visible to user for support escalation | ✅ |
| FR-59 | TUI: connection loss → immediate "Server unreachable" lock screen with reconnect indicator | ✅ |
| FR-60 | TUI: all API/SignalR errors surfaced in a dedicated status bar error panel — never silently swallowed | ✅ |
| FR-61 | Web UI: all API errors surfaced as typed toast/banner with `detail` message and `correlationId` | ✅ |
| FR-62 | LLM provider errors caught and surfaced with actionable message (e.g. "Provider offline", "Rate limit hit", "Invalid API key") — never crash the session | ✅ |
| FR-63 | Git integration errors caught and surfaced explicitly (e.g. "Push rejected: branch protected", "Remote unreachable") | ✅ |
| FR-64 | Database errors caught at infrastructure layer — never leak raw SQL errors to application or client | ✅ |
| FR-65 | SignalR disconnect handled gracefully — client shows reconnecting state, retries with backoff, resumes on reconnect | ✅ |
| FR-66 | All server errors logged with structured logging (severity, correlationId, userId, endpoint, duration) | ✅ |
| FR-67 | 4xx errors logged at Warning level; 5xx errors logged at Error level with full context | ✅ |
| FR-68 | Health check endpoint (`/health`) — reports server + database + LLM provider connectivity status | ✅ |

---

## 5. General Chats (Personal)

| # | Requirement | Status |
|---|---|---|
| FR-69 | Users can create free-form chats not tied to any project | ✅ |
| FR-70 | Free-form chats organized in personal folders (max 2 levels) | ✅ |
| FR-71 | Chats sorted by recent activity; filterable/searchable by folder | ✅ |
| FR-72 | Chat search across all user's chats (title + message content) | ✅ |
| FR-73 | All chat features available outside projects: model selection, file upload, vision, RAG | ✅ |

---

## 6. Accessibility & Responsive Design

| # | Requirement | Status |
|---|---|---|
| FR-74 | Web UI is mobile-first — designed for mobile, progressively enhanced for larger screens | ✅ |
| FR-75 | Three component breakpoints: mobile (<768px), tablet (768–1199px), desktop (≥1200px) | ✅ |
| FR-76 | Mobile: single-column layout, bottom navigation bar, board shown as card list | ✅ |
| FR-77 | Tablet: dual-pane layout, side navigation, board shown as compact scrollable columns | ✅ |
| FR-78 | Desktop: full multi-column board, all panels open, keyboard shortcuts active | ✅ |
| FR-79 | Separate component sets per breakpoint — not just CSS hiding, actual component variants where UX differs significantly | ✅ |
| FR-80 | Web UI: ARIA labels, focus management, keyboard navigation, sufficient color contrast (WCAG AA) | ✅ |
| FR-81 | TUI: full keyboard navigation, vim-style movement, no mouse required | ✅ |

---

## 7. Admin

| # | Requirement | Status |
|---|---|---|
| FR-82 | Admin dashboard: users, all projects overview, LLM providers, system health, global audit log | ✅ |
| FR-83 | Admin can manage users: create, disable, reset password, assign admin role | ✅ |
| FR-84 | Admin can see and manage all projects regardless of membership | ✅ |
| FR-85 | Admin can configure feature flags: disable Compare, Cookbook, Deep Research, Gallery per install | ✅ |
| FR-86 | Admin CANNOT access user personal data: chats, brain/memory, notes, tasks, calendar, gallery, documents | ✅ |
| FR-87 | Admin can configure system: SearXNG URL, ntfy server, LLM providers, platform branding | ✅ |

---

## 8. Brain / Memory

| # | Requirement | Status |
|---|---|---|
| FR-88 | Per-user persistent memory store — categories: fact, preference, identity, event, contact, instruction | ✅ |
| FR-89 | Memory auto-extracted from conversations (toggle per session, user controls) | ✅ |
| FR-90 | Memory search: keyword + vector via pgvector (same DB, no separate ChromaDB needed) | ✅ |
| FR-91 | Memory pin (prioritized recall in context), edit, delete, timeline view | ✅ |
| FR-92 | Memory injected into chat context (toggle per session) | ✅ |
| FR-93 | Memory import/export (JSON, markdown, plain text) | ✅ |
| FR-94 | Memory tidy: AI deduplication and cleanup | ✅ |
| FR-95 | Memory usage statistics (count by category, storage used) | ✅ |

---

## 9. Calendar

| # | Requirement | Status |
|---|---|---|
| FR-96 | Per-user calendar with event CRUD (title, description, start/end, all-day, recurrence) | ✅ |
| FR-97 | CalDAV sync: Radicale, Nextcloud, Apple Calendar, Fastmail, generic CalDAV servers | ✅ |
| FR-98 | Per-calendar color customization | ✅ |
| FR-99 | .ics import/export | ✅ |
| FR-100 | Natural language → event quick-parse ("Meeting tomorrow at 3pm" → creates event) | ✅ |
| FR-101 | Timezone support per user | ✅ |
| FR-102 | Agent-aware: AI can query/suggest calendar events in chat context | ✅ |

---

## 10. Compare

| # | Requirement | Status |
|---|---|---|
| FR-103 | Blind A/B model comparison: same prompt sent to two models, responses hidden until vote | ✅ |
| FR-104 | Vote on winner (A / B / Tie), per-user comparison history | ✅ |
| FR-105 | Feature-flaggable per install — admin can disable for enterprise deployments | ✅ |

---

## 11. Cookbook

| # | Requirement | Status |
|---|---|---|
| FR-106 | Model browser: search and browse models (Hugging Face registry, Ollama library) | ✅ |
| FR-107 | Hardware fitness scoring: given GPU/RAM specs, score which models fit (llmfit-style) | ✅ |
| FR-108 | Cookbook connects to an external model server on the network (vLLM, Ollama, llama.cpp) — does NOT install models on HydraForge server | ✅ |
| FR-109 | Serve configuration recommendations: quantization, context window, batch size for given hardware | ✅ |
| FR-110 | For single-machine setups: guide to run model server alongside HydraForge on same host | ✅ |
| FR-111 | Feature-flaggable per install | ✅ |

---

## 12. Deep Research

| # | Requirement | Status |
|---|---|---|
| FR-112 | Per-user deep research sessions: multi-step web search → AI synthesis → visual report | ✅ |
| FR-113 | Live progress streaming via SignalR during research | ✅ |
| FR-114 | Research library: history, archive, spinoff from existing session | ✅ |
| FR-115 | Hide/unhide images in reports | ✅ |
| FR-116 | Web search via SearXNG — bundled as optional docker-compose service; admin can override with external instance URL via system settings | ✅ |

---

## 13. Gallery

| # | Requirement | Status |
|---|---|---|
| FR-117 | Per-user image gallery: upload, view, manage images | ✅ |
| FR-118 | AI + user tags, EXIF metadata extraction (camera, location, date) | ✅ |
| FR-119 | Albums with cover photos, favorites, deduplication by hash | ✅ |
| FR-120 | Bulk operations (tag, deduplicate), ZIP download | ✅ |
| FR-121 | Gallery editor: layer-based editing, opacity/visibility, inpainting, AI upscaling, rotation | ✅ |
| FR-122 | Editor drafts: save in-progress edits, resume later | ✅ |

---

## 14. Documents / Library

| # | Requirement | Status |
|---|---|---|
| FR-123 | Per-user document library: upload and manage files (PDF, markdown, code, CSV, HTML) | ✅ |
| FR-124 | Living documents: AI-assisted editor with version history + restore + compare | ✅ |
| FR-125 | PDF rendering, form-fill detection, AI-powered annotation filling | ✅ |
| FR-126 | Document search by title and content | ✅ |
| FR-127 | Archive, export as ZIP | ✅ |
| FR-128 | AI tidy: keep/junk classification | ✅ |
| FR-129 | Syntax highlighting per language in code documents | ✅ |

---

## 15. Notes

| # | Requirement | Status |
|---|---|---|
| FR-130 | Per-user notes: create, pin, archive, checklists with toggle completion | ✅ |
| FR-131 | Reminders with custom repeat patterns (daily, weekly, monthly, custom cron) | ✅ |
| FR-132 | ntfy ping on reminder trigger | ✅ |
| FR-133 | AI classification of notes (auto-tag, summary) | ✅ |
| FR-134 | Drag reorder, image attachments | ✅ |

---

## 16. Personal Tasks

| # | Requirement | Status |
|---|---|---|
| FR-135 | Per-user personal tasks separate from project board cards | ✅ |
| FR-136 | Task reminders with cron-style scheduling | ✅ |
| FR-137 | Mark complete/incomplete, ntfy notification on due date | ✅ |

---

## 17. Theme

| # | Requirement | Status |
|---|---|---|
| FR-138 | Per-user theme: select from 20+ bundled themes | ✅ |
| FR-139 | Custom color palette builder: chat bubbles, sidebar, input, buttons, code highlight, toggles | ✅ |
| FR-140 | Font selection: monospace, sans-serif, serif, custom | ✅ |
| FR-141 | Density modes: comfortable, compact, spacious | ✅ |
| FR-142 | Live preview before applying | ✅ |
| FR-143 | Theme persists server-side per user (not just local storage) | ✅ |

---

## 18. Presets & Tools

| # | Requirement | Status |
|---|---|---|
| FR-144 | Prompt presets: reusable templates with groups, per-user | ✅ |
| FR-145 | User Tool Builder: create HTML mini-apps with persistent key-value store, pin, session or global scope | ✅ |
| FR-146 | STT (speech-to-text) input in chat | ✅ |
| FR-147 | TTS (text-to-speech) output with cache | 🔜 |
| FR-148 | Signatures: draw and save handwritten signatures (PNG/SVG), encrypted at rest, reuse in documents | 🔜 |

---

## 19. Card Dependencies

| # | Requirement | Status |
|---|---|---|
| FR-149 | Cards support typed relationships: `BlockedBy`, `Precedes`, `Relates` — scalable to full DAG later | ✅ |
| FR-150 | Circular dependency detection at application layer — reject any new relationship that creates a cycle before writing to DB | ✅ |
| FR-151 | Blocked card shows lock icon + blocked-by count badge on board at all times — visible without opening the card | ✅ |
| FR-152 | Card with `Precedes` or `Relates` relationships shows a chain/link indicator badge on board | ✅ |
| FR-153 | Moving a blocked card to a new column triggers soft warning: "This card is blocked by #42 (Auth Middleware). Move anyway?" — user decides, flow not hard-blocked | ✅ |
| FR-154 | Dependency-resolved notification: when a blocking card moves to Done, assignees of the now-unblocked card receive ntfy ping: "Card #43 is now unblocked — #42 was completed" | ✅ |
| FR-155 | Archiving a card with dependents: warn user with list of affected cards. On confirm — dependency records soft-deleted (invisible), but preserved in audit log | ✅ |
| FR-156 | Archived dependency relationships retained in audit log for historical reference and future auditing | ✅ |
| FR-157 | Cross-project dependencies: explicitly out of scope. Each project is isolated. Cards may only depend on other cards within the same project | ✅ |
| FR-158 | TUI: keyboard flow for dependency management in card detail — `d` opens dependency panel, search/type card by number or title, select type, confirm | ✅ |
| FR-159 | ProjectContextSnapshot card index includes: id, title, column, type — enabling AI to reference cards by number when proposing dependencies | ✅ |

---

## 20. Chat → Card Creation

| # | Requirement | Status |
|---|---|---|
| FR-160 | Cards can be created manually in both Web UI and TUI without AI involvement | ✅ |
| FR-161 | After manual card creation, user can optionally open chat to ask AI to review, enhance, or suggest dependencies | ✅ |
| FR-162 | From project/card chat, AI can propose creating one or multiple cards in a single interaction | ✅ |
| FR-163 | AI card proposals include: title, type, column, assignee (optional), description, dependencies (optional) | ✅ |
| FR-164 | Bulk card creation: AI proposes all cards at once as a confirmation list — user approves or edits the full batch, not one at a time | ✅ |
| FR-165 | When creating from a card's chat panel, newly created cards auto-link to the source card as `Relates` unless user specifies otherwise | ✅ |
| FR-166 | AI uses ProjectContextSnapshot card index to propose meaningful dependencies (e.g. "blocked by #42") — never fabricates card IDs | ✅ |

---

## 21. Full Keyboard Navigation

| # | Requirement | Status |
|---|---|---|
| FR-167 | Web UI fully keyboard navigable — all actions reachable without mouse | ✅ |
| FR-168 | Web UI keyboard shortcuts: navigate board columns, select/open cards, move cards between columns, trigger primary actions | ✅ |
| FR-169 | TUI: full keyboard navigation, vim-style movement (`h/j/k/l`), no mouse required | ✅ |
| FR-170 | Both interfaces expose a keyboard shortcut reference (Web: modal/overlay, TUI: `?` command) | ✅ |

---

## 22. Model Routing & Token Management

| # | Requirement | Status |
|---|---|---|
| FR-171 | Admin defines model tiers: Economy, Standard, Premium — maps each configured LLM provider/model to a tier | ✅ |
| FR-172 | Admin assigns default tier per feature (personal chat, project chat, deep research, agent pipeline, memory extraction, notes classify, etc.) | ✅ |
| FR-173 | Admin can allow or lock per-feature user overrides — e.g. personal chat allows user to go up to Standard but not Premium | ✅ |
| FR-174 | Project owner can set a tier ceiling for their project (within admin-configured maximum) | ✅ |
| FR-175 | Per-user token budget: admin sets optional daily/monthly token cap per user (0 = unlimited) | ✅ |
| FR-176 | Token usage tracking: every LLM call records input + output tokens, feature, model, userId, projectId | ✅ |
| FR-177 | Admin token usage dashboard: usage by user, by feature, by model, by day/month | ✅ |
| FR-178 | User token usage view: users see their own consumption, not others' | ✅ |
| FR-179 | Context window guard: before routing, check compressed context size against model's window limit — auto-bump tier if Economy model's window is insufficient | ✅ |
| FR-180 | Context compression: when injected context (board state, memory, card detail) exceeds configurable token threshold, auto-summarize before sending | ✅ |
| FR-181 | Prompt caching: `ProjectContextSnapshot` and user memory formatted as cache-eligible blocks in `ILlmClient` — reduces cost on repeated project chat calls | ✅ |
| FR-182 | Model fallback chain: admin defines secondary model per tier — if primary is rate-limited or unavailable, auto-retry with fallback before surfacing error | ✅ |
| FR-183 | Cost estimate before expensive operations (Deep Research, agent pipeline): show estimated input token count + model tier before user confirms run | ✅ |
| FR-184 | Agent pipeline token tracking: multi-turn pipelines (Planner → Developer → Reviewer) tracked as a single pipeline run with total cost visible in admin | ✅ |

### Default Tier Assignments (admin-configurable, these are install defaults)

| Feature | Default Tier | Rationale |
|---|---|---|
| Notes AI classify | Economy | Simple classification, no reasoning needed |
| Brain memory extraction | Economy | Pattern extraction from conversation |
| Personal chats | Economy | General conversation, user-adjustable |
| Cookbook recommendations | Economy | Simple model lookups |
| Card AI review / suggestions | Standard | Code and spec awareness required |
| Project chats | Standard | Larger context injection (board state) |
| Document AI editing | Standard | Precision and coherence matter |
| Deep Research synthesis | Premium | Long-context, multi-step reasoning |
| Agent pipeline (Dev/Reviewer) | Premium | Accuracy is mission-critical |
| Compare | User selects | Explicitly a per-comparison choice |
| Image generation (chat/docs) | Standard | Quality matters for usable output |
| Image generation (gallery editor) | Premium | Inpainting/upscaling needs best quality |

---

## 23. Image Generation

| # | Requirement | Status |
|---|---|---|
| FR-185 | Image generation via admin-configured image providers (same provider management system as text, `ProviderType: Image`) | ✅ |
| FR-186 | Image providers: OpenAI DALL-E, Stability AI, local Stable Diffusion (diffusers/ComfyUI/A1111), Flux variants (schnell/dev/pro), any OpenAI-compatible image endpoint | ✅ |
| FR-187 | Image tiers: Economy (local SD, Flux-schnell, SDXL-turbo), Standard (DALL-E 2, SD3, Flux-dev), Premium (DALL-E 3, Flux-pro) | ✅ |
| FR-188 | In-chat image generation: user requests image → rendered inline in chat bubble with download option | ✅ |
| FR-189 | Gallery editor uses configured image backend: inpainting, AI upscaling, style transfer, generate from prompt | ✅ |
| FR-190 | Document editor: insert AI-generated image into living document | ✅ |
| FR-191 | Admin sets default image tier per surface (chat, gallery editor) — same override ceiling mechanism as text tiers | ✅ |
| FR-192 | Image usage tracked separately from text: `ImageUsageRecord` (userId, `AiFeature`, providerId, modelName, imageCount, resolution, cost, createdAt) | ✅ |
| FR-193 | Admin image usage dashboard: images generated by user / feature / model / period | ✅ |
| FR-194 | Image generation respects model fallback chain: if primary image provider unavailable, retry with configured fallback | ✅ |

---

## 24. Non-Functional Requirements

| # | Requirement | Target |
|---|---|---|
| NFR-1 | TUI startup time | < 1 second |
| NFR-2 | Card move latency (LAN) | < 50ms (PostgreSQL + SignalR on LAN) |
| NFR-3 | Card move sync latency (LAN) | < 100ms (SignalR) |
| NFR-4 | Network requirement | Active connection to server required (VPN for remote access) |
| NFR-5 | Server resource usage | Lightweight — runs on a dev machine |
| NFR-6 | Web UI load time | < 2 seconds first load |
| NFR-7 | Audit Log retention | At least 90 days |
| NFR-8 | Code coverage (business logic) | > 90% |
| NFR-9 | Zero unhandled exceptions in production — global middleware catches all | Non-negotiable |
| NFR-10 | Error discoverability | Every error a user sees must include a `correlationId` they can report |
| NFR-11 | No silent failures — every operation either succeeds visibly or fails visibly | Non-negotiable |
| NFR-12 | Log volume | Errors and warnings only in production by default; debug level configurable |
| NFR-13 | Error response time | Error responses must be as fast as success responses — no hanging on failure |
| NFR-14 | Resilience | LLM/Git/ntfy failures must not crash or block core board functionality |
| NFR-15 | Archived item retention | Archived GalleryImages, Albums, and AlbumImages remain visible to their owner (including inside archived albums); a scheduled housekeeping job hard-deletes them after an admin-configured retention period |

---

## 25. Development Phase Checklists

### Phase 1: Foundation 🏗️
> Goal: running skeleton, auth, error handling infrastructure. Nothing user-facing yet.

- [x] Scaffold monorepo: `HydraForge.Domain`, `HydraForge.Application`, `HydraForge.Infrastructure`, `HydraForge.Server`, `HydraForge.Tui`, `web-ui`
- [x] Docker Compose: .NET server + PostgreSQL 16 + pgvector + SearXNG (optional `--profile search`)
- [x] EF Core + Npgsql: DbContext, all current entities, startup migration application behind config
- [x] PostgreSQL `pgvector` extension: enabled in migrations, `vector(1536)` columns on `MemoryEntry.Embedding` and `DocumentChunk.Embedding`
- [x] Archive/housekeeping schema foundation: `ArchivedAt?` on ownable entities; `IsArchived: bool` replaced with `ArchivedAt?` on Note and Document; FK `OnDelete: Cascade` for `Document→DocumentVersion`, `Note→NoteReminder`, `Note→NoteImageAttachment`, `ChatSession→ChatMessage`
- [x] `SystemSettings` singleton entity (id `00000000-0000-0000-0000-000000000001`) with admin-configurable retention knobs: `ArchivedItemRetentionDays=730`, `AuditLogRetentionDays=90` (satisfies NFR-7), `NotificationRetentionDays=30`
- [ ] `HousekeepingBackgroundService`: deferred implementation. Phase 1 provides schema/settings/cascade foundation; hard-delete scheduling, polymorphic `DocumentChunk` cleanup, file cleanup, and per-run audit entries are implemented in later archive/service phases.
- [x] Basic auth: user store, Argon2 hashing, JWT tokens, admin seeded on first run
- [x] Error handling: expected `Result<T, Error>` failures map to ProblemDetails where endpoints exist; unhandled exceptions map to ProblemDetails via global middleware
- [x] `Result<T, Error>` pattern in Domain layer — typed error codes, no business logic exceptions
- [x] Structured logging: `Microsoft.Extensions.Logging` + Serilog, severity-appropriate, correlationId on every request
- [x] `/health` endpoint: server + DB + LLM provider connectivity status
- [x] Audit Log infrastructure: `AuditLogEntry`, audit service abstraction, and EF writer available for future mutation handlers
- [x] CI/CD pipeline

### Phase 2: Project Space — API & Domain 📋
> Goal: all project/board business logic complete and tested via API. No UI yet.

> ⚠️ **Pre-phase decision needed:** File storage default — local FS or S3? Define `IFileStore` abstraction (`LocalFileStore` + `S3FileStore` implementations) and add storage config to app settings (`FILE_STORAGE_PROVIDER`, `FILE_STORAGE_PATH` / S3 credentials). Decide before implementing `Attachment` upload. Recommendation: default to local FS, S3 is opt-in via env var.

- [x] Project CRUD + ProjectMember management (Owner / Member roles)
- [x] `ProjectArchiveService.Archive(projectId)`: sets `Project.ArchivedAt` (if/when added) + cascades to chat folder and sessions via `ChatArchiveService.ArchiveFolder`. Project archive is the entry point that triggers cascading archive down the chat subtree.
- [x] Column CRUD + reordering + per-project default columns
- [x] Card CRUD + move between columns + position ordering
- [x] Card types: Task / Bug / Epic / Spec / Idea
- [x] Epic → child card linking
- [x] Checklists on cards (items, completion, assignee per item)
- [x] Comments on cards + @mention extraction + CardWatcher auto-add
- [x] File attachments on cards (local FS storage, S3-compatible abstraction)
- [ ] Specs: versioned markdown documents linked to cards
- [ ] Plans: versioned numbered markdown documents linked to specs/cards
- [ ] CardRelationship CRUD: BlockedBy, Precedes, Relates
- [ ] Circular dependency detection: `CardDependencyService.ValidateAcyclic()` — reject on insert
- [ ] Archive card with dependents: warn payload → confirm → soft-delete relationships → audit log
- [ ] ProjectContextSnapshot: maintain + auto-regenerate on board mutations (card index: id, title, column, type)
- [ ] SignalR hubs: board mutations broadcast to all connected project members
- [ ] Presence: `PresenceHub` — join/leave events, ephemeral only (no DB writes)
- [ ] All endpoints covered by xUnit tests (> 90% business logic coverage)

### Phase 3: Project Space — Web UI 🌐
> Goal: full project board usable in browser. Feature-complete project workspace.

- [ ] Auth pages: login, first-run admin setup
- [ ] Project list + create project flow
- [ ] Board view: columns + cards, drag-and-drop move, column reorder
- [ ] Card detail modal: title, description, type, assignees, checklist, comments, attachments, spec link, plan link
- [ ] Dependency panel in card detail: view/add BlockedBy, Precedes, Relates — search cards by number/title
- [ ] Blocked card lock icon + badge on board (always visible)
- [ ] Soft warning modal on column move when blocked (points to blocking card)
- [ ] Spec editor: versioned markdown, version history sidebar, restore
- [ ] Plan editor: numbered steps markdown, version history
- [ ] Archive card with dependents: warning modal listing affected cards
- [ ] Real-time board updates via SignalR (no page refresh)
- [ ] Presence dots on board and card detail
- [ ] Full keyboard navigation: board columns/cards with arrow keys, `n` new card, `m` move, `/` search, `Enter` open, `Escape` close
- [ ] Keyboard shortcut reference overlay (`?`)
- [ ] ARIA labels, focus management, WCAG AA color contrast (built in from day one)
- [ ] Error display: typed toast with `detail` + `correlationId` copy button

### Phase 4: Project Space — TUI 🖥️
> Goal: full project board usable in terminal. Feature parity with Web UI board.

- [ ] Connection handling: lock screen when server unreachable (`⚠ Server unreachable. Retrying...`), auto-reconnect
- [ ] Auth: login prompt on startup, JWT stored in user config
- [ ] Project list view + create project
- [ ] Board view: ASCII/rich columns + cards, real-time updates via SignalR
- [ ] Card detail view: all fields editable (description opens in `$EDITOR`)
- [ ] Create / edit / move cards via keyboard (`h/j/k/l` navigation, `n` new, `m` move)
- [ ] Dependency panel: `d` key → search/type card → select type → confirm
- [ ] Blocked card indicator in board view
- [ ] Spec + plan viewer/editor (opens in `$EDITOR`)
- [ ] Comments: inline view + add
- [ ] Checklists: toggle completion from keyboard
- [ ] Keyboard shortcut reference: `?`
- [ ] Status bar: sync status, unread notification count, online presence count
- [ ] Error panel in status bar: surfaced errors with correlationId, dismissible

### Phase 5: Multi-User, Notifications & Admin 🔔
> Goal: team collaboration working end-to-end. Admin can manage the install.

- [ ] ntfy integration: per-user topic `hydraforge-{userId}`, configurable ntfy server URL
- [ ] Notification rules: card move → assignees, card assigned → user, comment → watchers, @mention → user, dependency resolved → unblocked assignees, project archived/edited → all members, PR created → all members
- [ ] In-app bell icon (Web UI) + unread count in TUI status bar
- [ ] Admin dashboard: users list, all projects overview, system health
- [ ] Admin: create user, disable user, reset password, assign admin role
- [ ] Admin: system settings (ntfy URL, SearXNG URL, platform branding)
- [ ] Admin UI: edit `SystemSettings` retention knobs at runtime (`ArchivedItemRetentionDays`, `AuditLogRetentionDays`, `NotificationRetentionDays`) without redeploy; reflects on next housekeeping run (5-min settings cache TTL)
- [ ] Admin: see and manage all projects regardless of membership
- [ ] Audit log viewer: filter by project, user, entity type, date range

### Phase 6: LLM Infrastructure 🔧
> Goal: all AI plumbing in place before any chat or AI feature is built on top.

> ⚠️ **Pre-phase decision needed:** Nightly job scheduler — pick one before implementing `ProjectContextSnapshot.AiNarrative` and any other scheduled work. Options: (a) `BackgroundService` (built-in .NET, simple, no UI) — recommended for MVP; (b) Hangfire (persistent jobs, retry, admin dashboard); (c) Quartz.NET (full cron engine). Recommendation: start with `BackgroundService`, migrate to Hangfire if job visibility becomes important.

- [ ] `ILlmClient` abstraction: `StreamChatAsync()`, `GetModelsAsync()`, `SupportsToolCalling()`, cache block placement
- [ ] OpenAI-compatible adapter (covers OpenAI, Groq, DeepSeek, OpenRouter, vLLM, llama.cpp)
- [ ] Anthropic adapter (with prompt caching `cache_control` blocks)
- [ ] Ollama adapter
- [ ] `IImageClient` abstraction: `GenerateImageAsync()`, `InpaintAsync()`
- [ ] Image adapters: OpenAI DALL-E, Stability AI, diffusers/ComfyUI (local)
- [ ] Embedding service: `IEmbeddingClient` abstraction — generate `vector(1536)` from text (needed for RAG + Brain/Memory)
- [ ] Admin LLM provider management UI: add/edit/disable providers, assign `ProviderType` + `ModelTier`, set fallback chain
- [ ] Admin image provider management UI (same panel, filtered by ProviderType: Image)
- [ ] `FeatureRoutingConfig`: admin assigns default tier per `AiFeature`, sets user override ceiling per feature
- [ ] `ModelRouter` service: feature + user context → correct provider, context window guard, auto-bump tier, fallback on rate-limit/5xx
- [ ] `ContextCompressor` service: auto-summarize injected context when threshold exceeded
- [ ] `TokenUsageRecord`: log every text LLM call
- [ ] `ImageUsageRecord`: log every image generation call
- [ ] `UserTokenBudget`: daily/monthly cap, enforce at call time → `TOKEN_BUDGET_EXCEEDED` error
- [ ] Admin token usage dashboard: by user / feature / model / period
- [ ] Admin image usage dashboard: by user / feature / model / period
- [ ] User self-service usage view

### Phase 7: Chat — General & Project 💬
> Goal: full chat system built on top of Phase 6 LLM infrastructure.

**General chats (personal, non-project):**
- [ ] ChatSession CRUD + ChatMessage persistence
- [ ] Chat folder system: max 2 levels, free-form naming
- [ ] Chat search: title + message content
- [ ] Model selection per session (from admin-configured providers, respects tier ceilings)
- [ ] File upload + vision in chat
- [ ] RAG: `DocumentChunk` pipeline — uploaded files chunked, embedded, stored; retrieved at query time via pgvector similarity search
- [ ] Prompt presets: CRUD, groups, inject into session
- [ ] AgentPersonality: user-defined system prompt, multiple per user, default
- [ ] Streaming responses via SignalR

**Project chats:**
- [ ] Project creation auto-creates matching chat folder
- [ ] Project chat panel: collapsible in board view, does not obstruct board
- [ ] Fresh session on panel open + smart context injection (`ProjectContextSnapshot.TemplateContent` + open card)
- [ ] "Card #42 [title] opened — what are we doing?" auto-prompt when card is open
- [ ] AI edit permission: session-scoped, confirmation dialog per mutation OR grant session-level blanket permission
- [ ] AI edit permission revoked on session end or new chat
- [ ] CardChatLink: auto-link + summary generation on session close
- [ ] Card-level chat summary table (collapsible, owner-clickable, others read-only)
- [ ] Shared project chats: visible to all members read-only
- [ ] "Summarize → start my own" fork action
- [ ] Project archive → chat folder archived (revivable)
- [ ] `ChatArchiveService.ArchiveFolder(folderId)`: sets `ChatFolder.ArchivedAt` and cascades to every child `ChatSession.ArchivedAt`. Invoked by `ProjectArchiveService` and by explicit user "archive folder" action.
- [ ] Nightly scheduled job: generate `ProjectContextSnapshot.AiNarrative` for all active projects
- [ ] TUI: chat mode for general chats + project chat panel

### Phase 8: AI Features — Project Space 🤖
> Goal: AI agents can operate on the board. Chat can create and mutate cards.

- [ ] Card creation from chat: AI proposes cards (title, type, column, description, dependencies) → bulk confirmation list → user approves all at once
- [ ] Card creation auto-links to source card as `Relates` when created from card chat
- [ ] AI uses ProjectContextSnapshot card index — no fabricated IDs
- [ ] Manual card creation always available without AI
- [ ] After manual creation: user can ask AI to review/enhance/suggest dependencies from chat
- [ ] "Write spec" from card description → AI drafts spec → user reviews + confirms
- [ ] "Propose plan" from spec → AI drafts numbered plan → user reviews + confirms
- [ ] Agent pipeline: Planner → Orchestrator → Developer → Reviewer → Documenter → Git Agent
- [ ] Agent pipeline human gate: PR lands on Review column, human must approve before Done
- [ ] Git Agent: commit, PR creation with AI-generated description
- [ ] Cost estimate shown before agent pipeline run
- [ ] Agent pipeline run grouping (`PipelineRunId`) for cost tracking
- [ ] Stream all agent output through SignalR

### Phase 9: Personal Workspace 🧠
> Goal: every user has their own private AI workspace alongside the team board.

> ⚠️ **Pre-phase decision needed:** `UserPreferences` entity is missing from the data model. Theme, font, density, timezone, and other per-user settings need a persistence layer. Add to data model before starting this phase:
> ```
> UserPreferences { UserId, ThemeId, FontFamily, DensityMode, Timezone, UpdatedAt }
> UserTheme { Id, UserId, Name, ColorPalette (JSON), IsDefault }   ← for custom themes
> ```
> Migration must run before any preference-related endpoints are built.

- [ ] Brain/Memory: per-user store, categories (fact/preference/identity/event/contact/instruction), pgvector semantic search, auto-extract from chat (toggle), pin, inject into chat (toggle), import/export, tidy (AI dedup), usage stats
- [ ] Notes: CRUD, pin, archive, checklists, reminders + custom repeat patterns, ntfy ping on trigger, AI classification, drag reorder, image attachments
- [ ] `NoteArchiveService.Archive(noteId)`: sets `Note.ArchivedAt`; disables recurring `NoteReminder`s (sets `IsSent=true`, clears `RepeatPattern`); one-shot reminders keep their `IsSent` state and get hard-deleted by housekeeping 30 days later.
- [ ] Personal Tasks: CRUD, cron-style scheduling, completion tracking, ntfy on due date
- [ ] Calendar: event CRUD, CalDAV sync (Radicale/Nextcloud/Apple/Fastmail/generic), per-calendar color, .ics import/export, NLP quick-parse, timezone, agent-aware lookups
- [ ] Theme: 20+ bundled themes, custom palette (chat bubbles/sidebar/input/buttons/code highlight), font selection, density modes, live preview, server-side persistence per user
- [ ] User Tool Builder: HTML mini-apps, persistent key-value store, pin, session/global scope
- [ ] TUI coverage: notes list, tasks list, brain search, calendar view, theme picker

### Phase 10: Knowledge & Media 📚
> Goal: documents, gallery, research, image generation, comparison tools.

- [ ] Documents/Library: upload (PDF/MD/code/CSV/HTML), living documents (AI-assisted editor), version history + restore + compare, PDF rendering + form-fill + annotation, search, archive, export ZIP, AI tidy
- [ ] Gallery: upload, AI + user tags, EXIF extraction, albums + cover photos, favorites, deduplication by hash, bulk ops, ZIP download
- [ ] Gallery archive UI: archive/restore for GalleryImage, Album, and AlbumImage; archived items remain visible to owner (including in archived albums); hard-deletion handled by the future `HousekeepingBackgroundService` after the admin-configured `ArchivedItemRetentionDays` (default 730)
- [ ] Gallery editor: layer-based, opacity/visibility, inpainting, AI upscaling, style transfer, rotation, editor drafts
- [ ] Image generation: in-chat inline, document insert, gallery editor backend — admin-configured image providers, image tiers, `ImageUsageRecord`
- [ ] Deep Research: multi-step web search → AI synthesis → visual report, streaming progress, library, archive, spinoff — uses bundled SearXNG
- [ ] Compare: blind A/B model testing, vote (A/B/Tie), history — feature-flaggable
- [ ] Cookbook: model browser (HuggingFace/Ollama registry), hardware fitness scoring, external model server config, single-machine guide — feature-flaggable
- [ ] STT: speech-to-text input in chat
- [ ] TUI coverage: document viewer, gallery browse, research results view

### Phase 11: Mobile & Accessibility 📱
> Goal: Web UI works well on any device and is accessible to all users.

- [ ] Mobile component set (<768px): bottom navigation bar, board as card list, simplified chat panel, collapsible sections
- [ ] Tablet component set (768–1199px): dual-pane layout, side navigation, compact board columns
- [ ] Desktop verified: all Phase 3 keyboard shortcuts + full board maintained
- [ ] ARIA labels on all interactive elements
- [ ] Focus trap in all modals
- [ ] Screen reader tested (NVDA/VoiceOver basics)
- [ ] WCAG AA color contrast audit on all themes including custom palette
- [ ] PWA manifest: installable on mobile + desktop, per-route icons

### Phase 12: Polish & Enterprise ✨
> Goal: install is configurable, discoverable, and production-ready.

> ⚠️ **Pre-phase decision needed:** Feature flag storage — DB table or app config? Options: (a) `AppSetting` key-value table in PostgreSQL (admin can toggle at runtime via UI, no redeploy needed) — recommended; (b) `appsettings.json` / env vars (requires redeploy to change). Recommendation: DB-backed `AppSetting { Key, Value, UpdatedAt }` table so admin can toggle Compare, Cookbook, etc. from the admin UI without touching the server.

- [ ] Admin feature flags: disable Compare, Cookbook, Deep Research, Gallery per install
- [ ] Platform configuration: name, logo, primary color, branding
- [ ] Onboarding guide / interactive tour for new users
- [ ] Performance tuning: slow query analysis, pgvector index tuning, SignalR connection pooling, EF Core query optimization
- [ ] Load testing: concurrent users on board, SignalR under load
- [ ] Full documentation: setup guide, admin guide, user guide, API reference
- [ ] Backup guide: `pg_dump` schedule, restore procedure
