# Phase 7 — Chat (General & Project) Design

**Branch:** `feat/phase-7-chat`
**Date:** 2026-08-02
**Goal:** Full chat system built on Phase 6 LLM infrastructure — personal chats, project chat panel, RAG, prompt presets, personalities, streaming, and the session-lifecycle + AI-edit-permission model that Phase 8 agent edits depend on.

---

## Settled Decisions (input)

| ID | Decision |
|---|---|
| **F1=C** | Session-scoped RAG by default + optional "search all my docs" toggle per session. New `ChatSessionDocument` join entity. |
| **F2=C** | AI edit permission revoked on (a) explicit "End session" action, and (b) new `ChatSession` creation from the same panel. |
| **F3=B** | `CardChatLink` + summary both generated only on explicit "Close session" / panel close. One LLM call. |
| **F4=A** | `PromptPresetGroup` = named container entity. `PromptPreset` belongs to ≤1 group (nullable FK). |
| **F5=A** | Extend `Application.Llm.ChatMessage` with `IReadOnlyList<ImageBlock>? Images`. |
| **F6** | New chat from project-chat panel → new session created and returned immediately; old session's implicit close (`Status=Closed`, `CardChatLink`+summary, AI edit permission revoked) runs as an async background job, does not block the request. One LLM call, off the request path. |

---

## 1. Data Model

All schema changes go through EF Core migrations. New entities live under `src/HydraForge.Domain/Entities/Chat/` (and `PromptPreset`/`PromptPresetGroup` under `Entities/Chat/` for cohesion). `AgentPersonality` already exists under `Entities/PersonalSpace/` and is reused as-is.

### 1.1 Enums (new, under `Domain/Enums`)

```
ChatSessionStatus { Active = 1, Closed = 2 }
AiEditMode        { PerMutation = 1, Blanket = 2 }
```

### 1.2 `ChatSession` (modified)

Existing fields retained. New fields:

| Field | Type | Description |
|---|---|---|
| `Status` | `ChatSessionStatus` | `Active` on creation. `Closed` on explicit/implicit close. Drives AI-edit-permission gating (F2). |
| `AiEditMode` | `AiEditMode` | `PerMutation` (default) — each AI board mutation requires a confirmation dialog. `Blanket` — session-level permission, no per-mutation confirm. Only meaningful for project chats; ignored for personal chats. |
| `SearchAllMyDocs` | `bool` | RAG scope toggle (F1). `false` (default) = retrieve only from documents joined via `ChatSessionDocument`. `true` = retrieve across all of the owner's `DocumentChunk` rows. |
| `PersonalityId` | `Guid?` | FK to `AgentPersonality`. Nullable — no personality = empty system prompt beyond project context. |
| `OpenCardId` | `Guid?` | The card context the session was opened with (project chats). Not an enforced FK (card may be archived); tracked for `CardChatLink` generation on close. |
| `ClosedAt` | `DateTime?` | Set when `Status` flips to `Closed`. |
| `Summary` | `string?` | LLM-generated summary produced on close (F3). For project chats with an `OpenCardId`, the same string is also written to the generated `CardChatLink.Summary`. Nullable until first close. |

> **State transitions** (encapsulated on the entity, called by services — never set properties directly per repo convention):
> - `Open(personalityId?, openCardId?, aiEditMode)` — sets `Status=Active`, defaults.
> - `Close(summary)` — sets `Status=Closed`, `ClosedAt=now`, `Summary=summary`. Idempotent: closing an already-Closed session is a no-op.
> - `ToggleSearchAllMyDocs(value)` — flips the bool.
> - `SetAiEditMode(mode)` — only allowed while `Status=Active`.

### 1.3 `ChatMessage` (modified)

Existing fields retained. New field:

| Field | Type | Description |
|---|---|---|
| `ImagesJson` | `string?` | JSON-serialized `ImageBlock[]` for vision messages (`[{"storageKey":"...","mediaType":"image/png"}]`). Null for text-only messages. Kept as JSON column (not a child entity) to keep the entity count at the agreed set and because images are write-once message payload, not queryable domain data. |

> The Domain `ChatMessage` carries the persisted form; the Application `Llm.ChatMessage` DTO (F5) carries the in-flight form with `IReadOnlyList<ImageBlock>? Images`. A mapper converts between them.

### 1.4 `ChatSessionDocument` (new — F1)

Join entity binding a document to a session for session-scoped RAG retrieval.

| Field | Type | Description |
|---|---|---|
| `Id` | `Guid` | PK |
| `SessionId` | `Guid` | FK to `ChatSession` (cascade delete on session hard-delete) |
| `DocumentId` | `Guid` | FK to `Document` (no cascade — document deletion removes chunks; the join row is cleaned by housekeeping) |
| `AddedByUserId` | `Guid` | FK to `User` |
| `AddedAt` | `DateTime` | |

> Unique index on `(SessionId, DocumentId)`. A document can be attached to many sessions; a session can have many documents. Retrieval scope (F1): when `ChatSession.SearchAllMyDocs == false`, only `DocumentChunk` rows whose `DocumentId ∈ (SELECT DocumentId FROM ChatSessionDocument WHERE SessionId = @s)` AND `UserId = session.OwnerId` are candidates. When `true`, all `DocumentChunk` rows with `UserId = session.OwnerId` are candidates (the join is ignored).

### 1.5 `PromptPresetGroup` (new — F4)

| Field | Type | Description |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK to `User` |
| `Name` | `string` | Display name |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |
| `ArchivedAt` | `DateTime?` | Soft-delete |

### 1.6 `PromptPreset` (new — F4)

| Field | Type | Description |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK to `User` |
| `GroupId` | `Guid?` | FK to `PromptPresetGroup` (nullable — preset may be ungrouped). Cascade delete on group archive. |
| `Name` | `string` | Display name |
| `Content` | `string` | The prompt body (markdown) |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |
| `ArchivedAt` | `DateTime?` | Soft-delete |

