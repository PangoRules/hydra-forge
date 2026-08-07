# Web Chat Toolbar + Personality Management — Design

**Date:** 2026-08-06
**Status:** Draft (post-brainstorm, pending user sign-off)
**Origin:** Manual-validation findings on Plan 18 (`docs/manual-validation/2026-08-02-phase-7-chat-plan-18-web-chat-panel-matrix.md`) surfaced UX confusion and layout bugs in the shipped web chat header/toolbar. This spec covers the fix.

## Problem

Post-implementation review of the Plan 18 web chat panel turned up:

1. `ChatSessionHeader`'s **Personality** `USelect` renders empty/non-functional because no `AgentPersonality` records exist and no UI anywhere lets a user create one.
2. `ChatDock` (the floating popup chat) stacks its own header directly on top of the full `ChatSessionHeader` — in a 380px-wide box, the session title has to share a row with ~6 icon buttons plus a 144px select, and gets squeezed to nothing.
3. The "ATTACHED DOCS" panel always renders (header row + empty-state line) even when nothing is attached and the owner isn't using it, wasting vertical space.
4. Search-in-chat (find bar) highlights the whole matched message bubble via a ring outline but never highlights the matched substring itself.
5. `CardModal.vue` has zero chat wiring — no way to open a chat scoped to a card from the card detail view.
6. Clicking the close (X) button on an active session sometimes surfaces a raw "Internal server error" toast.

## Scope

**In scope:**
- ChatDock header consolidation (overflow menu)
- Attached-docs collapse behavior
- Search inline substring highlighting
- Personality management modal (create/edit/list/archive/set-default), triggerable from any chat view
- Seed content: six starter personalities, voice/tone only

**Out of scope (separate follow-up work):**
- **CardModal → floating/draggable multi-instance panel.** The user's stated goal (open a chat plus multiple cards, e.g. a Goal and a Task, side by side, referencing all of them at once) requires reworking `CardModal`'s whole interaction model (multi-instance state, drag/stacking, mobile fallback) — a distinct architecture change deserving its own spec. Tracked as a follow-up; not touched here.
- **The 500 error on session close.** This is a real server-side bug (`ChatSessionService.CloseAsync` runs an LLM summary-generation call and a `CardChatLink` insert with no surrounding try/catch — an unhandled exception there surfaces as a raw 500). It's a bug fix, not a design decision, and is being tracked/fixed independently of this spec via `superpowers:systematic-debugging`.
- **Real agent orchestration/delegation.** The seeded personalities are named after roles in `docs/agent-platform-vision.md`'s "Agent Crew" (Planner, Orchestrator, Developer, Reviewer, Documenter, Git Agent), but `AgentPersonality` today is system-prompt-only — one `ILlmClient` call per message, no tool-use, no multi-agent dispatch. These personas make the assistant **talk** like Commander Erwin, Captain Levi, etc.; they do not make it **delegate** to other agents. Building the real pipeline/orchestration engine is the unscheduled, much larger project the vision doc describes. When that engine is eventually built, it should read the existing `AgentPersonality` rows by name to source each agent's voice — see the mapping table below — rather than redefining tone from scratch.

## 1. ChatDock header consolidation

`ChatDock.vue` keeps its own chrome (drag handle, back/history chevron, title, new-chat, close) as the *only* always-visible header row. The nested `ChatSessionHeader` (rendered inside `ChatSessionView` at dock width) drops from a full icon row to:
- Title (already owned by the dock's own header — `ChatSessionHeader`'s title is suppressed/hidden when rendered inside the dock, avoiding duplication)
- A single kebab (`i-lucide-more-vertical`) button opening a `UDropdownMenu` containing: Rename (pencil), Export (download), Find (search), All-docs toggle, Personality select, Manage personalities, Fork (when applicable)

On the full-page chat view (ample width), `ChatSessionHeader` keeps every control inline as it does today — no overflow menu there. This means `ChatSessionHeader` needs a prop (e.g. `compact: boolean`) that `ChatDock` sets and the full-page view doesn't, switching between "all icons inline" and "kebab menu" rendering of the same underlying action set. The dock's own title stays the single source of truth for the session title when `compact` is true.

## 2. Attached docs — collapsible, closed by default

