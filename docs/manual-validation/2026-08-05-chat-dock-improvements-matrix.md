# Manual Validation Matrix — Slice A.2: Chat Dock Improvements

**Plan:** docs/superpowers/plans/2026-08-05-chat-dock-improvements-design.md
**Spec:** docs/superpowers/specs/2026-08-05-chat-dock-improvements-design.md
**Branch:** task/web-chat-panel

## TC-1: Draft mode — click FAB, no session created
**Steps:**
1. Click the chat FAB.
2. Observe the popup.
**Expected:** Popup opens in draft mode (empty state, no session created server-side). No "New chat" session appears in the history list. No POST to `/api/chat/sessions` was made.

## TC-2: First message creates session
**Steps:**
1. In draft mode, type "Hello" and press Enter.
**Expected:** A new session is created via POST `/api/chat/sessions`. The message is sent. The popup switches to session mode (ChatSessionView renders with the new session).

## TC-3: History panel shows past sessions
**Steps:**
1. Open the chat popup.
2. Click the history button (☰ or chevron).
**Expected:** A scrollable list of past sessions appears. Each item shows title and summary. Clicking a session loads it.

## TC-4: Infinite scroll in history
**Steps:**
1. Have 25+ chat sessions.
2. Open history panel.
3. Scroll to the bottom.
**Expected:** More sessions load automatically (IntersectionObserver triggers `loadMore`). "All caught up" appears when all sessions are loaded.

## TC-5: Session resume on reopen
**Steps:**
1. Open chat, send a message (session created).
2. Close the popup (X or ESC).
3. Reopen the popup.
**Expected:** The same session is loaded (not draft mode). Messages are preserved.

## TC-6: New chat clears to draft
**Steps:**
1. In an active session, click "New chat" (➕).
**Expected:** Popup returns to draft mode. The old session stays active server-side (can be found in history).

## TC-7: Model persistence across refresh
**Steps:**
1. Open a new chat on a board page.
2. Select a specific model from the model picker (e.g., deepseek).
3. Send a message.
4. Refresh the page.
5. Open the chat popup — it resumes the session.
**Expected:** The model picker shows the same model (deepseek) that was selected before the refresh. The server stored `PreferredModelConfigId` on the session.

## TC-8: Correct feature routing on board
**Steps:**
1. On a board page, open the chat popup.
2. Check the model picker's model list.
**Expected:** The model list matches the `ProjectChat` feature's routing config (server routes as `ProjectChat`). Not the `PersonalChat` list.

## TC-9: Card context in new chat
**Steps:**
1. On a board page, open a card modal.
2. Open the chat popup.
3. Click "New chat".
**Expected:** The new session includes `openCardId` set to the open card's ID. F6 implicit close of the prior panel session fires server-side.

## TC-10: Height is half screen
**Steps:**
1. Open the chat popup on a desktop browser.
**Expected:** The popup height is approximately 50% of the viewport height (not the previous fixed 560px).

## TC-11: Auto-title fallback
**Steps:**
1. Open a new chat, send a message.
2. Wait for the reply.
**Expected:** The session gets a title. If the LLM title generator fails (slow local model), the title falls back to the first user message (truncated to 60 chars). The title appears in the dock header and history list.

## TC-12: /chats page infinite scroll
**Steps:**
1. Navigate to `/chats`.
2. Scroll the sidebar session list to the bottom.
**Expected:** More sessions load automatically. Same cursor-paginated behavior as the dock history panel.

## TC-13: /chats page resumes dock session
**Steps:**
1. Open the chat popup, send a message (active session).
2. Click "Chats" in the left navigation.
**Expected:** The `/chats` page opens with the same session loaded in the right panel. The session is highlighted in the sidebar.

## TC-14: Identity prompt is model-honest
**Steps:**
1. Open a new chat.
2. Ask "What can you do?"
**Expected:** The model describes its actual capabilities. It does NOT claim it can create projects. It mentions future planned capabilities. It says "be honest about your capabilities" — a text-only model should not claim vision/image generation.

## TC-15: Title rename in dock
**Steps:**
1. Open a chat session in the popup.
2. Click the title in the header.
3. Type a new title and press Enter.
**Expected:** The title updates via PATCH. The new title appears in the header and in the history list.

## TC-16: Z-index (verify in browser)
**Steps:**
1. On a board page, open a card modal.
2. Open the chat popup (click FAB).
**Expected:** The chat popup is visible and usable above the card modal. You can type in the chat while the card modal remains open behind it.
