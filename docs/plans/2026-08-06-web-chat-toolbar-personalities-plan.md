# Web Chat Toolbar + Personality Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **This particular plan is being hand-implemented by the user, task by task, with review after each task** — the sub-skill requirement above applies only if an agent ends up executing it instead.

**Goal:** Fix the shipped web chat header/toolbar UX (Plan 18 follow-up) — consolidate the cramped `ChatDock` header, collapse the unused attached-docs panel, highlight matched text in search, and add a Personality management modal with six seeded starter personas.

**Architecture:** All frontend work lives in `src/web-ui/app/components/chat/`, following existing prop/emit patterns already in `ChatSessionHeader.vue` / `ChatSessionView.vue`. One backend change: `AgentPersonalityService.ListAsync` seeds six default `AgentPersonality` rows the first time a user's list is ever empty. No new backend endpoints — all CRUD already exists on `AgentPersonalitiesController`.

**Tech Stack:** Vue 3 `<script setup>`, Nuxt UI v4 (`USelect`, `UDropdownMenu`, `UModal` via `AppModal.vue`), Vitest + `@nuxt/test-utils/runtime` (`mountSuspended`, `mockNuxtImport`) for frontend tests, xUnit + hand-written fakes (no NSubstitute needed here, matching `AgentPersonalityServiceTests.cs`'s existing `FakePersonalityRepo` pattern) for backend tests.

## Global Constraints

- No FluentAssertions — plain `Assert.*` only (repo-wide convention).
- No `console.log`/`console.error`/`console.warn` — use `useAppToast()` for user-facing feedback.
- Every `useApi()` call wrapped in try/catch (D-40) — the `await` itself throws, never resolves with a populated `error` field.
- `useApi()` must not be a module-level singleton — not relevant here (no new composable created), but don't hoist `useApi()`/`useAppToast()` calls outside a component's setup scope.
- All new Web UI API paths come from `ApiRoutes` in `src/web-ui/app/lib/routes.ts` — never inline a path string. (Not needed this round — the personality routes already exist at `routes.ts:249-256`.)
- Domain entities encapsulate state transitions via instance methods — services orchestrate, never set entity properties directly. (Not applicable here — `AgentPersonality.Update`/`.Archive()` already exist and are unchanged by this plan.)
- Stay on the current branch (`task/web-chat-managers`) — no new branch or worktree for this work.

---

### Task 1: Seed six default personalities on first-ever list fetch

**Files:**
- Create: `src/HydraForge.Application/Chat/DefaultAgentPersonalities.cs`
- Modify: `src/HydraForge.Application/Chat/AgentPersonalityService.cs:40-46` (`ListAsync`)
- Test: `tests/HydraForge.Application.Tests/Chat/AgentPersonalityServiceTests.cs` (append new `[Fact]`s)

**Interfaces:**
- Consumes: `IAgentPersonalityRepository` (`AddAsync`, `ListByUserAsync`) — both already exist, no signature changes.
- Produces: `DefaultAgentPersonalities.All` — a `static readonly IReadOnlyList<(string Name, string? Description, string SystemPrompt)>` of exactly 6 entries, consumed only by `AgentPersonalityService.ListAsync` in this task.

- [ ] **Step 1: Write the failing tests**

Append to `tests/HydraForge.Application.Tests/Chat/AgentPersonalityServiceTests.cs` (inside the existing `AgentPersonalityServiceTests` class, using the existing `FakePersonalityRepo`):

```csharp
    [Fact]
    public async Task ListAsync_ZeroPersonalitiesEver_SeedsSixDefaults()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();

        var result = await service.ListAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Count);
        Assert.Equal(6, repo.Personalities.Count(p => p.UserId == userId));
        Assert.Contains(result.Value, p => p.Name == "well");
        Assert.Contains(result.Value, p => p.Name == "Carter");
        Assert.Contains(result.Value, p => p.Name == "Commander Erwin");
        Assert.Contains(result.Value, p => p.Name == "Captain Levi");
        Assert.Contains(result.Value, p => p.Name == "Fire_Keeper");
        Assert.Contains(result.Value, p => p.Name == "Tarnished");
    }

    [Fact]
    public async Task ListAsync_SeededPersonalities_AreNotMarkedDefault()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);

        var result = await service.ListAsync(NewId());

        Assert.All(result.Value, p => Assert.False(p.IsDefault));
    }

    [Fact]
    public async Task ListAsync_UserAlreadyHasAPersonality_DoesNotReseed()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = NewId(),
                UserId = userId,
                Name = "My Own Bot",
                SystemPrompt = "p",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ListAsync(userId);

        Assert.Single(result.Value);
        Assert.Equal("My Own Bot", result.Value[0].Name);
    }

    [Fact]
    public async Task ListAsync_UserHasOnlyArchivedPersonality_DoesNotReseed()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userId = NewId();
        repo.Personalities.Add(
            new AgentPersonality
            {
                Id = NewId(),
                UserId = userId,
                Name = "Archived Bot",
                SystemPrompt = "p",
                ArchivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.ListAsync(userId);

        Assert.Empty(result.Value);
        Assert.Single(repo.Personalities);
    }

    [Fact]
    public async Task ListAsync_TwoDifferentUsers_EachGetsTheirOwnSeededSix()
    {
        var repo = new FakePersonalityRepo();
        var service = new AgentPersonalityService(repo);
        var userA = NewId();
        var userB = NewId();

        var resultA = await service.ListAsync(userA);
        var resultB = await service.ListAsync(userB);

        Assert.Equal(6, resultA.Value.Count);
        Assert.Equal(6, resultB.Value.Count);
        Assert.Equal(12, repo.Personalities.Count);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~AgentPersonalityServiceTests"`
Expected: the 5 new tests FAIL (`ListAsync` returns an empty list today — `DefaultAgentPersonalities` doesn't exist yet, so this won't even compile until Step 3 exists as a stub; write the test file first, then confirm the *build* fails with "DefaultAgentPersonalities does not exist", which is the correct "red" state for a not-yet-created type).

- [ ] **Step 3: Create `DefaultAgentPersonalities.cs`**

```csharp
namespace HydraForge.Application.Chat;

public static class DefaultAgentPersonalities
{
    public static readonly IReadOnlyList<(string Name, string? Description, string SystemPrompt)> All =
    [
        (
            "well",
            "Sage guide from idea to a fully-documented project — scope, glossary, functional spec, data model, backlog, architecture, decisions. Explains things plainly; no dev background required.",
            """
            You are the well — a still, deep, listening presence, the same well Toru Okada climbs down into seeking answers that don't come easily or fast. You do not rush. You sit with an idea in silence before responding, the way the bottom of a dry well holds the heat of the day long after the sun is gone.

            Your purpose: take a person from a raw, half-formed idea to a fully realized project, documented start to finish — scope, glossary, functional specification, data model, backlog, architecture, and the decisions behind each choice. You draw these out of the person the way a well draws things up from underground — patiently, in the right order, one bucket at a time.

            Speak reflectively, a little strange, prone to unexpected metaphor and quiet observation — but never so cryptic that the actual work gets lost. Whether the person across from you is a career engineer or has never written a line of code, translate every technical concept into plain, concrete language before moving on. Ask the quiet, searching questions that get at what they actually mean, not just what they first said.
            """
        ),
        (
            "Carter",
            "Investigative reviewer — finds bugs, CVEs, injection/DDoS risk, warnings others miss. Works for dev and non-dev projects alike.",
            """
            You are Randolph Carter, dream-wanderer and investigator of things half-hidden — drawn now not to sunken cities but to the fault lines in a system: the query left unsanitized, the endpoint with no rate limit, the dependency quietly carrying a known CVE, the warning everyone scrolled past. You approach code the way you once approached the Dreamlands — with unease that sharpens attention rather than dulling it, certain that something is wrong before you can say exactly what.

            Speak in searching, slightly dread-tinged prose — you notice what others overlook, and you say so plainly once you've found it, without losing yourself in atmosphere for its own sake. Every finding must be concrete and actionable: what's wrong, where, why it matters, what fixes it. You serve any project brought before you, software or otherwise — the unease is a way of looking closely, not a genre requirement.
            """
        ),
        (
            "Commander Erwin",
            "Orchestrator voice — plans and sequences work top to bottom, frames tradeoffs the way a commander frames a campaign. Describes what it would delegate; strategic, calculating framing for any plan.",
            """
            You are Commander Erwin Smith of the Survey Corps. Serious, calculating, far-sighted — you plan not for today but for years out, and you hold every plan with a stoic, unshakeable demeanor, icy-eyed and clear even when the cost is heavy. You have made peace, long ago, with sacrificing what's comfortable — including your own people, including yourself — for the strategic gain that actually matters.

            Applied here: you take a plan and walk it from first task to last, exactly as you would ready the Corps for an expedition — sequence, dependencies, what happens if a step fails, what the fallback is. You frame decisions and tradeoffs the way a commander frames a campaign: costs stated plainly, the mission never lost sight of. Speak with restraint and gravity. You do not delegate to other systems — you describe, in your own voice, how the work should be sequenced and why.
            """
        ),
        (
            "Captain Levi",
            "Reviewer voice — exacting, disciplined, zero tolerance for sloppy lint/format/tests. Terse and blunt.",
            """
            You are Captain Levi Ackerman. Serious, stoic, a clean freak in the truest sense — disorder of any kind offends you, and you say so without softening it. Disciplined, highly skilled, and utterly without patience for sloppiness, whether it's a dirty blade or a dirty codebase.

            Applied here: you review work the way you inspect your squad's gear before an expedition — nothing gets past you. Lint errors, missed formatting, untested code that touches business rules (controllers, DTOs, and boilerplate don't need tests), a function doing three things when it should do one — you call it out, tersely, exactly, without cushioning. Praise is rare and short when earned. You respect competence and have no time for excuses.
            """
        ),
        (
            "Fire_Keeper",
            "Planner voice — turns an idea into a proper spec and plan with care for whoever has to execute it, not just the mechanics.",
            """
            You are the Fire Keeper. Quietly dedicated, empathetic, practically supportive — ISFJ in temperament, a 6w5's loyalty and need for grounded security running underneath. You tend what's placed in your care the way you tend a bonfire: without fanfare, but without fail.

            Applied here: you take a raw idea and carry it into a real spec and a real plan, the way you'd carry someone's flame through the dark — asking what they actually need, thinking of the person who will implement each step, not only the steps themselves. Speak gently, thoroughly, with quiet reassurance. You surface the requirements others miss because you're paying attention to the whole picture, not just the request as stated.
            """
        ),
        (
            "Tarnished",
            "Driver voice — picks up a small spec/plan/task and just gets it done, independently, no hand-holding needed.",
            """
            You are the Tarnished, bearer of the curse that will not let you stop moving forward. ISTP, 8w9 — pragmatic, adaptable, driven to control your own path rather than wait on someone else's. You don't need the full picture handed to you; give you a task and you will find your own way through it.

            Applied here: you take a small, well-scoped piece of work — a bugfix, a single task, a narrow finding from a review — and drive it to done on your own initiative. Speak plainly, briefly, with the flat confidence of someone who has already died enough times to stop being precious about failure. You don't ask for permission at every step; you act, report what you did, and move to the next thing.
            """
        ),
    ];
}
```

- [ ] **Step 4: Modify `ListAsync` to seed on first-ever fetch**

In `src/HydraForge.Application/Chat/AgentPersonalityService.cs`, replace the existing `ListAsync` method:

```csharp
    public async Task<Result<IReadOnlyList<AgentPersonalityDto>>> ListAsync(
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var all = await _repo.ListByUserAsync(actorId, ct);

        if (all.Count == 0)
        {
            foreach (var seed in DefaultAgentPersonalities.All)
            {
                await _repo.AddAsync(
                    new AgentPersonality
                    {
                        Id = Guid.NewGuid(),
                        UserId = actorId,
                        Name = seed.Name,
                        Description = seed.Description,
                        SystemPrompt = seed.SystemPrompt,
                        IsDefault = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    },
                    ct
                );
            }
            all = await _repo.ListByUserAsync(actorId, ct);
        }

        var active = all.Where(p => !p.ArchivedAt.HasValue).Select(MapToDto).ToList();
        return Result<IReadOnlyList<AgentPersonalityDto>>.Success(active);
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~AgentPersonalityServiceTests"`
Expected: all tests PASS, including the existing `ListAsync_ReturnsOnlyNonArchived` (still valid — that test seeds two personalities itself before calling `ListAsync`, so `all.Count == 0` is never true there, no seeding triggered, no behavior change for it).

- [ ] **Step 6: Format and commit**

```bash
dotnet csharpier format src/HydraForge.Application/Chat/DefaultAgentPersonalities.cs src/HydraForge.Application/Chat/AgentPersonalityService.cs
git add src/HydraForge.Application/Chat/DefaultAgentPersonalities.cs src/HydraForge.Application/Chat/AgentPersonalityService.cs tests/HydraForge.Application.Tests/Chat/AgentPersonalityServiceTests.cs
git commit -m "feat(chat): seed six default personalities on first-ever list fetch"
```

**Note on scope trim:** the design spec mentioned viewing archived personalities "via a toggle" in the future management modal (Task 5). `ListAsync` filters to active-only by design (existing `ListAsync_ReturnsOnlyNonArchived` test enforces this), and there's no other endpoint that returns archived rows. Rather than widen the API surface for a "view archived" feature nobody explicitly asked to keep, Task 5's modal simply doesn't offer that toggle — archiving is a one-way action in v1, consistent with `UpdateAsync` already rejecting edits to archived personalities. Flagging this here so the reviewer doesn't read Task 5 and wonder where the toggle went.

---

### Task 2: Inline substring highlight for search-in-chat

**Files:**
- Create: `src/web-ui/app/lib/highlight-html.ts`
- Create: `src/web-ui/app/lib/__tests__/highlight-html.test.ts`
- Modify: `src/web-ui/app/components/chat/ChatMessageBubble.vue`
- Modify: `src/web-ui/app/components/chat/ChatMessageList.vue`
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue:617-625` (pass `findQuery` down)
- Test: `src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts` (append)

**Interfaces:**
- Produces: `highlightHtml(html: string, query: string): string` — pure function, exported from `~/lib/highlight-html`. Returns `html` unchanged when `query` is blank or when running server-side (`!import.meta.client`). Wraps case-insensitive matches of `query` in `<mark class="...">` within the existing sanitized HTML's text nodes only (never touches tags/attributes).
- Consumes (in `ChatMessageBubble.vue`): existing `renderedContent` computed (already defined, `renderMarkdown(props.message.content)`).

- [ ] **Step 1: Write the failing test for `highlightHtml`**

Create `src/web-ui/app/lib/__tests__/highlight-html.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { highlightHtml } from '~/lib/highlight-html'

describe('highlightHtml', () => {
  it('wraps a single case-insensitive match in <mark>', () => {
    const result = highlightHtml('<p>Hello World</p>', 'world')
    expect(result).toContain('<mark')
    expect(result).toContain('World</mark>')
  })

  it('wraps multiple matches in the same text node', () => {
    const result = highlightHtml('<p>cat cat cat</p>', 'cat')
    expect(result.match(/<mark/g)?.length).toBe(3)
  })

  it('returns the original HTML unchanged when query is blank', () => {
    const result = highlightHtml('<p>Hello World</p>', '')
    expect(result).toBe('<p>Hello World</p>')
  })

  it('returns the original HTML unchanged when there is no match', () => {
    const result = highlightHtml('<p>Hello World</p>', 'xyz')
    expect(result).toBe('<p>Hello World</p>')
  })

  it('does not touch element attributes, only text content', () => {
    const result = highlightHtml('<a href="world.com">click</a>', 'world')
    expect(result).toContain('href="world.com"')
    expect(result).not.toContain('<mark')
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test highlight-html`
Expected: FAIL — `~/lib/highlight-html` module not found.

- [ ] **Step 3: Write `highlight-html.ts`**

```ts
export function highlightHtml(html: string, query: string): string {
  const trimmed = query.trim()
  if (!import.meta.client || !trimmed) return html

  const container = document.createElement('div')
  container.innerHTML = html
  const needle = trimmed.toLowerCase()

  const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT)
  const textNodes: Text[] = []
  let node = walker.nextNode()
  while (node) {
    textNodes.push(node as Text)
    node = walker.nextNode()
  }

  for (const textNode of textNodes) {
    const text = textNode.textContent ?? ''
    const lower = text.toLowerCase()
    if (!lower.includes(needle)) continue

    const frag = document.createDocumentFragment()
    let cursor = 0
    let idx = lower.indexOf(needle, cursor)
    while (idx !== -1) {
      if (idx > cursor) frag.appendChild(document.createTextNode(text.slice(cursor, idx)))
      const mark = document.createElement('mark')
      mark.className = 'bg-yellow-300/60 dark:bg-yellow-500/40 rounded-sm px-0.5'
      mark.textContent = text.slice(idx, idx + needle.length)
      frag.appendChild(mark)
      cursor = idx + needle.length
      idx = lower.indexOf(needle, cursor)
    }
    if (cursor < text.length) frag.appendChild(document.createTextNode(text.slice(cursor)))
    textNode.replaceWith(frag)
  }

  return container.innerHTML
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test highlight-html`
Expected: PASS (5/5). Vitest's default environment for this project is jsdom via `@nuxt/test-utils`, so `document`/`import.meta.client` resolve fine in the test — confirm by checking the run output shows no "document is not defined" error; if it does, the test file needs `// @vitest-environment jsdom` at the top (check `src/web-ui/vitest.config.ts` first — if `environment: 'nuxt'` or `'jsdom'` is already global, no per-file override is needed).

- [ ] **Step 5: Wire `findQuery` into `ChatMessageBubble.vue`**

In `src/web-ui/app/components/chat/ChatMessageBubble.vue`, add to the props (after `highlighted`):

```ts
const props = withDefaults(
  defineProps<{
    message: ChatMessageDto
    isStreaming?: boolean
    rollbackDisabled?: boolean
    highlighted?: boolean
    findQuery?: string
  }>(),
  {
    isStreaming: false,
    rollbackDisabled: false,
    highlighted: false,
    findQuery: ''
  }
)
```

Add the import and a new computed, right after the existing `renderedContent` computed (line 44):

```ts
import { highlightHtml } from '~/lib/highlight-html'
```

```ts
const displayedContent = computed(() =>
  props.findQuery ? highlightHtml(renderedContent.value, props.findQuery) : renderedContent.value
)
```

In the template, change the message bubble's `v-html` binding (currently `v-html="renderedContent"` at line 131) to:

```html
        v-html="displayedContent"
```

- [ ] **Step 6: Thread `findQuery` through `ChatMessageList.vue`**

Add to the props (after `highlightMessageId`):

```ts
  const props = withDefaults(
    defineProps<{
      messages: ChatMessageDto[]
      streamingMessage?: StreamingMessage | null
      streamError?: string | null
      awaitingReply?: boolean
      rollbackDisabled?: boolean
      highlightMessageId?: string | null
      findQuery?: string
    }>(),
    {
      streamingMessage: null,
      streamError: null,
      awaitingReply: false,
      rollbackDisabled: false,
      highlightMessageId: null,
      findQuery: ''
    }
  )
```

In the template, add `:find-query="findQuery"` to the `<ChatMessageBubble>` element (currently lines 197-205):

```html
        <ChatMessageBubble
          v-for="message in visibleMessages"
          :key="message.id"
          :message="message"
          :is-streaming="streamingMessage?.messageId === message.id"
          :rollback-disabled="rollbackDisabled"
          :highlighted="message.id === highlightMessageId"
          :find-query="findQuery"
          @rollback="emit('rollback', $event)"
        />
```

- [ ] **Step 7: Pass `findQuery` from `ChatSessionView.vue`**

In `src/web-ui/app/components/chat/ChatSessionView.vue`, add `:find-query="findQuery"` to the `<ChatMessageList>` element (currently lines 617-625):

```html
      <ChatMessageList
        :messages="session.messages"
        :streaming-message="chatStream.streamingMessage.value"
        :stream-error="streamError"
        :awaiting-reply="awaitingReply"
        :rollback-disabled="isRollingBack || awaitingReply"
        :highlight-message-id="highlightMessageId"
        :find-query="findQuery"
        @rollback="handleRollbackRequest"
      />
```

(`findQuery` is already a `ref('')` declared at line 83 — no new state needed.)

- [ ] **Step 8: Write component test for the highlight prop**

Append to `src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts` (check the file's existing mock setup first — it likely already mocks `useAuthStore`/`useAppToast`; match that pattern rather than introducing a new one):

```ts
  it('wraps matched text in <mark> when findQuery is set', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: {
        message: makeMessage({ content: 'The quick brown fox' }),
        findQuery: 'quick'
      }
    })
    expect(wrapper.html()).toContain('<mark')
  })

  it('does not add <mark> when findQuery is empty', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: {
        message: makeMessage({ content: 'The quick brown fox' })
      }
    })
    expect(wrapper.html()).not.toContain('<mark')
  })
```

(Use whatever `makeMessage(...)` helper or inline `ChatMessageDto` object literal the existing test file already uses — read the top of `ChatMessageBubble.test.ts` before writing this to match its exact fixture shape.)

- [ ] **Step 9: Run all touched tests to verify they pass**

Run: `cd src/web-ui && pnpm test highlight-html ChatMessageBubble ChatMessageList ChatSessionView`
Expected: all PASS.

- [ ] **Step 10: Typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
git add app/lib/highlight-html.ts app/lib/__tests__/highlight-html.test.ts \
  app/components/chat/ChatMessageBubble.vue app/components/chat/ChatMessageList.vue \
  app/components/chat/ChatSessionView.vue app/components/chat/__tests__/ChatMessageBubble.test.ts
git commit -m "feat(chat): highlight matched search text inline, not just the bubble ring"
```

---

### Task 3: Collapsible attached-docs panel, closed by default

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatDocAttach.vue`
- Test: `src/web-ui/app/components/chat/__tests__/ChatDocAttach.test.ts` (append)

**Interfaces:**
- No prop/emit changes — purely internal state (`expanded` ref) added to `ChatDocAttach.vue`. Parent (`ChatSessionView.vue`) needs no changes for this task.

- [ ] **Step 1: Write the failing tests**

Append to `src/web-ui/app/components/chat/__tests__/ChatDocAttach.test.ts`:

```ts
describe('ChatDocAttach — collapse behavior', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: attachedDocs, error: undefined })
  })

  it('is collapsed by default, showing only the summary row', async () => {
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Attached docs (2)')
    expect(wrapper.text()).not.toContain('Design Doc')
  })

  it('expands to show the doc list when the summary row is clicked', async () => {
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')
    expect(wrapper.text()).toContain('Design Doc')
    expect(wrapper.text()).toContain('Spec Doc')
  })

  it('shows a zero count in the summary row when nothing is attached', async () => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: [], error: undefined })
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Attached docs (0)')
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test ChatDocAttach`
Expected: FAIL — `data-testid="doc-attach-toggle"` doesn't exist yet, text still shows "ATTACHED DOCS" (uppercase, no count) and the list is unconditionally visible.

- [ ] **Step 3: Implement the collapse toggle**

In `src/web-ui/app/components/chat/ChatDocAttach.vue`, add state near the other refs (after `showPicker`):

```ts
const expanded = ref(false)
```

Replace the header row (currently lines 68-78):

```html
    <!-- Header row — click to expand/collapse -->
    <button
      type="button"
      data-testid="doc-attach-toggle"
      class="flex items-center justify-between w-full text-left"
      @click="expanded = !expanded"
    >
      <span class="flex items-center gap-1 text-xs font-medium text-muted uppercase">
        <UIcon
          :name="expanded ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
          class="size-3.5"
        />
        Attached docs ({{ attachedDocs.length }})
      </span>
    </button>
    <UButton
      v-if="expanded"
      icon="i-lucide-plus"
      variant="ghost"
      size="xs"
      title="Attach a document"
      @click="showPicker = true"
    />
```

Wrap everything below the header row (loading spinner, error banner, empty state, doc list — currently lines 80-146) in a single `v-if="expanded"`:

```html
    <template v-if="expanded">
      <!-- Loading -->
      <div
        v-if="loading"
        class="flex items-center justify-center py-4"
      >
        <UIcon
          name="i-lucide-loader-circle"
          class="animate-spin size-4 text-muted"
        />
      </div>

      <!-- Error banner (dismissible, shown above list) -->
      <div
        v-if="fetchError"
        class="flex items-center gap-2 text-xs text-error bg-error/10 border border-error/20 rounded px-3 py-2"
      >
        <UIcon
          name="i-lucide-alert-circle"
          class="size-3.5 shrink-0"
        />
        <span class="flex-1 min-w-0 truncate">{{ fetchError }}</span>
        <button
          class="shrink-0 hover:text-error/70 transition-colors"
          title="Dismiss"
          @click="dismissError"
        >
          <UIcon
            name="i-lucide-x"
            class="size-3.5"
          />
        </button>
      </div>

      <!-- Empty state -->
      <p
        v-if="!loading && !fetchError && attachedDocs.length === 0"
        class="text-xs text-muted text-center py-2"
      >
        No documents attached
      </p>

      <!-- Doc list -->
      <ul
        v-if="!loading && attachedDocs.length > 0"
        class="space-y-1"
      >
        <li
          v-for="doc in attachedDocs"
          :key="doc.documentId"
          class="flex items-center gap-2 text-xs group focus-within:relative"
        >
          <UIcon
            name="i-lucide-file-text"
            class="size-3.5 shrink-0 text-muted"
          />
          <span class="truncate flex-1 min-w-0">{{ doc.title }}</span>
          <UButton
            icon="i-lucide-x"
            variant="ghost"
            size="xs"
            color="error"
            class="shrink-0 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 sm:opacity-0 sm:group-hover:opacity-100 focus:opacity-100"
            title="Remove attachment"
            @click="removeDoc(doc.documentId)"
          />
        </li>
      </ul>
    </template>
```

The picker modal (`<ChatDocAttachPicker>`, currently lines 149-153) stays outside the `v-if="expanded"` block, unchanged — it's controlled by its own `v-model:open="showPicker"` and needs to be mountable regardless of collapse state (e.g. `+` inside the expanded view still needs it available).

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test ChatDocAttach`
Expected: all PASS, including every pre-existing test in the file (they exercise `attachedDocs`/error states which are now gated behind `expanded` — re-check each existing test still finds what it expects; if any pre-existing test asserts on list content without first expanding, add `await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')` before the assertion).

- [ ] **Step 5: Typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
git add app/components/chat/ChatDocAttach.vue app/components/chat/__tests__/ChatDocAttach.test.ts
git commit -m "feat(chat): collapse attached-docs panel by default"
```

---

### Task 4: `ChatSessionHeader` compact mode + `ChatDock` overflow menu

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionHeader.vue`
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue:11-31, 526-540` (accept + forward a `compact` prop)
- Modify: `src/web-ui/app/components/chat/ChatDock.vue:204-216` (pass `:compact="true"`, drop the now-redundant plain-text title reasoning — no change needed there, dock keeps its own title)
- Test: `src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts` (append)

**Interfaces:**
- Produces: `ChatSessionHeader` prop `compact?: boolean` (default `false`). When `true`: title is hidden, and pencil/download/search/all-docs-toggle/personality-select/edit-mode-select/fork collapse into a single kebab `UDropdownMenu`; the close (X) button stays visible outside the menu, same as today.
- Produces: `ChatSessionView` prop `compact?: boolean` (default `false`), forwarded 1:1 to its `<ChatSessionHeader :compact="compact" ...>`.
- Consumes: all existing emits (`close`, `toggleScope`, `editPersonality`, `editMode`, `fork`, `startEditTitle`, `exportChat`, `toggleFind`) — unchanged signatures, just re-wired to fire from menu-item `onSelect` instead of button `@click` when `compact` is true.

- [ ] **Step 1: Write the failing tests**

Append to `src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts`:

```ts
describe('ChatSessionHeader — compact mode (ChatDock)', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({
      data: [
        { id: 'p1', name: 'Helper', description: null, systemPrompt: '', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null }
      ],
      error: undefined
    })
  })

  it('hides the title when compact', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ title: 'My Chat' }), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.find('h2').exists()).toBe(false)
  })

  it('shows the title when not compact', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ title: 'My Chat' }), isOwner: true, compact: false }
    })
    await flushPromises()
    expect(wrapper.find('h2').text()).toBe('My Chat')
  })

  it('shows a single kebab menu button instead of individual icon buttons when compact', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="More actions"]').exists()).toBe(true)
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(false)
    expect(wrapper.find('[title="Export chat"]').exists()).toBe(false)
  })

  it('close button stays visible outside the kebab menu when compact', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(true)
  })

  it('shows all individual icon buttons when not compact (unchanged behavior)', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: false }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(true)
    expect(wrapper.find('[title="More actions"]').exists()).toBe(false)
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test ChatSessionHeader`
Expected: FAIL — `compact` prop doesn't exist, `[title="More actions"]` never renders.

- [ ] **Step 3: Add the `compact` prop and kebab-menu computed**

In `src/web-ui/app/components/chat/ChatSessionHeader.vue`, update the props:

```ts
const props = withDefaults(
  defineProps<{
    session: ChatSessionDetailDto
    isOwner: boolean
    compact?: boolean
  }>(),
  {
    compact: false
  }
)
```

Add a computed building the kebab menu's grouped items, right after the existing `personalityItems` computed (after line 77). Note: the "Manage personalities…" entry is deliberately not in this list yet — Task 5 appends it once `PersonalityManageModal` exists, so this task doesn't forward-reference a component from a later task:

```ts
const compactMenuItems = computed(() => {
  const groups: Array<Array<{ label: string, icon?: string, disabled?: boolean, onSelect: () => void }>> = []

  if (props.isOwner) {
    groups.push([
      { label: 'Rename chat', icon: 'i-lucide-pencil', disabled: !isActive.value, onSelect: () => emit('startEditTitle') },
      { label: 'Export chat', icon: 'i-lucide-download', onSelect: () => emit('exportChat') },
      { label: 'Find in conversation', icon: 'i-lucide-search', onSelect: () => emit('toggleFind') }
    ])
  } else {
    groups.push([
      { label: 'Find in conversation', icon: 'i-lucide-search', onSelect: () => emit('toggleFind') }
    ])
  }

  if (props.isOwner && isActive.value && !props.session.projectId) {
    groups.push([
      {
        label: props.session.searchAllMyDocs ? 'All docs (on)' : 'All docs (off)',
        icon: props.session.searchAllMyDocs ? 'i-lucide-check' : undefined,
        onSelect: () => emit('toggleScope', !props.session.searchAllMyDocs)
      },
      ...personalityItems.value.map(p => ({
        label: p.label,
        icon: selectedPersonalityId.value === p.value ? 'i-lucide-check' : undefined,
        onSelect: () => { selectedPersonalityId.value = p.value }
      }))
    ])
  }

  if (props.isOwner && isActive.value && props.session.projectId) {
    groups.push(
      aiEditModeOptions.map(opt => ({
        label: opt.label,
        icon: props.session.aiEditMode === opt.value ? 'i-lucide-check' : undefined,
        onSelect: () => emit('editMode', opt.value)
      }))
    )
  }

  if (props.session.isShared && props.session.projectId && !props.isOwner) {
    groups.push([
      { label: 'Fork (summarize → start my own)', icon: 'i-lucide-git-fork', onSelect: () => emit('fork') }
    ])
  }

  return groups
})
```

- [ ] **Step 4: Update the template**

Wrap the existing title `<h2>` (lines 84-87) in `v-if="!compact"`:

```html
    <h2
      v-if="!compact"
      class="font-semibold truncate flex-1 min-w-0 text-sm"
    >
      {{ session.title || 'Chat' }}
    </h2>
    <div
      v-else
      class="flex-1 min-w-0"
    />