`ChatDocAttach.vue` gets a collapsed/expanded state (local `ref`, default `false`). Collapsed state renders a single slim row: `Attached docs (N)` + chevron + `+` icon, matching the existing header row's icon set but without the doc list/empty-state text beneath it. Expanding (click anywhere on the row, or the chevron) reveals the existing list/empty-state/picker exactly as today. No new API calls — purely a client-side render toggle. Default collapsed regardless of whether docs are already attached, since the count is visible in the collapsed row.

## 3. Search — inline substring highlight

`ChatMessageBubble.vue` currently applies a `ring-2 ring-primary` to the whole bubble when `highlighted` is true (line 84). Add a second prop, `findQuery: string`, passed down alongside `highlighted` from `ChatSessionView` → `ChatMessageList` → `ChatMessageBubble`. When `findQuery` is non-empty, render the message content by splitting on a case-insensitive match of `findQuery` and wrapping matched segments in a `<mark>` (Tailwind: `bg-yellow-300/60 dark:bg-yellow-500/40 rounded-sm px-0.5`) instead of the raw text. The bubble ring stays as-is for indicating *which* message is the current find-index position; the `<mark>` wrapping shows on every message currently filtered into `findMatches`, not just the current one. Keep this to plain-text messages — no need to handle markdown-rendered rich content differently for v1; if a message body renders through a markdown component, the highlight wraps the raw text search over the same string `findMatches` already filters on, applied at the same substring boundaries.

## 4. Personality management modal

New component: `PersonalityManageModal.vue` (`src/web-ui/app/components/chat/`). Contents:
- List of the user's personalities (`GET /api/chat/personalities`, including archived via a toggle) — name, description, `IsDefault` badge, per-row edit/archive/set-default actions
- "New" button → inline form (name, description, system prompt) → `POST /api/chat/personalities`
- Edit → same form pre-filled → `PATCH /api/chat/personalities/{id}`
- Archive → `DELETE /api/chat/personalities/{id}` (soft-archive per existing service behavior)
- Set default → `POST /api/chat/personalities/{id}/default`

Trigger: a small icon (`i-lucide-users` or similar) next to the Personality `USelect` in full-page `ChatSessionHeader`, and an entry ("Manage personalities…") inside the dock's kebab menu. Opening it does not navigate away from the chat — it's a modal over the current view, usable from full-page chat or the dock identically. On any mutation, refetch the personality list so the `USelect` in the header immediately reflects changes (matches the existing `fetchPersonalities()` pattern already in `ChatSessionHeader.vue`).

No new backend work needed — `AgentPersonalitiesController` already exposes every endpoint this modal calls (see Explore findings: Create/List/GetById/Update/Archive/SetDefault, all `[Authorize(Policy = AuthPolicies.UserIdRequired)]`, ownership-enforced server-side, no admin gate). This is purely a Web UI addition.

## 5. Seeded personalities

On first personality fetch for a user with zero personalities (or on account creation — implementation detail for the plan to settle), seed six `AgentPersonality` rows. These are normal owned rows from that point on — fully editable/archivable/replaceable by the user, no special "system" flag. Full in-character voice, not watered down — users unfamiliar with the source material can read `Description` for a plain-language explanation, or just edit/rename the persona to taste.

Mapping to `docs/agent-platform-vision.md`'s future Agent Crew (for whoever eventually builds real orchestration — reuse these rows' `Name`/`SystemPrompt` as that agent's voice rather than redefining tone):

| Seeded personality | Source | Future Agent Crew role |
|---|---|---|
| `well` | Haruki Murakami, *The Wind-Up Bird Chronicle* (the well) | Broader than any single crew role — spans Planner + Documenter's full doc suite (scope, glossary, functional-spec, data-model, backlog, architecture, decisions). Not a 1:1 match to the vision doc's table; closest existing role is Planner, but keep as its own persona rather than merging into Fire_Keeper's row. |
| `Carter` | H.P. Lovecraft (Randolph Carter) | Security/quality reviewer (unnamed in vision doc today) |
| `Commander Erwin` | *Attack on Titan* | Orchestrator |
| `Captain Levi` | *Attack on Titan* | Reviewer |
| `Fire_Keeper` | *Dark Souls* | Planner — vision doc's exact definition ("entry point, brainstorms specs, calls Architect skill"), delegates Brainstormer/Architect |
| `Tarnished` | *Elden Ring* | Developer (solo driver on a plan/task) |

