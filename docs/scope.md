# HydraForge — Scope / Statement of Work

> **Version:** 1.0
> **Date:** 2026-06-03

---

## Table of Contents

1. [Vision](#1-vision)
2. [The Problem](#2-the-problem)
3. [The Solution](#3-the-solution)
4. [User Personas](#4-user-personas)
5. [In-Scope Modules](#5-in-scope-modules)
6. [Out of Scope](#6-out-of-scope)
7. [Change Request Triggers](#7-change-request-triggers)
8. [Development Phases](#8-development-phases)

---

## 1. Vision

A **collaborative AI workspace and project management platform** for engineering teams — combining a TUI (Terminal User Interface) for power users, a Web UI for the broader team, and a Server as the single source of truth.

**HydraForge** — A multi-headed project forge. Each head is a project being forged in parallel. Like the legendary hydra, the platform has many heads — TUI, Web UI, AI agents — all connected to one body (the server), working simultaneously across the entire development lifecycle.

Core philosophy: **Server-authoritative. Terminal-native. Personal-first.**

- **Project space** — shared, members-only visibility
- **Personal space** — private per user (chats, memory, notes, tasks, calendar, gallery, documents)
- **Admin space** — operational only; admins cannot access user personal data

---

## 2. The Problem

| Problem | Impact |
|---|---|
| Jira/Linear/Asana are heavy, slow, browser-only | Developers context-switch out of the terminal to manage work |
| Tools enforce rigid workflows (column names, card types) | Teams can't tailor the tool to how they actually work |
| LLM access is centrally locked down or absent | AI can't be used natively in the project workflow |
| No TUI/terminal access | Velocity lost switching to a browser for every card update |
| Data trapped in SaaS silos | No self-hosting, no data ownership, vendor lock-in |
| LLM integration is bolted on | AI agents have no native home in existing tools |
| Chat with AI is disconnected from project context | AI has no awareness of board state, cards, or specs |

---

## 3. The Solution

Self-hosted, server-authoritative project management platform with dual interfaces:

- **TUI** — full feature parity, terminal-native, keyboard-driven, locks gracefully when server is unreachable
- **Web UI** — same data, mobile-first, responsive across mobile/tablet/desktop
- **Server** — .NET backend, single source of truth; manages all state in PostgreSQL, pushes real-time updates via SignalR, brokers LLM access (admin-configured, centrally managed)

All services start with `docker-compose up`. SearXNG enabled via `--profile search` or auto-detected when Deep Research is enabled.

---

## 4. User Personas

### 4.1 Terminal Power User (Primary)
Works in CLI all day (Neovim, tmux, git). Manages work without switching to a browser. Connects via VPN when remote — always online. Values speed, keyboard shortcuts, muscle memory. TUI locks clearly when server is unreachable.

### 4.2 Team Member (Secondary)
Uses Web UI for daily standup and progress checks. Adds comments, moves cards. Doesn't live in the terminal but needs visibility. Browser handles connectivity like any web app.

### 4.3 Manager / Stakeholder
Reads high-level status, views reports. Uses Web UI — focused on overview and audit views. Values clarity and real-time updates.

### 4.4 AI Agent (Emerging)
Reads and writes cards, specs, and plans via server API. **Proposes mutations — human confirms before any board state change.** Orchestrated by the server; admin-configured LLM providers. First-class actor with its own identity in audit logs.

---

## 5. In-Scope Modules

### Project Space (Shared, Members-Only)

| Module | Description |
|---|---|
| Board | Projects, renamable/reorderable columns, card CRUD + move (drag-and-drop / keyboard) |
| Cards | Title, description, type, assignees, checklist, comments, attachments, spec link, plan link |
| Card Types | Task, Bug, Epic, Spec, Idea |
| Card Dependencies | BlockedBy, Precedes, Relates — directed acyclic graph with cycle detection |
| Specs | Versioned markdown documents linked to cards |
| Plans | Versioned numbered markdown documents linked to specs/cards |
| Real-Time Sync | SignalR push to all connected members on every board mutation |
| Presence | Ephemeral green dot indicators on card/board (no DB persistence) |
| Project Context Snapshot | Template-rendered board state regenerated on every mutation; nightly AI narrative |
| Project Chats | Collapsible panel; fresh session per open; AI proposes card mutations (human confirms) |
| Audit Log | Immutable record of all mutations — actor, action, entity, before/after snapshot |
| Git Integration | Link project to any git remote; Git Agent for commits and PRs |
| Keyboard Navigation | Full keyboard control — vim-style in TUI, full shortcut set in Web UI |

### Personal Space (Private Per User)

| Module | Description |
|---|---|
| General Chats | Free-form sessions with folder organization (max 2 levels), search, model selection, file upload, vision, RAG |
| Agent Personality | User-defined system prompts, multiple per user, one default per user |
| Brain / Memory | Persistent store (6 categories); pgvector semantic search; auto-extract from chat; pin/inject/import/export |
| Notes | Pinnable, archivable notes; checklists; reminders with repeat patterns; ntfy ping; AI classification; image attachments |
| Personal Tasks | Per-user task list with cron-style scheduling, completion tracking, ntfy on due date |
| Calendar | Event CRUD; CalDAV sync (Radicale/Nextcloud/Apple/Fastmail/generic); NLP quick-parse; .ics import/export; timezone support |
| Documents / Library | Upload files (PDF/markdown/code/CSV/HTML); AI-assisted living documents; version history + restore + compare; PDF rendering + annotation; search; archive; export ZIP |
| Gallery | Upload; AI + user tags; EXIF metadata; albums + cover photos; favorites; deduplication by hash; bulk ops; ZIP download; layer-based editor with inpainting/upscaling |
| Theme | 20+ bundled themes; custom color palette; font selection; density modes; live preview; server-persisted per user |
| Prompt Presets | Reusable chat templates with groups |
| User Tool Builder | HTML mini-apps with persistent key-value store, pin, session/global scope |
| STT | Speech-to-text input in chat |

### Platform / Infrastructure

| Module | Description |
|---|---|
| Auth | JWT-based; admin seeded on first boot; bcrypt/Argon2 password hashing |
| LLM Infrastructure | ModelRouter, ContextCompressor, prompt caching, fallback chains, context window guard |
| Token Budget | Per-user daily/monthly token cap; enforcement at call time |
| Token & Image Usage | Per-call logging; admin dashboards by user/feature/model/period |
| Model Tiers | Economy / Standard / Premium — admin maps providers; features route to tiers |
| Image Generation | Admin-configured image providers; Economy/Standard/Premium tiers; in-chat + gallery editor + documents |
| Admin Dashboard | Users, all projects, LLM providers, system health, global audit log, feature flags |
| Deep Research | Multi-step web search → AI synthesis → visual report; streaming progress; bundled SearXNG |
| Compare | Blind A/B model testing; user vote history; feature-flaggable |
| Cookbook | Model browser (HuggingFace/Ollama registry); hardware fitness scoring; external model server config; single-machine guide; feature-flaggable |
| Notifications | In-app SignalR bell + ntfy push (per-user topic `hydraforge-{userId}`) |
| Error Handling | Global exception middleware → ProblemDetails RFC 7807 + correlationId; `Result<T, Error>` pattern throughout |
| Accessibility | WCAG AA; ARIA labels; keyboard navigation; mobile-first (3 breakpoints: <768px / 768–1199px / ≥1200px) |
| Deployment | Docker Compose one-command setup; SearXNG as optional profile |

---

## 6. Out of Scope

These items are **explicitly excluded**. Any request to include them is a change request.

| Item | Rationale |
|---|---|
| **Offline mode** | Rejected. Server connection always required. VPN for remote access. (D-3) |
| **Multi-tenant / SaaS deployment** | One install per team. No shared server for multiple organizations. |
| **SSO / OAuth / external auth providers** | Basic JWT auth only. Self-hosted; no paid auth infrastructure. |
| **Email notifications** | In-app bell + ntfy only. No SMTP, no email templating. |
| **Cross-project card dependencies** | Each project is isolated. Cards may only depend on cards within the same project. (FR-157) |
| **Conflict resolution / sync engine** | No offline mode = no divergent local states = no sync conflicts. (D-7) |
| **Model hosting on HydraForge server** | Cookbook connects to *external* model servers (vLLM, Ollama, llama.cpp). HydraForge does not install or host models. |
| **Public project visibility** | Projects are members-only. No public share links or anonymous read access. |
| **Native mobile apps** | Web UI is mobile-first (responsive). No iOS/Android native apps. |
| **View-only member role** | Owner and Member roles only. View-only role deferred post-MVP. (FR-46) |
| **WIP column limits** | Data model has `WipLimit` field reserved; enforcement is future scope. |
| **TTS (text-to-speech)** | Marked 🔜 in requirements. Not in any active phase. |
| **Handwritten signatures** | Marked 🔜 in requirements. Not in any active phase. |
| **Billing / subscription system** | Self-hosted only. No usage billing or payment processing. |
| **White-label multi-org theming** | Admin sets platform name/logo per install. Not designed for resale. |
| **External integrations** | No Jira sync, Confluence import, Slack/Teams integration, Zapier, webhooks. |

---

## 7. Change Request Triggers

Any of the following requires a formal scope discussion before work begins:

- Any feature not covered by FR-1…FR-194 in the [Functional Specification](functional-spec.md)
- Modifying the server-authoritative architecture (e.g. adding local state or offline capability)
- Adding a new LLM adapter beyond the three planned (OpenAI-compatible, Anthropic, Ollama)
- Adding a new notification channel beyond in-app + ntfy
- Any cross-project feature
- Any external system integration not listed in the current scope
- Replacing PostgreSQL with another database engine
- Extracting the TUI or Web UI into a separate repo or independent deployment
- Re-scoping any item from Section 6 (Out of Scope)
- Promoting any 🔜 (future) requirement into an active phase

---

## 8. Development Phases

Phases are **sequential**. Each phase goal must be met before the next begins.

| Phase | Name | Goal |
|---|---|---|
| 1 | Foundation | Running skeleton, auth, global error handling, migrations, CI. Nothing user-facing. |
| 2 | Project Space — API & Domain | All board/card business logic complete and tested via API. No UI. |
| 3 | Project Space — Web UI | Full board usable in browser. Feature-complete project workspace. |
| 4 | Project Space — TUI | Full board usable in terminal. Feature parity with Web UI board. |
| 5 | Multi-User, Notifications & Admin | Team collaboration end-to-end. Admin can manage the install. |
| 6 | LLM Infrastructure | All AI plumbing in place before any chat or AI feature is built on top. |
| 7 | Chat — General & Project | Full chat system built on top of Phase 6 LLM infrastructure. |
| 8 | AI Features — Project Space | AI agents operate on the board. Chat can create and mutate cards. |
| 9 | Personal Workspace | Brain/Memory, Notes, Tasks, Calendar, Theme, Tool Builder. |
| 10 | Knowledge & Media | Documents, Gallery, Deep Research, Image Generation, Compare, Cookbook, STT. |
| 11 | Mobile & Accessibility | Web UI works on any device. WCAG AA compliant. |
| 12 | Polish & Enterprise | Feature flags, platform branding, performance tuning, full documentation. |

For detailed phase task lists, see [functional-spec.md](functional-spec.md) §13 — Development Phase Checklists.

### Pre-Phase Decision Points

> **Before Phase 2:** Define `IFileStore` abstraction (`LocalFileStore` + `S3FileStore`). Default: local FS. S3 opt-in via env var.

> **Before Phase 6:** Pick nightly job scheduler. Recommendation: start with `.NET BackgroundService`, migrate to Hangfire if job visibility becomes important.

> **Before Phase 9:** Add `UserPreferences` and `UserTheme` entities to the data model before building any preference-related endpoints.

> **Before Phase 12:** Choose feature flag storage. Recommendation: `AppSetting { Key, Value, UpdatedAt }` DB table so admin can toggle flags at runtime without a redeploy.