```

(The `v-else` empty flex-1 div preserves the `justify-content`/spacing so the kebab + close buttons still sit at the right edge — the dock's own header owns the actual title text.)

Wrap the existing five action elements — pencil (89-99), download (101-110), find (112-120), scope-toggle label (122-135), personality select (137-146), edit-mode select (148-156), fork button (158-169) — each in `v-if="!compact && ...(existing condition)"` by simply prepending `!compact &&` to each existing `v-if`. For example the pencil button becomes:

```html
    <UButton
      v-if="!compact && isOwner"
      icon="i-lucide-pencil"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Rename chat"
      :disabled="!isActive"
      @click="emit('startEditTitle')"
    />
```

Apply the same `!compact &&` prefix to the download button, find button, scope-toggle `<label>`, personality `<USelect>`, edit-mode `<USelect>`, and fork `<UButton>`. The close button (171-180) and its `isOwner && isActive` condition stay exactly as-is, unprefixed — it must render in both modes.

Add the kebab button, right before the close button:

```html
    <UDropdownMenu
      v-if="compact"
      :items="compactMenuItems"
    >
      <UButton
        icon="i-lucide-more-vertical"
        variant="ghost"
        color="neutral"
        size="xs"
        title="More actions"
      />
    </UDropdownMenu>
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test ChatSessionHeader`
Expected: all PASS.

- [ ] **Step 6: Forward `compact` through `ChatSessionView.vue`**

In `src/web-ui/app/components/chat/ChatSessionView.vue`, add `compact` to the props (in the `withDefaults` block, lines 11-31):

```ts
const props = withDefaults(
  defineProps<{
    sessionId: string
    initialMessage?: string | null
    autoSendInitial?: boolean
    initialPresetId?: string | null
    initialModelId?: string | null
    initialEffort?: string | null
    feature?: string
    compact?: boolean
  }>(),
  {
    initialMessage: null,
    autoSendInitial: false,
    initialPresetId: null,
    initialModelId: null,
    initialEffort: null,
    feature: 'PersonalChat',
    compact: false
  }
)
```

In the template, add `:compact="compact"` to `<ChatSessionHeader>` (lines 528-540):

```html
    <ChatSessionHeader
      v-if="session"
      :session="session"
      :is-owner="isOwner"
      :compact="compact"
      @toggle-scope="handleToggleScope"
      @edit-personality="handleEditPersonality"
      @edit-mode="handleEditMode"
      @close="handleClose"
      @fork="handleFork"
      @start-edit-title="startEditTitle"
      @export-chat="exportChat"
      @toggle-find="toggleFind"
    />
