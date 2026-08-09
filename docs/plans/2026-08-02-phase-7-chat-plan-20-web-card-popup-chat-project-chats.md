# Plan 20: Web UI — Card Popup Chat Tab + Project Chats Tab + Context-Aware ChatDock + CardChatLinkList + Card Linking
**Branch:** `task/web-card-popup-chat`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 20
**Test scope:** e2e

> **Renamed 2026-08-06 (was "CardChatLinkList + Chat Pages").** Scope broadened per design session: the card-detail surface is now a multi-popup (`CardPopup`, infra landed in Plan 19b) rather than a single `CardModal`; chat visibility changes from owner-only to "sessions the user participated in" (owner OR project member); the project view gains a "Chats" tab; the card popup gains a "Chat" tab; and the `ChatDock` becomes context-aware (detects `currentProjectId` + `currentCardId` from where it was opened) with a manual card-linking button. `@mention` card linking is deferred to V2 (noted in spec §5.4).

> **Status:** All steps complete (LGTM received).

**Goal:** Adopt the Plan-19b popup shell for cards; add a "Chat" tab to the card popup showing linked chats (`CardChatLinkList`); add a "Chats" tab to the project view (All / Project / Card filter); make the `ChatDock` context-aware; add a manual "link this card to the current session" button inside the dock; broaden the chat-session list query to sessions the caller participated in (owner OR project member) via an in-place upgrade that preserves existing sessions.

## Decisions (from session)

- **Q2 — Chat visibility:** Option 3. `GET /api/chat/sessions` lists sessions the caller **participated in** — i.e. sessions they own **or** project chats where they are a `ProjectMember` (admin bypass). Not "all project chats in projects I'm a member of" (that would leak chats the user never took part in); a session counts as "participated in" if the caller is its owner, or the caller is a member of the session's project. This is an in-place upgrade of `ChatSessionService.ListAsync` + `IChatSessionRepository.ListAsync`/`CountAsync`/`SearchByTitleAsync` — no data migration, existing sessions keep their `OwnerId`/`ProjectId`.
- **Q2-secondary — PersonalChat → ProjectChat migration:** Option (b), upgrade in-place to preserve story. The listing/visibility change is a query broadening, not a reclassification — no session is rewritten, no `AiFeature` flip, no data backfill. Existing personal chats stay personal; existing project chats stay project. The only change is which sessions appear in the caller's list.
- **Q2 — Project "Chats" tab:** New tab in `pages/projects/[id]/index.vue` board view. Filter: All / Project / Card (Card = chats linked to a card in this project via `CardChatLink`). Reuses the same `GET /api/chat/sessions?projectId=…` backend; the "Card" filter is a client-side grouping over `CardChatLink` lookups (or a new optional `?cardLinked=true` query — see step below; prefer reusing existing endpoints and grouping client-side to avoid a new backend endpoint in this plan).
- **Q2 — Card popup "Chat" tab:** New tab in the card popup (the migrated `CardModal` body). Shows `CardChatLinkList` (linked chats for this card). Any project member can interact (open a linked session, send messages) — same auth as the session itself (owner OR project member). Owner-clickable row opens the session (full if owner, read-only if not — matches existing `CardChatLinkList` contract).
- **Q2 — Context-aware ChatDock:** The dock already derives `currentProjectId` from the route and reads `useBoardStore().openCardId` as the card context (see `stores/chatDock.ts:124`). This plan makes the context explicit and origin-aware: opening the dock from the project "Chats" tab sets `currentProjectId`, no card; opening from a card popup's "Chat" tab sets `currentProjectId` + `currentCardId`; opening from the board header toggle sets `currentProjectId` only. The dock's "new session" call uses whichever context is active.
- **Q2 — Card linking button (MVP):** A button inside the `ChatDock` (session mode) that links the current session's `OpenCardId` to the active card (`currentCardId`), when both are set and the session is project-scoped. Calls a new lightweight endpoint or reuses the existing session-update path (see step). This is the manual MVP for linking a chat to a card; `@mention`-based auto-linking is V2 (deferred — noted in spec §5.4 and §11).
- **Q1 — Escape:** handled by Plan 19b's shared composable (LIFO across card popups + dock). No new Escape logic here.
- **Q3 — Max cards:** enforced by Plan 19b's store. This plan just calls `cardPopup.openCard(cardId)`; the 4th-open toast comes from the store.

## Files

