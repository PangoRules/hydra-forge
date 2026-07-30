# Plan 19: Web UI admin routing

**Branch:** `task/webui-admin-routing`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 19

## Steps

### 1. Create routing page
- File: `src/web-ui/app/pages/admin/routing.vue`
- `DataTable` with one row per `AiFeature` (11 rows).
- Columns: Feature name (human-readable), Default Tier (select: Economy/Standard/Premium), Max User Tier (select with null option = "Locked").
- Inline editing: changing a select auto-saves via PUT to `/api/admin/routing/{feature}`.
- Toast on success/failure.

### 2. Feature name mapping
- File: `src/web-ui/app/lib/ai-feature.ts` (new)
- Map `AiFeature` enum values to display names:
  ```ts
  export const AI_FEATURE_LABELS: Record<string, string> = {
    PersonalChat: 'Personal Chat',
    ProjectChat: 'Project Chat',
    DeepResearch: 'Deep Research',
    AgentPipeline: 'Agent Pipeline',
    MemoryExtraction: 'Memory Extraction',
    NotesClassification: 'Notes Classification',
    DocumentEditing: 'Document Editing',
    CardReview: 'Card Review',
    ImageChat: 'Image Chat',
    ImageDocument: 'Image Document',
    ImageGalleryEditor: 'Image Gallery Editor',
  }
  ```

### 3. Tier select component
- Reuse or create simple `USelect` with Economy/Standard/Premium options.
- For MaxUserTier, add "Locked (default only)" option that sends `null`.

## Verification
- `pnpm typecheck`
- `pnpm lint`
- `pnpm build`
- Manual: navigate to `/admin/routing`, change a tier, verify persisted on reload.