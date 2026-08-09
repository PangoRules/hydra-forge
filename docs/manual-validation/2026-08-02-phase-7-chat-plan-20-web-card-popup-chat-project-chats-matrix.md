# Manual Validation — Plan 20: Web UI — Card Popup Chat Tab + Project Chats Tab + Context-Aware ChatDock + CardChatLinkList + Card Linking

**Branch:** `task/web-card-popup-chat`
**Parent:** `feat/phase-7-chat`
**Date:** 2026-08-08
**Spec:** `2026-08-02-phase-7-chat-design.md` §2.1, §5.1, §5.2, §5.4

## Environment

- API server running with `ASPNETCORE_ENVIRONMENT=Development`
- Web UI dev server (`pnpm dev`) up
- At least 2 test users: one project owner, one project member (non-owner)
- A project with ≥1 card, ≥1 chat session

---

## Validation Matrix

| # | Scenario | Steps | Expected | Pass |
|---|----------|-------|----------|------|
| 1 | Card opens as CardPopup | Click a card on the board | Card opens as a draggable popup (not the old modal). Title shows in header. | |
| 2 | Multiple card popups coexist | Open card 1, then card 2, then card 3 | Three popups visible, cascaded positions. | |
| 3 | Max-3 enforcement | Try to open a 4th card | Error toast "Close a card popup first (max 3 open)". No 4th popup. | |
| 4 | Card popup "Chat" tab | Open a card popup, click "Chat" tab | `CardChatLinkList` renders. If no linked chats, shows "No chats linked" message. | |
| 5 | CardChatLinkList after close | Close a project chat that was opened from a card. Reopen the card popup → Chat tab | The closed chat appears as a linked chat with summary. | |
| 6 | Open linked session from card popup | Click the "open" button on a linked chat row | ChatDock opens with that session loaded. | |
| 7 | Project "Chats" tab | Navigate to a project, click "Chats" tab | Lists chats the user participated in (own + project-member). | |
| 8 | Chats tab filters | Switch between All / Project / Card filters | All shows all sessions; Project shows sessions without openCardId; Card shows sessions with openCardId. | |
| 9 | Open session from Chats tab | Click a session row in the Chats tab | ChatDock opens with that session loaded. | |
| 10 | Context-aware dock — board origin | Open dock from board header toggle. Create new chat. | New session has projectId set, no openCardId. | |
| 11 | Context-aware dock — card-popup origin | Open a card popup, go to Chat tab, click "new chat" (or open dock from there). Create new chat. | New session has projectId + openCardId set. | |
| 12 | ChatLinkCardButton appears | Open dock with a project session active. Open a card popup (different card than session's openCardId). | "Link card" button appears in dock header. | |
| 13 | ChatLinkCardButton links card | Click "Link card" button. | Toast "Card linked to chat". Button disappears. Session's openCardId updated. | |
| 14 | ChatLinkCardButton hidden when already linked | Session already linked to card X. Open card X popup. | Button does NOT appear (already linked). | |
| 15 | Escape LIFO with card popups + dock | Open dock, then open 2 card popups. Press Escape 3 times. | Card 2 closes, card 1 closes, dock closes (LIFO order). | |
| 16 | List visibility — project member sees project chats | As a project member (not owner), open Chats tab. | Project chats where you're a member appear. | |
| 17 | List visibility — non-member doesn't see project chats | As a non-member, try to view project chats. | No project chats visible (or access denied). | |
| 18 | No regression — personal chats still work | Open `/chats` page. | Personal chats still listed. Create/send/close all work. | |

---

## Backend Verification

### List visibility (Step 1)

| Test | Command | Expected |
|------|---------|----------|
| Owner sees own sessions | `dotnet test --filter "ListAsync_ReturnsOnlyCallersNonArchivedSessions"` | Pass |
| Member sees project sessions | `dotnet test --filter "ListAsync_ParticipationIn"` | Pass |
| Non-member doesn't see project session | `dotnet test --filter "ListAsync_NonMemberExcluded"` | Pass |
| Admin sees all | `dotnet test --filter "ListAsync_AdminSeesAll"` | Pass |

### Card linking (Step 2)

| Test | Command | Expected |
|------|---------|----------|
| Link active project session | `dotnet test --filter "LinkCardAsync_ActiveProjectSession"` | Pass |
| Link closed session → error | `dotnet test --filter "LinkCardAsync_ClosedSession"` | Pass |
| Link personal session → error | `dotnet test --filter "LinkCardAsync_PersonalSession"` | Pass |
| Card from different project → error | `dotnet test --filter "LinkCardAsync_CardDifferentProject"` | Pass |
| Non-member non-owner → denied | `dotnet test --filter "LinkCardAsync_NonMemberDenied"` | Pass |
| Idempotent (same card again) | `dotnet test --filter "LinkCardAsync_Idempotent"` | Pass |
| Project member (not owner) can link | `dotnet test --filter "LinkCardAsync_MemberCanLink"` | Pass |

---

## Notes

- `@mention`-based card auto-linking is **V2** (deferred — spec §5.4 + §11). Manual `ChatLinkCardButton` is the MVP.
- TUI equivalent of card-popup "Chat" tab / project "Chats" tab is **deferred** per Plan 23.
- A dedicated `?cardLinked=true` backend filter for the project "Chats" tab is **deferred** — this plan groups client-side.