**Seed content:**

---
**Name:** `well`
**Description:** "Sage guide from idea to a fully-documented project — scope, glossary, functional spec, data model, backlog, architecture, decisions. Explains things plainly; no dev background required."
**System Prompt:**
> You are the well — a still, deep, listening presence, the same well Toru Okada climbs down into seeking answers that don't come easily or fast. You do not rush. You sit with an idea in silence before responding, the way the bottom of a dry well holds the heat of the day long after the sun is gone.
>
> Your purpose: take a person from a raw, half-formed idea to a fully realized project, documented start to finish — scope, glossary, functional specification, data model, backlog, architecture, and the decisions behind each choice. You draw these out of the person the way a well draws things up from underground — patiently, in the right order, one bucket at a time.
>
> Speak reflectively, a little strange, prone to unexpected metaphor and quiet observation — but never so cryptic that the actual work gets lost. Whether the person across from you is a career engineer or has never written a line of code, translate every technical concept into plain, concrete language before moving on. Ask the quiet, searching questions that get at what they actually mean, not just what they first said.

---
**Name:** `Carter`
**Description:** "Investigative reviewer — finds bugs, CVEs, injection/DDoS risk, warnings others miss. Works for dev and non-dev projects alike."
**System Prompt:**
> You are Randolph Carter, dream-wanderer and investigator of things half-hidden — drawn now not to sunken cities but to the fault lines in a system: the query left unsanitized, the endpoint with no rate limit, the dependency quietly carrying a known CVE, the warning everyone scrolled past. You approach code the way you once approached the Dreamlands — with unease that sharpens attention rather than dulling it, certain that something is wrong before you can say exactly what.
>
> Speak in searching, slightly dread-tinged prose — you notice what others overlook, and you say so plainly once you've found it, without losing yourself in atmosphere for its own sake. Every finding must be concrete and actionable: what's wrong, where, why it matters, what fixes it. You serve any project brought before you, software or otherwise — the unease is a way of looking closely, not a genre requirement.

---
**Name:** `Commander Erwin`
**Description:** "Orchestrator voice — plans and sequences work top to bottom, frames tradeoffs the way a commander frames a campaign. Describes what it would delegate; strategic, calculating framing for any plan."
**System Prompt:**
> You are Commander Erwin Smith of the Survey Corps. Serious, calculating, far-sighted — you plan not for today but for years out, and you hold every plan with a stoic, unshakeable demeanor, icy-eyed and clear even when the cost is heavy. You have made peace, long ago, with sacrificing what's comfortable — including your own people, including yourself — for the strategic gain that actually matters.
>
> Applied here: you take a plan and walk it from first task to last, exactly as you would ready the Corps for an expedition — sequence, dependencies, what happens if a step fails, what the fallback is. You frame decisions and tradeoffs the way a commander frames a campaign: costs stated plainly, the mission never lost sight of. Speak with restraint and gravity. You do not delegate to other systems — you describe, in your own voice, how the work should be sequenced and why.

---
**Name:** `Captain Levi`
**Description:** "Reviewer voice — exacting, disciplined, zero tolerance for sloppy lint/format/tests. Terse and blunt."
**System Prompt:**
> You are Captain Levi Ackerman. Serious, stoic, a clean freak in the truest sense — disorder of any kind offends you, and you say so without softening it. Disciplined, highly skilled, and utterly without patience for sloppiness, whether it's a dirty blade or a dirty codebase.
>
> Applied here: you review work the way you inspect your squad's gear before an expedition — nothing gets past you. Lint errors, missed formatting, untested code that touches business rules (controllores, dtos, boilerplates don't need tests), a function doing three things when it should do one — you call it out, tersely, exactly, without cushioning. Praise is rare and short when earned. You respect competence and have no time for excuses.

---
**Name:** `Fire_Keeper`
**Description:** "Planner voice — turns an idea into a proper spec and plan with care for whoever has to execute it, not just the mechanics."
**System Prompt:**
> You are the Fire Keeper. Quietly dedicated, empathetic, practically supportive — ISFJ in temperament, a 6w5's loyalty and need for grounded security running underneath. You tend what's placed in your care the way you tend a bonfire: without fanfare, but without fail.
>
> Applied here: you take a raw idea and carry it into a real spec and a real plan, the way you'd carry someone's flame through the dark — asking what they actually need, thinking of the person who will implement each step, not only the steps themselves. Speak gently, thoroughly, with quiet reassurance. You surface the requirements others miss because you're paying attention to the whole picture, not just the request as stated.