- Create: `src/web-ui/app/components/chat/CardChatLinkList.vue` — collapsible summary table of chats linked to a card. Columns: owner, summary, created, actions. Owner-clickable row → opens the session (read-only for non-owners, full for owners). Props: `cardId`.
- Create: `src/web-ui/app/components/chat/ChatLinkCardButton.vue` — the MVP "link this card to the current session" button shown inside the `ChatDock` when `currentCardId` is set and the active session is project-scoped and not already linked to that card.
- Create: `src/web-ui/app/pages/projects/[id]/chats.vue` — OR a tab inside the existing board `pages/projects/[id]/index.vue`. **Decision: add as a tab in the existing board page** (matches the Phase-3 tabbed board layout; avoids a new route + keeps the board header/filter bar shared). Filter bar: All / Project / Card.
- Modify: `src/web-ui/app/components/card/CardPopup.vue` (from Plan 19b) — replace the placeholder slot with the migrated `CardModal.vue` body (details/checklist/comments/related/docs tabs) **plus** a new "Chat" tab hosting `CardChatLinkList`.
- Modify: `src/web-ui/app/components/card/CardModal.vue` — split into a headless body (`CardModalBody.vue` or inline in `CardPopup`'s slot) that the popup renders. The single-modal `UTabs` + `AppModal` shell is retired; the tab body moves into the `CardPopup` slot. (Keep the existing tab logic, version-ownership pattern D-41, and all child panels — only the outer container changes.)
- Modify: `src/web-ui/app/components/board/BoardCard.vue` + `src/web-ui/app/stores/board.ts` — card open action calls `cardPopup.openCard(cardId)` instead of `setOpenCardId(cardId)` + mounting `CardModal`. `setOpenCardId` stays as the realtime bridge (the dock reads it for context) but is set in addition to `openCard`, not instead.
- Modify: `src/web-ui/app/stores/chatDock.ts` — add explicit `currentCardId` (mirrors `currentProjectId` derivation, sourced from `cardPopup.activeCardId` when on a project page), and an `openOrigin: 'board' | 'project-chats' | 'card-popup'` ref set by whichever UI opened the dock. The "new session" request uses `currentProjectId` + `currentCardId` per origin.
- Modify: `src/web-ui/app/components/chat/ChatDock.vue` — render `ChatLinkCardButton` in the session-mode header when `currentCardId` is set and the session is project-scoped.
- Modify: `src/HydraForge.Application/Chat/ChatSessionService.cs` + `IChatSessionRepository.cs` — broaden `ListAsync`/`CountAsync`/`SearchByTitleAsync` to "sessions the caller participated in" (owner OR project member of the session's project). In-place query change; no schema migration.
- Modify: `src/HydraForge.Application/Chat/ChatSessionService.cs` — add a `LinkCardAsync(sessionId, cardId, actorId)` method (or extend `UpdateAsync` to accept `openCardId`) that sets `ChatSession.OpenCardId = cardId` on an Active project-scoped session, with membership + card-in-project validation. Used by `ChatLinkCardButton`.
- Modify: `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs` — expose the card-link action (`POST /api/chat/sessions/{sessionId}/link-card` `{ cardId }` or `PATCH` with `openCardId`). Reuse `CHAT_CARD_NOT_IN_PROJECT` error code.
- Modify: `docs/specs/2026-08-02-phase-7-chat-design.md` — §2.1 (list = participated-in), §5.1/§5.2/§5.4 (CardPopup, project Chats tab, card-popup Chat tab, context-aware dock, linking button, `@mention` V2 deferral). (Reconciled notes added in this plan's parent-commit; this plan implements them.)

## Steps

### Step 1: Backend — list visibility (in-place upgrade)

**Files to modify:**
- `src/HydraForge.Application/Chat/IChatSessionRepository.cs`
- `src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs`
- `src/HydraForge.Application/Chat/ChatSessionService.cs`

**1a. Change `IChatSessionRepository` signatures:**

Rename `ownerId` → `actorId` on `ListAsync`, `CountAsync`, `SearchByTitleAsync`. The parameter name change signals the semantic shift: these methods no longer filter by owner-only.

```csharp
// IChatSessionRepository.cs — changed signatures:
Task<IReadOnlyList<ChatSession>> ListAsync(
    Guid actorId,        // was: ownerId
    Guid? folderId,
    Guid? projectId,
    DateTime? before,
    Guid? beforeId,
    int limit,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    CancellationToken ct = default
);

Task<int> CountAsync(
    Guid actorId,        // was: ownerId
    Guid? folderId,
    Guid? projectId,
    ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
    CancellationToken ct = default
);

Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
    Guid actorId,        // was: ownerId
    string query,
    Guid? projectId,
    int limit,
    CancellationToken ct = default
);
```

**1b. Update `EfChatSessionRepository` queries:**

Replace `s.OwnerId == ownerId` with a "participated-in" predicate:

```csharp
// Participated-in = owner OR project member (for project-scoped sessions)
// Admin bypass: admins see all project chats (handled at service layer, not repo)
private static IQueryable<ChatSession> WhereParticipatedIn(
    IQueryable<ChatSession> query,
    Guid actorId
) =>
    query.Where(s =>
        s.OwnerId == actorId
        || (s.ProjectId != null
            && context.ProjectMembers.Any(m =>
                m.ProjectId == s.ProjectId && m.UserId == actorId))
    );
```

Apply `WhereParticipatedIn` in `ListAsync`, `CountAsync`, `SearchByTitleAsync` instead of `s.OwnerId == ownerId`.

**1c. Update `ChatSessionService.ListAsync` call sites:**

The service already passes `actorId` to the repo — just update the parameter name in the call. No logic change needed in the service for list visibility.

**1d. Admin bypass:**

The `ChatSessionService.ListAsync` currently doesn't have admin bypass logic. Add it: if the caller is an admin, bypass the participated-in filter and return all sessions matching the folder/project/status filters. This requires injecting `IUserRepository` (already available in the service constructor) and checking `user.IsAdmin`.

Implementation in `ListAsync`:
```csharp
var user = await _userRepo.GetByIdAsync(actorId, ct);
if (user?.IsAdmin == true)
{
    // Admin sees all sessions — bypass participated-in filter
    // Pass Guid.Empty as actorId to signal "no filter" (repo checks for this)
    // OR: add a separate repo method for admin listing
}
```

**Decision:** Add a `bool isAdmin` parameter to `ListAsync`/`CountAsync`/`SearchByTitleAsync` on the repo interface. When `true`, the repo skips the participated-in filter entirely (returns all non-archived sessions). The service sets `isAdmin = user.IsAdmin`.

**1e. Tests:**

Add Application tests for `ChatSessionService.ListAsync`:
- Owner's personal chats still listed (no regression)
- Project chat where caller is a member (not owner) now listed
- Project chat where caller is neither owner nor member is NOT listed
- Admin sees all project chats regardless of membership
- `folderId`/`projectId`/`before`/`limit` filters still work correctly with the broader query

Add Infrastructure EF model tests: verify the `WhereParticipatedIn` query compiles correctly (no runtime EF translation errors). Test pattern: create in-memory context with test data, run the query, assert results.

### Step 2: Backend — card linking

**Files to modify:**
- `src/HydraForge.Domain/Entities/Chat/ChatSession.cs` — add `SetOpenCard` method
- `src/HydraForge.Application/Chat/IChatSessionService.cs` — add `LinkCardAsync`
- `src/HydraForge.Application/Chat/ChatSessionService.cs` — implement `LinkCardAsync`
- `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs` — add endpoint
- `src/web-ui/app/lib/routes.ts` — add `ApiRoutes.Chat.sessions.linkCard`

**2a. Add `SetOpenCard` entity method to `ChatSession.cs`:**

```csharp
public void SetOpenCard(Guid cardId)
{
    if (Status != ChatSessionStatus.Active)
        throw new InvalidOperationException("Cannot set open card on a non-active session.");
    if (ProjectId == null)
        throw new InvalidOperationException("Cannot set open card on a non-project session.");

    OpenCardId = cardId;
    UpdatedAt = DateTime.UtcNow;
}
```

**2b. Add `LinkCardAsync` to `IChatSessionService`:**

```csharp
Task<Result<ChatSessionDto>> LinkCardAsync(
    Guid sessionId,
    Guid cardId,
    Guid actorId,
    CancellationToken ct = default
);
```

**2c. Implement `LinkCardAsync` in `ChatSessionService`:**

```csharp
public async Task<Result<ChatSessionDto>> LinkCardAsync(
    Guid sessionId,
    Guid cardId,
    Guid actorId,
    CancellationToken ct = default
)
{
    var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
    if (session == null)
        return Result<ChatSessionDto>.Failure(
            new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found."));

    // Must be Active
    if (session.Status != ChatSessionStatus.Active)
        return Result<ChatSessionDto>.Failure(
            new Error(DomainErrorCodes.Chat.SessionClosed, "Cannot link card to a closed session."));

    // Must be project-scoped
    if (!session.ProjectId.HasValue)
        return Result<ChatSessionDto>.Failure(
            new Error(DomainErrorCodes.Chat.CardNotInProject, "Cannot link card to a personal session."));

    // Membership guard: owner OR project member (admin bypass)
    if (session.OwnerId != actorId)
    {
        if (!await MembershipGuard.HasAccessAsync(
                _userRepo, _memberRepo, session.ProjectId.Value, actorId, ct))
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));
    }

    // Validate card belongs to session's project
    var card = await _cardRepo.GetByIdAsync(cardId, ct);
    if (card == null)
        return Result<ChatSessionDto>.Failure(
            new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));
    if (card.ProjectId != session.ProjectId.Value)
        return Result<ChatSessionDto>.Failure(
            new Error(DomainErrorCodes.Chat.CardNotInProject, "Card is in a different project."));

    // Idempotent: already linked to this card
    if (session.OpenCardId == cardId)
        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));

    session.SetOpenCard(cardId);
    await _sessionRepo.UpdateAsync(session, ct);

    return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
}
```

**2d. Add controller endpoint:**

In `ChatSessionsController.cs`, add:

```csharp
[HttpPost("{sessionId:guid}/link-card")]
[ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<IActionResult> LinkCard(Guid sessionId, [FromBody] LinkCardRequest request)
{
    var userId = User.GetRequiredUserId();
    var result = await sessionService.LinkCardAsync(sessionId, request.CardId, userId);

    if (result.IsFailure)
        return this.ToProblemResult(result.Error);

    return Ok(result.Value);
}

// Add request DTO at bottom of file:
public record LinkCardRequest(Guid CardId);
```

**2e. Add `ApiRoutes` entry:**

In `src/web-ui/app/lib/routes.ts`, add to `Chat.sessions`:
```typescript
linkCard: (sessionId: string) => `/api/chat/sessions/${sessionId}/link-card`,
```

**2f. Tests:**

- Application tests: link card to active project session → success; link to closed session → `CHAT_SESSION_CLOSED`; link to personal session → `CHAT_CARD_NOT_IN_PROJECT`; link card from different project → `CHAT_CARD_NOT_IN_PROJECT`; non-member non-owner → `MembershipDenied`; idempotent (same card again → success, no change); project member (not owner) can link.
- Server integration tests: `POST /api/chat/sessions/{id}/link-card` with valid/invalid cardId, auth checks.

### Step 3: `CardChatLinkList.vue`

**File:** `src/web-ui/app/components/chat/CardChatLinkList.vue`

**Props:**
```typescript
defineProps<{
  cardId: string
}>()
```

**Emits:**
```typescript
defineEmits<{
  'open-session': [sessionId: string]
}>()
```

**Implementation:**

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'

type CardChatLinkDto = components['schemas']['CardChatLinkDto']

const props = defineProps<{ cardId: string }>()
const emit = defineEmits<{ 'open-session': [sessionId: string] }>()

const api = useApi()
const toast = useAppToast()

const links = ref<CardChatLinkDto[]>([])
const loading = ref(true)
const isExpanded = ref(false)

async function fetchLinks() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Chat.cardLinks.byCard(props.cardId))
    links.value = (data as CardChatLinkDto[]) ?? []
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : 'Failed to load linked chats')
  } finally {
    loading.value = false
  }
}

