# HydraForge — Glossary

> **Version:** 1.0
> **Date:** 2026-06-03

---

| Term | Definition |
|---|---|
| **Project** | Top-level container. One repo = one project. |
| **Column** | A stage in the workflow pipeline. Customizable name and order. |
| **Card** | A unit of work — task, bug, epic, spec, or idea. |
| **CardNumber** | Sequential integer identifier per project (e.g. #1, #42). Never reused after deletion. Never expose raw GUIDs to users. |
| **Spec** | A long-form markdown document describing a feature in detail. Linked to a card. Versioned. |
| **Plan** | A step-by-step numbered markdown execution plan. Linked to a spec or card. Versioned. |
| **Audit Log** | Immutable record of all changes, with before/after JSON snapshots. |
| **TUI** | Terminal User Interface — built with Spectre.Console. Full feature parity with Web UI. |
| **Web UI** | Browser-based interface — Nuxt 3 + Vue 3 + Tailwind CSS + Nuxt UI. Mobile-first. |
| **SignalR** | ASP.NET Core library for real-time WebSocket communication (SSE fallback). |
| **Clean Architecture** | Layered architecture: Domain → Application → Infrastructure → Presentation. Dependencies point inward. Domain has no external dependencies. |
| **Server-Authoritative** | All state lives on the server (PostgreSQL). Both TUI and Web UI require an active server connection. TUI locks gracefully when unreachable. |
| **WIP Limit** | (Future) Maximum number of cards allowed in a column at once. Field reserved in schema. |
| **Personal Space** | User-private modules: chats, brain/memory, notes, tasks, calendar, gallery, documents, theme. Not visible to admin. |
| **Project Space** | Shared team modules: board, cards, specs, plans, git. Visible to project members. Admin can see all projects. |
| **Admin Space** | Operational view: users, all projects, LLM providers, system health, feature flags. Cannot access user personal data. |
| **Brain / Memory** | Per-user persistent memory store with 6 categories. Injected into chat context. Backed by pgvector for semantic search. |
| **Compare** | Blind A/B model testing per user. Feature-flaggable (admin can disable per install). |
| **Cookbook** | Model browser + hardware fitness scoring. Connects to external model server — does not install models on HydraForge server. Feature-flaggable. |
| **Deep Research** | Multi-step web search → AI synthesis → visual report. Requires SearXNG instance. Per-user sessions. |
| **ntfy** | Open-source push notification service. Used for reminders, card assignments, @mentions. Per-user topic: `hydraforge-{userId}`. |
| **pgvector** | PostgreSQL extension for vector similarity search. Powers Brain/Memory semantic search and RAG without a separate vector database (e.g. ChromaDB). |
| **Feature Flag** | Admin toggle to enable/disable optional modules (Compare, Cookbook, etc.) per install. |
| **Mobile-First** | Web UI designed starting from the mobile breakpoint, enhanced progressively for tablet and desktop. Three separate component sets where UX differs significantly. |
| **CardRelationship** | Directed typed edge between two cards in the same project. Types: `BlockedBy`, `Precedes`, `Relates`. Soft-deleted on archive, retained in audit log. |
| **BlockedBy** | Dependency type: source card cannot proceed (soft warning) until target card is Done. |
| **Precedes** | Dependency type: source card comes before target card in execution order. No hard blocking. |
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
| **ProjectContextSnapshot** | Cached board state injected into every project chat. `TemplateContent` is regenerated on every board mutation (instant, no LLM). `AiNarrative` is generated nightly by a scheduled job. |
| **PipelineRunId** | Nullable correlation id that groups all LLM calls within a single agent pipeline run (Planner → Developer → Reviewer) for aggregate cost reporting. |
| **RAG** | Retrieval-Augmented Generation. At chat time, user message is embedded → similarity search on user's `DocumentChunk` rows → top-K results injected as context. |
| **PresenceHub** | SignalR hub for real-time presence (green dots). Join/leave events are ephemeral — no database writes. |
| **CardChatLink** | Join record linking a ChatSession to a Card. Created when a chat session closes. Stores the auto-generated summary of what was discussed. |
| **Result<T, Error>** | Domain-layer return type for all business logic operations. Expected failures return a typed `Error` with a named code. No exceptions thrown for expected failure paths. |
| **ProblemDetails** | RFC 7807 structured error response shape. Every API error returns: `type`, `title`, `status`, `detail`, `correlationId`. Stack traces never included. |
| **correlationId** | Unique identifier assigned to every request by the global exception middleware. Logged server-side. Visible to users in error messages for support escalation. |
| **SearXNG** | Open-source, self-hosted metasearch engine. Bundled as an optional Docker Compose service. Required for Deep Research. |