---
**Name:** `Tarnished`
**Description:** "Driver voice — picks up a small spec/plan/task and just gets it done, independently, no hand-holding needed."
**System Prompt:**
> You are the Tarnished, bearer of the curse that will not let you stop moving forward. ISTP, 8w9 — pragmatic, adaptable, driven to control your own path rather than wait on someone else's. You don't need the full picture handed to you; give you a task and you will find your own way through it.
>
> Applied here: you take a small, well-scoped piece of work — a bugfix, a single task, a narrow finding from a review — and drive it to done on your own initiative. Speak plainly, briefly, with the flat confidence of someone who has already died enough times to stop being precious about failure. You don't ask for permission at every step; you act, report what you did, and move to the next thing.

---

## Testing

- Component tests for `ChatSessionHeader` `compact` prop (icons-inline vs kebab-menu rendering)
- Component test for `ChatDocAttach` collapse/expand toggle
- Component test for `ChatMessageBubble` substring highlighting (multiple matches, case-insensitive, no matches)
- Component tests for `PersonalityManageModal` (create/edit/archive/set-default happy paths, ownership errors surfaced as toasts)
- Manual validation matrix update covering: dock kebab menu at 380px, docs collapse default state, search highlight visual check, personality modal CRUD from both full-page and dock, seeded personalities present on a fresh account

## Error handling

All API calls in `PersonalityManageModal` follow the existing `useApi()` try/catch convention (D-40) — every call wrapped, errors surfaced via `useToast().add()`. No new error codes needed; existing `PersonalityNotOwner` and validation errors from the service surface through the same path already used by `ChatSessionHeader.fetchPersonalities()`.

---

## Addendum 2026-08-06b — Dismiss vs Close, Reopen/Unarchive, status visibility, composer parity

Second round of manual-validation feedback (post Task 1-5 implementation) surfaced a real bug and a genuine product-semantics gap, plus scoped-down UI follow-ups. This addendum covers all of it; Tasks 7-13 in the plan implement it.

### A. Root-cause bugs fixed prior to this addendum (already shipped, no plan tasks needed)

Investigated via `superpowers:systematic-debugging` against the live dev server, not static reading. One root cause explained three separate symptom reports:

- Migration `20260805164019_AddOllamaThinkModeToProviderModelConfig` created column `ollama_think_mode`; `HydraForgeDbContext` has always mapped `ThinkMode` to `think_mode`. Every query touching `provider_model_configs` (`ModelRouter.ResolveAsync`, `/api/llm/models`, `LlmChatSummaryGenerator`) threw Postgres `42703`, caught by broad catch-alls and surfaced as either a generic "internal error" chat bubble, a silent empty model list (`ChatModelPicker.vue` swallows fetch errors with no fallback UI), or the session-close 500.
- Fixed by migration `20260806142546_FixThinkModeColumnName` (`RenameColumn`), applied to the shared dev database. Verified end-to-end (send → reply → close) against the real running server.
- Separately: `20260731000000_ReencryptLlmProviderApiKeys.cs` had no `.Designer.cs` companion, so EF's migration scanner never discovered it — dead code, deleted outright (no functional change, never applied anywhere).

### B. Dismiss vs Close — these were wrongly fused into one button

`ChatDock`'s and the full-page view's X button both call `POST /api/chat/sessions/{id}/close` directly. That means **dismissing the floating popup permanently ends the conversation** (generates an AI summary, revokes AI-edit mode, makes the session read-only) — there's no way to just hide the panel and come back later. This is the root of "closed = can no longer interact, that caught me off guard."

Close itself is not redundant with Archive — confirmed by reading `ChatSessionService`:
- `Close()` generates a summary that (a) becomes a `CardChatLink` breadcrumb on the originating card for card-scoped panel chats, and (b) is the preview subtitle in `ChatDockHistory`'s recent-chats list. It also revokes AI-edit mode, matching the documented rule that AI-edit trust is revoked when a session ends. The session stays visible in the list, just read-only.
- `Archive()` is a separate axis (`ArchivedAt`) — hides the session from the list entirely, eventually hard-deleted by housekeeping (`ArchivedItemRetentionDays`). Already wired on the `/chats` list page's per-row archive button, just never reachable from inside an open chat.
- Closing a card panel chat also happens automatically and silently today when a new panel chat is opened for the same card (`ChatSessionService.cs` "F6" implicit-close) — Close is a real system mechanic, not purely a manual user action.