> A preset belongs to ≤1 group (F4=A). Ungrouped presets have `GroupId = null`. Injecting a preset into a session prepends its `Content` as a `CacheBlock(SystemContext)` (or appends to the next user message — see §4.4). When a group is archived, its presets are **not** archived — `GroupId` is set to `null` on all of them (ungrouped, kept). See §1.10 and §2.4, which are now the single source of truth for this behavior.

### 1.7 `AgentPersonality` (existing, unchanged)

Reused as-is. `IsDefault` (one per user) drives the default personality preselected on new sessions.

### 1.8 `CardChatLink` (existing, unchanged)

Generated on close (F3) when `ChatSession.OpenCardId != null` AND `ChatSession.ProjectId != null`. The `Summary` field is populated with the same string as `ChatSession.Summary`.

### 1.9 `ChatFolder`, `ChatMessage` (existing, retained)

`ChatFolder` unchanged. `ChatMessage` modified per §1.3.

### 1.10 EF configuration notes

- `ChatSession.Status`, `AiEditMode` → enum-to-int columns.
- `ChatSessionDocument` → composite unique index `(SessionId, DocumentId)`.
- `PromptPreset.GroupId` → FK with `OnDelete: Cascade`, but that cascade only fires on **hard**-delete (housekeeping deleting the group row for real — then its presets go too). The normal **soft**-archive path (`DELETE /api/chat/preset-groups/{groupId}`) is an Application-service operation that sets `GroupId = null` on every preset in the group and leaves the presets themselves untouched — archiving a group never deletes or archives a user's presets, only ungroups them. §1.6, §1.10, and §2.4 all describe this same nulling behavior; do not reintroduce a "presets get archived too" path.
- `ChatSession.PersonalityId` → FK to `AgentPersonality` with `OnDelete: SetNull`. This only fires on hard-delete. `AgentPersonality` is soft-deleted via `ArchivedAt` in normal use (confirmed: `src/HydraForge.Domain/Entities/PersonalSpace/AgentPersonality.cs` has no hard-delete path), so archiving a personality does **not** null `ChatSession.PersonalityId` — the FK stays populated pointing at an archived row. The read/build path must check `ArchivedAt` explicitly instead of relying on the FK: see §4.2 step 6 and the "Personality archived mid-session" row in §7.
- `ChatMessage.ImagesJson` → `nvarchar(max) NULL`.
- No new pgvector columns — `DocumentChunk.Embedding` (existing `vector(1536)`) is reused for RAG.

---

## 2. API Endpoints (REST)

All routes are versioned under `/api`. Auth required. Controllers live under `src/HydraForge.Server/Controllers/Chat/`. Sub-resource route pattern follows repo convention: `[Route("api/chat/[controller]")]` on class, scoped actions on the resource.

### 2.1 ChatSession

| Method | Route | Body / Query | Returns | Notes |
|---|---|---|---|---|
| `POST` | `/api/chat/sessions` | `{ title?, folderId?, projectId?, openCardId?, personalityId?, searchAllMyDocs?, aiEditMode? }` | `201 ChatSessionDto` | Creates `Active` session and returns it immediately. If `projectId` set, membership guard (admin bypass). If `openCardId` set, card must belong to `projectId`. **F6 (async)**: if the panel already has an `Active` session for the same `(projectId, openCardId, ownerId)` tuple, the server enqueues a background job to close that old session (summary + CardChatLink, one LLM call, AI edit permission revoked) and returns the new session's `201` without waiting for it. The old session remains `Active` for the brief window until the background job completes — this is fine, its AI-edit permission is revoked the moment the job finishes, and nothing else reads its state in that window. |
| `GET` | `/api/chat/sessions` | `?folderId=&projectId=&includeArchived=&before=&limit=` | `ChatSessionPageDto` | Lists owner's sessions. Filter by folder/project. Paginated cursor (`before` = `UpdatedAt` of last item). |
| `GET` | `/api/chat/sessions/{sessionId}` | — | `ChatSessionDetailDto` (session + last N messages) | Owner or project member (for shared project chats). |
| `PATCH` | `/api/chat/sessions/{sessionId}` | `{ title?, personalityId?, searchAllMyDocs?, aiEditMode? }` | `ChatSessionDto` | Only while `Status=Active`. |
| `POST` | `/api/chat/sessions/{sessionId}/close` | `{ }` | `ChatSessionDto` | **F3 explicit close.** Generates summary (one LLM call), creates `CardChatLink` if `OpenCardId+ProjectId` set, sets `Status=Closed`, revokes AI edit permission. Idempotent. |
| `DELETE` | `/api/chat/sessions/{sessionId}` | — | `204` | Soft-archive (`ArchivedAt=now`). |
| `POST` | `/api/chat/sessions/{sessionId}/documents` | `{ documentId }` | `201 ChatSessionDocumentDto` | **F1** attach. Validates `Document.UserId == owner`. |
| `GET` | `/api/chat/sessions/{sessionId}/documents` | — | `ChatSessionDocumentDto[]` | Lists attached docs. |
| `DELETE` | `/api/chat/sessions/{sessionId}/documents/{documentId}` | — | `204` | Detach. |

### 2.2 ChatMessage (history + send-persist; streaming via hub)

