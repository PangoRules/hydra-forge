# HydraForge — Agent Platform Vision Document

## Core Philosophy

> **Two interfaces, one brain. Server as source of truth. Human always in the loop.**

The platform is a dual-interface agentic development system:

| Interface | Role | Primary Use |
|---|---|---|
| **Web UI** | Visual workspace | Brainstorming, spec writing, visual board, PR review, mobile/tablet access |
| **TUI** | Terminal workspace | Full feature parity — board, chat, personal space, agent pipeline, all in the terminal |

**Critical rule:** Both interfaces have **full feature parity** — whatever you can do in one, you can do in the other. The TUI is not a dumbed-down version. It has board views, card views, dependency panels, chat mode, personal workspace (notes, brain, tasks) — everything, via keyboard.

---

## The Pipeline (Trello-Like Board)

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│   IDEA   │───→│   SPEC   │───→│   PLAN   │───→│  IN DEV  │───→│  REVIEW  │───→│   DONE   │
│          │    │          │    │          │    │          │    │          │    │          │
│ Raw      │    │ Specs    │    │ Task     │    │ Dev      │    │ Human    │    │ PR       │
│ thoughts │    │ Docs     │    │ Steps 1-N│    │ Loop     │    │ reviews  │    │ Merged   │
│ Brain-   │    │ Research │    │ Break-   │    │ TUI      │    │ comments │    │ Deployed │
│ storming │    │          │    │ down     │    │ executes │    │ approves │    │          │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    │ or sends  │    └──────────┘
                                                                  │ back 🔄   │
                                                                  └──────────┘
```

Each **Project** has:
- A board with cards flowing through status columns
- Associated specs (with version history), plans (with version history), and PRs
- An optional git remote for the Git Agent to commit and create PRs
- A `ProjectContextSnapshot` (template-rendered board state, updated on every mutation)

---

## The Agent Crew

### Pipeline Agents (called by Orchestrator)

| Agent | Role | When |
|---|---|---|
| **Planner** | Entry point — brainstorms specs, calls Architect skill | Idea → Spec → Plan |
| **Orchestrator** (devlib) | Runs the pipeline autonomously from Plan → Done | Sequences agents |
| **Developer** | Implements code, one task at a time (reviews each task before moving on) | Plan → Implementation |
| **Reviewer** | Validates code, gives LGTM or requests changes | After each task/plan |
| **Documenter** | Writes docs, user stories, PR descriptions | After all tasks approved |
| **Git Agent** | Commits, creates PR, marks board card for human review | End of pipeline |

### Skill Library (powers the agents)

Brainstorming, Architect, Authority, Identity, Shell, Business Logic, Frontend, Backend, Android, etc.

---

## The Review Loop (Human-in-the-Loop)

**Nothing gets marked "Done" without a human.** The flow:

1. **Developer** implements a task, then **reviews its own work** before the next task
2. Each task gets reviewed by the **Reviewer agent** (automated)
3. Once all tasks in a plan are complete → **Git Agent** creates a PR
4. PR lands on the board under **"Review"** column
5. **Human** must:
   - Review the PR
   - Test it (dev environment / preview)
   - Leave comments if needed
   - Approve ✅ or send back 🔄 to dev loop with feedback
6. Once approved → **"Done"** (merged, deployed if applicable)

---

## Feature Parity: Web UI ↔ TUI

| Feature | Web UI | TUI |
|---|---|---|
| **Board view** | ✅ Drag & drop columns/cards | ✅ ASCII/rich board, real-time |
| **Card detail** | ✅ Full modal editor | ✅ Opens in `$EDITOR` |
| **Card dependencies** | ✅ Dependency panel, lock badge | ✅ `d` key → dependency panel |
| **Spec writing** | ✅ Versioned markdown editor | ✅ Opens in `$EDITOR`, version history |
| **Plan creation** | ✅ Numbered steps editor | ✅ Opens in `$EDITOR` |
| **PR review** | ✅ GitHub-style diff + comments | ✅ CLI git review |
| **Comment threads** | ✅ Threaded discussions + @mention | ✅ Inline view + add |
| **Chat (general)** | ✅ Chat panel, folders, RAG | ✅ Chat mode, folder nav |
| **Chat (project)** | ✅ Collapsible board panel | ✅ Chat mode in project context |
| **Personal workspace** | ✅ Notes, Tasks, Brain, Calendar | ✅ Notes list, tasks, brain search, calendar |
| **Agent pipeline** | ✅ Trigger + monitor output | ✅ Trigger + stream output |
| **Keyboard navigation** | ✅ Full keyboard, shortcut overlay (`?`) | ✅ vim-style, shortcut ref (`?`) |
| **Search** | ✅ Full-text search | ✅ CLI search |

Both interfaces talk to the **same server**. The board state, card status, comments, chats — all stored in PostgreSQL, accessible from either interface. No offline mode — server connection required.

---

## Dev Environment & Testing

Before something can be approved:
- A **dev environment** must be spun up (preview URL, local server, Docker container, etc.)
- The human reviews the actual running code, not just the diff
- Comments can be made on specific lines/files
- If rejected, the card goes back to "In Dev" with the feedback attached

This ensures the human has enough context to meaningfully review, test, and approve.

---

## Chat ↔ Project Integration

Chats are organized in folders (max 2 levels). When a project is created, a matching chat folder is auto-created. When a project is archived, its folder is archived (revivable).

```
Chats
├── General/
│   └── SubFolder/          ← max 2 levels
└── Projects/
    ├── KenanAdvantage/      ← auto-created with project
    │   ├── OrdersApi/
    │   └── [chat sessions]
    └── PersonalProject/
        └── [chat sessions]
