# Plan 19: Web UI — ChatSessionHeader + ChatDocAttach
**Branch:** `task/web-chat-managers`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 19

**Goal:** Session header (scope toggle, personality, AI-edit mode, close) + document attachment UI.

**Files:**
- Create: `src/web-ui/app/components/chat/ChatSessionHeader.vue`
- Create: `src/web-ui/app/components/chat/ChatDocAttach.vue`
- Create: `src/web-ui/app/components/chat/ChatDocAttachPicker.vue`

**Steps:**

- [ ] `ChatSessionHeader.vue`: title, scope toggle checkbox (`SearchAllMyDocs`), personality picker dropdown, AI-edit-mode picker (project chats only), close button. Props: `session`. Emits: `close`, `toggleScope`, `editPersonality`, `editMode`
- [ ] `ChatDocAttach.vue`: lists attached docs with remove button. Add button opens `ChatDocAttachPicker`. Props: `sessionId`. Emits: `attached`
- [ ] `ChatDocAttachPicker.vue`: modal listing owner's documents with search. Emits: `picked(documentId)`
- [ ] Scope toggle: `PATCH /api/chat/sessions/{sessionId}` with `searchAllMyDocs`. Takes effect on next send
- [ ] Close button: `POST /api/chat/sessions/{sessionId}/close` → F3 explicit close

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