| Method | Route | Body / Query | Returns | Notes |
|---|---|---|---|---|
| `GET` | `/api/chat/sessions/{sessionId}/messages` | `?before=&limit=` | `ChatMessagePageDto` | Paginated history (cursor = `CreatedAt` of oldest in page). Owner or project member. |
| `POST` | `/api/chat/sessions/{sessionId}/messages` | `{ content, images? }` | `201 ChatMessageDto` | Persists a `User` message and returns its `messageId`. This is the **only** path that persists a user message — the hub never persists on the client's behalf. **Client contract: call this first, then pass the returned `messageId` into `ChatHub.SendMessage` (§3.2).** That ordering is what makes the message durable if the client disconnects before the stream starts, and it's why every client (Web, TUI) must call both endpoints, not just the hub. |

### 2.3 ChatFolder

| Method | Route | Body | Returns | Notes |
|---|---|---|---|---|
| `POST` | `/api/chat/folders` | `{ name, parentFolderId?, projectId? }` | `201 ChatFolderDto` | Max depth 2 enforced. |
| `GET` | `/api/chat/folders` | `?projectId=` | `ChatFolderDto[]` | Tree shape (flat list with `ParentFolderId`). |
| `PATCH` | `/api/chat/folders/{folderId}` | `{ name }` | `ChatFolderDto` | |
| `DELETE` | `/api/chat/folders/{folderId}` | — | `204` | Archive (cascade: `ChatArchiveService.ArchiveFolder` sets `ArchivedAt` on folder + all child sessions). |

### 2.4 PromptPresetGroup

