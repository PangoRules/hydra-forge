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

> **Reconciled 2026-08-04:** "main menu" below now refers to the real `MainMenuScreen` added in Plan 22 (Login → `MainMenuScreen` → Projects/Chat), not an assumed-but-never-built concept.
>
> Web's `CardChatLinkList` (Plan 20 — a per-card table of past chat summaries in the card modal) has no TUI equivalent planned here. `ProjectChatScreen`'s board-focused-card `c` binding below covers *opening* a project chat scoped to a card (the TUI analog of the web's `ChatPanel`), but not *browsing prior linked chats* from `CardDetailScreen`. Deferred rather than silently dropped — worth a follow-up plan if wanted, not blocking this one.

**Steps:**

- [ ] `ProjectChatScreen.cs`: from board view, focus card + press `c` → opens with `projectId` + `cardId`. F6 implicit close of prior active session. Same layout as `ChatSessionScreen` but project-scoped
- [ ] `PromptPresetScreen.cs`: preset + group CRUD. Launch from `MainMenuScreen` or chat screen `P`
- [ ] `PersonalityScreen.cs`: personality CRUD. Launch from `MainMenuScreen` or chat screen `A`
- [ ] `DocumentListScreen.cs`: personal documents list + upload (path prompt). Launch from `MainMenuScreen`
- [ ] Modify `BoardScreen.cs`: add `c` key binding when card focused → `ProjectChatScreen`
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-23-tui-chat-hub-matrix.md` — `c` from a focused card opens `ProjectChatScreen`; opening a second time triggers F6 without blocking the TUI

**Acceptance:**
- `dotnet build src/HydraForge.Tui`
- `dotnet test` — existing TUI tests pass
