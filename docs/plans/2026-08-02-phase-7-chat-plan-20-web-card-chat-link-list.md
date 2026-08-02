# Plan 20: Web UI — CardChatLinkList + Chat Pages
**Branch:** `task/web-card-chat-link-list`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 20

**Goal:** `CardChatLinkList` in card modal. Personal chat pages (`index`, `[sessionId]`).

**Files:**
- Create: `src/web-ui/app/components/chat/CardChatLinkList.vue`
- Create: `src/web-ui/app/pages/chat/index.vue`
- Create: `src/web-ui/app/pages/chat/[sessionId].vue`
- Modify: card modal component (to include `CardChatLinkList`)

**Steps:**

- [ ] `CardChatLinkList.vue`: collapsible summary table in card modal. Columns: owner, summary, created, actions. Owner-clickable row → opens session read-only (or full if owner). Props: `cardId`
- [ ] `pages/chat/index.vue`: personal chat home. Folder tree (left), session list (middle), search bar. Mobile: stacked. Create new session button
- [ ] `pages/chat/[sessionId].vue`: full-page personal chat session view. Reuses `ChatSessionView`
- [ ] Integrate `CardChatLinkList` into existing card modal component
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-20-web-card-chat-link-list-matrix.md` — card modal shows the link list after a project chat closes; personal chat pages list/search/open sessions correctly

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
