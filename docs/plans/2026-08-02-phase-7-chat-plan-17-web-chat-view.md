# Plan 17: Web UI — useChatStream + ChatSessionView + Components
**Branch:** `task/web-chat-view`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 17

**Goal:** `useChatStream` composable, `ChatSessionView`, `ChatMessageList`, `ChatMessageBubble`, `ChatInput` with streaming.

**Files:**
- Create: `src/web-ui/app/composables/useChatStream.ts`
- Create: `src/web-ui/app/components/chat/ChatSessionView.vue`
- Create: `src/web-ui/app/components/chat/ChatMessageList.vue`
- Create: `src/web-ui/app/components/chat/ChatMessageBubble.vue`
- Create: `src/web-ui/app/components/chat/ChatInput.vue`
- Modify: `src/web-ui/app/lib/routes.ts`

**Steps:**

- [ ] Add `ApiRoutes.Chat` to `routes.ts`: sessions, messages, folders, presets, groups, personalities, cardLinks, documents, search
- [ ] `useChatStream.ts`: wraps `@microsoft/signalr` `HubConnection` to `/hubs/chat`. Expose `connect()`, `join(sessionId)`, `leave()`, `send(content, images?, presetId?)`, `cancel()`. Internally: (1) `POST` persist user message → get `messageId`, (2) `hubConnection.invoke('SendMessage', sessionId, messageId, presetId)`. Reactive `streamingMessage` ref. Toasts on `StreamError`. Reconnect with backoff
- [ ] `ChatSessionView.vue`: top-level container. Owns SignalR connection, message list, input, streaming state. Props: `sessionId`
- [ ] `ChatMessageList.vue`: virtualized list. Renders `ChatMessageBubble` per message + live "typing" bubble for in-flight assistant. Props: `messages[]`, `streamingMessageId?`
- [ ] `ChatMessageBubble.vue`: markdown render (markdown-it), image thumbnails for vision messages. Props: `message`
- [ ] `ChatInput.vue`: textarea + image attach + preset chip + send/cancel. Enter send, Shift+Enter newline. Props: `disabled`, `personalityId`, `presetId`. Emits: `send(content, images?)`, `cancel`

**Acceptance:**
- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