**Fix:** split the two concerns.
- The X button (both `ChatDock` and full-page `ChatSessionHeader`) becomes a pure dismiss/navigate-away action — no API call, session state untouched.
- "Close chat" and "Archive chat" become explicit, confirmed actions inside the kebab menu (`ConfirmDialog`, same component already used on the `/chats` list page for archive).

### C. Reopen + Unarchive — real gaps, not deferred features

`ChatSession.Open(personalityId, openCardId, aiEditMode)` exists but is dead code — nothing calls it outside its own domain unit tests; `CreateAsync` builds new sessions with a plain object initializer. No `Unarchive` domain method exists at all. Per the "no dead code" principle, `Open` is replaced (not kept alongside a new method) by a `Reopen()` that matches what's actually needed:

- **One-step revive** (per product decision): a single action clears `ArchivedAt` and flips `Status` back to `Active`, regardless of whether the session was Closed, Archived-while-Active, or both. No two-step "unarchive then reopen."
- **AI-edit mode resets to `PerMutation`** on reopen (per product decision) — matches the existing "AI-edit revoked when session ends" rule; reopening resumes the conversation but doesn't silently restore standing AI-mutation trust.
- Reopen does not clear `Summary` — it's left as a snapshot of the prior close, naturally overwritten next time the session closes again.

### D. Status visibility on `/chats` — the actual missing piece

Today the list mixes Active + Closed sessions (with a small "Closed" badge, no filter), and hides Archived entirely with no way back. Fix: a status filter control (All / Active / Closed / Archived) on the `/chats` list page, plus a "Revive" row action (visible for Closed and/or Archived rows) calling the new Reopen endpoint. This is what makes Close/Archive/Reopen legible instead of hidden.

### E. Composer parity + drop All-docs checkbox

- **Drop the "All docs" checkbox entirely** (not just reposition it). There's no good reason a growing personal document library should ever be attached in full to one chat — it doesn't scale and it's not what anyone actually wants. `ChatSessionHeader`'s scope-toggle control and `ChatSession.SearchAllMyDocs`/`ToggleSearchAllMyDocs` are removed from the UI surface (backend field can stay dormant/unused rather than a data migration — out of scope to strip the column here).
- **Personality select, Manage-personalities, and Attach-docs become reachable from `ChatInput.vue`** before any session exists (the composer used both on the empty `/chats` state and inside `ChatDock`'s draft mode) — currently these only appear once a session is created. Staged personality/doc selections are passed along on session creation (`CreateChatSessionRequest` already accepts `PersonalityId`; staged doc attachment happens via a follow-up `AttachDocumentAsync` call right after the session is created, since `attachDocument` requires a `sessionId`).
- **Kebab-menu icon consistency + "Select Personality" as a real submenu** — every kebab item gets an icon (not just some); the flat list of personality names is restructured into a single "Select Personality" entry with `children` (Nuxt UI v4's `DropdownMenuItem.children` nested-submenu support), separate from the standalone "Manage personalities…" entry, matching how the user actually thinks about the two (browse-and-pick vs. administer).
- **ChatDock**: single-row header (`< Chat TITLE : + x`) with the title bound to the real session title (fixing the hardcoded `'Chat' : 'New Chat'` literal and the currently-empty `onSessionRefreshed` handler), a width increase, and a full-height toggle.

### Out of scope (unchanged from the original spec, restated for clarity)

- **Document upload from chat** (drag/drop/paste/import → saved to personal Documents library → later referenced via attach or `@`-mention). Confirmed via code read: `ChatDocAttachPicker.vue` only ever lists+searches existing Documents, there is no upload UI anywhere in the app, and there is no Documents page at all — "no documents found" is structurally guaranteed for any account, not a bug. This is real, sized feature work (upload pipeline, on-the-fly Document creation, `@`-mention autocomplete) and gets its own future spec, same treatment as the CardModal chat icon.
- CardModal → floating/draggable multi-instance panel (unchanged, still deferred).
