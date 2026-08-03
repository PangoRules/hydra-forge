# Plan 10: PromptPreset + PromptPresetGroup Services
**Branch:** `task/prompt-preset-service`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 10

**Goal:** CRUD for presets and groups. Group archive nulls presets' GroupId (keeps presets).

**Files:**
- Create: `src/HydraForge.Application/Chat/PromptPresetService.cs`
- Create: `src/HydraForge.Application/Chat/IPromptPresetService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/PromptPresetServiceTests.cs`

**Steps:**

- [x] Define `IPromptPresetService`: `CreatePresetAsync`, `ListPresetsAsync(groupId?)`, `UpdatePresetAsync`, `ArchivePresetAsync`, `CreateGroupAsync`, `ListGroupsAsync`, `UpdateGroupAsync`, `ArchiveGroupAsync`
- [x] `ArchiveGroupAsync`: soft-archive group, set `GroupId = null` on all presets in group (presets kept, not archived — per §1.10)
- [x] `ListPresetsAsync`: `?groupId=` empty → ungrouped only; `?groupId=guid` → group's presets; no param → all
- [x] `CreatePresetAsync`: validate `groupId` exists if set, validate ownership
- [x] Write tests: group archive nulls presets, ungrouped preset CRUD, ownership validation
- [x] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-10-prompt-preset-service-matrix.md` — archiving a group leaves its presets intact and ungrouped, not deleted

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~PromptPreset"`