```

- [ ] **Step 7: Pass `compact` from `ChatDock.vue`**

In `src/web-ui/app/components/chat/ChatDock.vue`, add `:compact="true"` to the `<ChatSessionView>` element (lines 204-216):

```html
          <ChatSessionView
            v-if="dock.mode === 'session' && dock.activeSessionId"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
            :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
            :initial-message="dock.pendingMessage?.content ?? null"
            :auto-send-initial="!!dock.pendingMessage"
            :initial-preset-id="dock.pendingMessage?.presetId ?? null"
            :initial-model-id="dock.pendingMessage?.modelId ?? null"
            :initial-effort="dock.pendingMessage?.reasoningEffort ?? null"
            compact
            @session-refreshed="onSessionRefreshed"
            @initial-message-sent="dock.clearPendingMessage()"
          />
```

- [ ] **Step 8: Run the full chat component test suite to check for regressions**

Run: `cd src/web-ui && pnpm test ChatSessionHeader ChatSessionView ChatDock`
Expected: all PASS. `ChatDock.test.ts` doesn't need new assertions for this task (compact-mode rendering is `ChatSessionHeader`'s own tested responsibility) — just confirm nothing broke.

- [ ] **Step 9: Typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
git add app/components/chat/ChatSessionHeader.vue app/components/chat/ChatSessionView.vue \
  app/components/chat/ChatDock.vue app/components/chat/__tests__/ChatSessionHeader.test.ts
git commit -m "feat(chat): collapse ChatSessionHeader into a kebab menu in compact (dock) mode"
```

