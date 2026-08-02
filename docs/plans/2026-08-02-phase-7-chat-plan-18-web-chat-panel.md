# Plan 18: Web UI — ChatPanel + F6 Implicit Close
**Branch:** `task/web-chat-panel`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 18

**Goal:** `ChatPanel` component for project board side rail. F6 implicit close. Auto-prompt pre-fill.

**Files:**
- Create: `src/web-ui/app/components/chat/ChatPanel.vue`
- Modify: `src/web-ui/app/pages/projects/[projectId]/index.vue`

**Steps:**

- [ ] `ChatPanel.vue`: collapsible side panel (drawer mobile, side rail desktop). Hosts `ChatSessionView`. Props: `projectId`, `cardId?`. On open: `POST /api/chat/sessions` with `projectId` + `cardId` (F6 implicit close of prior). Pre-fill first message: "Card #N [title] opened — what are we doing?" (user edits then sends)
- [ ] Collapsing panel does NOT close session — session stays Active, resumable
- [ ] Integrate into board view: toggle button in board header. When card modal open, `ChatPanel` opens with `cardId` set
- [ ] Tab close / navigate-away does NOT close session (per F2=C)

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
