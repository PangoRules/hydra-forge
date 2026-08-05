# Plan 20: Web UI — CardChatLinkList + Chat Pages
**Branch:** `task/web-card-chat-link-list`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 20

**Goal:** `CardChatLinkList` in card modal. Extend the personal chat home page with folder tree + search.

> **Reconciled 2026-08-04:** Plan 17 shipped ahead of its own written scope and already built `pages/chats/index.vue` (plural — single-page app, session list in a sidebar + `ChatSessionView` inline via an `activeSessionId` ref, no separate route per session). This plan originally called for `pages/chat/index.vue` + `pages/chat/[sessionId].vue` (singular, two routes). Decision: extend the existing `chats/index.vue` rather than build a second, competing entry point — dropped the `[sessionId]` route from scope below.

**Files:**
- Create: `src/web-ui/app/components/chat/CardChatLinkList.vue`
- Modify: `src/web-ui/app/pages/chats/index.vue` (add folder tree + search to the existing sidebar)
- Modify: card modal component (to include `CardChatLinkList`)

**Steps:**

- [ ] `CardChatLinkList.vue`: collapsible summary table in card modal. Columns: owner, summary, created, actions. Owner-clickable row → opens session read-only (or full if owner). Props: `cardId`
- [ ] Extend `pages/chats/index.vue`'s sidebar with a folder tree above the session list (reuse the folder CRUD backend from Plan 9 — no folder UI exists yet either) and a search bar wired to `GET /api/chat/search` (Plan 13's backend, also currently UI-less)
- [ ] Integrate `CardChatLinkList` into existing card modal component
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-20-web-card-chat-link-list-matrix.md` — card modal shows the link list after a project chat closes; sidebar folder tree + search work against the existing session list

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