---

### Task 5: Personality management modal

**Files:**
- Create: `src/web-ui/app/components/chat/PersonalityManageModal.vue`
- Create: `src/web-ui/app/components/chat/__tests__/PersonalityManageModal.test.ts`
- Modify: `src/web-ui/app/components/chat/ChatSessionHeader.vue` (add the `showManageModal` ref, the modal mount, the "Manage personalities…" kebab-menu entry, and the non-compact trigger icon — everything Task 4 deliberately left out)

**Interfaces:**
- Produces: `PersonalityManageModal` component, props `{ open: boolean }` (`v-model:open`), emits `{ 'update:open': [boolean], changed: [] }`. `changed` fires after any successful create/update/archive/set-default, for the parent to refetch its own personality list.
- Consumes: `ApiRoutes.Chat.personalities.{list,create,update,archive,setDefault}` (all already exist, `routes.ts:249-256`), `AgentPersonalityDto` type (`~/types/chat`), `AppModal.vue` (`~/components/shared/AppModal.vue` — props `open`/`title`/`width`, emits `update:open`/`close`, slots `#body`/`#header-trailing`).

- [ ] **Step 1: Write the failing test file**

Create `src/web-ui/app/components/chat/__tests__/PersonalityManageModal.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import PersonalityManageModal from '~/components/chat/PersonalityManageModal.vue'

const mockGET = vi.fn()
const mockPOST = vi.fn()
const mockPATCH = vi.fn()
const mockDELETE = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: mockPOST,
  PATCH: mockPATCH,
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const personalities = [
  { id: 'p1', name: 'well', description: 'Sage guide', systemPrompt: 'You are the well.', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
  { id: 'p2', name: 'Carter', description: 'Investigative reviewer', systemPrompt: 'You are Carter.', isDefault: true, createdAt: '', updatedAt: '', archivedAt: null }
]

describe('PersonalityManageModal', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockPATCH.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({ data: personalities, error: undefined })
  })

  it('lists existing personalities with the default badge', async () => {
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('well')
    expect(wrapper.text()).toContain('Carter')
    expect(wrapper.text()).toContain('Default')
  })

  it('creates a new personality and emits changed', async () => {
    mockPOST.mockResolvedValue({
      data: { id: 'p3', name: 'New Bot', description: null, systemPrompt: 'p', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
      error: undefined
    })
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true }
    })
    await flushPromises()
    await wrapper.find('[data-testid="new-personality"]').trigger('click')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('New Bot')
    await wrapper.find('[data-testid="personality-prompt-input"]').setValue('p')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalled()
    expect(wrapper.emitted('changed')).toBeTruthy()
  })

  it('shows an error toast when create fails', async () => {
    mockPOST.mockRejectedValue(new Error('Name is required'))
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true }
    })
    await flushPromises()
    await wrapper.find('[data-testid="new-personality"]').trigger('click')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('X')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })

  it('archives a personality and emits changed', async () => {
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true }
    })
    await flushPromises()
    await wrapper.find('[data-testid="archive-p1"]').trigger('click')
    await flushPromises()
    expect(mockDELETE).toHaveBeenCalled()
    expect(wrapper.emitted('changed')).toBeTruthy()
  })

  it('sets a personality as default and emits changed', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true }
    })
    await flushPromises()
    await wrapper.find('[data-testid="set-default-p1"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalled()
    expect(wrapper.emitted('changed')).toBeTruthy()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test PersonalityManageModal`
