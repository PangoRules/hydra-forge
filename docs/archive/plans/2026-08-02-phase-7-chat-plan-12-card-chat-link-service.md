# Plan 12: CardChatLink Service
**Branch:** `task/card-chat-link-service`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 12

**Goal:** List per card, archive, owner-only delete.

**Files:**
- Create: `src/HydraForge.Application/Chat/CardChatLinkService.cs`
- Create: `src/HydraForge.Application/Chat/ICardChatLinkService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/CardChatLinkServiceTests.cs`

**Steps:**

- [x] Define `ICardChatLinkService`: `GetByCardAsync(cardId)`, `ArchiveAsync(linkId, userId)`
- [x] `GetByCardAsync`: return links with owner (id+username), summary, createdAt, archivedAt. Project members only
- [x] `ArchiveAsync`: owner-only soft-delete. Non-owner → `CHAT_SESSION_NOT_OWNER`
- [x] Write tests: project member can list, non-member 403, owner-only archive
- [x] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-12-card-chat-link-service-matrix.md` — closing a project chat with an open card produces a visible `CardChatLink` on that card for all project members

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~CardChatLink"`
