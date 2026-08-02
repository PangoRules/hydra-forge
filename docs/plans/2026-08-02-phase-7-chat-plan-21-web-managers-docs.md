# Plan 21: Web UI — PromptPresetManager + PersonalityManager + DocumentUploader
**Branch:** `task/web-managers-docs`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 21

**Goal:** Preset/group CRUD UI, personality CRUD UI, document upload page.

**Files:**
- Create: `src/web-ui/app/components/chat/PromptPresetManager.vue`
- Create: `src/web-ui/app/components/chat/PersonalityManager.vue`
- Create: `src/web-ui/app/components/chat/DocumentUploader.vue`
- Create: `src/web-ui/app/pages/chat/presets.vue`
- Create: `src/web-ui/app/pages/chat/personalities.vue`
- Create: `src/web-ui/app/pages/documents.vue`

**Steps:**

- [ ] `PromptPresetManager.vue`: CRUD for groups + presets. Drag-to-reorder within group (native HTML5 DnD). Group archive → presets ungrouped (not deleted)
- [ ] `PersonalityManager.vue`: CRUD for personalities. "Set default" button. System prompt textarea
- [ ] `DocumentUploader.vue`: drag-drop or file picker. Shows chunking/embedding progress. Emits: `uploaded`
- [ ] `pages/chat/presets.vue`: hosts `PromptPresetManager`
- [ ] `pages/chat/personalities.vue`: hosts `PersonalityManager`
- [ ] `pages/documents.vue`: personal documents list + `DocumentUploader`

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