function handleOpenSession(sessionId: string) {
  emit('open-session', sessionId)
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return ''
  return new Date(iso).toLocaleDateString()
}

onMounted(() => fetchLinks())
</script>

<template>
  <div class="space-y-2">
    <UButton
      variant="ghost"
      size="sm"
      :icon="isExpanded ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
      @click="isExpanded = !isExpanded"
    >
      Linked Chats ({{ links.length }})
    </UButton>

    <div v-if="isExpanded">
      <div v-if="loading" class="text-sm text-muted py-2">Loading...</div>
      <div v-else-if="links.length === 0" class="text-sm text-muted py-2">
        No chats linked to this card yet. Close a project chat to create a link.
      </div>
      <UTable
        v-else
        :rows="links"
        :columns="[
          { key: 'ownerUsername', label: 'Owner' },
          { key: 'summary', label: 'Summary' },
          { key: 'createdAt', label: 'Created' }
        ]"
      >
        <template #ownerUsername-cell="{ row }">
          <span class="text-sm">{{ row.ownerUsername ?? 'Unknown' }}</span>
        </template>
        <template #summary-cell="{ row }">
          <span class="text-sm truncate max-w-[200px] block">
            {{ row.summary ?? '(no summary)' }}
          </span>
        </template>
        <template #createdAt-cell="{ row }">
          <span class="text-sm text-muted">{{ formatDate(row.createdAt) }}</span>
        </template>
        <template #actions-cell="{ row }">
          <UButton
            variant="ghost"
            size="xs"
            icon="i-lucide-external-link"
            title="Open chat session"
            @click="handleOpenSession(row.chatSessionId)"
          />
        </template>
      </UTable>
    </div>
  </div>