| Method | Route | Body | Returns |
|---|---|---|---|
| `POST` | `/api/chat/preset-groups` | `{ name }` | `201 PromptPresetGroupDto` |
| `GET` | `/api/chat/preset-groups` | — | `PromptPresetGroupDto[]` (with nested `Presets[]`) |
| `PATCH` | `/api/chat/preset-groups/{groupId}` | `{ name }` | `PromptPresetGroupDto` |
| `DELETE` | `/api/chat/preset-groups/{groupId}` | — | `204` (soft-archive the group; presets' `GroupId` set to `null`, presets kept — this is the authoritative behavior, see §1.10) |

### 2.5 PromptPreset

| Method | Route | Body | Returns |
|---|---|---|---|
| `POST` | `/api/chat/presets` | `{ name, content, groupId? }` | `201 PromptPresetDto` |
| `GET` | `/api/chat/presets` | `?groupId=` (nullable; `?groupId=` empty = ungrouped only) | `PromptPresetDto[]` |
| `PATCH` | `/api/chat/presets/{presetId}` | `{ name?, content?, groupId? }` | `PromptPresetDto` |
| `DELETE` | `/api/chat/presets/{presetId}` | — | `204` (soft) |

### 2.6 AgentPersonality

| Method | Route | Body | Returns |
|---|---|---|---|
| `POST` | `/api/chat/personalities` | `{ name, description?, systemPrompt, isDefault? }` | `201 AgentPersonalityDto` |
| `GET` | `/api/chat/personalities` | — | `AgentPersonalityDto[]` |
| `PATCH` | `/api/chat/personalities/{personalityId}` | `{ name?, description?, systemPrompt? }` | `AgentPersonalityDto` |
| `DELETE` | `/api/chat/personalities/{personalityId}` | — | `204` (soft) |
| `POST` | `/api/chat/personalities/{personalityId}/default` | — | `204` (sets `IsDefault=true`, clears others) |

### 2.7 CardChatLink

| Method | Route | Query | Returns | Notes |
|---|---|---|---|---|
| `GET` | `/api/cards/{cardId}/chat-links` | — | `CardChatLinkDto[]` | Project members. Each entry: link id, session id, owner (id+username), summary, created at, archived at. |
| `DELETE` | `/api/chat/card-links/{linkId}` | — | `204` | Owner only (soft). |

### 2.8 Personal Documents (chunking + embedding pipeline)

| Method | Route | Body | Returns | Notes |
|---|---|---|---|---|
| `POST` | `/api/documents` | multipart (`file` or `text` + `title` + `contentType`) | `201 DocumentDto` | Upload → chunk → embed → persist `DocumentChunk[]`. See §4. |
| `GET` | `/api/documents` | `?q=` | `DocumentDto[]` | Owner's documents. |
| `DELETE` | `/api/documents/{documentId}` | — | `204` | Soft-archive document + its chunks (`DocumentChunk` cleanup via housekeeping polymorphic path). |

### 2.9 Search

| Method | Route | Query | Returns |
|---|---|---|---|
| `GET` | `/api/chat/search` | `?q=&projectId=` | `ChatSearchResultDto[]` (session id, title, snippet, matched-on) |

Searches across the caller's chat sessions' titles + message content (postgres full-text or ILIKE; pgvector not used for title/content search). `projectId` optional filter.

### 2.10 Result/Error conventions

Per repo convention, expected Application-layer failures return `Result<T, Error>` with named error codes in Domain. New error codes for Phase 7:

- `CHAT_SESSION_NOT_FOUND`
- `CHAT_MESSAGE_NOT_FOUND` — `ChatHub.SendMessage`'s `userMessageId` doesn't exist or isn't owned by the caller (client skipped the required `POST /messages` step, or raced it).
- `CHAT_SESSION_CLOSED` — attempted to send/stream on a `Closed` session.
- `CHAT_SESSION_NOT_OWNER`
- `CHAT_FOLDER_MAX_DEPTH` — exceeds 2-level nesting.
- `CHAT_DOCUMENT_NOT_OWNED` — attaching another user's document.
- `CHAT_STREAM_IN_PROGRESS` — a stream is already active for this session (one active stream per session).
- `CHAT_MODEL_NO_VISION` — selected/routed model does not support images.
- `CHAT_SUMMARY_FAILED` — summary LLM call failed; session still closed with `Summary=null`.
- `CHAT_PRESET_GROUP_NOT_FOUND`
- `CHAT_PERSONALITY_NOT_FOUND`
- `CHAT_CARD_NOT_IN_PROJECT` — `openCardId` does not belong to `projectId`.

---

## 3. SignalR Hub — `ChatHub` (streaming transport, F5/D-64)

Lives at `src/HydraForge.Infrastructure/Realtime/ChatHub.cs` (mirrors `BoardHub`). Interface `IChatHub` in `src/HydraForge.Application/Realtime/IChatHub.cs`. Registered in `Server/Program.cs` alongside `BoardHub`/`PresenceHub` under the `SignalR` rate-limiting policy.

### 3.1 Server → Client events (`IChatHub`)

| Event | Payload | When |
|---|---|---|
| `StreamStart` | `{ messageId, modelId, modelName }` | Server accepted `SendMessage`, routed a model, begins streaming. `messageId` is the pre-allocated assistant message id. |
| `StreamDelta` | `{ messageId, delta }` | One `ChatChunk.Delta` forwarded from the adapter. |
| `StreamDone` | `{ messageId, usage }` | Adapter yielded `FinishReason.Stop`/`Length`/`ContentFilter`. Assistant `ChatMessage` persisted with token counts. |
| `StreamError` | `{ messageId, code, message }` | Adapter yielded `FinishReason.Error`, or routing/budget/embedding failed before streaming. Partial assistant message (if any) is discarded. |
| `Typing` | `{ sessionId, userId }` | (Optional, Phase 7+ later) other user typing in a shared project chat. |

### 3.2 Client → Server methods

| Method | Args | Server action |
|---|---|---|
| `JoinSession(sessionId)` | `Guid` | Authorize (owner or project member for shared). Add connection to group `chat-{sessionId}`. |
| `LeaveSession(sessionId)` | `Guid` | Remove from group. |
| `SendMessage(sessionId, userMessageId, presetId?)` | `Guid, Guid, Guid?` | **Main entry.** Look up the already-persisted user `ChatMessage` by `userMessageId` (client must have called `POST /messages` first — §2.2); `404`/`StreamError(code=CHAT_MESSAGE_NOT_FOUND)` if it doesn't exist or isn't owned by the caller. No content-matching, no hub-side persist — the message is guaranteed to exist by contract. Run RAG retrieval (§4). Build `ChatRequest` with personality system prompt + cache blocks + messages, applying `presetId` injection (§4.4) if set. Route via `ModelRouter`. Allocate assistant `messageId`. Emit `StreamStart`. `await foreach` over `ILlmClient.StreamChatAsync` → forward each `ChatChunk.Delta` as `StreamDelta`. On finish: persist assistant `ChatMessage` (with `InputTokens`/`OutputTokens`/`CachedTokens`/`ModelName`/`ImagesJson=null`), emit `StreamDone`. On error chunk: emit `StreamError`, discard partial. |
| `CancelStream(sessionId)` | `Guid` | Cancel the active `CancellationTokenSource` for that session's stream. Emit `StreamDone` (with `usage=null`) or `StreamError` (`code=Cancelled`). Partial assistant message discarded. |

### 3.3 Streaming lifecycle invariants

- **One active stream per session.** Server tracks `ConcurrentDictionary<sessionId, StreamContext>` where `StreamContext` holds the `CancellationTokenSource` and the allocated `messageId`. A second `SendMessage` while one is active → `StreamError(code=CHAT_STREAM_IN_PROGRESS)`.
- **Closed sessions reject `SendMessage`.** Server checks `ChatSession.Status == Active` before routing; else `StreamError(code=CHAT_SESSION_CLOSED)`.
- **Usage recording.** The final `ChatChunk.Usage` (provider-reported, per D-63) drives `IUsageRecorder.AccrueTokenUsageAsync` with `AiFeature.PersonalChat` or `AiFeature.ProjectChat` (based on `ChatSession.ProjectId`).
- **Context compression.** Long histories go through `IContextCompressor` before the call (per CLAUDE.md compressor rules — error-chunk passthrough, index-based block exclusion).
- **No back-edge.** `ChatHub` → `IModelRouter` → `ILlmClientFactory` → adapters. No cycle.

---

## 4. RAG Pipeline

### 4.1 Ingestion (document upload)

1. Client `POST /api/documents` with file bytes or text + `contentType` (`pdf`/`markdown`/`code`/`csv`/`html`).
2. Server stores the file via `IFileStore` (storage key `{userId}/document/{documentId}/{guid}`) and creates the `Document` row.
3. **Chunking**: split text into ~500-token chunks (~2000 chars) with ~100-token overlap. For PDF, extract text first (PDF text-extraction lib — chosen at implementation time; if none desired, restrict Phase 7 to text/markdown/code/csv/html and defer PDF to a follow-up). Chunk index 0..N.
4. **Embedding**: batch all chunks through `IEmbeddingClient.EmbedAsync` (one call per batch; batch size = embedding model's max input). On failure: log warning, store chunks with null embeddings? No — `DocumentChunk.Embedding` is non-nullable `vector(1536)`. On embedding failure, **fail the upload**: return `500` with `CHAT_EMBEDDING_FAILED`, do not persist the document. (RAG with null embeddings is worse than no document; surface the failure.)
5. Persist `DocumentChunk` rows: `UserId`, `DocumentId`, `SourceType="document"`, `SourceId=DocumentId`, `ChunkIndex`, `Content`, `Embedding`.
6. Return `DocumentDto`.

### 4.2 Retrieval (at `SendMessage` time)

1. Embed the user's message content via `IEmbeddingClient` (single input).
2. Build the candidate chunk set per `ChatSession.SearchAllMyDocs`:
   - `false` (default, F1): `SELECT ... FROM document_chunk WHERE user_id = @owner AND document_id IN (SELECT document_id FROM chat_session_document WHERE session_id = @session)`.
   - `true`: `SELECT ... FROM document_chunk WHERE user_id = @owner`.
3. pgvector similarity: `ORDER BY embedding <=> @queryEmbedding LIMIT @k` (k=8, configurable in `Llm:Rag:TopK`). `<=>` = cosine distance.
4. Build `CacheBlock(SystemContext, Content=concatenated chunk texts)` and prepend to `ChatRequest.CacheBlocks` — **without** `cache_control`. RAG content is different on every message (different retrieval each time), so caching it never pays off and would burn a breakpoint slot for nothing (CLAUDE.md's 4-cache-breakpoint limit).
5. For project chats, prepend `CacheBlock(ProjectSnapshot, Content=ProjectContextSnapshot.TemplateContent)` **only on the session's first message** — same `snapshotInjected`-flag pattern the existing adapters already use (CLAUDE.md: "Project snapshot injected into first user message only"), not re-derived per session type. Determine "first message" from whether any prior `ChatMessage` exists for this session before the current one is persisted. Every later message in the session omits this block — the model already has it from turn 1.
6. Personality system prompt (if `PersonalityId` set and not archived — see §1.10, check `ArchivedAt`) → first `ChatMessage(Role=System, Content=systemPrompt)`.
7. Injected prompt preset (if user picked one for this send) → either prepend to user content or add as a `System` message. **Decision: prepend preset content to the user's message content** (simpler, avoids an extra system message that some adapters handle inconsistently). UI shows the preset as a chip above the input.

### 4.3 Scope toggle UX

- `SearchAllMyDocs` is a checkbox in the session header (Web) / a toggle in the session screen footer (TUI).
- Flipping it is a `PATCH /api/chat/sessions/{sessionId}` and takes effect on the *next* `SendMessage` — no re-embedding, no re-retrieval of past messages.
- Default `false` (session-scoped, F1).

### 4.4 PromptPreset injection

- User picks a preset from a dropdown in the input area (Web) or via a hotkey menu (TUI).
- The selected preset id is held client-side; on send, the client includes `presetId` in the `SendMessage` args (§3.2: `SendMessage(sessionId, userMessageId, presetId?)`).
- Server fetches the preset, validates ownership, prepends `preset.Content` to the user message content (wrapped: `"<preset>\n{content}\n</preset>\n\n{userContent}"`). The persisted `ChatMessage.Content` (written earlier via `POST /messages`) stores the user's raw content only; the preset-wrapped version is built in-memory for the LLM call and discarded.

---

## 5. Web UI Components

Nuxt 4 source layout (`src/web-ui/app/`). All API paths via `ApiRoutes.Chat.*` in `app/lib/routes.ts`. Toasts via `useAppToast`. Modals via `AppModal.vue`. `useApi()` throws — every call site wrapped in try/catch (D-40).

### 5.1 Pages

- `pages/chat/index.vue` — personal chat home: folder tree (left), session list (middle), search bar. Mobile: stacked.
- `pages/chat/[sessionId].vue` — full-page personal chat session view (reuses `ChatSessionView`).
- `pages/chat/presets.vue` — `PromptPresetManager` + `PromptPresetGroupManager`.
- `pages/chat/personalities.vue` — `PersonalityManager`.
- `pages/documents.vue` — personal documents list + upload.

### 5.2 Components (`components/chat/`)

| Component | Props | Emits | Notes |
|---|---|---|---|
| `ChatSessionView.vue` | `sessionId` | — | Top-level session container. Owns the SignalR connection, message list, input, streaming state. Used by both the personal-chat page and the project `ChatPanel`. |
| `ChatMessageList.vue` | `messages[]`, `streamingMessageId?` | — | Virtualized list. Renders `ChatMessageBubble` per message; renders a live "typing" bubble for the in-flight assistant message. |
| `ChatMessageBubble.vue` | `message` | — | Markdown render (Tiptap or `markdown-it`); image thumbnails for vision messages. |
| `ChatInput.vue` | `disabled`, `personalityId`, `presetId` | `send(content, images?)`, `cancel` | Textarea + image attach + preset chip + send/cancel button. Enter to send, Shift+Enter newline. |
| `ChatSessionHeader.vue` | `session` | `close`, `toggleScope`, `editPersonality`, `editMode` | Title, scope toggle, personality picker, AI-edit-mode picker, close button. |
| `ChatDocAttach.vue` | `sessionId` | `attached` | Lists attached docs, add/remove via `ChatDocAttachPicker`. |
| `ChatDocAttachPicker.vue` | `sessionId` | `picked` | Modal listing owner's documents. |
| `ChatPanel.vue` | `projectId`, `cardId?` | — | **Project board side panel.** Collapsible (drawer on mobile, side rail on desktop). Hosts a `ChatSessionView`. On open: creates a new session (F6 implicit close of prior). Shows the "Card #N [title] opened — what are we doing?" auto-prompt as the first user message (sent automatically, or pre-filled for user to edit — **decision: pre-filled, user edits then sends**). |
| `CardChatLinkList.vue` | `cardId` | — | Collapsible summary table in the card modal. Owner-clickable row → opens the session read-only (or full if owner). |
| `PromptPresetManager.vue` | — | — | CRUD for groups + presets. Drag-to-reorder within a group (native HTML5 DnD — `vue-draggable-plus` is removed per repo convention). |
| `PersonalityManager.vue` | — | — | CRUD for personalities. "Set default" button. |
| `DocumentUploader.vue` | — | `uploaded` | Drag-drop or file picker. Shows chunking/embedding progress. |

### 5.3 Streaming client

- `app/composables/useChatStream.ts` — wraps `@microsoft/signalr` `HubConnection` to `/chat`. Exposes `connect()`, `join(sessionId)`, `leave()`, `send(sessionId, content, images, presetId)`, `cancel()`. Internally, `send()` is two steps per the §2.2/§3.2 contract: (1) `POST ApiRoutes.Chat.sendMessage(sessionId)` via `useApi()` (try/catch per D-40) to persist the user message and get `messageId`, (2) `hubConnection.invoke('SendMessage', sessionId, messageId, presetId)` to start the stream. Callers of the composable still just call `send(content, images, presetId)` — the two-step split is an implementation detail, not exposed. Reactive `streamingMessage` ref (accumulated deltas). Emits toasts on `StreamError` and on REST-step failure (message never reached the server — nothing to stream).
- Connection lifecycle: connect on `ChatSessionView` mount, disconnect on unmount. Reconnect with backoff (reuse `SignalRConnectionManager` pattern from Phase 4 if applicable).

### 5.4 Project board integration

- `pages/projects/[projectId]/index.vue` (board view) gains a collapsible `ChatPanel` rail. Toggle button in the board header. When a card is open (card modal), `ChatPanel` is opened with `cardId` set; the panel's session is project-scoped + card-scoped.
- Collapsing the panel does **not** close the session — the session stays `Active` (resumable) and AI edit permission stays granted. Only the explicit "End session" button inside the panel fires `POST /api/chat/sessions/{sessionId}/close` (F3). Tab close / navigate-away does **not** close the session either — matches F2=C, which deliberately limits revocation triggers to explicit End + new-session creation (F6) and rejects any auto-revoke-on-navigate-away path (permission silently dropping while the user still thinks it's active is the exact failure mode F2 ruled out). The session is simply left `Active`; it's resumable next time the panel or tab reopens.

---

## 6. TUI Screens

Spectre.Console. NSwag `HydraForgeApiClient` for REST, `Microsoft.AspNetCore.SignalR.Client` for `ChatHub`. Keyboard-navigable, parity with Web UI per repo constraint.

### 6.1 Screens (`src/HydraForge.Tui/Screens/`)

| Screen | Purpose | Launch |
|---|---|---|
| `ChatListScreen` | Personal chats: folder tree, session list, search. | Main menu `c` |
| `ChatSessionScreen` | Message thread + input + streaming. | Enter on a session |
| `ProjectChatScreen` | Project chat panel (board → `c` when card focused). Hosts session with project+card context. | Board view `c` |
| `PromptPresetScreen` | Preset + group CRUD. | Main menu or chat screen `P` |
| `PersonalityScreen` | Personality CRUD. | Main menu or chat screen `A` |
| `DocumentListScreen` | Personal documents list + upload (path prompt). | Main menu |

### 6.2 `ChatSessionScreen` layout

- Top: session title, scope toggle (`[ ] All my docs`), personality name, AI-edit mode (project chats only), unread/online indicator.
- Middle: scrollable message list (markdown rendered via Spectre markdown support; images show as `[image: filename]` placeholders — TUI cannot render images).
- Bottom: input area (multi-line via Spectre text prompt), doc-attach indicator, preset chip, send (`Enter`), cancel stream (`Ctrl+C`), close session (`q`), help (`?`).
- Status bar: connection state, streaming indicator, token count for current stream.

### 6.3 Streaming

- `ChatHubConnection` service (mirrors `SignalRConnectionManager` from Phase 4). `await foreach` over the hub's `StreamDelta` events accumulates into the live assistant bubble.
- `CancelStream` bound to `Ctrl+C` within the session screen (overrides the default exit).

### 6.4 Project chat panel

- From the board view, focusing a card and pressing `c` opens `ProjectChatScreen` with `projectId` + `cardId`. Same F6 implicit-close behavior: opening a new session closes the prior active one for the same `(project, card, user)` tuple.

---

## 7. Edge Cases

| Case | Handling |
|---|---|
| **Stream active when `close` requested** | Server cancels the active stream (`CancelStream` semantics), waits for the assistant message to be discarded, then runs the summary LLM call. The summary covers all persisted messages (the cancelled assistant turn is not persisted). |
| **Client disconnects mid-stream** | Server keeps the stream alive for a grace period (30s configurable `Llm:Chat:DisconnectGraceSeconds`). If the client reconnects and re-joins the session group within the grace window, deltas continue flowing (already-emitted deltas are lost — client shows a "stream interrupted" placeholder and may `CancelStream` then resend). After grace, server cancels the stream, discards the partial assistant message. Session stays `Active` — only explicit close closes it. |
| **New chat from panel while old session Active (F6)** | `POST /api/chat/sessions` with the same `(projectId, openCardId, ownerId)` as an existing `Active` session → server creates and returns the new session immediately, then enqueues a background job to close the old one (summary + CardChatLink, one LLM call, revoke AI edit). Request never blocks on the LLM call. If the background summary fails (`CHAT_SUMMARY_FAILED`), the old session is still closed (`Summary=null`) by the same job — since this happens after the response already went out, the failure can't be returned synchronously; it's logged server-side and surfaced to the client the next time it fetches that session (toast on next `GET`/list, not a blocked operation). |
| **RAG with no docs attached + `SearchAllMyDocs=false`** | No retrieval; the LLM call proceeds with personality + project context only. Not an error. |
| **RAG with `SearchAllMyDocs=true` but user has no documents** | No retrieval; proceed. |
| **Embedding failure at retrieval time** | Log warning, skip RAG (no `SystemContext` cache block), proceed with the chat. Do not fail the send. |
| **Embedding failure at ingestion time** | Fail the upload (`CHAT_EMBEDDING_FAILED`); do not persist the document or chunks. |
| **Model unavailable / rate-limited** | `ModelRouter` fallback chain (per Phase 6). All fallbacks fail → `StreamError(code=ROUTING_NO_MODEL)` (existing error code). |
| **Budget exceeded** | `LlmCallGuard.CheckTokenBudgetAsync` returns `TOKEN_BUDGET_EXCEEDED` before routing → `StreamError(code=TOKEN_BUDGET_EXCEEDED)`. |
| **Project archived mid-session** | `ProjectArchiveService` cascades `ChatFolder.ArchivedAt` + `ChatSession.ArchivedAt`. An in-flight stream completes; new `SendMessage` on the archived session → `StreamError(code=CHAT_SESSION_ARCHIVED)`. |
| **Card archived mid-session** | `ChatSession.OpenCardId` stays. On close, `CardChatLink` is still created (link survives via its own `ArchivedAt`). The card's `CardChatLinkList` shows the link with an "archived card" badge. |
| **Personality archived mid-session** | `ChatSession.PersonalityId` stays populated — `AgentPersonality` is soft-archived via `ArchivedAt`, not hard-deleted, so the FK's `OnDelete: SetNull` never fires here (see §1.10). Chat-request build (§4.2 step 6) checks `ArchivedAt` at read time and skips the system prompt if archived. Session read/patch responses include `personalityArchived: bool` (derived, not stored) so the UI can toast "personality removed, using default context" without depending on the FK ever changing. Next send has no personality system prompt. |
| **Concurrent sends in one session** | One active stream per session (§3.3). Second `SendMessage` → `StreamError(code=CHAT_STREAM_IN_PROGRESS)`. Client UI disables send while streaming. |
| **Images on a non-vision model** | `ModelRouter` does not yet route by vision capability in Phase 7 (that's Phase 8 routing refinement). If the routed adapter rejects images, it yields `ChatChunk(FinishReason=Error)` → `StreamError(code=CHAT_MODEL_NO_VISION)`. Client UI should warn when images are attached and the session's effective model is known-non-vision (best-effort, may not be known client-side). |
| **Empty session close (no messages)** | No summary LLM call. Set `Status=Closed`, `ClosedAt=now`, `Summary=null`. No `CardChatLink` created (nothing to summarize). |
| **Summary LLM call fails** | Still close the session: `Status=Closed`, `ClosedAt=now`, `Summary=null`. If `OpenCardId+ProjectId` set, create `CardChatLink` with `Summary="Chat closed (summary unavailable)"`. Surface `CHAT_SUMMARY_FAILED` as a toast; do not block close. |
| **Closing an already-Closed session** | Idempotent no-op (`Close(summary)` on a Closed session returns without changes). |
| **Attaching a document that's already attached** | Unique index `(SessionId, DocumentId)` — return `409` with `CHAT_DOCUMENT_ALREADY_ATTACHED` (add to error code list). |
| **Attaching another user's document** | `403 CHAT_DOCUMENT_NOT_OWNED`. |
| **`PATCH` on a Closed session** | `409 CHAT_SESSION_CLOSED`. |
| **Shared project chat — non-owner member sends** | Phase 7: shared project chats are **read-only for non-owners** (per FR "visible to all members read-only"). Non-owner `SendMessage` → `403 CHAT_SESSION_NOT_OWNER`. Fork action ("Summarize → start my own") is a Phase 7 deliverable: `POST /api/chat/sessions` with `forkedFromSessionId` body field → new session owned by caller, pre-populated with a summary of the source session (one LLM call). |
| **Folder nesting > 2** | `409 CHAT_FOLDER_MAX_DEPTH`. |
| **TUI image attach** | TUI cannot attach images via file picker in Phase 7 (no image-rendering capability). Vision messages are Web-UI-only. TUI users can view image placeholders in history but not send new images. Documented as a known parity gap. |

---

## 8. AI Edit Permission Model (F2 — state only; enforcement is Phase 8)

Phase 7 establishes the **permission state**; Phase 8 implements the **enforcement** (AI proposing card mutations, confirmation dialogs, executing edits). This phase's deliverable is the state machine and the revocation triggers.

### 8.1 State

- `ChatSession.Status == Active` AND `ChatSession.ProjectId != null` → AI edit permission is **granted** for cards in `ChatSession.ProjectId`, subject to `AiEditMode`:
  - `PerMutation`: each proposed mutation requires a per-action confirmation (Phase 8 surfaces this as a dialog).
  - `Blanket`: all proposed mutations within the session are auto-approved (Phase 8 surfaces a "blanket granted" indicator; user can revoke by switching mode or closing).
- `ChatSession.Status == Closed` → AI edit permission **revoked**. Any in-flight Phase 8 edit proposal is cancelled.
- Personal chats (`ProjectId == null`) → AI edit permission is **never granted** (no project to edit).

### 8.2 Revocation triggers (F2=C)

1. **Explicit "End session"** — user clicks the close button / presses `q` in TUI → `POST /api/chat/sessions/{sessionId}/close` → `Status=Closed` → permission revoked.
2. **New `ChatSession` from the same panel** — `POST /api/chat/sessions` matching an existing `Active` session's `(projectId, openCardId, ownerId)` → F6 implicit close of the old session → permission revoked on the old session. The new session starts with permission granted (it's `Active`).

### 8.3 Phase 7 deliverables for the permission model

- `ChatSession.Status`, `ChatSession.AiEditMode` fields + entity state methods.
- The close endpoint (explicit + implicit via F6).
- A read endpoint exposing the current permission state for a session: `GET /api/chat/sessions/{sessionId}/permission` → `{ granted: bool, mode: AiEditMode }` (used by Phase 8 UI to show the indicator and by the Web/TUI to disable AI-edit controls when revoked). Cheap endpoint, no LLM.

---

## 9. Migrations

One migration: `AddPhase7Chat` covering:

- `ChatSession`: add `Status`, `AiEditMode`, `SearchAllMyDocs`, `PersonalityId`, `OpenCardId`, `ClosedAt`, `Summary`.
- `ChatMessage`: add `ImagesJson`.
- `ChatSessionDocument`: new table + unique index.
- `PromptPresetGroup`: new table.
- `PromptPreset`: new table + FK to `PromptPresetGroup`.
- FKs: `ChatSession.PersonalityId` → `AgentPersonality` (`OnDelete: SetNull`).

No pgvector changes. Verify with `dotnet ef migrations has-pending-model-changes`.

---

## 10. Testing

- **Domain tests**: `ChatSession` state transitions (`Open`/`Close` idempotency, `ToggleSearchAllMyDocs`, `SetAiEditMode` rejected when Closed).
- **Application tests**: RAG retrieval scope filter (session-scoped vs all-my-docs), summary generation on close, F6 implicit-close ordering, error code paths.
- **Infrastructure EF model tests**: `AssertProperties` for new entities (`ChatSessionDocument`, `PromptPresetGroup`, `PromptPreset`) and modified `ChatSession`/`ChatMessage`. Unique index on `ChatSessionDocument`. FK `OnDelete` behaviors.
- **Server integration tests**: endpoint auth (owner vs project member vs non-member), close idempotency, F6 implicit close, `CHAT_STREAM_IN_PROGRESS`, `CHAT_SESSION_CLOSED` on send-after-close.
- **SignalR tests**: `ChatHub` join/leave/send/cancel; one-active-stream invariant; `StreamError` on closed session.
- **Web UI**: `ChatSessionView` mount/stream/cancel; `ChatPanel` open/close; `CardChatLinkList` render; `useApi()` try/catch audit per D-40.
- **TUI**: `ChatSessionScreen` render + stream; `ProjectChatScreen` F6 implicit close.
- **Manual validation matrix**: per-task E2E steps consolidated into `docs/archive/manual-validation/` at phase close (per repo convention).

---

## 11. Out of Scope (Phase 8+)

- AI actually proposing/executing board mutations (only the permission state is Phase 7).
- `ModelRouter` vision-capability routing (Phase 7 surfaces `CHAT_MODEL_NO_VISION`; Phase 8 adds capability-aware routing).
- Card creation from chat, spec/plan drafting, agent pipeline, Git Agent (all Phase 8).
- Tool calling / function calling beyond the existing `ToolDefinition` DTO plumbing (Phase 8 wires actual tools).
- Memory extraction from chat (`AiFeature.MemoryExtraction` — Phase 9).
- PDF text extraction (Phase 7 restricts document upload to text/markdown/code/csv/html; PDF deferred unless a lib is chosen at implementation time).
- TUI image attach (Web-UI-only in Phase 7).

---

## Tasks

- [ ] Task 1: Domain entities + enums (`ChatSessionStatus`, `AiEditMode`, `ChatSession` mods, `ChatMessage.ImagesJson`, `ChatSessionDocument`, `PromptPresetGroup`, `PromptPreset`) + state-transition methods
- [ ] Task 2: EF migration `AddPhase7Chat` + Infrastructure model config + `AssertProperties` tests
- [ ] Task 3: Application ports + DTOs (`IChatSessionRepository`, `IChatMessageRepository`, `IChatSessionDocumentRepository`, `IPromptPresetRepository`, `IPromptPresetGroupRepository`, `IAgentPersonalityRepository`, `ICardChatLinkRepository`, `IDocumentRepository`, `IChatRagRetriever`, `IChatSummaryGenerator`) + error codes
- [ ] Task 4: `Application.Llm.ChatMessage` `Images` extension + `ImageBlock` DTO + mapper (F5)
- [ ] Task 5: Document upload + chunking + embedding ingestion service (`IEmbeddingClient` pipeline)
- [ ] Task 6: RAG retrieval service (scope toggle F1, pgvector similarity, cache-block assembly)
- [ ] Task 7: ChatSession service (CRUD, F6 implicit close, close-with-summary, permission-state read)
- [ ] Task 8: ChatMessage service (history pagination, user-message persist)
- [ ] Task 9: ChatFolder service (CRUD, max-depth-2, archive cascade via `ChatArchiveService`)
- [ ] Task 10: PromptPreset + PromptPresetGroup services (CRUD, group-nulling on group archive)
- [ ] Task 11: AgentPersonality service (CRUD, default management)
- [ ] Task 12: CardChatLink service (list per card, archive, owner-only delete)
- [ ] Task 13: Chat search service (title + message content)
- [ ] Task 14: ChatSummaryGenerator (one LLM call on close; `CHAT_SUMMARY_FAILED` fallback)
- [ ] Task 15: `ChatHub` + `IChatHub` (streaming transport, one-active-stream, cancel, usage recording, context compression)
- [ ] Task 16: REST controllers (sessions, messages, folders, presets, personalities, card-links, documents, search) + `Program.cs` wiring + rate limiting
- [ ] Task 17: Web UI — `useChatStream` composable + `ChatSessionView` + `ChatMessageList`/`ChatMessageBubble`/`ChatInput` + streaming
- [ ] Task 18: Web UI — `ChatPanel` (project board rail) + F6 implicit close + auto-prompt pre-fill
- [ ] Task 19: Web UI — `ChatSessionHeader` (scope toggle, personality, AI-edit mode, close) + `ChatDocAttach`
- [ ] Task 20: Web UI — `CardChatLinkList` in card modal + `pages/chat/index.vue` + `pages/chat/[sessionId].vue`
- [ ] Task 21: Web UI — `PromptPresetManager` + `PersonalityManager` + `DocumentUploader` + `pages/documents.vue`
- [ ] Task 22: TUI — `ChatListScreen` + `ChatSessionScreen` (streaming, cancel, close) + `ChatHubConnection` service
- [ ] Task 23: TUI — `ProjectChatScreen` (F6 implicit close) + `PromptPresetScreen` + `PersonalityScreen` + `DocumentListScreen`
- [ ] Task 24: Server integration tests + SignalR hub tests + manual validation matrix consolidation