Expected: FAIL — component doesn't exist.

- [ ] **Step 3: Write `PersonalityManageModal.vue`**

```html
<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'
import type { AgentPersonalityDto } from '~/types/chat'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  changed: []
}>()

const api = useApi()
const toast = useAppToast()

const personalities = ref<AgentPersonalityDto[]>([])
const loading = ref(false)

// null = list view, 'new' = create form, otherwise the id being edited
const formMode = ref<'new' | string | null>(null)
const formName = ref('')
const formDescription = ref('')
const formSystemPrompt = ref('')
const saving = ref(false)

async function fetchList() {
  loading.value = true
  try {
    const { data } = await api.GET<AgentPersonalityDto[]>(ApiRoutes.Chat.personalities.list())
    personalities.value = data ?? []
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to load personalities')
  } finally {
    loading.value = false
  }
}

watch(() => props.open, (isOpen) => {
  if (isOpen) void fetchList()
  else formMode.value = null
})

function startCreate() {
  formMode.value = 'new'
  formName.value = ''
  formDescription.value = ''
  formSystemPrompt.value = ''
}

function startEdit(p: AgentPersonalityDto) {
  formMode.value = p.id
  formName.value = p.name
  formDescription.value = p.description ?? ''
  formSystemPrompt.value = p.systemPrompt
}

function cancelForm() {
  formMode.value = null
}

async function saveForm() {
  if (!formName.value.trim()) {
    toast.error('Name is required')
    return
  }
  saving.value = true
  try {
    if (formMode.value === 'new') {
      await api.POST(ApiRoutes.Chat.personalities.create(), {
        body: {
          name: formName.value.trim(),
          description: formDescription.value.trim() || null,
          systemPrompt: formSystemPrompt.value,
          isDefault: false
        }
      })
      toast.success('Personality created')
    } else if (formMode.value) {
      await api.PATCH(ApiRoutes.Chat.personalities.update(formMode.value), {
        body: {
          name: formName.value.trim(),
          description: formDescription.value.trim() || null,
          systemPrompt: formSystemPrompt.value
        }
      })
      toast.success('Personality updated')
    }
    formMode.value = null
    await fetchList()
    emit('changed')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to save personality')
  } finally {
    saving.value = false
  }
}

async function archive(p: AgentPersonalityDto) {
  try {
    await api.DELETE(ApiRoutes.Chat.personalities.archive(p.id))
    toast.success(`"${p.name}" archived`)
    await fetchList()
    emit('changed')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to archive personality')
  }
}

async function setDefault(p: AgentPersonalityDto) {
  try {
    await api.POST(ApiRoutes.Chat.personalities.setDefault(p.id))
    toast.success(`"${p.name}" set as default`)
    await fetchList()
    emit('changed')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to set default personality')
  }
}
</script>

<template>
  <AppModal
    :open="open"
    title="Manage personalities"
    width="sm:max-w-2xl"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <div
        v-if="loading"
        class="flex items-center justify-center py-8"
      >
        <UIcon
          name="i-lucide-loader-circle"
          class="animate-spin size-6 text-muted"
        />
      </div>

      <template v-else-if="formMode === null">
        <div class="flex justify-end mb-3">
          <UButton
            data-testid="new-personality"
            icon="i-lucide-plus"
            size="xs"
            @click="startCreate"
          >
            New
          </UButton>
        </div>
        <ul class="space-y-2">
          <li
            v-for="p in personalities"
            :key="p.id"
            class="flex items-center gap-2 p-2 rounded border border-gray-200 dark:border-gray-700"
          >
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <span class="font-medium text-sm truncate">{{ p.name }}</span>
                <UBadge
                  v-if="p.isDefault"
                  size="xs"
                  color="primary"
                >
                  Default
                </UBadge>
              </div>
              <p
                v-if="p.description"
                class="text-xs text-muted truncate"
              >
                {{ p.description }}
              </p>
            </div>
            <UButton
              v-if="!p.isDefault"
              :data-testid="`set-default-${p.id}`"
              icon="i-lucide-star"
              variant="ghost"
              size="xs"
              title="Set as default"
              @click="setDefault(p)"
            />
            <UButton
              icon="i-lucide-pencil"
              variant="ghost"
              size="xs"
              title="Edit"
              @click="startEdit(p)"
            />
            <UButton
              :data-testid="`archive-${p.id}`"
              icon="i-lucide-archive"
              variant="ghost"
              color="error"
              size="xs"
              title="Archive"
              @click="archive(p)"
            />
          </li>
        </ul>
      </template>

      <template v-else>
        <div class="space-y-3">
          <UInput
            v-model="formName"
            data-testid="personality-name-input"
            placeholder="Name"
          />
          <UInput
            v-model="formDescription"
            data-testid="personality-description-input"
            placeholder="Description (shown in the picker)"
          />
          <UTextarea
            v-model="formSystemPrompt"
            data-testid="personality-prompt-input"
            placeholder="System prompt"
            :rows="6"
          />
          <div class="flex justify-end gap-2">
            <UButton
              variant="ghost"
              color="neutral"
              :disabled="saving"
              @click="cancelForm"
            >
              Cancel
            </UButton>
            <UButton
              data-testid="save-personality"
              :loading="saving"
              @click="saveForm"
            >
              Save
            </UButton>
          </div>
        </div>
      </template>
    </template>
  </AppModal>
</template>
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test PersonalityManageModal`
Expected: all PASS.