</template>
```

**Key points:**
- Collapsible — starts collapsed, user clicks to expand
- Fetches `GET /api/cards/{cardId}/chat-links` (existing endpoint from Task 12)
- Each row has an "open" button that emits `open-session` with the `chatSessionId`
- The parent (`CardPopup`) handles the `open-session` event by loading the session into the `ChatDock`
- `useApi()` try/catch per D-40; errors surfaced via `useAppToast`

### Step 4: Migrate `CardModal.vue` body into `CardPopup.vue` slot

**Prerequisite:** Plan 19b must be complete. `CardPopup.vue`, `CardPopupLayer.vue`, `stores/cardPopup.ts`, and `usePopupZIndex.ts` must exist.

**Files to modify:**
- `src/web-ui/app/components/card/CardPopup.vue` — replace placeholder slot with full card detail body
- `src/web-ui/app/components/card/CardModal.vue` — keep as-is initially (used as reference), then retire after migration verified
- `src/web-ui/app/components/project/ProjectBoard.vue` — change card open path (see Step 5)

**4a. Extract `CardModal` body into a new component `CardPopupBody.vue`:**

Create `src/web-ui/app/components/card/CardPopupBody.vue` — this is the entire `<template #body>` content from `CardModal.vue` (lines 376–579), plus the script logic that drives it (lines 1–319), minus:
- The `AppModal` wrapper (lines 322–331, 581)
- The `closeWithAnimation` / `onClose` / `handleOpenChange` modal-specific logic (lines 112–131)
- The `isOpen` ref (line 26) — the popup's open/close is managed by `cardPopup` store

**What `CardPopupBody.vue` keeps:**
- `card` ref + `fetchCard` + `refetchCardQuietly` + `applyCardUpdate`
- `activeTab` + `tabs` + `desktopTabs` computed
- All child panel imports (`CardDescription`, `CardMetadata`, `CardChecklist`, `CardComments`, `CardDependencies`, `CardSpec`, `CardPlan`, `CardAttachments`)
- Archive/restore logic (`handleArchive`, `confirmArchive`, `handleRestore`, `fetchCardRelationships`)
- Watch logic (`hasDocsTab`, `activeTab` → expand docs, `board.cardContentEvent` → refresh)
- Keyboard shortcuts (`useKeyboard` register/unregister)
- Presence/viewer logic (`otherViewers`, `viewerName`, `toggleWatch`)
- Version ownership pattern (D-41): `card.value.version` stays on the single `card` ref

