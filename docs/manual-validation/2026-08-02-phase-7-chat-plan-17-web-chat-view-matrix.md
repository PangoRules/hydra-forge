# E2E Regression Matrix — Plan 17: Web UI — useChatStream + ChatSessionView + Components

## Validate: Chat streaming UI (Web)

### Setup
- [ ] Branch `task/web-chat-view` checked out, clean worktree
- [ ] `cd src/web-ui && pnpm typecheck` → 0 errors
- [ ] `cd src/web-ui && pnpm lint` → 0 errors (1 unrelated `vue/no-v-html` warning in `ChatMessageBubble.vue:103` is acceptable — content sanitized via marked `Renderer` + protocol strip)
- [ ] `cd src/web-ui && pnpm build` → succeeds
- [ ] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [ ] Server running with chat migrations applied (`dotnet run --project src/HydraForge.Server`)
- [ ] Authenticated user (`auth_token` cookie) with at least one existing `ChatSession` (seed via `POST /api/chat/sessions` or reuse one created in Plan 16 smoke test)
- [ ] Web dev server running (`cd src/web-ui && pnpm dev`); navigate to the chat session in a browser

### Happy Path — Streaming
1. Mount `ChatSessionView` for the session → previous messages load (zero or more), sorted chronologically (oldest at top, newest at bottom)
2. Type `Hello` in the input, press Enter → optimistic user bubble appears at the end of the list, input clears
3. Within 1-2s → server sends `StreamStart` → typing-dots bubble appears
4. Server sends first `StreamDelta` → typing bubble is replaced with a bubble that grows live as content arrives
5. Server sends `StreamDone` → final bubble is committed, a `fetchSession` refetch inserts the persisted assistant message into the list, no flicker
6. Refresh the page → list still shows user message + assistant message in order (persistence confirmed)
7. Send a second message → streaming again, scroll auto-follows new content (user was at bottom); on resize prepended messages scroll position is preserved (windowing sentinel)

### Happy Path — Cancellation
1. Start a stream (send a message that produces a long response)
2. Click the cancel (X) button in the input → `CancelStream` invoked, typing bubble clears, `sendingLock` resets, input re-enabled
3. Refresh → partial assistant message is NOT persisted (server stops on `OperationCanceledException`)

### Happy Path — Connection Lifecycle
1. Open `ChatSessionView` → SignalR `connect()` then `join(sessionId)` are awaited on mount
2. Open DevTools → Network → WS → confirm a WebSocket connection to `/hubs/chat` is established
3. In DevTools console run `window.stop()` to drop the WebSocket → `onclose` fires, backoff schedule starts at `5000ms`
4. Restore connectivity → next reconnect attempt succeeds; `onreconnected` re-invokes `JoinSession` with the previously-joined sessionId
5. Close the modal (trigger `close` emit) → `leave()` then `disconnect()` called; reconnect timer cleared; no leaked socket

### Happy Path — Markdown Rendering
1. Send a message containing `**bold**`, `` `code` ``, and `[link](https://example.com)` → assistant response renders bold, inline code, and a clickable link (not raw markdown)
2. Send a message containing `[bad](javascript:alert(1))` → link href is rewritten to `#blocked-...` (no script execution)
3. Send a message containing `[bad](data:text/html,<script>alert(1)</script>)` → link href is rewritten to `#blocked-...` (no script execution)
4. Send a message containing raw HTML `<script>alert(1)</script>` → does not execute (marked `Renderer.html` is a no-op)

### Happy Path — Image Attach (text-only path)
1. Click the image button → file picker opens (image MIME filter)
2. Select a `.png` file → thumbnail preview appears above the input
3. Click the × button on the thumbnail → preview removed, blob URL revoked
4. Send a message with the image attached → optimistic user message contains the `imagesJson`; **`POST /api/chat/sessions/{id}/messages` request body shape note: the UI sends `images: [{Url, Base64}]` but the server model currently expects `IReadOnlyList<ImageBlock>` with `StorageKey`+`MediaType`. The image-attach POST will fail to deserialize on the server today; this is out of scope for the UI plan and should be resolved in a follow-up that wires a real upload endpoint.**

### Edge Cases — Scroll Behavior
1. Load a session with 60+ messages → list shows the last 50, sentinel is visible at top
2. Scroll to top → sentinel intersects, window grows by 50, scroll position preserved (no jump to top)
3. While scrolled in the middle of the list, a new message arrives → no auto-scroll (user is not at bottom)
4. Scroll to bottom → isAtBottom flag flips; next message arrival auto-scrolls to bottom
5. Mid-stream, scroll up → typing bubble continues to grow off-screen without jumping the viewport

### Edge Cases — Disabled / Empty States
1. Mount a session with `Status !== 'Active'` → input is disabled regardless of streaming state
2. Mount a session with zero messages → empty-state placeholder ("No messages yet") shown until first `streamingMessage` arrives
3. Network drop during `send` → user message is removed from the optimistic list, error toast appears (`toast.error`)

### Regressions
1. Web UI build still green: `pnpm build` → no new errors
2. Nuxt typecheck still green: `pnpm typecheck` → no new errors
3. Existing chat routes added to `routes.ts` use consistent shape with the rest of `ApiRoutes` (camelCase query params, no inline literal URLs in any new component)
4. `useChatStream` callbacks (`onStreamStart`/`onStreamDelta`/`onStreamDone`/`onStreamError`) register exactly once per `ChatSessionView` instance (no leak when modal is opened/closed repeatedly)
5. Server-side `ChatHub` `/hubs/chat` route still mapped (`MapHub<ChatHub>("/hubs/chat")` in `Program.cs:446`) — untouched by this plan
6. Server-side `ChatMessagesController` `[Route("api/chat/sessions/{sessionId:guid}/messages")]` still resolves text-only sends (no `Images` field sent → null on server)
7. Existing `useRealtime` / `usePresence` / `useNotificationHub` composables still build and ship (no shared state with `useChatStream`)

### Cleanup
- [ ] None — no DB writes, no persistent state. Closing the modal stops the WebSocket and revokes any blob URLs.
