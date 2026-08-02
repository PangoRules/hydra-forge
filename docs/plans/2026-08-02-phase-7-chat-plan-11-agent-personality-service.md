# Plan 11: AgentPersonality Service
**Branch:** `task/agent-personality-service`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 11

**Goal:** CRUD, default management (one default per user).

**Files:**
- Create: `src/HydraForge.Application/Chat/AgentPersonalityService.cs`
- Create: `src/HydraForge.Application/Chat/IAgentPersonalityService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/AgentPersonalityServiceTests.cs`

**Steps:**

- [ ] Define `IAgentPersonalityService`: `CreateAsync`, `ListAsync`, `UpdateAsync`, `ArchiveAsync`, `SetDefaultAsync`
- [ ] `CreateAsync`: if `isDefault=true`, clear existing default for user
- [ ] `SetDefaultAsync`: set `IsDefault=true` on target, clear all others for user
- [ ] `ArchiveAsync`: soft-delete via `ArchivedAt`. FK `OnDelete: SetNull` only fires on hard-delete (housekeeping), not here
- [ ] Write tests: default uniqueness, archive doesn't null session FK, CRUD ownership

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~AgentPersonality"`