**What `CardPopupBody.vue` adds:**
- New `'chat'` tab in `activeTab` union type and `tabs`/`desktopTabs` arrays
- `CardChatLinkList` component in the chat tab
- `handleOpenChatSession(sessionId)` — loads the session into the `ChatDock`:
  ```typescript
  const chatDock = useChatDockStore()
  function handleOpenChatSession(sessionId: string) {
    chatDock.loadSession(sessionId)
    chatDock.openDock()
  }
  ```

**Props:**
```typescript
defineProps<{
  cardId: string
  projectId: string
  readonly?: boolean
}>()
```

**Emits:**
```typescript
defineEmits<{
  close: []
  archived: []
  restored: []
}>()
```

**4b. Update `CardPopup.vue` to render `CardPopupBody`:**

Replace the placeholder `<slot>` content (Plan 19b's lines 362–369) with:

```vue
<CardPopupBody
  :card-id="cardId"
  :project-id="projectId"
  @close="cardPopup.closeCard(cardId)"
  @archived="/* boardStore.fetchBoard handled by parent */"
  @restored="/* boardStore.fetchBoard handled by parent */"
/>
```

The `CardPopup.vue` header (currently showing `Card {{ cardId.slice(0, 8) }}…`) needs the card title. Options:
- **Option A:** `CardPopupBody` emits the title after fetch, `CardPopup` displays it
- **Option B:** `CardPopup` fetches the card itself for the title only
- **Decision: Option A** — `CardPopupBody` emits `'title-loaded'` with the card title after `fetchCard()` completes. `CardPopup` watches this and updates its header.

Add to `CardPopupBody`:
```typescript
const emit = defineEmits<{
  close: []
  archived: []
  restored: []
  'title-loaded': [title: string]
}>()

// In fetchCard(), after card.value is set:
watch(card, (c) => {
  if (c) emit('title-loaded', c.title ?? '')
})
```

Update `CardPopup.vue` header:
```vue
<span class="text-sm font-medium truncate">
  {{ cardTitle || `Card ${cardId.slice(0, 8)}…` }}
</span>
```
Where `cardTitle` is a local `ref<string>('')` updated by `@title-loaded`.

**4c. Update `CardPopupLayer.vue`:**

Pass `projectId` to each `CardPopup`. The layer needs to know the project ID — derive it from the route or pass it as a prop. Since `CardPopupLayer` is mounted in `layouts/default.vue` (no route context for project), use a different approach:

**Decision:** Don't pass `projectId` through the layer. Instead, `CardPopupBody` fetches the card first, then derives `projectId` from the card response (`card.projectId`). The `CardPopupBody` already calls `fetchCard()` which returns the full card including `projectId`.

Update `CardPopupBody` to derive `projectId` from the fetched card:
```typescript
const projectId = ref<string>('')

// In fetchCard():
const { data } = await api.GET(ApiRoutes.Cards.detail(/* need projectId to call this */))
```

**Problem:** `ApiRoutes.Cards.detail` requires `projectId`. We don't have it before fetching the card.

**Solution:** Add a lightweight `GET /api/cards/{cardId}/project` endpoint that returns just `{ projectId }` — OR use the existing `GET /api/cards/{cardId}/chat-links` pattern where the controller derives project context from the card. **Better solution:** The `CardPopup` store already knows which project the user is viewing (from the route). Store the `projectId` in the `cardPopup` store when `openCard` is called from a project page.

Add to `stores/cardPopup.ts`:
```typescript
const cardProjectIds = ref<Record<string, string>>({})

function openCard(cardId: string, projectId?: string) {
  // ... existing logic ...
  if (projectId) {
    cardProjectIds.value[cardId] = projectId
  }
}

function getProjectId(cardId: string): string | null {
  return cardProjectIds.value[cardId] ?? null
}
```

Then `CardPopupBody` reads `cardPopup.getProjectId(props.cardId)` to get the project ID for API calls.

**4d. Chat tab in `CardPopupBody`:**

Add to `activeTab` type:
```typescript
const activeTab = ref<'details' | 'checklist' | 'comments' | 'related' | 'docs' | 'chat'>('details')
```

Add to `tabs` computed (mobile):
```typescript
{ label: 'Chat', value: 'chat' as const },
```

Add to `desktopTabs` computed:
```typescript
{ label: 'Chat', value: 'chat' as const },
```

Add chat tab content in both desktop and mobile layouts:
```vue
<div v-else-if="activeTab === 'chat'">
  <CardChatLinkList
    :card-id="cardId"
    @open-session="handleOpenChatSession"
  />
</div>
```

### Step 5: Board open path

**Files to modify:**
- `src/web-ui/app/components/project/ProjectBoard.vue` — change `openCardModal` to use `cardPopup.openCard`
- `src/web-ui/app/stores/board.ts` — keep `setOpenCardId` for realtime bridge

**5a. Update `ProjectBoard.vue`:**

Current flow (lines 87–91, 108–111, 300–308):
```
BoardCard click → BoardView emit 'card-click' → ProjectBoard.handleCardClick
  → openCardModal(card) → selectedCardId = card.id, showCardModal = true
  → <CardModal v-if="selectedCardId" ... />
```

New flow:
```
BoardCard click → BoardView emit 'card-click' → ProjectBoard.handleCardClick
  → cardPopup.openCard(card.id, props.projectId)
  → boardStore.setOpenCardId(card.id)  // keep for realtime bridge + dock context
  → REMOVE <CardModal> mount
```

Changes in `ProjectBoard.vue`:
1. Remove `showCardModal`, `selectedCard` refs (lines 32–33)
2. Change `openCardModal` (lines 87–91):
   ```typescript
   function openCardModal(card: CardResponse) {
     const cardPopup = useCardPopupStore()
     cardPopup.openCard(card.id, props.projectId)
     boardStore.setOpenCardId(card.id)
   }
   ```
3. Change `handleCardModalClose` (lines 113–117):
   ```typescript
   function handleCardModalClose() {
     boardStore.setOpenCardId(null)
     boardStore.fetchBoard(props.projectId)
   }
   ```
   Note: `handleCardModalClose` is called when a card popup closes. The `CardPopupBody` emits `close`, which `CardPopup` handles by calling `cardPopup.closeCard(cardId)`. We need a way for `ProjectBoard` to know a card was closed so it can refresh the board. **Solution:** Watch `cardPopup.openCardIds` — when a card ID is removed, refresh the board.

   ```typescript
   const cardPopup = useCardPopupStore()
   watch(
     () => cardPopup.openCardIds.length,
     (newLen, oldLen) => {
       if (newLen < oldLen) {
         // A card popup was closed — refresh board to pick up any changes
         boardStore.fetchBoard(props.projectId)
       }
     }
   )
   ```

4. Remove `<CardModal>` mount (lines 300–308)
5. Update `anyModalOpen` computed (line 46–52) — remove `selectedCardId` check (card popups are managed by the popup stack, not by this component's modal state)

**5b. `BoardCard.vue` — no changes needed.** It already emits `'click'` which flows through `BoardView` → `ProjectBoard.handleCardClick`. The change is only in `ProjectBoard`.

**5c. `stores/board.ts` — no changes needed.** `setOpenCardId` stays as the realtime bridge. The dock reads `useBoardStore().openCardId` for context (see `chatDock.ts` line 125).

### Step 6: Project "Chats" tab

**File to modify:** `src/web-ui/app/pages/projects/[id]/index.vue`

**6a. Add "Chats" tab to the tab bar:**

Add a third tab button alongside "Board" and "Docs" (after line 213):

```vue
<UButton
  :variant="activeTab === 'chats' ? 'solid' : 'ghost'"
  size="sm"
  @click="activeTab = 'chats'"
>
  <UIcon name="i-lucide-messages-square" class="size-4 mr-1" />
  Chats
</UButton>
```

Update `activeTab` type:
```typescript
const activeTab = ref<'board' | 'docs' | 'chats'>('board')
```

**6b. Create the Chats tab content component:**

Create `src/web-ui/app/components/project/ProjectChatsTab.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'

type ChatSessionDto = components['schemas']['ChatSessionDto']
type CardChatLinkDto = components['schemas']['CardChatLinkDto']

const props = defineProps<{ projectId: string }>()

const api = useApi()
const toast = useAppToast()
const chatDock = useChatDockStore()

const sessions = ref<ChatSessionDto[]>([])
const loading = ref(true)
const filter = ref<'all' | 'project' | 'card'>('all')

// Fetch sessions the user participated in for this project
async function fetchSessions() {
  loading.value = true
  try {
    const url = ApiRoutes.Chat.sessions.list(undefined, props.projectId, undefined, undefined, 50)
    const { data } = await api.GET(url)
    const page = data as { sessions: ChatSessionDto[], totalCount: number }
    sessions.value = page?.sessions ?? []
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : 'Failed to load chats')
  } finally {
    loading.value = false
  }
}

// "Card" filter: fetch all CardChatLinks for this project's cards,
// then filter sessions to only those with a CardChatLink.
// Since we can't easily fetch all card-links for all cards in a project
// without N+1 calls, we use a heuristic: sessions with OpenCardId set
// are "card-linked" sessions.
const filteredSessions = computed(() => {
  switch (filter.value) {
    case 'project':
      return sessions.value.filter(s => !s.openCardId)
    case 'card':
      return sessions.value.filter(s => !!s.openCardId)
    default:
      return sessions.value
  }
})

function handleOpenSession(sessionId: string) {
  chatDock.loadSession(sessionId)
  chatDock.openDock()
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return ''
  return new Date(iso).toLocaleDateString()
}

onMounted(() => fetchSessions())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0 p-4">
    <!-- Filter bar -->
    <div class="flex items-center gap-2 mb-4">
      <USelect
        v-model="filter"
        :items="[
          { label: 'All', value: 'all' },
          { label: 'Project', value: 'project' },
          { label: 'Card', value: 'card' }
        ]"
        size="sm"
      />
      <UButton
        icon="i-lucide-refresh-cw"
        variant="ghost"
        size="sm"
        :loading="loading"
        @click="fetchSessions"
      />
    </div>

    <!-- Session list -->
    <div v-if="loading" class="flex-1 flex items-center justify-center">
      <UIcon name="i-lucide-loader-circle" class="size-6 animate-spin" />
    </div>
    <div v-else-if="filteredSessions.length === 0" class="flex-1 flex items-center justify-center">
      <p class="text-sm text-muted">No chats found.</p>
    </div>
    <UTable
      v-else
      :rows="filteredSessions"
      :columns="[
        { key: 'title', label: 'Title' },
        { key: 'status', label: 'Status' },
        { key: 'updatedAt', label: 'Updated' }
      ]"
      @select="(row: ChatSessionDto) => handleOpenSession(row.id)"
    >
      <template #title-cell="{ row }">
        <span class="text-sm font-medium">{{ row.title || '(untitled)' }}</span>
      </template>
      <template #status-cell="{ row }">
        <UBadge :color="row.status === 'Active' ? 'success' : 'neutral'" variant="subtle" size="xs">
          {{ row.status }}
        </UBadge>
      </template>
      <template #updatedAt-cell="{ row }">
        <span class="text-sm text-muted">{{ formatDate(row.updatedAt) }}</span>
      </template>
    </UTable>
  </div>
</template>
```

**6c. Mount `ProjectChatsTab` in the board page:**

In `pages/projects/[id]/index.vue`, add after the Docs tab content (line 227):

```vue
<ProjectChatsTab
  v-else-if="activeTab === 'chats'"
  :project-id="projectId"
/>
```

### Step 7: Context-aware `ChatDock`

**Files to modify:**
- `src/web-ui/app/stores/chatDock.ts`
- `src/web-ui/app/components/chat/ChatDock.vue`

**7a. Add `currentCardId` and `openOrigin` to `chatDock.ts`:**

```typescript
// Add after currentProjectId computed (line 40):

// openOrigin tracks which UI surface opened the dock — drives context for new sessions
const openOrigin = ref<'board' | 'project-chats' | 'card-popup' | null>(null)

// currentCardId: the card context for the dock.
// When opened from a card popup's Chat tab, this is the card's ID.
// When opened from the board header or project-chats tab, this is null.
// Derived from the cardPopup store's activeCardId when on a project page.
const currentCardId = computed(() => {
  if (!currentProjectId.value) return null
  // If the dock was opened from a card popup, use that card's ID
  if (openOrigin.value === 'card-popup') {
    const cardPopup = useCardPopupStore()
    return cardPopup.activeCardId
  }
  // Fall back to boardStore.openCardId for backward compat (board header toggle)
  return useBoardStore().openCardId
})
```

**7b. Update `startNewChat` to use `currentCardId`:**

Current code (lines 124–125):
```typescript
const openCardId = currentProjectId.value ? useBoardStore().openCardId : null
```

Change to:
```typescript
const openCardId = currentProjectId.value ? currentCardId.value : null
```

**7c. Update `openDock` to accept origin:**

```typescript
function openDock(origin?: 'board' | 'project-chats' | 'card-popup') {
  if (origin) openOrigin.value = origin
  isOpen.value = true
  // ... rest of existing logic ...
}
```

**7d. Update `ChatDock.vue` — no structural changes needed.**

The dock already renders `ChatSessionView` with the session from the store. The context (`currentProjectId`, `currentCardId`) is used by `startNewChat` which is called from the draft mode's `ChatInput`. No template changes needed for context awareness itself.

**7e. Update callers to pass origin:**

- Board header toggle (in `ProjectBoard.vue` or wherever the dock toggle button lives): `chatDock.openDock('board')`
- Project Chats tab row click: `chatDock.openDock('project-chats')` then `chatDock.loadSession(sessionId)`
- Card popup Chat tab "open session": `chatDock.openDock('card-popup')` then `chatDock.loadSession(sessionId)`

### Step 8: `ChatLinkCardButton.vue`

**File:** `src/web-ui/app/components/chat/ChatLinkCardButton.vue`

**Props:**
```typescript
defineProps<{
  sessionId: string
  currentCardId: string
  sessionOpenCardId: string | null | undefined
  isProjectSession: boolean
}>()
```

**Emits:**
```typescript
defineEmits<{
  linked: [cardId: string]
}>()
```

**Implementation:**

```vue
<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'

const props = defineProps<{
  sessionId: string
  currentCardId: string
  sessionOpenCardId: string | null | undefined
  isProjectSession: boolean
}>()

const emit = defineEmits<{ linked: [cardId: string] }>()

const api = useApi()
const toast = useAppToast()
const linking = ref(false)

const isVisible = computed(() =>
  props.isProjectSession
  && !!props.currentCardId
  && props.sessionOpenCardId !== props.currentCardId
)

async function handleLink() {
  if (linking.value) return
  linking.value = true
  try {
    await api.POST(ApiRoutes.Chat.sessions.linkCard(props.sessionId), {
      body: { cardId: props.currentCardId }
    })
    toast.success('Card linked to chat')
    emit('linked', props.currentCardId)
  } catch (err) {
    const msg = err instanceof ApiError ? err.message : 'Failed to link card'
    toast.error(msg)
  } finally {
    linking.value = false
  }
}
</script>

<template>
  <UButton
    v-if="isVisible"
    icon="i-lucide-link"
    variant="ghost"
    size="xs"
    :loading="linking"
    title="Link this card to the current chat session"
    @click="handleLink"
  />
</template>
```

**8a. Integrate into `ChatDock.vue`:**

In the session-mode header area (where `ChatSessionHeader` is rendered, lines 137–158), add after the existing header buttons:

```vue
<ChatLinkCardButton
  v-if="dock.mode === 'session' && sessionViewRef?.session"
  :session-id="dock.activeSessionId!"
  :current-card-id="dock.currentCardId ?? ''"
  :session-open-card-id="sessionViewRef.session.openCardId"
  :is-project-session="!!sessionViewRef.session.projectId"
  @linked="(cardId: string) => { /* update session ref's openCardId */ }"
/>
```

The `@linked` handler should update the session's `openCardId` in the `sessionViewRef` so the button hides immediately. `ChatSessionView` should expose a method or the session ref should be reactive to this change.

**Simpler approach:** After linking, re-fetch the session from the API to get the updated `openCardId`. The `ChatSessionView` already has a `session` ref — emit an event to trigger a refresh.

### Step 9: Spec update

Confirm the spec's reconciled notes (§2.1, §5.1, §5.2, §5.4) match the shipped behavior. The parent commit already added these notes; this step verifies no drift.

### Step 10: Manual validation

Create `docs/manual-validation/2026-08-02-phase-7-chat-plan-20-web-card-popup-chat-project-chats-matrix.md`:

| # | Scenario | Steps | Expected |
|---|----------|-------|----------|
| 1 | Card opens as CardPopup | Click a card on the board. | Card opens as a draggable popup (not the old modal). Title shows in header. |
| 2 | Multiple card popups coexist | Open card 1, then card 2, then card 3. | Three popups visible, cascaded positions. |
| 3 | Max-3 enforcement | Try to open a 4th card. | Error toast "Close a card popup first (max 3 open)". No 4th popup. |
| 4 | Card popup "Chat" tab | Open a card popup, click "Chat" tab. | `CardChatLinkList` renders. If no linked chats, shows "No chats linked" message. |
| 5 | CardChatLinkList after close | Close a project chat that was opened from a card. Reopen the card popup → Chat tab. | The closed chat appears as a linked chat with summary. |
| 6 | Open linked session from card popup | Click the "open" button on a linked chat row. | ChatDock opens with that session loaded. |
| 7 | Project "Chats" tab | Navigate to a project, click "Chats" tab. | Lists chats the user participated in (own + project-member). |
| 8 | Chats tab filters | Switch between All / Project / Card filters. | All shows all sessions; Project shows sessions without a linked card; Card shows sessions with at least one CardChatLink in this project. |
| 9 | Open session from Chats tab | Click a session row in the Chats tab. | ChatDock opens with that session loaded. |
| 10 | Context-aware dock — board origin | Open dock from board header toggle. Create new chat. | New session has projectId set, no openCardId. |
| 11 | Context-aware dock — card-popup origin | Open a card popup, go to Chat tab, click "new chat" (or open dock from there). Create new chat. | New session has projectId + openCardId set. |
| 12 | ChatLinkCardButton appears | Open dock with a project session active. Open a card popup (different card than session's openCardId). | "Link card" button appears in dock header. |
| 13 | ChatLinkCardButton links card | Click "Link card" button. | Toast "Card linked to chat". Button disappears. Session's openCardId updated. |
| 14 | ChatLinkCardButton hidden when already linked | Session already linked to card X. Open card X popup. | Button does NOT appear (already linked). |
| 15 | Escape LIFO with card popups + dock | Open dock, then open 2 card popups. Press Escape 3 times. | Card 2 closes, card 1 closes, dock closes (LIFO order). |
| 16 | List visibility — project member sees project chats | As a project member (not owner), open Chats tab. | Project chats where you're a member appear. |
| 17 | List visibility — non-member doesn't see project chats | As a non-member, try to view project chats. | No project chats visible (or access denied). |
| 18 | No regression — personal chats still work | Open `/chats` page. | Personal chats still listed. Create/send/close all work. |

## Acceptance

- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
- `cd src/web-ui && pnpm test`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatSession"`
- `dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~ChatSessions"` (new link-card + visibility tests pass)

## Out of scope (V2 / later)

- `@mention`-based card auto-linking inside chat messages (parsing `#card-123` or `@card` mentions to set `OpenCardId` / create `CardChatLink`). Noted in spec §5.4 + §11. The manual `ChatLinkCardButton` is the MVP.
- TUI equivalent of the card-popup "Chat" tab / project "Chats" tab (TUI keeps `ProjectChatScreen` board-`c` flow from Plan 23; browsing linked chats from the TUI card screen remains deferred per Plan 23's note).
- A dedicated `?cardLinked=true` backend filter for the project "Chats" tab — this plan groups client-side to avoid a new endpoint.
