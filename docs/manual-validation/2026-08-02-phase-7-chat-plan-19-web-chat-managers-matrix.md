# Manual Validation: Plan 19 — Web UI ChatSessionHeader + ChatDocAttach

**Branch:** `task/web-chat-managers`
**Plan:** `docs/plans/2026-08-02-phase-7-chat-plan-19-web-chat-managers.md`

## Setup

1. Start server: `dotnet run --project src/HydraForge.Server`
2. Start web dev: `cd src/web-ui && pnpm dev`
3. Login as **user A** (owner) and **user B** (non-owner collaborator on shared project chat)

---

## ChatSessionHeader

### Session loaded safely (regression)
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Null guard | Open session detail before fetch completes | No crash; loading spinner shown | |
| Null guard | Fetch error | Error message shown; no header crash | |

### Title display + edit
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Title display | Personal chat | Session title shown | |
| Title display | Project chat | Session title shown | |
| Rename button | Owner + Active session | Pencil icon visible; click opens inline input | |
| Rename button | Owner + Closed session | Pencil icon absent | |
| Rename button | Non-owner | Pencil icon absent | |
| Rename submit | Type new name + blur/Enter | `PATCH /api/chat/sessions/{id}` with new title; title updated | |
| Rename cancel | Type + Esc | Input closes; title unchanged | |
| Rename error | Server returns 500 | Toast error; original title restored | |

### Scope toggle (personal chats only)
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Toggle visible | Owner + personal chat + Active | "All docs" checkbox visible | |
| Toggle hidden | Project chat | "All docs" checkbox absent | |
| Toggle disabled | Owner + Closed session | Checkbox disabled | |
| Toggle effect | Toggle on, send message | Next send uses full-doc retrieval | |
| API call | Toggle | `PATCH /api/chat/sessions/{id}` with `searchAllMyDocs: true|false` | |

### Personality picker (personal chats only)
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Picker visible | Owner + personal chat + Active | Dropdown shows personalities | |
| Picker hidden | Project chat | Dropdown absent | |
| Picker disabled | Owner + Closed session | Dropdown disabled | |
| Default option | Server has default personality | "Default" option present; selecting it sends default ID | |
| Loading | While fetching | Spinner shown | |
| Fetch error | Server returns 500 | Error toast; picker degrades gracefully | |
| Change persists | Select personality, send message | `PATCH /api/chat/sessions/{id}` with `personalityId` | |

### AI edit mode (project chats only)
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Picker visible | Owner + project chat + Active | Dropdown with PerMutation/Blanket | |
| Picker hidden | Personal chat | Dropdown absent | |
| Picker disabled | Owner + Closed session | Dropdown disabled | |
| Change persists | Select mode, send message | `PATCH /api/chat/sessions/{id}` with `aiEditMode` | |

### Fork button (shared project chats)
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Fork hidden | Own shared project chat | Button absent | |
| Fork hidden | Personal chat | Button absent | |
| Fork visible | Another user's shared project chat | "Fork" button visible | |
| Fork flow | Click Fork | `POST /api/chat/sessions` with `forkedFromSessionId`; navigates to new session | |
| Fork folderId | Forked session | New session has no folderId (not source user's folder) | |

### Close button
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Close visible | Owner + Active session | Close button visible | |
| Close hidden | Owner + Closed session | Close button absent | |
| Close hidden | Non-owner | Close button absent | |
| Close effect | Click Close | `POST /api/chat/sessions/{id}/close`; session status → Closed; toast; all controls disable | |

### Export, Find (regression)
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Export button | Any owner | Download icon visible; click downloads `.md` file | |
| Export button | Non-owner | Export button absent | |
| Find button | Any user | Search icon visible in header area | |
| Find bar | Click Find | Bar appears with input, prev/next, close | |
| Find navigation | Type query, hit Enter | Highlights match in message list; count shown `1/N` | |
| Find prev/next | Click arrows | Cycles through matches | |
| Find close | Esc or × | Bar closes; highlight cleared | |
| highlightMessageId | Find with matches | `ChatMessageList` highlights current match | |

---

## ChatDocAttach

### Attached docs panel
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Panel mounted | Owner + Active session | Panel visible below message list | |
| Panel hidden | Non-owner | Panel absent | |
| Panel hidden | Closed session | Panel absent | |
| Loading | On mount | Spinner shown | |
| Fetch error | Server error | Error message shown; toast error | |
| Empty state | No docs attached | "No documents attached" | |
| Doc list | Has attached docs | List with title + remove button per doc | |
| Remove visible | Desktop hover | × button appears on hover | |
| Remove visible | Mobile/focus | × button always visible; focus-visible style | |
| Remove error | Server returns error | Toast error | |
| Remove success | Remove doc | `DELETE /api/chat/sessions/{id}/documents/{docId}`; removed from list; toast | |

### Picker modal
| Feature | Test | Expected | Pass |
|---------|------|----------|------|
| Modal open | Click + button | Modal opens with document list | |
| Modal close | Click backdrop/Esc | Modal closes | |
| Search | Type in search box | Debounced search; results update | |
| Search cancel | Overlapping searches | Stale responses discarded | |
| Search error | Server error | Error message shown; toast error | |
| Empty search | No matching docs | Empty state shown | |
| Pick | Click document | `POST /api/chat/sessions/{id}/documents` with `documentId`; toast; modal closes | |
| Pick error | Server returns error | Toast error; modal stays open | |

---

## Acceptance Criteria

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

- [ ] `pnpm typecheck` passes
- [ ] `pnpm lint` passes
- [ ] `pnpm build` succeeds
