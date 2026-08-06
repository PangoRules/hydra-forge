# Manual Validation: Plan 19 — Web UI ChatSessionHeader + ChatDocAttach

**Branch:** `task/web-chat-managers`
**Plan:** `docs/plans/2026-08-02-phase-7-chat-plan-19-web-chat-managers.md`

## Components

### ChatSessionHeader.vue
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Title display | Open personal chat | Session title shown | |
| Title display | Open project chat | Session title shown | |
| Scope toggle | Personal chat → toggle "All docs" | Checkbox visible; `PATCH /api/chat/sessions/{id}` with `searchAllMyDocs`; toggle affects next send | |
| Scope toggle | Project chat | "All docs" checkbox absent | |
| Personality picker | Personal chat → select preset | `PATCH /api/chat/sessions/{id}` with `personalityId`; preset applied | |
| Personality picker | Loading | Spinner shown while fetching presets | |
| Personality picker | Fetch error | Dropdown degrades to "Default" silently | |
| AI edit mode picker | Project chat → PerMutation | Shown; `PATCH` with `aiEditMode: "PerMutation"` | |
| AI edit mode picker | Project chat → Blanket | Shown; `PATCH` with `aiEditMode: "Blanket"` | |
| AI edit mode picker | Personal chat | Picker absent | |
| Fork button | Own shared project chat | Absent (caller owns it) | |
| Fork button | Another user's shared project chat | Visible; clicking `POST /api/chat/sessions` with `forkedFromSessionId`; navigates to new session | |
| Close button | Active chat | Shown; `POST /api/chat/sessions/{id}/close` → status = Closed | |
| Close button | Closed chat | Absent | |
| ChatSessionView integration | Title edit (pencil icon) | Still functional (via `ChatSessionView` inline, not header) | |
| ChatSessionView integration | Export button | Still functional | |
| ChatSessionView integration | Find bar | Still functional | |

### ChatDocAttach.vue
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Attached docs list | Chat with attached docs | Listed with remove button per doc | |
| Add button | Click → open picker | `ChatDocAttachPicker` modal opens | |
| Remove | Click × on attached doc | `DELETE /api/chat/sessions/{id}/documents/{docId}`; removed from list | |
| Empty state | Chat with no attached docs | "No documents attached" or equivalent empty state | |

### ChatDocAttachPicker.vue
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Modal open | Open picker | Lists user's documents with search | |
| Search | Type in search box | Filters documents by title | |
| Pick | Click document row | Emits `picked(documentId)`; modal closes | |
| Empty search | No matching docs | Empty state shown | |

## Acceptance Criteria

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

- [ ] `pnpm typecheck` passes
- [ ] `pnpm lint` passes
- [ ] `pnpm build` succeeds
