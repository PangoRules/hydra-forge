# Plan 21: Web UI — PromptPresetManager + PersonalityManager + DocumentUploader + pages/documents.vue
**Branch:** `task/web-managers-docs`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 21
**Test scope:** unit

**Goal:** Page-level managers for prompt presets (groups + presets with drag-to-reorder) and personalities (CRUD + set default), a document uploader component with chunking/embedding progress, and a personal documents page.

**Files:**
- Create: `src/web-ui/app/components/chat/PromptPresetManager.vue`
- Create: `src/web-ui/app/components/chat/PersonalityManager.vue`
- Create: `src/web-ui/app/components/chat/DocumentUploader.vue`
- Create: `src/web-ui/app/pages/chat/presets.vue`
- Create: `src/web-ui/app/pages/chat/personalities.vue`
- Create: `src/web-ui/app/pages/documents.vue`
- Modify: `src/web-ui/app/types/chat.ts` — add `PromptPresetGroupDto`, `DocumentDto`
- Modify: `src/web-ui/app/lib/routes.ts` — add `UiRoutes.Chat.Presets`, `UiRoutes.Chat.Personalities`, `UiRoutes.Documents`

**Steps:**

- [x] Add missing TypeScript types to `src/web-ui/app/types/chat.ts`:
  ```ts
  export interface PromptPresetGroupDto {
    id: string
    name: string
    createdAt: string
    updatedAt: string
    archivedAt: string | null
    presets: PromptPresetDto[]
  }

  export interface DocumentDto {
    id: string
    title: string
    contentType: string
    language: string | null
    version: number
    createdAt: string
    updatedAt: string
    archivedAt: string | null
  }
  ```

- [x] Add `UiRoutes` entries to `src/web-ui/app/lib/routes.ts` under `UiRoutes`:
  ```ts
  Chat: {
    Presets: '/chat/presets',
    Personalities: '/chat/personalities'
  },
  Documents: '/documents'
  ```
  Integrate into existing `UiRoutes` object — `Chat` already has `ChatSessions`; add `Presets` and `Personalities` alongside it. `Documents` is a top-level key.

- [x] `PromptPresetManager.vue` — page-level component. Two-column layout on desktop (groups left, presets right), stacked on mobile. Groups panel: list of groups with create/rename/archive buttons. Archive a group → `DELETE /api/chat/preset-groups/{groupId}` (server nulls presets' `GroupId`, presets kept ungrouped per spec §1.10). Presets panel: list of presets in selected group (or ungrouped). Create/edit/archive preset. Drag-to-reorder within a group via native HTML5 DnD (no `vue-draggable-plus` — removed per repo convention). Reorder: track `position` locally, PATCH preset with new `groupId`/position on drop. Preset form: name + content (textarea). Uses `useApi()`, `useAppToast()`, `ApiRoutes.Chat.presets.*` and `ApiRoutes.Chat.presetGroups.*`. All API calls wrapped in try/catch (D-40).

- [x] `PersonalityManager.vue` — page-level component. Full-page list of personalities with create/edit/archive/set-default. Reuses the same CRUD pattern as `PersonalityManageModal.vue` but as a standalone page (not a modal). List view: each personality shows name, description, system prompt preview, "Default" badge if `isDefault`, and action buttons (edit, set default, archive). Create/edit form: name input, description input, system prompt textarea. "Set default" button calls `POST /api/chat/personalities/{personalityId}/default`. Uses `useApi()`, `useAppToast()`, `ApiRoutes.Chat.personalities.*`. All API calls wrapped in try/catch (D-40).

- [ ] `DocumentUploader.vue` — drag-drop zone or file picker button. Accepts text/markdown/code/csv/html files (PDF deferred per spec §11). On file select: `POST /api/documents` as multipart form with `file` + `title` + `contentType`. Shows progress states: "Uploading…" → "Chunking & embedding…" → "Done" (or error toast). Emits `uploaded(document: DocumentDto)` on success. Uses `useApi()`, `useAppToast()`, `ApiRoutes.Chat.documents.create()`. All API calls wrapped in try/catch (D-40).

- [ ] `pages/chat/presets.vue` — thin page wrapper. `definePageMeta({ middleware: ['auth'] })`. Hosts `<PromptPresetManager />`. Page title "Prompt Presets" with a back link to `/chats`.

- [ ] `pages/chat/personalities.vue` — thin page wrapper. `definePageMeta({ middleware: ['auth'] })`. Hosts `<PersonalityManager />`. Page title "Personalities" with a back link to `/chats`.

- [ ] `pages/documents.vue` — personal documents page. `definePageMeta({ middleware: ['auth'] })`. Two sections: (1) document list — fetches `GET /api/documents`, renders as a table or card grid with title, content type, version, date, archive button. (2) upload area — hosts `<DocumentUploader />` at top, refreshes list on `@uploaded`. Archive calls `DELETE /api/documents/{documentId}`. Uses `useApi()`, `useAppToast()`, `ApiRoutes.Chat.documents.*`. All API calls wrapped in try/catch (D-40).

- [ ] Verify: `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
- Presets page: create a group, create presets in it, drag to reorder, archive group (presets become ungrouped), archive a preset
- Personalities page: create, edit, set default, archive a personality
- Documents page: upload a text/markdown file, see it in the list, archive it