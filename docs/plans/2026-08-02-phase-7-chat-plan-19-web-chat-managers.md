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

- [x] `ChatSessionHeader.vue`: title, scope toggle checkbox (`SearchAllMyDocs`), personality picker dropdown, AI-edit-mode picker (project chats only), close button, **Fork button** ("Summarize → start my own" — visible on shared project chats the caller doesn't own, `POST /api/chat/sessions` with `forkedFromSessionId`, navigates to the new session on success). Props: `session`. Emits: `close`, `toggleScope`, `editPersonality`, `editMode`, `fork`
- [x] `ChatDocAttach.vue`: lists attached docs with remove button. Add button opens `ChatDocAttachPicker`. Props: `sessionId`. Emits: `attached`
- [x] `ChatDocAttachPicker.vue`: modal listing owner's documents with search. Emits: `picked(documentId)`
- [x] Scope toggle: `PATCH /api/chat/sessions/{sessionId}` with `searchAllMyDocs`. Takes effect on next send
- [x] Close button: `POST /api/chat/sessions/{sessionId}/close` → F3 explicit close
- [x] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-19-web-chat-managers-matrix.md` — scope toggle changes retrieval on next send; fork button appears only on shared chats the caller doesn't own and produces a new session

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
