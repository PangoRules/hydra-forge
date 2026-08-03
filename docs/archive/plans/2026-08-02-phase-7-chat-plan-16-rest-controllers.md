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

- [x] `ChatSessionsController`: `[Route("api/chat/[controller]")]`. POST create (F6 async close; body accepts `forkedFromSessionId?` for the fork action, spec §7), GET list, GET detail, PATCH update, POST close, DELETE archive, POST attach doc, GET list docs, DELETE detach doc, GET permission
- [x] `ChatMessagesController`: GET history, POST user message (persist only — hub streams)
- [x] `ChatFoldersController`: CRUD, max-depth-2
- [x] `PromptPresetsController` + `PromptPresetGroupsController`: CRUD, group archive nulls presets
- [x] `AgentPersonalitiesController`: CRUD, set-default
- [x] `CardChatLinksController`: GET by card, DELETE archive
- [x] `DocumentsController`: POST upload (multipart), GET list, DELETE archive
- [x] `ChatSearchController`: GET search
- [x] Wire DI in `Program.cs`: register all services, repositories, `ChatHub`
- [x] All controllers use `[Authorize(Policy = AuthPolicies.UserIdRequired)]`, `[ApiController]`, `[ProducesResponseType]`
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-16-rest-controllers-matrix.md` — `.http` smoke test per controller (per repo convention), verifying each resolved route matches what's documented in the spec. **Not done** — no `.http` files or matrix exist for chat controllers despite this being marked complete. A route-introspection check during review (`IActionDescriptorCollectionProvider`, no DB needed) caught `ChatMessagesController`'s route resolving to `api/chat/messages/sessions/{sessionId}/messages` instead of the spec'd `api/chat/sessions/{sessionId}/messages` — exactly the class of bug this step exists to catch. Fixed in review, but the `.http` suite + matrix itself still needs to be written before this step can be honestly checked off.

**Acceptance:**
- `dotnet build`
- `dotnet test` — all existing tests pass
