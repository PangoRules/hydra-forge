# Plan 9: ChatFolder Service
**Branch:** `task/folder-service`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 9

**Goal:** CRUD, max-depth-2 enforcement, archive cascade via `ChatArchiveService`.

**Files:**
- Create: `src/HydraForge.Application/Chat/ChatFolderService.cs`
- Create: `src/HydraForge.Application/Chat/IChatFolderService.cs`
- Create: `src/HydraForge.Application/Chat/ChatArchiveService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/ChatFolderServiceTests.cs`

**Steps:**

- [ ] Define `IChatFolderService`: `CreateAsync`, `ListAsync`, `UpdateAsync`, `ArchiveAsync`
- [ ] `CreateAsync`: validate max depth 2 (count parents). If `parentFolderId` set, verify parent depth ≤ 1
- [ ] `ListAsync`: flat list with `ParentFolderId` (tree shape reconstructed client-side). Filter by `projectId`
- [ ] `ArchiveAsync`: delegate to `ChatArchiveService.ArchiveFolder` — sets `ArchivedAt` on folder + all child sessions
- [ ] `ChatArchiveService`: `ArchiveFolder(folderId)` → cascade archive sessions, `ArchiveSession(sessionId)` → soft-archive
- [ ] Write tests: depth-3 rejection, archive cascade, empty folder archive

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatFolder"`