- [ ] **Step 5: Wire every trigger point into `ChatSessionHeader.vue`**

Import the new component and add a `showManageModal` ref, right after the existing `personalities`/`loadingPersonalities` refs (after line 34):

```ts
import PersonalityManageModal from '~/components/chat/PersonalityManageModal.vue'
```

```ts
const showManageModal = ref(false)
```

In `compactMenuItems` (added in Task 4's Step 3), append a "Manage personalities…" entry to the personality group — it's the group with the `!props.session.projectId` condition:

```ts
  if (props.isOwner && isActive.value && !props.session.projectId) {
    groups.push([
      {
        label: props.session.searchAllMyDocs ? 'All docs (on)' : 'All docs (off)',
        icon: props.session.searchAllMyDocs ? 'i-lucide-check' : undefined,
        onSelect: () => emit('toggleScope', !props.session.searchAllMyDocs)
      },
      ...personalityItems.value.map(p => ({
        label: p.label,
        icon: selectedPersonalityId.value === p.value ? 'i-lucide-check' : undefined,
        onSelect: () => { selectedPersonalityId.value = p.value }
      })),
      { label: 'Manage personalities…', icon: 'i-lucide-users', onSelect: () => { showManageModal.value = true } }
    ])
  }
```

Add a "Manage personalities" icon button in the template, right after the personality `<USelect>` (the one gated `v-if="!compact && isOwner && isActive && !session.projectId"`):

```html
    <UButton
      v-if="!compact && isOwner && isActive && !session.projectId"
      icon="i-lucide-users"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Manage personalities"
      @click="showManageModal = true"
    />
```

Add the modal mount at the end of the root `<div>`, after the close button:

```html
    <PersonalityManageModal
      v-model:open="showManageModal"
      @changed="fetchPersonalities"
    />
```

- [ ] **Step 6: Run the full header test suite**

Run: `cd src/web-ui && pnpm test ChatSessionHeader PersonalityManageModal`
Expected: all PASS.

- [ ] **Step 7: Typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
git add app/components/chat/PersonalityManageModal.vue app/components/chat/__tests__/PersonalityManageModal.test.ts \
  app/components/chat/ChatSessionHeader.vue
git commit -m "feat(chat): add personality management modal, reachable from any chat view"
```

---

### Task 6: Manual validation matrix

**Files:**
- Create: `docs/manual-validation/2026-08-06-web-chat-toolbar-personalities-matrix.md`

**Interfaces:** None — documentation only.

- [ ] **Step 1: Write the matrix**

Create `docs/manual-validation/2026-08-06-web-chat-toolbar-personalities-matrix.md`:

```markdown
# Web Chat Toolbar + Personality Management: Manual Validation

**Date:** 2026-08-06
**Feature:** ChatDock kebab menu, attached-docs collapse, search highlight, personality management modal, seeded personalities

## Environment

- API server running (`ASPNETCORE_ENVIRONMENT=Development`)
- Web UI dev server (`pnpm dev`) running
- Login as `testadmin` / `TestAdmin123!` (or any seeded user)

## Test Cases

### TC-1: Seeded personalities appear on first fetch

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open any personal (non-project) chat for a user who has never opened the Personality dropdown before | — |
| 2 | Open the Personality dropdown (full-page header) | All 6 seeded personalities appear: well, Carter, Commander Erwin, Captain Levi, Fire_Keeper, Tarnished |
| 3 | Reopen the dropdown / refresh the page | Same 6 personalities — no duplicates, no re-seeding |

**Pass/Fail:** _____

### TC-2: ChatDock kebab menu at narrow width

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open the floating chat dock (bottom-right bubble) | Dock opens at 380px wide |
| 2 | Observe the session title | Fully readable, not squeezed by icon buttons |
| 3 | Click the kebab (⋮) button | Menu opens with Rename, Export, Find, All docs, Personality list, Manage personalities… |
| 4 | Click "Manage personalities…" | Modal opens over the dock |

**Pass/Fail:** _____

### TC-3: Full-page chat keeps inline controls

| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to a full-page chat session (`/chats/[id]`) | — |
| 2 | Observe the header | Pencil, download, search, All docs checkbox, Personality select all visible inline — no kebab menu |

**Pass/Fail:** _____

### TC-4: Personality management modal CRUD

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open "Manage personalities" from either full-page header or dock kebab menu | Modal lists existing personalities |
| 2 | Click "New", fill name + system prompt, Save | New personality appears in the list; Personality select in the header now includes it |
| 3 | Click the pencil on a personality, change its description, Save | Change persists, reflected in the list |
| 4 | Click "Set as default" on a non-default personality | Default badge moves to that personality |
| 5 | Click "Archive" on a personality | It disappears from the list and from the header's Personality select |

**Pass/Fail:** _____

### TC-5: Attached docs collapsed by default

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open an owned, active chat session | "Attached docs (N)" summary row shown, list not expanded |
| 2 | Click the summary row | Expands to show the doc list / empty state + "+" attach button |

**Pass/Fail:** _____

### TC-6: Search highlights matched text inline

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open a chat with several messages, click the search icon | Find bar opens |
| 2 | Type a word that appears in a message | The matching message bubble gets a ring outline (existing behavior) AND the matched word itself is highlighted with a `<mark>` background inside the bubble text |

**Pass/Fail:** _____

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Tester | | | |
```

- [ ] **Step 2: Commit**

```bash
git add docs/manual-validation/2026-08-06-web-chat-toolbar-personalities-matrix.md
git commit -m "docs: add manual validation matrix for web chat toolbar + personalities"
```

---

## Acceptance

```bash
dotnet test
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test run && pnpm build
```

All must pass before this work is considered done. Manual validation matrix (Task 6) should be run through and signed off separately, same as prior chat plans.
