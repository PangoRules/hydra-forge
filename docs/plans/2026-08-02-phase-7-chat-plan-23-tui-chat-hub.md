# Plan 23: TUI — ProjectChatScreen + Preset/Personality/Document Screens
**Branch:** `task/tui-chat-hub`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 23

**Goal:** Project chat from board, preset/personality/document management screens.

**Files:**
- Create: `src/HydraForge.Tui/Screens/ProjectChatScreen.cs`
- Create: `src/HydraForge.Tui/Screens/PromptPresetScreen.cs`
- Create: `src/HydraForge.Tui/Screens/PersonalityScreen.cs`
- Create: `src/HydraForge.Tui/Screens/DocumentListScreen.cs`
- Modify: `src/HydraForge.Tui/Screens/BoardScreen.cs`

**Steps:**

- [ ] `ProjectChatScreen.cs`: from board view, focus card + press `c` → opens with `projectId` + `cardId`. F6 implicit close of prior active session. Same layout as `ChatSessionScreen` but project-scoped
- [ ] `PromptPresetScreen.cs`: preset + group CRUD. Launch from main menu or chat screen `P`
- [ ] `PersonalityScreen.cs`: personality CRUD. Launch from main menu or chat screen `A`
- [ ] `DocumentListScreen.cs`: personal documents list + upload (path prompt). Launch from main menu
- [ ] Modify `BoardScreen.cs`: add `c` key binding when card focused → `ProjectChatScreen`
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-23-tui-chat-hub-matrix.md` — `c` from a focused card opens `ProjectChatScreen`; opening a second time triggers F6 without blocking the TUI

**Acceptance:**
- `dotnet build src/HydraForge.Tui`
- `dotnet test` — existing TUI tests pass
