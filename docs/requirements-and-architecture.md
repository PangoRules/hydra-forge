# HydraForge — Requirements & Architecture Blueprint

> **Version:** 0.1 (Draft)
> **Date:** 2026-06-02
> **Status:** Requirements Gathering / Initial Architecture

---

## Table of Contents

1. [Vision Statement](#1-vision-statement)
2. [Project Name](#2-project-name)
3. [The Problem](#3-the-problem)
4. [The Solution](#4-the-solution)
5. [User Personas](#5-user-personas)
6. [Functional Requirements](#6-functional-requirements)
   - Core Board, Auth, Real-time, Error Handling
   - General Chats, Accessibility
   - Admin Scope
   - Brain/Memory, Calendar, Compare, Cookbook, Deep Research
   - Gallery, Documents/Library, Notes, Personal Tasks, Theme, Presets
7. [Non-Functional Requirements](#7-non-functional-requirements)
8. [Data Model](#8-data-model)
9. [System Architecture](#9-system-architecture)
10. [Technology Stack](#10-technology-stack)
11. [Repository Structure](#11-repository-structure)
12. [Development Phases](#12-development-phases) (12 phases)
13. [Open Questions](#13-open-questions)
14. [Glossary](#14-glossary)

---

## 1. Vision Statement

A **collaborative AI workspace and project management platform** for engineering teams that combines:

- A **TUI** (Terminal User Interface) for power users who want to manage work without leaving the terminal
- A **Web UI** for the broader team, visibility, and lightweight use — mobile-first, responsive across mobile / tablet / desktop
- A **Server** that is the single source of truth: state, real-time sync, LLM access, git integration

The core philosophy: **Server-authoritative, terminal-native, personal-first.**

- **Project space** is shared — board, cards, specs, plans visible to all project members
- **Personal space** is private — chats, brain/memory, notes, tasks, calendar, gallery, documents scoped to the individual user
- **Admin space** is operational — users, projects overview, LLM providers, health, audit logs. Admins do not access user personal data.
- Both TUI and Web UI require an active server connection. TUI locks gracefully when offline or off-VPN.

---

## 2. Project Name

**HydraForge** — A multi-headed project forge.

> *Each hydra head is a project being forged in parallel. Like the legendary hydra, this platform has many heads — TUI, Web UI, AI agents — all connected to one body (the server), working simultaneously across the entire development lifecycle. One head writes specs, another implements code, a third reviews. The forge never stops.*
## 3. The Problem

Engineering teams face friction when managing work across multiple systems:

| Problem | Impact |
|---|---|
| Jira/Linear/Asana are heavy, slow, browser-only | Developers must context-switch out of the terminal to manage work |
| Tools enforce rigid workflows (column names, card types, processes) | Teams can't tailor the tool to how they actually work |
| LLM access is centrally locked down or absent | Developers can't use AI natively in their project workflow |
| No TUI/terminal access | Developers lose velocity switching to a browser for every card update |
| Data is trapped in SaaS silos | No self-hosting, no data ownership, vendor lock-in |
| Too many clicks to do simple things | Lowers velocity, increases friction |
| LLM integration is bolted on | AI agents don't have a native home in existing tools |
| Chat with AI is disconnected from project context | AI has no awareness of board state, cards, or specs |

---

## 4. The Solution

This project is a **self-hosted, server-authoritative project management platform** with dual interfaces:

- **TUI (Terminal):** Full feature parity with the Web UI. Requires server connection (VPN when remote). Locks gracefully with a clear message when offline. Designed for developers who live in the terminal.
- **Web UI (Browser):** Same data, same features. Designed for visibility, sharing, and lightweight interaction.
- **Server:** .NET backend — single source of truth. Manages all state in PostgreSQL, pushes real-time updates via SignalR (WebSockets), and brokers LLM access (admin-configured, centrally managed).

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 LAN / VPN                                  │
│                                                                  │
│   ┌──────────────┐     ┌──────────────┐     ┌──────────────┐   │
│   │ Workstation A │     │ Workstation B │     │ Remote Dev   │   │
│   │ ┌──────────┐  │     │ ┌──────────┐  │     │ (via VPN)    │   │
│   │ │  TUI 🔥  │  │     │ │  TUI 🔥  │  │     │ ┌──────────┐ │   │
│   │ └──────────┘  │     │ └──────────┘  │     │ │  TUI 🔥  │ │   │
│   │ ┌──────────┐  │     │ ┌──────────┐  │     │ └──────────┘ │   │
│   │ │  Web UI  │  │     │ │  Web UI  │  │     │ ┌──────────┐ │   │
│   │ └──────────┘  │     │ └──────────┘  │     │ │  Web UI  │ │   │
│   └──────┬───────┘     └──────┬───────┘     │ └──────────┘ │   │
│          │                    │              └──────┬───────┘   │
│          └────────────┬───────┴─────────────────────┘           │
│                       │                                          │
│              ┌────────▼────────┐  ┌──────────────┐              │
│              │  🧠 Server      │  │  📦 Git      │              │
│              │  .NET + SignalR  │  │  Remote      │              │
│              │  - REST API      │  │  (GitHub/    │              │
│              │  - SignalR hubs  │  │   GitLab/    │              │
│              │  - LLM broker    │  │   self-host) │              │
│              └────────┬────────┘  └──────────────┘              │
│                       │                                          │
│              ┌────────▼────────┐  ┌──────────────┐              │
│              │  🐘 PostgreSQL  │  │  🔍 SearXNG  │              │
│              │  (all state)    │  │  (optional   │              │
│              └─────────────────┘  │   profile)   │              │
│                                   └──────────────┘              │
└─────────────────────────────────────────────────────────────────┘

> All services run via `docker-compose up`. SearXNG enabled with `--profile search` or auto-detected if Deep Research is enabled.
```

---

## 5. User Personas

### 5.1 The Terminal Power User (Primary)
- Works in CLI all day (Neovim, tmux, git)
- Wants to manage work without switching to a browser
- Connects to the server via VPN when remote — always online
- Values speed, keyboard shortcuts, muscle memory
- TUI locks with a clear "not connected" message when server is unreachable

### 5.2 The Team Member (Secondary)
- Uses Web UI for daily standup, checking progress
- Adds comments, moves cards
- Doesn't live in the terminal but needs visibility
- Browser handles connectivity the same way as any web app

### 5.3 The Manager / Stakeholder
- Reads high-level status, views reports
- Values clarity, real-time updates
- Uses Web UI — same app, focused on overview and audit views

### 5.4 The AI Agent (Emerging)
- LLM can read/write cards, specs, plans programmatically via server API
- Proposes mutations — human confirms before any board state change
- Orchestrated by the server; admin-configured LLM providers
- Treated as a first-class actor with its own identity in audit logs

> **Persona rationale (confirmed):** Terminal Power User drives the TUI. Team Member drives the Web UI. Manager/Stakeholder uses a subset of the Web UI (overviews, audit). AI Agent is a first-class actor. All require server connectivity — no persona has an offline mode.

---

## 6. Functional Requirements

> ✅ = Confirmed | ❓ = Needs discussion | 🔜 = Future

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
| FR-18 | Real-time: Server pushes updates via SignalR (WebSocket) | ✅ |
| FR-19 | LLM Integration: AI agent can interact with cards, specs, plans | ✅ |
| FR-20 | LLM Integration: Server brokers LLM access (company-paid) | ✅ |
| FR-21 | Auth: Basic authentication (no SSO — keep it open, self-hosted) | ✅ |
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
| FR-42 | ProjectContextSnapshot: `TemplateContent` regenerated on every board mutation (instant, no LLM). `AiNarrative` generated nightly by scheduled job (AI narrative of day's activity). Chat always injects TemplateContent. | ✅ |
| FR-43 | Project visibility: members-only; non-members cannot see project exists | ✅ |
| FR-44 | Admin sees all projects regardless of membership | ✅ |
| FR-45 | Any authenticated user can create a project; creator becomes owner | ✅ |
| FR-46 | ProjectMember roles: Owner, Member (view-only deferred to post-MVP) | ✅ |
| FR-47 | Notification rules: card move → assignees, comment → watchers, @mention → user, project events → all members | ✅ |
| FR-48 | CardWatcher auto-created when user comments on or is assigned to a card | ✅ |
| FR-49 | LLM providers: admin-configured only; users select from available providers | ✅ |
| FR-50 | LlmProvider pluggable via ILlmClient; ship OpenAI-compat, Anthropic, Ollama adapters | ✅ |
| FR-51 | AI agent personality: user-definable system prompt, multiple per user, one default | ✅ |
| FR-52 | Presence indicators: green dot on card/board via SignalR PresenceHub (ephemeral) | ✅ |
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

### General Chats (Personal, Non-Project)

| # | Requirement | Status |
|---|---|---|
| FR-69 | Users can create free-form chats not tied to any project | ✅ |
| FR-70 | Free-form chats organized in personal folders (max 2 levels) | ✅ |
| FR-71 | Chats sorted by recent activity; filterable/searchable by folder | ✅ |
| FR-72 | Chat search across all user's chats (title + message content) | ✅ |
| FR-73 | All chat features available outside projects: model selection, file upload, vision, RAG | ✅ |

### Accessibility & Responsive Design

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

### Admin Scope

| # | Requirement | Status |
|---|---|---|
| FR-82 | Admin dashboard: users, all projects overview, LLM providers, system health, global audit log | ✅ |
| FR-83 | Admin can manage users: create, disable, reset password, assign admin role | ✅ |
| FR-84 | Admin can see and manage all projects regardless of membership | ✅ |
| FR-85 | Admin can configure feature flags: disable Compare, Cookbook, Deep Research, Gallery per install | ✅ |
| FR-86 | Admin CANNOT access user personal data: chats, brain/memory, notes, tasks, calendar, gallery, documents | ✅ |
| FR-87 | Admin can configure system: SearXNG URL, ntfy server, LLM providers, platform branding | ✅ |

### Brain / Memory

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

### Calendar

| # | Requirement | Status |
|---|---|---|
| FR-96 | Per-user calendar with event CRUD (title, description, start/end, all-day, recurrence) | ✅ |
| FR-97 | CalDAV sync: Radicale, Nextcloud, Apple Calendar, Fastmail, generic CalDAV servers | ✅ |
| FR-98 | Per-calendar color customization | ✅ |
| FR-99 | .ics import/export | ✅ |
| FR-100 | Natural language → event quick-parse ("Meeting tomorrow at 3pm" → creates event) | ✅ |
| FR-101 | Timezone support per user | ✅ |
| FR-102 | Agent-aware: AI can query/suggest calendar events in chat context | ✅ |

### Compare

| # | Requirement | Status |
|---|---|---|
| FR-103 | Blind A/B model comparison: same prompt sent to two models, responses hidden until vote | ✅ |
| FR-104 | Vote on winner (A / B / Tie), per-user comparison history | ✅ |
| FR-105 | Feature-flaggable per install — admin can disable for enterprise deployments | ✅ |

### Cookbook

| # | Requirement | Status |
|---|---|---|
| FR-106 | Model browser: search and browse models (Hugging Face registry, Ollama library) | ✅ |
| FR-107 | Hardware fitness scoring: given GPU/RAM specs, score which models fit (llmfit-style) | ✅ |
| FR-108 | Cookbook connects to an external model server on the network (vLLM, Ollama, llama.cpp) — does NOT install models on HydraForge server | ✅ |
| FR-109 | Serve configuration recommendations: quantization, context window, batch size for given hardware | ✅ |
| FR-110 | For single-machine setups: guide to run model server alongside HydraForge on same host | ✅ |
| FR-111 | Feature-flaggable per install | ✅ |

### Deep Research

| # | Requirement | Status |
|---|---|---|
| FR-112 | Per-user deep research sessions: multi-step web search → AI synthesis → visual report | ✅ |
| FR-113 | Live progress streaming via SignalR during research | ✅ |
| FR-114 | Research library: history, archive, spinoff from existing session | ✅ |
| FR-115 | Hide/unhide images in reports | ✅ |
| FR-116 | Web search via SearXNG — bundled as optional docker-compose service; admin can override with external instance URL via system settings | ✅ |

### Gallery

| # | Requirement | Status |
|---|---|---|
| FR-117 | Per-user image gallery: upload, view, manage images | ✅ |
| FR-118 | AI + user tags, EXIF metadata extraction (camera, location, date) | ✅ |
| FR-119 | Albums with cover photos, favorites, deduplication by hash | ✅ |
| FR-120 | Bulk operations (tag, deduplicate), ZIP download | ✅ |
| FR-121 | Gallery editor: layer-based editing, opacity/visibility, inpainting, AI upscaling, rotation | ✅ |
| FR-122 | Editor drafts: save in-progress edits, resume later | ✅ |

### Documents / Library

| # | Requirement | Status |
|---|---|---|
| FR-123 | Per-user document library: upload and manage files (PDF, markdown, code, CSV, HTML) | ✅ |
| FR-124 | Living documents: AI-assisted editor with version history + restore + compare | ✅ |
| FR-125 | PDF rendering, form-fill detection, AI-powered annotation filling | ✅ |
| FR-126 | Document search by title and content | ✅ |
| FR-127 | Archive, export as ZIP | ✅ |
| FR-128 | AI tidy: keep/junk classification | ✅ |
| FR-129 | Syntax highlighting per language in code documents | ✅ |

### Notes

| # | Requirement | Status |
|---|---|---|
| FR-130 | Per-user notes: create, pin, archive, checklists with toggle completion | ✅ |
| FR-131 | Reminders with custom repeat patterns (daily, weekly, monthly, custom cron) | ✅ |
| FR-132 | ntfy ping on reminder trigger | ✅ |
| FR-133 | AI classification of notes (auto-tag, summary) | ✅ |
| FR-134 | Drag reorder, image attachments | ✅ |

### Personal Tasks

| # | Requirement | Status |
|---|---|---|
| FR-135 | Per-user personal tasks separate from project board cards | ✅ |
| FR-136 | Task reminders with cron-style scheduling | ✅ |
| FR-137 | Mark complete/incomplete, ntfy notification on due date | ✅ |

### Theme

| # | Requirement | Status |
|---|---|---|
| FR-138 | Per-user theme: select from 20+ bundled themes | ✅ |
| FR-139 | Custom color palette builder: chat bubbles, sidebar, input, buttons, code highlight, toggles | ✅ |
| FR-140 | Font selection: monospace, sans-serif, serif, custom | ✅ |
| FR-141 | Density modes: comfortable, compact, spacious | ✅ |
| FR-142 | Live preview before applying | ✅ |
| FR-143 | Theme persists server-side per user (not just local storage) | ✅ |

### Presets & Tools

| # | Requirement | Status |
|---|---|---|
| FR-144 | Prompt presets: reusable templates with groups, per-user | ✅ |
| FR-145 | User Tool Builder: create HTML mini-apps with persistent key-value store, pin, session or global scope | ✅ |
| FR-146 | STT (speech-to-text) input in chat | ✅ |
| FR-147 | TTS (text-to-speech) output with cache | 🔜 |
| FR-148 | Signatures: draw and save handwritten signatures (PNG/SVG), encrypted at rest, reuse in documents | 🔜 |

### Card Dependencies

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

### Chat → Card Creation

| # | Requirement | Status |
|---|---|---|
| FR-160 | Cards can be created manually in both Web UI and TUI without AI involvement | ✅ |
| FR-161 | After manual card creation, user can optionally open chat to ask AI to review, enhance, or suggest dependencies | ✅ |
| FR-162 | From project/card chat, AI can propose creating one or multiple cards in a single interaction | ✅ |
| FR-163 | AI card proposals include: title, type, column, assignee (optional), description, dependencies (optional) | ✅ |
| FR-164 | Bulk card creation: AI proposes all cards at once as a confirmation list — user approves or edits the full batch, not one at a time | ✅ |
| FR-165 | When creating from a card's chat panel, newly created cards auto-link to the source card as `Relates` unless user specifies otherwise | ✅ |
| FR-166 | AI uses ProjectContextSnapshot card index to propose meaningful dependencies (e.g. "blocked by #42") — never fabricates card IDs | ✅ |

### Full Keyboard Navigation

| # | Requirement | Status |
|---|---|---|
| FR-167 | Web UI fully keyboard navigable — all actions reachable without mouse | ✅ |
| FR-168 | Web UI keyboard shortcuts: navigate board columns, select/open cards, move cards between columns, trigger primary actions | ✅ |
| FR-169 | TUI: full keyboard navigation, vim-style movement (`h/j/k/l`), no mouse required | ✅ |
| FR-170 | Both interfaces expose a keyboard shortcut reference (Web: modal/overlay, TUI: `?` command) | ✅ |

### Model Routing & Token Management

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

#### Default Tier Assignments (admin-configurable, these are install defaults)

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

### Image Generation

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

## 7. Non-Functional Requirements

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

---

## 8. Data Model

### Entity Relationship (Textual)

```
User (1) ──┬── (N) ProjectMember
           ├── (N) ChatSession
           ├── (N) ChatFolder
           ├── (N) AgentPersonality
           ├── (N) MemoryEntry
           ├── (N) Note
           ├── (N) PersonalTask
           ├── (N) CalendarSource ──── (N) CalendarEvent
           ├── (N) Document ──── (N) DocumentVersion
           │                └── (N) DocumentChunk        ← RAG embeddings
           ├── (N) GalleryImage
           ├── (N) Album ──── (N) GalleryImage (via AlbumImage)
           ├── (1) UserTokenBudget
           ├── (N) TokenUsageRecord
           └── (N) ImageUsageRecord

Project (1) ──┬── (N) Column
              ├── (N) Card
              ├── (N) Spec ──── (N) SpecVersion
              ├── (N) Plan ──── (N) PlanVersion
              ├── (N) AuditLogEntry
              ├── (N) ProjectMember
              ├── (1) ChatFolder          ← auto-created on project creation
              └── (1) ProjectContextSnapshot

Column (1) ── (N) Card

Card (1) ──┬── (N) Comment
           ├── (N) ChecklistItem
           ├── (N) CardAssignee           ← replaces AssigneeIds array
           ├── (N) Attachment
           ├── (N) ChildCard (self-referencing via ParentCardId)
           ├── (N) CardChatLink
           ├── (N) CardWatcher
           ├── (N) CardRelationship (as source)
           ├── (N) CardRelationship (as target)
           ├── (1) Spec? (optional link)
           └── (1) Plan? (optional link)

ChatFolder (1) ──┬── (N) ChatFolder (self-referencing, max depth 2)
                 └── (N) ChatSession

ChatSession (1) ──┬── (N) ChatMessage
                  ├── (N) CardChatLink
                  └── (1) Project? (when in project folder)

Note (1) ──┬── (N) NoteReminder
           └── (N) NoteImageAttachment

AuditLogEntry (N) ── (1) Project
Notification (N) ── (1) User
LlmProvider — global, admin-managed provider connection
ProviderModelConfig — global, admin-managed model catalog row for one provider/model pairing
FeatureRoutingConfig — future routing policy row per feature, derived from default tier assignment requirements
```

### Detailed Entity Definitions

#### Project

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| Name | string | Project name |
| Description | string | Short description |
| GitRemoteUrl | string? | Optional git remote URL |
| GitProvider | string? | e.g. "github", "gitlab", "gitea", "self-hosted" |
| Columns | Column[] | Nav property — ordered list of columns (FK: Column.ProjectId) |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### Column

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| ProjectId | Guid | FK to Project |
| Name | string | e.g. "Backlog", "In Dev", "Done" |
| Position | int | Ordering (0-based) |
| WipLimit | int? | Optional WIP limit (future) |
| Color | string? | Hex color for visual distinction |

#### Card

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| CardNumber | int | Sequential per project (e.g. #1, #42) — unique within project |
| ProjectId | Guid | FK to Project |
| ColumnId | Guid | FK to Column |
| ParentCardId | Guid? | FK to parent card (epic → child) |
| SpecId | Guid? | FK to Spec (optional linked document) |
| PlanId | Guid? | FK to Plan (optional linked document) |
| Title | string | |
| Description | string | Markdown content |
| Type | CardType | Task / Bug / Epic / Spec / Idea |
| Position | int | Order within column |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| MovedAt | DateTime | When it last changed column |

#### CardAssignee

| Field | Type | Description |
|---|---|---|
| CardId | Guid | FK to Card |
| UserId | Guid | FK to User |
| AssignedAt | DateTime | |
| AssignedByUserId | Guid | Who made the assignment |

#### CardType (enum)
`Task`, `Bug`, `Epic`, `Spec`, `Idea`

#### CardRelationship

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| SourceCardId | Guid | FK to Card — the card that has the dependency |
| TargetCardId | Guid | FK to Card — the card being depended on |
| Type | RelationshipType | `BlockedBy`, `Precedes`, `Relates` |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | Who established the relationship (human or AI agent) |
| ArchivedAt | DateTime? | Set when source or target card is archived — soft delete, retained for audit |

> Both cards must belong to the same project. Application layer validates acyclic graph on every insert.

#### RelationshipType (enum)
`BlockedBy`, `Precedes`, `Relates`

> Future: extend to full DAG with critical path calculation without schema changes — the directed graph is already represented.

#### Comment

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| AuthorId | Guid | |
| Content | string | Markdown |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### ChecklistItem

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| Text | string | |
| IsCompleted | bool | |
| Position | int | |
| AssignedTo | Guid? | FK to User (optional per-item assignee) |

#### Attachment

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| FileName | string | Original filename |
| Size | long | Bytes |
| ContentType | string | MIME type |
| StoragePath | string | Local FS path or S3 key |
| UploadedByUserId | Guid | FK to User |
| CreatedAt | DateTime | |

#### Spec

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project |
| Title | string | Display name, e.g. "Auth Module Spec" |
| Content | string | Current markdown content |
| Version | int | Increments on each edit |
| CreatedByUserId | Guid | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### SpecVersion

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| SpecId | Guid | FK to Spec |
| Content | string | Full markdown snapshot at this version |
| Version | int | Matches Spec.Version at time of snapshot |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | Who saved this version |

#### Plan

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project |
| Title | string | Display name, e.g. "Auth Implementation Plan" |
| Content | string | Current markdown (numbered steps) |
| Version | int | Increments on each edit |
| CreatedByUserId | Guid | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### PlanVersion

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| PlanId | Guid | FK to Plan |
| Content | string | Full markdown snapshot at this version |
| Version | int | Matches Plan.Version at time of snapshot |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | Who saved this version |

#### AuditLogEntry

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project |
| ActorId | Guid | Who performed the action |
| Action | string | Human-readable, e.g. "Moved card 'Fix login' from Backlog to In Dev" |
| EntityType | string | "Card", "Column", "Spec", "Plan" |
| EntityId | Guid | |
| OldValue | string? | JSON snapshot before |
| NewValue | string? | JSON snapshot after |
| Timestamp | DateTime | |

#### ProjectMember

| Field | Type | Description |
|---|---|---|
| ProjectId | Guid | FK to Project |
| UserId | Guid | FK to User |
| Role | MemberRole | `Owner` / `Member` |
| JoinedAt | DateTime | |

#### ChatFolder

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| Name | string | Display name |
| OwnerId | Guid | FK to User |
| ParentFolderId | Guid? | FK to self — max depth 2 enforced at app layer |
| ProjectId | Guid? | Set when auto-created by project; null for free-form folders |
| CreatedAt | DateTime | |
| ArchivedAt | DateTime? | Set on project archive |

#### ChatSession

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| Title | string | |
| OwnerId | Guid | FK to User |
| FolderId | Guid? | FK to ChatFolder |
| ProjectId | Guid? | FK to Project when session is in a project folder |
| IsShared | bool | True for project-folder chats (visible to all members read-only) |
| CreatedAt | DateTime | |
| ArchivedAt | DateTime? | |

#### ChatMessage

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| SessionId | Guid | FK to ChatSession |
| Role | MessageRole | `User` / `Assistant` / `System` |
| Content | string | Message text (markdown for assistant, plain for user) |
| InputTokens | int | Tokens in this request (0 for user messages) |
| OutputTokens | int | Tokens in this response (0 for user messages) |
| CachedTokens | int | Prompt cache hits for this call |
| ModelName | string? | Model used (null for user messages) |
| CreatedAt | DateTime | |

#### MessageRole (enum)
`User`, `Assistant`, `System`

#### CardChatLink

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| ChatSessionId | Guid | FK to ChatSession |
| OwnerId | Guid | FK to User (chat owner) |
| Summary | string | Auto-generated summary of what was discussed |
| CreatedAt | DateTime | |

#### CardWatcher

| Field | Type | Description |
|---|---|---|
| CardId | Guid | FK to Card |
| UserId | Guid | FK to User |
| AddedAt | DateTime | Auto-added on comment or assignment |

#### ProjectContextSnapshot

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project (unique) |
| TemplateContent | string | Template-rendered board state: columns, card index (id+number+title+column+type), open blockers, recent moves. Regenerated on every board mutation — no LLM call, instant. |
| AiNarrative | string? | AI-generated end-of-day summary: "5 cards moved, 2 blockers resolved, PR created for #42." Generated nightly by scheduled job. Nullable — null until first nightly run. |
| TemplateGeneratedAt | DateTime | Set on every board mutation |
| AiNarrativeGeneratedAt | DateTime? | Set by nightly job |

#### AgentPersonality

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Name | string | e.g. "Senior Dev Mode", "Concise" |
| SystemPrompt | string | Injected at chat start after project context |
| IsDefault | bool | One per user |

#### LlmProvider

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| Name | string | Display name, e.g. "Company OpenAI" |
| BaseUrl | string | e.g. `https://api.openai.com/v1` |
| ApiKeyEncrypted | string | Encrypted at rest |
| Models | string[] | Cached from probe |
| IsEnabled | bool | Admin toggle |
| AdapterType | string | `openai-compat` / `anthropic` / `ollama` / `diffusers` / `comfyui` |
| ProviderType | ProviderType | `Text` / `Image` / `Both` |
| Tier | ModelTier | `Economy` / `Standard` / `Premium` |
| FallbackProviderId | Guid? | Used if this provider is rate-limited or unavailable |

#### ProviderType (enum)
`Text`, `Image`, `Both`

#### ModelTier (enum)
`Economy`, `Standard`, `Premium`

#### ProviderModelConfig

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| ProviderId | Guid | FK to `LlmProvider`; identifies which provider offers this model |
| ModelId | string | Provider/API-facing model identifier, e.g. `gpt-4.1`, `claude-sonnet-4`, `llama3.3` |
| Name | string | Human-friendly display name shown to admins and users |
| Tier | ModelTier | Admin-assigned tier: `Economy`, `Standard`, or `Premium` |
| PricePerToken | decimal? | Optional pricing metadata for cost estimation and usage reporting |
| MaxTokens | int? | Optional model token limit used by context-window guard and routing decisions |
| IsEnabled | bool | Admin toggle; disabled models remain configured but are not available for routing |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

> `ProviderModelConfig` is the configured model catalog. It is distinct from per-feature routing policy: one provider can expose many models, and each model can have its own tier, limit, pricing, and enablement state.

#### FeatureRoutingConfig (future schema)

| Field | Type | Description |
|---|---|---|
| Feature | AiFeature | e.g. `PersonalChat`, `ProjectChat`, `DeepResearch`, `AgentPipeline`, `MemoryExtraction` |
| DefaultTier | ModelTier | Install default tier for the feature |
| MaxUserTier | ModelTier? | Ceiling for user overrides — null means locked to default |

> The Phase 1 foundation migration creates `ProviderModelConfig`. `FeatureRoutingConfig` remains planned for the model-routing work that implements FR-172 and FR-173.

#### AiFeature

Shared domain enum for AI-driven product features. It is used to connect routing policy, token/image usage records, and admin usage dashboards without relying on free-form feature strings.

Initial values: `PersonalChat`, `ProjectChat`, `DeepResearch`, `AgentPipeline`, `MemoryExtraction`, `NotesClassification`, `DocumentEditing`, `CardReview`, `ImageChat`, `ImageDocument`, `ImageGalleryEditor`.

#### UserTokenBudget

| Field | Type | Description |
|---|---|---|
| UserId | Guid | FK to User |
| DailyLimit | int? | Token cap per day (null = unlimited) |
| MonthlyLimit | int? | Token cap per month (null = unlimited) |

#### TokenUsageRecord

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | Who triggered the call |
| Feature | AiFeature | Which feature made the call |
| ProviderId | Guid | Which LLM provider was used |
| ModelName | string | Exact model name |
| InputTokens | int | |
| OutputTokens | int | |
| CachedTokens | int | Prompt cache hits (reduces effective cost) |
| ProjectId | Guid? | Set if call was in project context |
| PipelineRunId | Guid? | Groups multi-turn agent pipeline calls |
| Timestamp | DateTime | |

#### ImageUsageRecord

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | Who triggered the generation |
| Feature | AiFeature | `ImageChat`, `ImageGalleryEditor`, or `ImageDocument` |
| ProviderId | Guid | Which image provider was used |
| ModelName | string | Exact model name (e.g. `dall-e-3`, `flux-pro`) |
| ImageCount | int | Number of images generated |
| Resolution | string | e.g. `1024x1024`, `1920x1080` |
| Cost | decimal | Recorded estimated/provider-reported generation cost |
| ProjectId | Guid? | Set if triggered in project context |
| CreatedAt | DateTime | |

> One row per image generation request, not per generated image. Used for admin usage dashboards, per-feature cost attribution, and provider billing reconciliation.

#### Notification

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | Recipient |
| Message | string | Human-readable text |
| CardId | Guid? | |
| ProjectId | Guid? | |
| IsRead | bool | |
| CreatedAt | DateTime | |

#### User

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| Username | string | Unique, case-insensitive |
| PasswordHash | string | bcrypt / Argon2 |
| IsAdmin | bool | Admin role |
| IsDisabled | bool | Admin can disable access without deleting |
| CreatedAt | DateTime | |
| LastLoginAt | DateTime? | |

---

### Personal Space Entities

#### MemoryEntry

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Category | MemoryCategory | `Fact`, `Preference`, `Identity`, `Event`, `Contact`, `Instruction` |
| Content | string | The memory text |
| Embedding | vector(1536) | pgvector — for semantic search |
| IsPinned | bool | Pinned memories injected first in context |
| Source | string? | e.g. "auto-extracted", "manual", session ID |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### MemoryCategory (enum)
`Fact`, `Preference`, `Identity`, `Event`, `Contact`, `Instruction`

#### Note

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Content | string | Markdown (checklist items inline as `- [ ]`) |
| IsPinned | bool | |
| IsArchived | bool | |
| SortOrder | int | Drag-reorder position |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### NoteReminder

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| NoteId | Guid | FK to Note |
| TriggerAt | DateTime | Next trigger time |
| RepeatPattern | string? | Cron expression or `daily`/`weekly`/`monthly` (null = one-time) |
| LastTriggeredAt | DateTime? | |

#### NoteImageAttachment

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| NoteId | Guid | FK to Note |
| FilePath | string | Storage path |
| CreatedAt | DateTime | |

#### PersonalTask

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Title | string | |
| Description | string? | |
| IsCompleted | bool | |
| DueAt | DateTime? | |
| CronExpression | string? | Recurring schedule (null = one-time) |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### CalendarSource

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Name | string | Display name |
| Color | string | Hex color |
| CalDavUrl | string? | CalDAV endpoint (null = local-only calendar) |
| CalDavUsername | string? | |
| CalDavPasswordEncrypted | string? | |
| LastSyncAt | DateTime? | |

#### CalendarEvent

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| CalendarSourceId | Guid | FK to CalendarSource |
| ExternalUid | string? | CalDAV UID for sync (null = local event) |
| Title | string | |
| Description | string? | |
| StartAt | DateTime | |
| EndAt | DateTime | |
| IsAllDay | bool | |
| RecurrenceRule | string? | iCal RRULE string |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### Document

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Title | string | |
| ContentType | string | `pdf` / `markdown` / `code` / `csv` / `html` |
| FilePath | string? | Storage path for uploaded binary files (PDF, etc.) |
| Content | string? | Text content for editable documents |
| Language | string? | Programming language for code documents |
| Version | int | Increments on each edit |
| IsArchived | bool | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

#### DocumentVersion

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| DocumentId | Guid | FK to Document |
| Content | string | Full content snapshot |
| Version | int | Matches Document.Version at time of snapshot |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | |

#### DocumentChunk

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User (for security scoping) |
| SourceType | string | `document` / `note` / `memory` |
| SourceId | Guid | FK to source entity |
| ChunkIndex | int | Order within source document |
| Content | string | ~500-token text chunk |
| Embedding | vector(1536) | pgvector — for RAG similarity search |
| CreatedAt | DateTime | |

> `DocumentChunk` powers RAG: at chat time, user message is embedded → similarity search on user's chunks → top-K results injected as context. Chunks regenerated when source document changes.

#### GalleryImage

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| FilePath | string | Storage path |
| OriginalFilename | string | |
| Hash | string | SHA-256 of file bytes — for deduplication |
| Size | long | Bytes |
| Width | int | |
| Height | int | |
| TakenAt | DateTime? | From EXIF |
| CameraModel | string? | From EXIF |
| Latitude | double? | From EXIF GPS |
| Longitude | double? | From EXIF GPS |
| IsFavorite | bool | |
| CreatedAt | DateTime | |

#### Album

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Name | string | |
| CoverImageId | Guid? | FK to GalleryImage |
| CreatedAt | DateTime | |

#### AlbumImage

| Field | Type | Description |
|---|---|---|
| AlbumId | Guid | FK to Album |
| ImageId | Guid | FK to GalleryImage |
| Position | int | Order within album |

#### ImageTag

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ImageId | Guid | FK to GalleryImage |
| Tag | string | |
| Source | TagSource | `User` / `AI` |

#### TagSource (enum)
`User`, `AI`

---

## 9. System Architecture

### 9.1 Layered Architecture (Clean Architecture)

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
│  │  - ProjectContextSnapshot      │        │
│  │  - ChatService                 │        │
│  │  - ModelRouter                 │        │
│  │  - ContextCompressor           │        │
│  │  - AuditService                │        │
│  │  - NotificationService         │        │
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
│  │  - SignalR messaging           │        │
│  └────────────────────────────────┘        │
└────────────────────────────────────────────┘
```

### 9.2 Real-Time Architecture (Online-Only)

Offline mode rejected (see D-3). TUI connects directly to the server over the network (VPN when remote). All mutations go through the API; SignalR pushes updates to all connected clients.

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

### 9.3 LLM Integration

```
TUI User: ──▶ Server ──▶ LLM Provider (OpenAI, Claude, etc.)
  "/agent write spec for card #42"     │
                                       │
Web UI User: ◀──────────────────────────┘
  (Result streamed back in real-time)
```

- Server is the only component that talks to LLMs
- Admin configures API keys centrally; users never touch credentials
- TUI and Web UI send requests to server, receive streamed responses

---

### 9.4 Error Handling Architecture

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

**ProblemDetails shape (every error response):**
```json
{
  "type": "https://hydraforge.io/errors/card-not-found",
  "title": "Card not found",
  "status": 404,
  "detail": "Card 'f3a1...' does not exist or you don't have access.",
  "correlationId": "req_7fKz9mXp"
}
```

**TUI error handling:**
- Connection lost → lock screen: `⚠ Server unreachable. Retrying... (correlationId: ...)` with exponential backoff
- API error → status bar error panel: shows `title` + `correlationId`, dismissible
- Never crashes to unhandled exception — all paths return to a stable TUI state

**Web UI error handling:**
- API error → typed toast: shows `detail` + `correlationId` copy button
- 401/403 → redirect to login / access denied page
- 5xx → "Something went wrong" banner with correlationId for support

**Structured logging (every request):**
```
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

**External service resilience — failures in these must never bring down core board:**

| Service | Failure behavior |
|---|---|
| LLM provider | Surface error in chat panel; board continues working |
| Git remote | Surface error in agent output; board continues working |
| ntfy | Log at Warning, skip notification; board continues working |
| PostgreSQL | 503 response; server cannot function without DB — fail loudly |

---

## 10. Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| **Server** | .NET 10 / C# | User's primary stack. Clean Architecture built-in. Great DI. |
| **TUI** | .NET + Spectre.Console | Same language as server. Rich terminal UI. Full keyboard support. |
| **Web UI** | Nuxt 3 + Vue 3 + Tailwind CSS | User's preference. Familiar, fast DX. |
| **Database** | PostgreSQL 16 + pgvector | MVCC handles concurrent multi-user writes. pgvector powers RAG and Brain/Memory semantic search. Native full-text search. EF Core Npgsql provider. |
| **Real-time** | SignalR (WebSocket + SSE fallback) | Built into ASP.NET Core. Battle-tested. Auto-fallback. |
| **Tests** | xUnit | Default .NET testing. No FluentAssertions (prone to deprecation). Use plain assertions. |
| **Containerization** | Docker + docker-compose | Single-command setup. Portable. |
| **Architecture** | Clean Architecture (DI, SOLID, CQRS) | Testable, maintainable, readable. |

---

## 11. Repository Structure (Monorepo)

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
│   │   ├── Hubs/             # SignalR hubs
│   │   ├── Middleware/
│   │   └── Program.cs
│   │
│   ├── HydraForge.Application/ # Use cases, services, DTOs
│   │   ├── Cards/
│   │   ├── Columns/
│   │   ├── Projects/
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
│   ├── HydraForge.Infrastructure/  # EF Core, PostgreSQL, LLM client, git
│   │   ├── Persistence/
│   │   ├── LlmClient/
│   │   ├── GitService/
│   │   └── SignalR/
│   │
│   ├── HydraForge.Tui/         # Spectre.Console TUI
│   │   ├── Commands/         # CLI commands (move, create, edit)
│   │   ├── Views/            # Screen rendering
│   │   └── Program.cs
│   │
│   └── web-ui/               # Nuxt 3 + Vue + Tailwind
│       ├── pages/
│       ├── components/
│       ├── composables/
│       ├── server/           # Nuxt server routes (proxy to .NET)
│       └── nuxt.config.ts
│
├── tests/
│   ├── HydraForge.Domain.Tests/
│   ├── HydraForge.Application.Tests/
│   ├── HydraForge.Infrastructure.Tests/
│   └── HydraForge.Tui.Tests/
│
└── docs/
    ├── ARCHITECTURE.md
    ├── SETUP.md
    └── CONTRIBUTING.md
```

---

## 12. Development Phases

### Phase 1: Foundation 🏗️
> Goal: running skeleton, auth, error handling infrastructure. Nothing user-facing yet.

- [ ] Scaffold monorepo: `HydraForge.Domain`, `HydraForge.Application`, `HydraForge.Infrastructure`, `HydraForge.Server`, `HydraForge.Tui`, `web-ui`
- [ ] Docker Compose: .NET server + PostgreSQL 16 + SearXNG (optional `--profile search`)
- [ ] EF Core + Npgsql: DbContext, all entities, auto-run migrations on startup
- [ ] PostgreSQL `pgvector` extension: enabled in migrations, `vector(1536)` columns on `MemoryEntry.Embedding` and `DocumentChunk.Embedding`
- [ ] Basic auth: user store, bcrypt/Argon2 hashing, JWT tokens, admin seeded on first run
- [ ] Global exception middleware: catch all unhandled exceptions → ProblemDetails RFC 7807
- [ ] `Result<T, Error>` pattern in Domain layer — typed error codes, no business logic exceptions
- [ ] Structured logging: `Microsoft.Extensions.Logging` + Serilog, severity-appropriate, correlationId on every request
- [ ] `/health` endpoint: server + DB + LLM provider connectivity
- [ ] Audit Log infrastructure: `AuditLogEntry` writes on every mutation
- [ ] CI/CD pipeline

### Phase 2: Project Space — API & Domain 📋
> Goal: all project/board business logic complete and tested via API. No UI yet.

> ⚠️ **Pre-phase decision needed:** File storage default — local FS or S3? Define `IFileStore` abstraction (`LocalFileStore` + `S3FileStore` implementations) and add storage config to app settings (`FILE_STORAGE_PROVIDER`, `FILE_STORAGE_PATH` / S3 credentials). Decide before implementing `Attachment` upload. Recommendation: default to local FS, S3 is opt-in via env var.

- [ ] Project CRUD + ProjectMember management (Owner / Member roles)
- [ ] Column CRUD + reordering + per-project default columns
- [ ] Card CRUD + move between columns + position ordering
- [ ] Card types: Task / Bug / Epic / Spec / Idea
- [ ] Epic → child card linking
- [ ] Checklists on cards (items, completion, assignee per item)
- [ ] Comments on cards + @mention extraction + CardWatcher auto-add
- [ ] File attachments on cards (local FS storage, S3-compatible abstraction)
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
- [ ] Admin: see and manage all projects regardless of membership
- [ ] Audit log viewer: filter by project, user, entity type, date range

### Phase 6: LLM Infrastructure 🔧
> Goal: all AI plumbing in place before any chat or AI feature is built on top.

> ⚠️ **Pre-phase decision needed:** Nightly job scheduler — pick one before implementing `ProjectContextSnapshot.AiNarrative` and any other scheduled work. Options: (a) `BackgroundService` (built-in .NET, simple, no UI) — recommended for MVP; (b) Hangfire (persistent jobs, retry, admin dashboard — useful if you want visibility into scheduled job runs); (c) Quartz.NET (full cron engine, more config). Recommendation: start with `BackgroundService`, migrate to Hangfire if job visibility becomes important.

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
- [ ] Personal Tasks: CRUD, cron-style scheduling, completion tracking, ntfy on due date
- [ ] Calendar: event CRUD, CalDAV sync (Radicale/Nextcloud/Apple/Fastmail/generic), per-calendar color, .ics import/export, NLP quick-parse, timezone, agent-aware lookups
- [ ] Theme: 20+ bundled themes, custom palette (chat bubbles/sidebar/input/buttons/code highlight), font selection, density modes, live preview, server-side persistence per user
- [ ] User Tool Builder: HTML mini-apps, persistent key-value store, pin, session/global scope
- [ ] TUI coverage: notes list, tasks list, brain search, calendar view, theme picker

### Phase 10: Knowledge & Media 📚
> Goal: documents, gallery, research, image generation, comparison tools.

- [ ] Documents/Library: upload (PDF/MD/code/CSV/HTML), living documents (AI-assisted editor), version history + restore + compare, PDF rendering + form-fill + annotation, search, archive, export ZIP, AI tidy
- [ ] Gallery: upload, AI + user tags, EXIF extraction, albums + cover photos, favorites, deduplication by hash, bulk ops, ZIP download
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

---

## 13. Open Questions

| # | Question | Status |
|---|---|---|
| Q-1 | Authentication — Basic? OAuth? SSO? | ✅ (Basic — self-hosted, no paid SSO) |
| Q-2 | Multi-tenant? (One server, many companies?) | ❌ No — fresh install per team |
| Q-4 | File attachments on cards? | ✅ Yes — local filesystem or S3-compatible storage |
| Q-5 | Notifications (email, push, in-app)? | ✅ In-app via SignalR bell + ntfy (open-source push, same as Odysseus). No email. Per-user ntfy topic: `hydraforge-{userId}`. |
| Q-6 | Epic vs regular card — subsumed into CardType enum? | ✅ (yes) |
| Q-7 | Should Plans be their own entity or just a card with type=Plan? | ✅ (own entity, linked) |
| Q-8 | LLM provider config — which providers to support initially? | ✅ Pluggable — ship OpenAI + Ollama adapters, user adds more |
| Q-9 | Offline capabilities — what exactly works? | ❌ Rejected — no offline mode (see D-3) |
| Q-10 | Conflict resolution strategy — Last-write-wins? Manual merge? CRDTs? | ❌ Rejected — no offline = no sync conflicts (see D-7) |

---

### Offline Capabilities (Q-9) — ❌ REJECTED

Offline mode was considered and rejected. HydraForge requires an active server connection at all times. Developers access the tool via VPN when remote — internet connectivity is always present in practice. The complexity of a local SQLite cache + sync engine + conflict resolution outweighs the benefit. See D-3 and D-7.

### Tools Clarification (Problem Table — "company-controlled")

| What "tools are company-controlled" means | How HydraForge solves it |
|---|---|
| LLM provider locked to whatever IT chose | Server brokers centrally-managed keys, but users access via TUI/Web UI natively |
| Column workflow is rigid | Each HydraForge project defines its own pipeline freely |
| Card types are limited to Task/Bug/Story | HydraForge has Spec, Idea, Epic, Plan as first-class types |
| Only web interface available | TUI is a first-class, feature-parity citizen |
| Data locked in SaaS | Self-hosted PostgreSQL — you own your data |
| No automation customization | Agent orchestration is built into the platform |

### Conflict Resolution Strategy (Q-10) — ❌ REJECTED

No conflict resolution needed. No offline mode = no divergent local states = no sync conflicts. See D-7. Concurrent edits from multiple live users are handled by standard last-write-wins at the DB level, which is sufficient for a team tool where all clients are connected in real-time.

---

### User Personas Summary (Confirmed)

| Persona | Interface | Why |
|---|---|---|
| **Terminal Power User** | TUI | Lives in terminal. Needs speed, keyboard, no browser context-switching. |
| **Team Member** | Web UI | Needs visibility, drag-and-drop, lightweight interaction. |
| **Manager / Stakeholder** | Web UI (overview) | High-level status, reports, clarity. Same web UI, focused views. |
| **AI Agent** | API / Server | Reads/writes cards, specs, plans. Is a first-class actor with its own identity. |

---

## 14. Glossary

| Term | Definition |
|---|---|
| **Project** | Top-level container. One repo = one project. |
| **Column** | A stage in the workflow pipeline. Customizable name and order. |
| **Card** | A unit of work — task, bug, epic, spec, or idea. |
| **Spec** | A long-form markdown document describing a feature in detail. |
| **Plan** | A step-by-step markdown execution plan. |
| **Audit Log** | Immutable record of all changes, with before/after snapshots. |
| **TUI** | Terminal User Interface (Spectre.Console) |
| **SignalR** | ASP.NET Core library for real-time WebSocket communication. |
| **Clean Architecture** | Layered architecture: Domain → Application → Infrastructure → Presentation. Dependencies point inward. |
| **Server-Authoritative** | All state lives on the server (PostgreSQL). Both TUI and Web UI require an active server connection. TUI locks gracefully when unreachable. |
| **WIP Limit** | (Future) Maximum number of cards allowed in a column at once. |
| **Personal Space** | User-private modules: chats, brain/memory, notes, tasks, calendar, gallery, documents, theme. Not visible to admin. |
| **Project Space** | Shared team modules: board, cards, specs, plans, git. Visible to project members. Admin can see all projects. |
| **Admin Space** | Operational view: users, all projects, LLM providers, system health, feature flags. Cannot access user personal data. |
| **Brain / Memory** | Per-user persistent memory store. Injected into chat context. Backed by pgvector for semantic search. |
| **Compare** | Blind A/B model testing per user. Feature-flaggable (admin can disable for enterprise). |
| **Cookbook** | Model browser + hardware fitness scoring. Connects to external model server — does not install models on HydraForge. Feature-flaggable. |
| **Deep Research** | Multi-step web search → AI synthesis → visual report. Requires SearXNG instance. Per-user sessions. |
| **ntfy** | Open-source push notification service. Used for reminders, card assignments, mentions. Per-user topic. |
| **pgvector** | PostgreSQL extension for vector similarity search. Powers Brain/Memory semantic search without a separate ChromaDB. |
| **Feature Flag** | Admin toggle to enable/disable optional modules (Compare, Cookbook, etc.) per install. |
| **Mobile-First** | Web UI designed starting from mobile breakpoint, enhanced progressively for tablet and desktop. Three component sets. |
| **CardRelationship** | Directed typed edge between two cards in the same project. Types: `BlockedBy`, `Precedes`, `Relates`. Soft-deleted on archive, retained in audit log. |
| **BlockedBy** | Dependency type: source card cannot proceed (soft warning) until target card is Done. |
| **Precedes** | Dependency type: source card comes before target card in execution order, but no hard blocking. |
| **Relates** | Dependency type: soft association — cards are related but no ordering or blocking implied. |
| **Cycle Detection** | Application-layer check on every `CardRelationship` insert. Rejects any relationship that would form a circular dependency. Error code: `DEPENDENCY_CYCLE_DETECTED`. |
| **Model Tier** | Abstract cost/capability classification: Economy (cheap, fast), Standard (balanced), Premium (most capable). Admin maps providers to tiers; features route to tiers, not specific models. |
| **AiFeature** | Shared enum for AI-driven product features. Connects routing policy, token/image usage logs, and usage dashboards without free-form feature strings. |
| **ModelRouter** | Application service that resolves feature + user context → correct LLM provider. Checks context window, applies user tier ceiling, triggers fallback on failure. |
| **ContextCompressor** | Service that summarizes injected context when it exceeds a configurable token threshold. Keeps costs predictable on large projects. |
| **Prompt Caching** | Reusing previously computed token representations for repeated context (ProjectContextSnapshot, user memory). Reduces effective token cost. Tracked in `TokenUsageRecord.CachedTokens`. |
| **TokenUsageRecord** | Per-call log of text LLM usage: user, `AiFeature`, model, input/output/cached tokens, project, pipeline group. Powers cost dashboards and budget enforcement. |
| **ImageUsageRecord** | Per-call log of image generation: user, `AiFeature`, model, image count, resolution, cost. Separate from token tracking — images are priced per image not per token. |
| **ProviderType** | Classification of an LLM provider: `Text` (chat/completion), `Image` (generation/inpainting), or `Both` (e.g. GPT-4o). Determines which features can use the provider. |

---

> **Next Step:** Review this document. Approve, modify, or challenge anything. Once we're aligned, Phase 1 begins.
