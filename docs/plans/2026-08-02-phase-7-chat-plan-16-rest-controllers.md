# Plan 16: REST Controllers + Program.cs Wiring
**Branch:** `task/rest-controllers`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 16

**Goal:** All REST endpoints for sessions, messages, folders, presets, personalities, card-links, documents, search. Wire DI.

**Files:**
- Create: `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/ChatMessagesController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/ChatFoldersController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/PromptPresetsController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/PromptPresetGroupsController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/AgentPersonalitiesController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/CardChatLinksController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/DocumentsController.cs`
- Create: `src/HydraForge.Server/Controllers/Chat/ChatSearchController.cs`
- Modify: `src/HydraForge.Server/Program.cs`

**Steps:**

- [ ] `ChatSessionsController`: `[Route("api/chat/[controller]")]`. POST create (F6 async close), GET list, GET detail, PATCH update, POST close, DELETE archive, POST attach doc, GET list docs, DELETE detach doc, GET permission
- [ ] `ChatMessagesController`: GET history, POST user message (persist only — hub streams)
- [ ] `ChatFoldersController`: CRUD, max-depth-2
- [ ] `PromptPresetsController` + `PromptPresetGroupsController`: CRUD, group archive nulls presets
- [ ] `AgentPersonalitiesController`: CRUD, set-default
- [ ] `CardChatLinksController`: GET by card, DELETE archive
- [ ] `DocumentsController`: POST upload (multipart), GET list, DELETE archive
- [ ] `ChatSearchController`: GET search
- [ ] Wire DI in `Program.cs`: register all services, repositories, `ChatHub`
- [ ] All controllers use `[Authorize(Policy = AuthPolicies.UserIdRequired)]`, `[ApiController]`, `[ProducesResponseType]`

**Acceptance:**
- `dotnet build`
- `dotnet test` — all existing tests pass
