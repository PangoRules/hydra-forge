# Manual Validation Matrix — Slice A: Global Chat Dock + Identity Prompt

**Plan:** docs/superpowers/plans/2026-08-04-slice-a-global-chat-dock.md
**Spec:** docs/superpowers/specs/2026-08-04-slice-a-global-chat-dock-design.md
**Branch:** task/web-chat-panel

## TC-1: Open dock on project list
**Steps:**
1. Navigate to `/projects` (project list page).
2. Click the chat FAB (bottom-right).
**Expected:** Popup window opens. A new chat session is created (user-scoped, no projectId). ChatSessionView renders. Can send a message and receive a reply.

## TC-2: Navigate to board — same session persists
**Steps:**
1. From TC-1, with the chat open, click a project to navigate to `/projects/[id]/board`.
**Expected:** The chat popup stays open with the same session (messages preserved). No new session is auto-created. The session remains user-scoped (it was created on the list).

## TC-3: New chat on board — project-scoped
**Steps:**
1. On `/projects/[id]/board`, with chat open, click "New chat" (plus icon in popup header).
**Expected:** A new session is created with `projectId` set to the current board's project. F6 implicit-close of the prior session fires server-side (prior session gets closed in the background). The popup shows the fresh empty session.

## TC-4: Model switch mid-conversation — identity stays
**Steps:**
1. Open a chat, send a message, get a reply.
2. Switch the model in the chat input.
3. Send another message.
**Expected:** The reply is coherent. The model knows it's HydraForge's assistant (identity System message in history flows to the new model).

## TC-5: /chats page — dock hidden
**Steps:**
1. Navigate to `/chats`.
**Expected:** The chat FAB is NOT visible. No chat dock renders on `/chats` pages.

## TC-6: Open card modal — chat dock usable above modal
**Steps:**
1. On `/projects/[id]/board`, open a card (click a card to open the card modal).
2. With the card modal open, click the chat FAB.
**Expected:** The chat popup opens ON TOP of the card modal (higher z-index). Can interact with the chat while the card modal is open behind it. No backdrop blocks the chat.

## TC-7: Drag the popup window
**Steps:**
1. Open the chat popup.
2. Click and drag the header (title bar) to move the window.
**Expected:** The popup moves with the cursor. Released at the new position.

## TC-8: ESC closes the popup
**Steps:**
1. Open the chat popup.
2. Press Escape.
**Expected:** The popup closes. The FAB reappears. The session is NOT closed (stays Active, resumable on next open).

## TC-9: Identity prompt hidden from UI
**Steps:**
1. Open a new chat session.
2. Inspect the rendered messages.
**Expected:** No "You are HydraForge's built-in assistant..." system message is visible in the chat. Only User/Assistant messages render.

## TC-10: Admin-configurable identity prompt
**Steps:**
1. Navigate to `/admin/settings`.
2. Find the "AI Identity Prompt" card.
3. Enter a custom prompt (e.g., "You are a pirate. Arrr.").
4. Save.
5. Start a NEW chat session (not an existing one).
6. Ask "who are you?".
**Expected:** The reply uses the custom prompt (pirate persona). Existing sessions (created before the change) still use the old prompt.

## TC-11: Collapsing the dock does not close the session
**Steps:**
1. Open chat, send a message.
2. Close the popup (X button or ESC).
3. Reopen the chat (click FAB).
**Expected:** The same session resumes with all messages visible. No new session is created on reopen (only "New chat" button creates a new one).