```

### Project Chat Panel
- Collapsible panel on the board view — does not obstruct kanban layout
- Opens a **fresh session** every time (no accumulated history)
- Auto-injects on open:
  1. `ProjectContextSnapshot.TemplateContent` (columns, card index by `#CardNumber`, open blockers — template-rendered, no LLM cost)
  2. Open card context if a card is currently open: `"Card #42 [title] opened — what are we doing?"`
  3. User's AI personality prompt
  > The nightly `AiNarrative` is shown on the project dashboard but not injected into chat — keeps context lean and cost-free.

### AI Edit Permissions
- AI **proposes** card mutations — never acts unilaterally
- User sees confirmation dialog before any board state change
- User can grant session-level permission ("allow edits this session") to skip per-action confirmation
- Permission is **chat-session-scoped**: revoked on session end or when user opens a new chat
- Reopening a closed chat = new session = permission must be re-granted

### Card-Level Chat Summaries
- Each card has a collapsible chat table: user avatar | summary | date
- Owner's row: clickable → reopens that chat
- Other users' rows: read-only view of summary only
- Auto-linked when: card is open during a project chat, or AI references a card by number

### Shared Project Chats
- All project-folder chats are visible to all project members (read-only for non-owners)
- "Summarize this → start my own" button: creates a new chat in user's project folder with summary injected as context

---

## Customizable Naming

On first run, the admin configures:
- Platform name (default: "HydraForge")
- Primary color / branding
- Default LLM provider (admin-configured, all users inherit)
- Logo emoji

---

## Data Architecture

All state lives in PostgreSQL. No filesystem-as-source-of-truth.

```
PostgreSQL + pgvector (hydraforge db)
│
├── Project space (shared, members-only visibility)
│   ├── Projects, Columns, Cards, CardAssignees, CardRelationships
│   ├── ChecklistItems, Attachments, Comments, CardWatchers
│   ├── Specs + SpecVersions, Plans + PlanVersions
│   ├── ProjectMembers, ProjectContextSnapshots, AuditLog
│   └── CardChatLinks
│
├── Personal space (private per user)
│   ├── ChatFolders, ChatSessions, ChatMessages
│   ├── MemoryEntries (with vector embeddings)
│   ├── Notes, NoteReminders, PersonalTasks
│   ├── CalendarSources, CalendarEvents
│   ├── Documents + DocumentVersions + DocumentChunks (RAG embeddings)
│   ├── GalleryImages, Albums, ImageTags
│   └── AgentPersonalities
│
├── Admin / system (global)
│   ├── Users, Notifications
│   ├── LlmProviders, FeatureModelConfigs
│   ├── UserTokenBudgets, TokenUsageRecords, ImageUsageRecords
│   └── FeatureFlags
│
File storage (local FS or S3)
└── Card attachments, Gallery images, Document binaries (PDF, etc.)
```

Runs as Docker Compose services: `.NET server` + `postgres:16-alpine` + `searxng` (optional `--profile search`). One-command setup. Backup via `pg_dump`.

Git integration: projects optionally link to a git remote. The Git Agent commits and creates PRs via the configured remote. Code lives in git; project management lives in HydraForge.
