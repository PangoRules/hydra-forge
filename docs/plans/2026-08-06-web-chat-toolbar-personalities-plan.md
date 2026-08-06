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

### Task 7: Domain — replace dead `Open()` with `Reopen()` + add `Unarchive()`

**Files:**
- Modify: `src/HydraForge.Domain/Entities/Chat/ChatSession.cs`
- Modify: `tests/HydraForge.Domain.Tests/Chat/ChatSessionTests.cs`

**Interfaces:**
- Produces: `ChatSession.Reopen()` — no params. Sets `Status = Active`, `ClosedAt = null`, `ArchivedAt = null`, `AiEditMode = AiEditMode.PerMutation`, bumps `UpdatedAt`. Leaves `Summary`/`PersonalityId`/`OpenCardId` untouched (unlike the old `Open`, which wiped them). Idempotent by construction — safe to call on any session regardless of current `Status`/`ArchivedAt` combination (Active-only, Closed-only, Archived-only, or Closed+Archived all converge to the same end state in one call, matching the "one-step revive" decision).
- Produces: `ChatSession.Unarchive()` — sets `ArchivedAt = null`, bumps `UpdatedAt`. (Kept as a separate primitive from `Reopen` even though `Reopen` also clears `ArchivedAt` — `Unarchive` alone is useful for an Active-but-Archived session that doesn't need its `Status`/`AiEditMode` touched. `ChatSessionService.ReopenAsync` in Task 8 uses `Reopen()`, not this, but `Unarchive()` is exercised directly by its own domain test for completeness.)
- Removes: `ChatSession.Open(Guid?, Guid?, AiEditMode)` — confirmed dead (only referenced by its own now-replaced unit tests, never called from `ChatSessionService` or anywhere else). Per repo convention, no unused method is kept "just in case."

- [ ] **Step 1: Replace the `Open` tests with `Reopen`/`Unarchive` tests**

In `tests/HydraForge.Domain.Tests/Chat/ChatSessionTests.cs`, delete the three `Open_*` tests (`Open_SetsStatusToActive`, `Open_SetsPersonalityIdAndOpenCardIdAndAiEditMode`, `Open_ClearsClosedAtAndSummary`) and replace them with:

```csharp
    [Fact]
    public void Reopen_SetsStatusToActive()
    {
        var session = new ChatSession();
        session.Close("summary");

        session.Reopen();

        Assert.Equal(ChatSessionStatus.Active, session.Status);
    }

    [Fact]
    public void Reopen_ClearsClosedAt()
    {
        var session = new ChatSession();
        session.Close("summary");

        session.Reopen();

        Assert.Null(session.ClosedAt);
    }

    [Fact]
    public void Reopen_ClearsArchivedAt()
    {
        var session = new ChatSession();
        session.Close("summary");
        session.Archive();

        session.Reopen();

        Assert.Null(session.ArchivedAt);
    }

    [Fact]
    public void Reopen_ResetsAiEditModeToPerMutation()
    {
        var session = new ChatSession();
        session.SetAiEditMode(AiEditMode.Blanket);
        session.Close(null);

        session.Reopen();

        Assert.Equal(AiEditMode.PerMutation, session.AiEditMode);
    }

    [Fact]
    public void Reopen_OnActiveArchivedSession_ClearsArchivedAtWithoutRequiringClose()
    {
        var session = new ChatSession();
        session.Archive();

        session.Reopen();

        Assert.Equal(ChatSessionStatus.Active, session.Status);
        Assert.Null(session.ArchivedAt);
    }

    [Fact]
    public void Reopen_LeavesSummaryUntouched()
    {
        var session = new ChatSession();
        session.Close("keep this");

        session.Reopen();

        Assert.Equal("keep this", session.Summary);
    }

    [Fact]
    public void Unarchive_ClearsArchivedAt()
    {
        var session = new ChatSession();
        session.Archive();

        session.Unarchive();

        Assert.Null(session.ArchivedAt);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ChatSessionTests"`
Expected: FAIL to compile — `Reopen`/`Unarchive` don't exist yet, `Open` still does (compile error on the deleted-test side is expected; this is the correct red state for a method rename).

- [ ] **Step 3: Replace `Open` with `Reopen` + add `Unarchive` in `ChatSession.cs`**

In `src/HydraForge.Domain/Entities/Chat/ChatSession.cs`, replace the `Open` method:

```csharp
    public void Reopen()
    {
        Status = ChatSessionStatus.Active;
        ClosedAt = null;
        ArchivedAt = null;
        AiEditMode = AiEditMode.PerMutation;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unarchive()
    {
        ArchivedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ChatSessionTests"`
Expected: all PASS.

- [ ] **Step 5: Format and commit**

```bash
dotnet csharpier format src/HydraForge.Domain/Entities/Chat/ChatSession.cs
git add src/HydraForge.Domain/Entities/Chat/ChatSession.cs tests/HydraForge.Domain.Tests/Chat/ChatSessionTests.cs
git commit -m "refactor(chat): replace dead ChatSession.Open with Reopen + add Unarchive"
```

---

### Task 8: Application — `ReopenAsync` + status filter for `ListAsync`

**Files:**
- Modify: `src/HydraForge.Application/Chat/ChatDtos.cs` (new `ChatSessionStatusFilter` enum)
- Modify: `src/HydraForge.Application/Chat/IChatSessionRepository.cs`
- Modify: `src/HydraForge.Application/Chat/ChatSessionService.cs`
- Test: `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs` (update `FakeChatSessionRepository`, append new `[Fact]`s)

**Interfaces:**
- Produces: `ChatSessionStatusFilter` enum — `ActiveAndClosed` (default, matches today's behavior), `Active`, `Closed`, `Archived`.
- Produces: `IChatSessionService.ReopenAsync(Guid sessionId, Guid actorId, CancellationToken ct = default) : Task<Result<ChatSessionDto>>`.
- Modifies: `IChatSessionRepository.ListAsync`/`CountAsync` — both gain a new **trailing** optional parameter `ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed`, placed *after* the existing `ct` parameter so every existing positional call site (tests included) keeps compiling unchanged.
- Modifies: `ChatSessionService.ListAsync` — same trailing optional param, forwarded to the repository.

- [ ] **Step 1: Add the `ChatSessionStatusFilter` enum**

In `src/HydraForge.Application/Chat/ChatDtos.cs`, add near the top (after the `using` statements, before `CreateChatSessionRequest`):

```csharp
public enum ChatSessionStatusFilter
{
    ActiveAndClosed,
    Active,
    Closed,
    Archived
}
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs` (inside the existing test class). First, update `FakeChatSessionRepository.ListAsync`/`CountAsync` to accept and honor the new trailing param (both methods currently end at `CancellationToken ct = default)`):

```csharp
        public Task<IReadOnlyList<ChatSession>> ListAsync(
            Guid ownerId,
            Guid? folderId,
            Guid? projectId,
            DateTime? before,
            Guid? beforeId,
            int limit,
            CancellationToken ct = default,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed
        )
        {
            var query = Sessions.Where(s => s.OwnerId == ownerId);
            query = statusFilter switch
            {
                ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
                ChatSessionStatusFilter.Active => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
                ),
                ChatSessionStatusFilter.Closed => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
                ),
                _ => query.Where(s => s.ArchivedAt == null)
            };
            if (folderId.HasValue)
                query = query.Where(s => s.FolderId == folderId.Value);
            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId.Value);
            if (before.HasValue)
                query = query.Where(s => s.CreatedAt < before.Value);

            return Task.FromResult<IReadOnlyList<ChatSession>>(
                query.OrderByDescending(s => s.UpdatedAt).Take(limit).ToList()
            );
        }

        public Task<int> CountAsync(
            Guid ownerId,
            Guid? folderId,
            Guid? projectId,
            CancellationToken ct = default,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed
        )
        {
            var query = Sessions.Where(s => s.OwnerId == ownerId);
            query = statusFilter switch
            {
                ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
                ChatSessionStatusFilter.Active => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
                ),
                ChatSessionStatusFilter.Closed => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
                ),
                _ => query.Where(s => s.ArchivedAt == null)
            };
            if (folderId.HasValue)
                query = query.Where(s => s.FolderId == folderId.Value);
            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId.Value);
            return Task.FromResult(query.Count());
        }
```

Then append the new test cases:

```csharp
    [Fact]
    public async Task ListAsync_StatusFilterArchived_ReturnsOnlyArchivedRegardlessOfStatus()
    {
        var sessionRepo = new FakeChatSessionRepository();
        var ownerId = NewId();
        var activeArchived = new ChatSession { Id = NewId(), OwnerId = ownerId, ArchivedAt = DateTime.UtcNow };
        var closedNotArchived = new ChatSession { Id = NewId(), OwnerId = ownerId, Status = ChatSessionStatus.Closed };
        sessionRepo.Sessions.Add(activeArchived);
        sessionRepo.Sessions.Add(closedNotArchived);
        var service = BuildService(sessionRepo: sessionRepo);

        var result = await service.ListAsync(
            ownerId, null, null, null, null, 20,
            statusFilter: ChatSessionStatusFilter.Archived
        );

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(activeArchived.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_StatusFilterClosed_ExcludesActiveAndArchived()
    {
        var sessionRepo = new FakeChatSessionRepository();
        var ownerId = NewId();
        var closed = new ChatSession { Id = NewId(), OwnerId = ownerId, Status = ChatSessionStatus.Closed };
        var active = new ChatSession { Id = NewId(), OwnerId = ownerId, Status = ChatSessionStatus.Active };
        var closedArchived = new ChatSession { Id = NewId(), OwnerId = ownerId, Status = ChatSessionStatus.Closed, ArchivedAt = DateTime.UtcNow };
        sessionRepo.Sessions.AddRange([closed, active, closedArchived]);
        var service = BuildService(sessionRepo: sessionRepo);

        var result = await service.ListAsync(
            ownerId, null, null, null, null, 20,
            statusFilter: ChatSessionStatusFilter.Closed
        );

        Assert.Single(result.Value.Items);
        Assert.Equal(closed.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task ReopenAsync_ClosedSession_SetsStatusActiveAndResetsAiEditMode()
    {
        var sessionRepo = new FakeChatSessionRepository();
        var ownerId = NewId();
        var session = new ChatSession { Id = NewId(), OwnerId = ownerId, Status = ChatSessionStatus.Closed, AiEditMode = AiEditMode.Blanket };
        sessionRepo.Sessions.Add(session);
        var service = BuildService(sessionRepo: sessionRepo);

        var result = await service.ReopenAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatSessionStatus.Active, result.Value.Status);
        Assert.Equal(AiEditMode.PerMutation, result.Value.AiEditMode);
    }

    [Fact]
    public async Task ReopenAsync_ArchivedAndClosedSession_ClearsArchivedAtInOneCall()
    {
        var sessionRepo = new FakeChatSessionRepository();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Closed,
            ArchivedAt = DateTime.UtcNow
        };
        sessionRepo.Sessions.Add(session);
        var service = BuildService(sessionRepo: sessionRepo);

        var result = await service.ReopenAsync(session.Id, ownerId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ArchivedAt);
        Assert.Equal(ChatSessionStatus.Active, result.Value.Status);
    }

    [Fact]
    public async Task ReopenAsync_NotOwner_ReturnsFailure()
    {
        var sessionRepo = new FakeChatSessionRepository();
        var session = new ChatSession { Id = NewId(), OwnerId = NewId(), Status = ChatSessionStatus.Closed };
        sessionRepo.Sessions.Add(session);
        var service = BuildService(sessionRepo: sessionRepo);

        var result = await service.ReopenAsync(session.Id, NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task ReopenAsync_SessionNotFound_ReturnsFailure()
    {
        var sessionRepo = new FakeChatSessionRepository();
        var service = BuildService(sessionRepo: sessionRepo);

        var result = await service.ReopenAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }
```

(Use whatever helper the file already has for constructing a `ChatSessionService` with a subset of fakes — e.g. if there's a `BuildService(...)` helper already in this file, match its exact name/signature; if not, construct `new ChatSessionService(sessionRepo, ...)` the same way the existing tests in the file do, reading a nearby test's arrange block for the exact constructor argument list before writing these.)

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ChatSessionServiceTests"`
Expected: FAIL to compile — `ReopenAsync`, `ChatSessionStatusFilter`, and the new `statusFilter:` named argument don't exist on `ListAsync` yet.

- [ ] **Step 4: Add the trailing `statusFilter` param to `IChatSessionRepository`**

In `src/HydraForge.Application/Chat/IChatSessionRepository.cs`, update both signatures:

```csharp
    Task<IReadOnlyList<ChatSession>> ListAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        CancellationToken ct = default,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed
    );

    Task<int> CountAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        CancellationToken ct = default,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed
    );
```

- [ ] **Step 5: Update `ChatSessionService.ListAsync` and add `ReopenAsync`**

In `src/HydraForge.Application/Chat/ChatSessionService.cs`, update `ListAsync`'s signature and body:

```csharp
    public async Task<Result<ChatSessionPageDto>> ListAsync(
        Guid actorId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        CancellationToken ct = default,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed
    )
    {
        var sessions = await _sessionRepo.ListAsync(
            actorId,
            folderId,
            projectId,
            before,
            beforeId,
            limit,
            ct,
            statusFilter
        );
        var dtos = new List<ChatSessionDto>();
        foreach (var session in sessions)
            dtos.Add(await MapToDtoAsync(session, ct));

        var totalCount = await _sessionRepo.CountAsync(actorId, folderId, projectId, ct, statusFilter);

        return Result<ChatSessionPageDto>.Success(new ChatSessionPageDto(dtos, totalCount));
    }
```

Add `ReopenAsync` right after `ArchiveAsync`:

```csharp
    public async Task<Result<ChatSessionDto>> ReopenAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (session.OwnerId != actorId)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the owner can reopen the session."
                )
            );

        // Idempotent: already active and not archived, nothing to do
        if (session.Status == ChatSessionStatus.Active && session.ArchivedAt == null)
            return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));

        session.Reopen();
        await _sessionRepo.UpdateAsync(session, ct);

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
    }
```

Add the matching signature to `IChatSessionService` (wherever `CloseAsync`/`ArchiveAsync` are declared):

```csharp
    Task<Result<ChatSessionDto>> ReopenAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    );
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ChatSessionServiceTests"`
Expected: all PASS, including every pre-existing `ListAsync_*` test (unchanged behavior when `statusFilter` is omitted — defaults to `ActiveAndClosed`, identical to today's hardcoded `ArchivedAt == null`).

- [ ] **Step 7: Format and commit**

```bash
dotnet csharpier format src/HydraForge.Application/Chat/ChatDtos.cs src/HydraForge.Application/Chat/IChatSessionRepository.cs src/HydraForge.Application/Chat/ChatSessionService.cs
git add src/HydraForge.Application/Chat/ChatDtos.cs src/HydraForge.Application/Chat/IChatSessionRepository.cs src/HydraForge.Application/Chat/ChatSessionService.cs tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs
git commit -m "feat(chat): add ReopenAsync and status filter for ListAsync"
```

---

### Task 9: Infrastructure + Server — repository filter, reopen endpoint, routes

**Files:**
- Modify: `src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs`
- Modify: `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs`
- Modify: `src/web-ui/app/lib/routes.ts`
- Test: `tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs` (append)

**Interfaces:**
- Produces: `POST /api/chat/sessions/{sessionId}/reopen` → `200 ChatSessionDto` (or `404`).
- Modifies: `GET /api/chat/sessions` gains `?status=Active|Closed|Archived` (omitted = today's behavior, both Active+Closed, excludes Archived).
- Produces: `ApiRoutes.Chat.sessions.reopen(sessionId)` and an updated `ApiRoutes.Chat.sessions.list(...)` accepting an optional `status` param.

- [ ] **Step 1: Write the failing repository test**

Append to `tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs`:

```csharp
    [Fact]
    public async Task ListAsync_StatusFilterArchived_ReturnsArchivedRegardlessOfStatus()
    {
        await using var context = CreateContext();
        var repo = new EfChatSessionRepository(context);
        var userId = Guid.NewGuid();

        var archivedActive = new ChatSession { Id = Guid.NewGuid(), OwnerId = userId, ArchivedAt = DateTime.UtcNow };
        var closedNotArchived = new ChatSession { Id = Guid.NewGuid(), OwnerId = userId, Status = ChatSessionStatus.Closed };
        context.ChatSessions.AddRange(archivedActive, closedNotArchived);
        await context.SaveChangesAsync();

        var results = await repo.ListAsync(
            userId, null, null, null, null, 20,
            CancellationToken.None,
            ChatSessionStatusFilter.Archived
        );

        Assert.Single(results);
        Assert.Equal(archivedActive.Id, results[0].Id);
    }
```

(Match this file's existing `CreateContext()` helper — read the top of the file first if the exact helper name differs.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~EfChatSessionRepositoryTests"`
Expected: FAIL to compile — `EfChatSessionRepository.ListAsync` doesn't accept a `ChatSessionStatusFilter` yet.

- [ ] **Step 3: Implement the filter in `EfChatSessionRepository`**

In `src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs`, update `ListAsync` and `CountAsync`:

```csharp
    public async Task<IReadOnlyList<ChatSession>> ListAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        CancellationToken ct = default,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed
    )
    {
        var query = ApplyStatusFilter(context.ChatSessions.Where(s => s.OwnerId == ownerId), statusFilter);

        if (folderId.HasValue)
            query = query.Where(s => s.FolderId == folderId.Value);
        if (projectId.HasValue)
            query = query.Where(s => s.ProjectId == projectId.Value);
        if (before.HasValue)
            query = query.Where(s =>
                s.UpdatedAt < before.Value || (s.UpdatedAt == before.Value && s.Id < beforeId)
            );

        return await query
            .OrderByDescending(s => s.UpdatedAt)
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Guid ownerId,
        Guid? folderId,
        Guid? projectId,
        CancellationToken ct = default,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.ActiveAndClosed
    )
    {
        var query = ApplyStatusFilter(context.ChatSessions.Where(s => s.OwnerId == ownerId), statusFilter);

        if (folderId.HasValue)
            query = query.Where(s => s.FolderId == folderId.Value);
        if (projectId.HasValue)
            query = query.Where(s => s.ProjectId == projectId.Value);

        return await query.CountAsync(ct);
    }

    private static IQueryable<ChatSession> ApplyStatusFilter(
        IQueryable<ChatSession> query,
        ChatSessionStatusFilter statusFilter
    ) =>
        statusFilter switch
        {
            ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
            ChatSessionStatusFilter.Active => query.Where(s =>
                s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
            ),
            ChatSessionStatusFilter.Closed => query.Where(s =>
                s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
            ),
            _ => query.Where(s => s.ArchivedAt == null)
        };
```

(Leave the rest of `CountAsync`'s current body — the existing `if (folderId.HasValue)` block etc. — this replaces the whole method, not a partial patch; check the file's current `CountAsync` end brace before replacing so nothing after it is accidentally deleted.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~EfChatSessionRepositoryTests"`
Expected: all PASS, including every pre-existing test (default `statusFilter` value preserves current behavior exactly).

- [ ] **Step 5: Add the reopen endpoint and status query param to the controller**

In `src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs`, update `List`:

```csharp
    [HttpGet]
    [ProducesResponseType(typeof(ChatSessionPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? folderId,
        [FromQuery] Guid? projectId,
        [FromQuery] DateTime? before = null,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int limit = 20,
        [FromQuery] ChatSessionStatusFilter status = ChatSessionStatusFilter.ActiveAndClosed
    )
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.ListAsync(
            userId,
            folderId,
            projectId,
            before,
            beforeId,
            limit,
            statusFilter: status
        );

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }
```

Add the reopen action right after `Close`:

```csharp
    [HttpPost("{sessionId:guid}/reopen")]
    [ProducesResponseType(typeof(ChatSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reopen(Guid sessionId)
    {
        var userId = User.GetRequiredUserId();
        var result = await sessionService.ReopenAsync(sessionId, userId);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return Ok(result.Value);
    }
```

- [ ] **Step 6: Add the routes**

In `src/web-ui/app/lib/routes.ts`, update `Chat.sessions.list` and add `reopen`:

```ts
      list: (folderId?: string, projectId?: string, before?: string, beforeId?: string, limit = 20, status?: string) =>
        `/api/chat/sessions?${folderId ? `folderId=${folderId}&` : ''}${projectId ? `projectId=${projectId}&` : ''}${before ? `before=${before}&` : ''}${beforeId ? `beforeId=${beforeId}&` : ''}${status ? `status=${status}&` : ''}limit=${limit}`,
      create: () => '/api/chat/sessions',
      detail: (sessionId: string) => `/api/chat/sessions/${sessionId}`,
      update: (sessionId: string) => `/api/chat/sessions/${sessionId}`,
      close: (sessionId: string) => `/api/chat/sessions/${sessionId}/close`,
      reopen: (sessionId: string) => `/api/chat/sessions/${sessionId}/reopen`,
      archive: (sessionId: string) => `/api/chat/sessions/${sessionId}`,
```

- [ ] **Step 7: Full backend test run, format, commit**

```bash
dotnet test
dotnet csharpier format src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs
git add src/HydraForge.Infrastructure/Chat/EfChatSessionRepository.cs src/HydraForge.Server/Controllers/Chat/ChatSessionsController.cs \
  src/web-ui/app/lib/routes.ts tests/HydraForge.Infrastructure.Tests/Chat/EfChatSessionRepositoryTests.cs
git commit -m "feat(chat): reopen endpoint + status filter query param on session list"
```

---

### Task 10: `/chats` list page — status filter + Reopen row action

**Files:**
- Modify: `src/web-ui/app/composables/useChatSessionList.ts`
- Modify: `src/web-ui/app/pages/chats/index.vue`
- Test: whatever test file already covers `useChatSessionList` / the chats index page — append; if neither has one today, skip a new test file here (matches the rest of this codebase's page-level testing gap) and rely on the manual validation matrix (Task 13) for this task's coverage.

**Interfaces:**
- Modifies: `useChatSessionList(options)` gains an optional `statusFilter: Ref<string>` passthrough to `ApiRoutes.Chat.sessions.list`.

- [ ] **Step 1: Thread a status filter into `useChatSessionList`**

In `src/web-ui/app/composables/useChatSessionList.ts`, find where `ApiRoutes.Chat.sessions.list(...)` is called (inside `loadMore`) and add a `statusFilter` ref + param:

```ts
export function useChatSessionList(options?: { folderId?: string, projectId?: string }) {
  const statusFilter = ref<'ActiveAndClosed' | 'Active' | 'Closed' | 'Archived'>('ActiveAndClosed')
  // ...existing refs (sessions, loading, hasMore, cursor state)...

  async function loadMore() {
    // ...existing guard/loading logic...
    const url = ApiRoutes.Chat.sessions.list(
      options?.folderId,
      options?.projectId,
      /* before */ undefined,
      /* beforeId */ undefined,
      20,
      statusFilter.value
    )
    // ...existing fetch + append logic...
  }

  // ...
  return { sessions, loading, hasMore, loadMore, patchSession, prependSession, removeSession, statusFilter }
}
```

(Read the actual current file before editing — this shows the shape of the change, not a full replacement; keep every existing ref/function name and the cursor-tracking logic exactly as it is today, just add `statusFilter` and pass it through. When `statusFilter` changes, the page (Step 2) is responsible for clearing `sessions` and re-calling `loadMore()` from page 1 — `useChatSessionList` itself doesn't need to know about that reset, same as changing `folderId`/`projectId` today doesn't self-reset since those are constructor options, not reactive.)

- [ ] **Step 2: Add the filter dropdown + Reopen action to `/chats/index.vue`**

In `src/web-ui/app/pages/chats/index.vue`, destructure `statusFilter` from `useChatSessionList()` and add a refresh-on-change watcher:

```ts
const { sessions, loading, hasMore, loadMore, patchSession, prependSession, removeSession, statusFilter } = useChatSessionList()

const statusFilterItems = [
  { label: 'Active + Closed', value: 'ActiveAndClosed' },
  { label: 'Active only', value: 'Active' },
  { label: 'Closed only', value: 'Closed' },
  { label: 'Archived', value: 'Archived' }
]

watch(statusFilter, async () => {
  sessions.value = []
  await loadMore()
})
```

Add the `USelect` above the session list (right after the "Chats" heading row, before `<ChatSessionList>`):

```html
        <div class="shrink-0 px-4 pb-2">
          <USelect
            v-model="statusFilter"
            :items="statusFilterItems"
            size="xs"
            class="w-full"
          />
        </div>
```

Add a `reopenTargetId`/`reopenSession` handler and a "Reopen" row button, shown whenever the row isn't both active+unarchived:

```ts
async function reopenSession(id: string) {
  try {
    const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.reopen(id))
    if (data) patchSession(id, { status: data.status, archivedAt: data.archivedAt })
    toast.success('Chat reopened')
  } catch {
    toast.error('Failed to reopen chat')
  }
}
```

In the `#item` slot template, add a "Reopen" button alongside the existing archive button (shown when the session isn't in its fully-live state):

```html
              <UButton
                v-if="session.status !== 'Active' || session.archivedAt"
                icon="i-lucide-lock-open"
                variant="ghost"
                color="neutral"
                size="xs"
                title="Reopen chat"
                class="absolute right-8 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity"
                @click.stop="reopenSession(session.id)"
              />
```

(This sits to the left of the existing archive button — adjust the archive button's `right-1` if the two overlap; give the archive button `right-1` and this one `right-8` as shown, 28px apart, matching the existing `xs`-sized button footprint.)

- [ ] **Step 3: Manual check**

Run `pnpm dev`, open `/chats`, close a chat, verify it still shows under "Active + Closed" with a "Reopen" hover action; switch the filter to "Archived", archive a chat, verify it now only shows there and "Reopen" clears it back to "Active + Closed" view. No automated test for this step — covered by Task 13's manual validation matrix.

- [ ] **Step 4: Typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
git add app/composables/useChatSessionList.ts app/pages/chats/index.vue
git commit -m "feat(chat): status filter + reopen action on the chats list page"
```

---

### Task 11: Dismiss vs Close/Archive split in `ChatSessionHeader`

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionHeader.vue`
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue`
- Test: `src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts` (append)

**Interfaces:**
- `ChatSessionHeader`'s `close` emit is renamed to `dismiss` — fires with no API call, meaning "stop showing this chat" (parent decides what that means: deselect on full page, hide the dock).
- New emits: `closeSession` (fires the real `POST .../close`, gated behind a `ConfirmDialog`) and `archiveSession` (fires `DELETE`, gated behind a `ConfirmDialog`) and `reopenSession` (fires `POST .../reopen`, no confirm needed — non-destructive).
- Every action in `compactMenuItems` gets an `icon` (audit: the "All docs (on/off)" toggle-icon-only-when-checked pattern is replaced with a consistent icon so unchecked items aren't icon-less — see Step 3).
- The flat personality list in the kebab is replaced by a single "Select Personality" entry with `children` (Nuxt UI v4 `DropdownMenuItem.children` — nested flyout), kept separate from "Manage personalities…".

- [ ] **Step 1: Write the failing tests**

Append to `src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts`:

```ts
describe('ChatSessionHeader — dismiss vs close/archive', () => {
  it('emits dismiss (not close) when the X button is clicked', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true }
    })
    await flushPromises()
    await wrapper.find('[title="Dismiss"]').trigger('click')
    expect(wrapper.emitted('dismiss')).toBeTruthy()
    expect(wrapper.emitted('close')).toBeFalsy()
  })

  it('shows a confirm dialog before emitting closeSession from the kebab', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    const closeItem = wrapper.vm.compactMenuItems.flat().find((i: { label: string }) => i.label === 'Close chat')
    closeItem.onSelect()
    await flushPromises()
    expect(wrapper.find('[data-testid="close-session-confirm"]').exists()).toBe(true)
  })

  it('every compact menu item has an icon', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    const items = wrapper.vm.compactMenuItems.flat()
    expect(items.length).toBeGreaterThan(0)
    expect(items.every((i: { icon?: string }) => !!i.icon)).toBe(true)
  })

  it('Select Personality is a single item with children, separate from Manage personalities', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    const items = wrapper.vm.compactMenuItems.flat()
    const selectPersonality = items.find((i: { label: string }) => i.label === 'Select Personality')
    const manage = items.find((i: { label: string }) => i.label === 'Manage personalities…')
    expect(selectPersonality).toBeTruthy()
    expect(Array.isArray(selectPersonality.children)).toBe(true)
    expect(manage).toBeTruthy()
    expect(manage).not.toBe(selectPersonality)
  })
})
```

(`wrapper.vm.compactMenuItems` requires the component under test to not fully encapsulate its script setup bindings — check whether the existing test file already reaches internal refs this way for a prior test; if `<script setup>`'s default closed bindings block this, use `defineExpose({ compactMenuItems })` at the bottom of `ChatSessionHeader.vue`'s script instead, and adjust these tests to `wrapper.vm.compactMenuItems` accordingly — either approach is fine, just be consistent with whatever the file already does elsewhere.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test ChatSessionHeader`
Expected: FAIL — `dismiss` emit doesn't exist yet (X still emits `close` and calls nothing itself, since the API call actually lives in `ChatSessionView.handleClose` today), no confirm dialog, missing icons, no "Select Personality" grouping.

- [ ] **Step 3: Rework `ChatSessionHeader.vue`**

Update the emits:

```ts
const emit = defineEmits<{
  dismiss: []
  closeSession: []
  archiveSession: []
  reopenSession: []
  toggleScope: [searchAllMyDocs: boolean]
  editPersonality: [personalityId: string | null]
  editMode: [mode: AiEditMode]
  fork: []
  startEditTitle: []
  exportChat: []
  toggleFind: []
}>()
```

Add confirm-dialog state and import `ConfirmDialog`:

```ts
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
```

```ts
const showCloseConfirm = ref(false)
const showArchiveConfirm = ref(false)

function confirmClose() {
  emit('closeSession')
  showCloseConfirm.value = false
}

function confirmArchive() {
  emit('archiveSession')
  showArchiveConfirm.value = false
}
```

Rewrite `compactMenuItems` — every item gets an icon, "Select Personality" becomes a single `children`-bearing item, and Close/Archive/Reopen join the menu:

```ts
const compactMenuItems = computed(() => {
  const groups: Array<Array<{ label: string, icon: string, disabled?: boolean, onSelect?: () => void, children?: unknown[] }>> = []

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
        icon: props.session.searchAllMyDocs ? 'i-lucide-check-square' : 'i-lucide-square',
        onSelect: () => emit('toggleScope', !props.session.searchAllMyDocs)
      },
      {
        label: 'Select Personality',
        icon: 'i-lucide-user-round',
        children: personalityItems.value.map(p => ({
          label: p.label,
          icon: selectedPersonalityId.value === p.value ? 'i-lucide-check' : 'i-lucide-user-round',
          onSelect: () => { selectedPersonalityId.value = p.value }
        }))
      },
      { label: 'Manage personalities…', icon: 'i-lucide-users', onSelect: () => { showManageModal.value = true } }
    ])
  }

  if (props.isOwner && isActive.value && props.session.projectId) {
    groups.push(
      aiEditModeOptions.map(opt => ({
        label: opt.label,
        icon: props.session.aiEditMode === opt.value ? 'i-lucide-check' : 'i-lucide-shield',
        onSelect: () => emit('editMode', opt.value)
      }))
    )
  }

  if (props.session.isShared && props.session.projectId && !props.isOwner) {
    groups.push([
      { label: 'Fork (summarize → start my own)', icon: 'i-lucide-git-fork', onSelect: () => emit('fork') }
    ])
  }

  if (props.isOwner) {
    const lifecycle: Array<{ label: string, icon: string, onSelect: () => void }> = []
    if (isActive.value) {
      lifecycle.push({ label: 'Close chat', icon: 'i-lucide-lock', onSelect: () => { showCloseConfirm.value = true } })
    } else {
      lifecycle.push({ label: 'Reopen chat', icon: 'i-lucide-lock-open', onSelect: () => emit('reopenSession') })
    }
    lifecycle.push({ label: 'Archive chat', icon: 'i-lucide-archive', onSelect: () => { showArchiveConfirm.value = true } })
    groups.push(lifecycle)
  }

  return groups
})
```

Update the template: rename the X button's title/emit, add inline Close/Archive/Reopen buttons for non-compact mode (mirroring the same lifecycle logic so full-page keeps everything inline, consistent with every other action), and mount the two `ConfirmDialog`s:

```html
    <!-- Reopen — owner, non-compact, session not fully active -->
    <UButton
      v-if="!compact && isOwner && (!isActive || session.archivedAt)"
      icon="i-lucide-lock-open"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Reopen chat"
      @click="emit('reopenSession')"
    />

    <!-- Close — owner, non-compact, active -->
    <UButton
      v-if="!compact && isOwner && isActive"
      icon="i-lucide-lock"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Close chat"
      @click="showCloseConfirm = true"
    />

    <!-- Archive — owner, non-compact -->
    <UButton
      v-if="!compact && isOwner"
      icon="i-lucide-archive"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Archive chat"
      @click="showArchiveConfirm = true"
    />

    <!-- Kebab menu — compact mode only -->
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

    <!-- Dismiss — always visible, never calls the API -->
    <UButton
      icon="i-lucide-x"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Dismiss"
      @click="emit('dismiss')"
    />

    <PersonalityManageModal
      v-model:open="showManageModal"
      @changed="fetchPersonalities"
    />

    <ConfirmDialog
      :open="showCloseConfirm"
      title="Close chat"
      message="This ends the conversation: an AI summary is generated, AI-edit permission is revoked, and the chat becomes read-only. It stays visible in your chat list — this doesn't delete anything."
      confirm-text="Close chat"
      confirm-color="error"
      data-testid="close-session-confirm"
      @update:open="(v: boolean) => { if (!v) showCloseConfirm = false }"
      @confirm="confirmClose"
    />

    <ConfirmDialog
      :open="showArchiveConfirm"
      title="Archive chat"
      message="This chat will be archived and removed from your list. This can't be undone from here."
      confirm-text="Archive"
      confirm-color="error"
      data-testid="archive-session-confirm"
      @update:open="(v: boolean) => { if (!v) showArchiveConfirm = false }"
      @confirm="confirmArchive"
    />
```

(Note the old unconditional close button — `v-if="isOwner && isActive"`, `title="Close chat"`, `@click="emit('close')"` — is removed entirely, replaced by the pieces above.)

Add `defineExpose({ compactMenuItems })` at the end of the script block (needed for Step 1's tests to reach it).

- [ ] **Step 4: Wire the new emits through `ChatSessionView.vue`**

In `src/web-ui/app/components/chat/ChatSessionView.vue`, rename `handleClose` to `handleCloseSession` (same body, unchanged), and add:

```ts
function handleDismiss() {
  emit('dismiss')
}

async function handleReopen() {
  if (!session.value) return
  try {
    const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.reopen(props.sessionId))
    if (data) {
      session.value.status = data.status
      session.value.archivedAt = data.archivedAt
      session.value.aiEditMode = data.aiEditMode
    }
    emit('sessionRefreshed', session.value.id, session.value.title, session.value.status)
    toast.success('Chat reopened')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to reopen chat')
  }
}

async function handleArchiveSession() {
  if (!session.value) return
  try {
    await api.DELETE(ApiRoutes.Chat.sessions.archive(props.sessionId))
    toast.success('Chat archived')
    emit('dismiss')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to archive chat')
  }
}
```

Add `dismiss` to `ChatSessionView`'s own `defineEmits` (alongside the existing `sessionRefreshed`/`initialMessageSent`), and update the `<ChatSessionHeader>` element:

```html
    <ChatSessionHeader
      v-if="session"
      :session="session"
      :is-owner="isOwner"
      :compact="compact"
      @toggle-scope="handleToggleScope"
      @edit-personality="handleEditPersonality"
      @edit-mode="handleEditMode"
      @dismiss="handleDismiss"
      @close-session="handleCloseSession"
      @archive-session="handleArchiveSession"
      @reopen-session="handleReopen"
      @fork="handleFork"
      @start-edit-title="startEditTitle"
      @export-chat="exportChat"
      @toggle-find="toggleFind"
    />
```

- [ ] **Step 5: Update the callers of the old `@close`**

In `src/web-ui/app/pages/chats/index.vue`, `<ChatSessionView>` currently has no explicit `@close` handler (it relies on `ChatSessionView`'s internal default) — add `@dismiss="activeSessionId = null"` to it, matching how selecting a different session already clears the view.

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test ChatSessionHeader ChatSessionView`
Expected: all PASS.

- [ ] **Step 7: Typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
git add app/components/chat/ChatSessionHeader.vue app/components/chat/ChatSessionView.vue \
  app/pages/chats/index.vue app/components/chat/__tests__/ChatSessionHeader.test.ts
git commit -m "feat(chat): split dismiss from close/archive, icon-consistent kebab, Select Personality submenu"
```

---

### Task 12: `ChatDock` single-row header + full-height toggle + width

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionHeader.vue` (back/new-chat buttons for dock context)
- Modify: `src/web-ui/app/components/chat/ChatDock.vue`
- Modify: `src/web-ui/app/stores/chatDock.ts` (full-height toggle state)
- Test: `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts` (append)

**Interfaces:**
- `ChatSessionHeader` gains two more props: `showBackButton?: boolean` (default `false`) and `showNewChatButton?: boolean` (default `false`), plus emits `back` and `newChat` — only ever set by `ChatDock` in session mode. Full-page usage doesn't set them, so nothing changes there.
- `ChatSessionHeader`'s title now always renders (both compact and non-compact) — compact mode just doesn't show the inline rename pencil beside it (rename moved to the kebab in Task 11).
- `useChatDockStore` gains `isFullHeight: boolean` + `toggleFullHeight()`.

- [ ] **Step 1: Write the failing tests**

Append to `src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts`:

```ts
describe('ChatSessionHeader — dock chrome (back/new-chat)', () => {
  it('shows a back button when showBackButton is true', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true, showBackButton: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Back to history"]').exists()).toBe(true)
  })

  it('does not show a back button by default', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Back to history"]').exists()).toBe(false)
  })

  it('shows the real session title even when compact', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ title: 'My Dock Chat' }), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('My Dock Chat')
  })
})
```

Append to `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`:

```ts
it('renders exactly one header row in session mode (no duplicate title bar)', async () => {
  // Arrange dock store to session mode with an active session — follow this file's existing
  // pattern for stubbing useChatDockStore (read the top of the file for the exact mock shape).
  const wrapper = await mountSuspended(ChatDock)
  await flushPromises()
  expect(wrapper.findAll('[data-testid="dock-header-row"]').length).toBe(1)
})
```

(This file's exact store-mocking setup needs to be read before writing the arrange block — match whatever pattern the existing `ChatDock.test.ts` tests already use for `dock.mode`/`dock.activeSessionId`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test ChatSessionHeader ChatDock`
Expected: FAIL — no `showBackButton` prop, title still hidden in compact mode, no `data-testid="dock-header-row"` anywhere (there are currently two un-marked header rows).

- [ ] **Step 3: Add back/new-chat chrome to `ChatSessionHeader.vue`**

Add to the props:

```ts
const props = withDefaults(
  defineProps<{
    session: ChatSessionDetailDto
    isOwner: boolean
    compact?: boolean
    showBackButton?: boolean
    showNewChatButton?: boolean
  }>(),
  {
    compact: false,
    showBackButton: false,
    showNewChatButton: false
  }
)
```

Add to the emits:

```ts
const emit = defineEmits<{
  back: []
  newChat: []
  // ...existing emits from Task 11 unchanged...
}>()
```

Replace the title block — it now always renders the title, in both modes (drop the `v-else` empty div entirely):

```html
    <UButton
      v-if="compact && showBackButton"
      icon="i-lucide-chevron-left"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Back to history"
      @click="emit('back')"
    />

    <h2
      class="font-semibold truncate flex-1 min-w-0 text-sm"
      :class="compact ? 'cursor-pointer hover:text-primary' : ''"
      :title="compact ? 'Click to rename' : undefined"
      @click="compact && isOwner && isActive ? emit('startEditTitle') : undefined"
    >
      {{ session.title || 'Chat' }}
    </h2>
```

(Clicking the title itself in compact mode is a convenience shortcut to the same rename flow already reachable via the kebab's "Rename chat" — not a new capability, just matches the affordance the old `ChatDock`-local title click already had.)

Add the new-chat button, right before the kebab/dismiss cluster:

```html
    <UButton
      v-if="compact && showNewChatButton"
      icon="i-lucide-plus"
      variant="ghost"
      color="neutral"
      size="xs"
      title="New chat"
      @click="emit('newChat')"
    />
```

- [ ] **Step 4: Run the header tests to verify they pass**

Run: `cd src/web-ui && pnpm test ChatSessionHeader`
Expected: all PASS.

- [ ] **Step 5: Add `isFullHeight` to the dock store**

In `src/web-ui/app/stores/chatDock.ts`, add state + action (read the file first to match its existing `defineStore` shape — options API vs setup-store — and add alongside the existing `position` ref/state):

```ts
  isFullHeight: false,
```

```ts
  toggleFullHeight() {
    this.isFullHeight = !this.isFullHeight
  },
```

(If the store uses the Composition-API `defineStore(() => { ... })` form instead of the options form, write `const isFullHeight = ref(false)` and `function toggleFullHeight() { isFullHeight.value = !isFullHeight.value }` and return both — match whichever style `position`/`openDock` already use in that same file.)

- [ ] **Step 6: Rework `ChatDock.vue` to a single header row**

Remove the entire old header `<div>` block (the drag-handle row containing the back-chevron, `<h2>`/title-edit-input, `+`, and `x` — currently lines ~123-172). `ChatDock` must not render its own `ChatSessionHeader`-equivalent chrome AND let `ChatSessionView` render a second, nested one — that duplication is exactly today's bug. Instead: `ChatSessionView` stops rendering any header of its own in compact mode, and exposes the state/handlers a header needs; `ChatDock` renders the single real `ChatSessionHeader` itself, driven through a `ref` to the mounted `ChatSessionView` instance.

In `ChatSessionView.vue`, add at the end of the script:

```ts
defineExpose({
  session,
  isOwner,
  handleToggleScope,
  handleEditPersonality,
  handleEditMode,
  handleDismiss,
  handleCloseSession,
  handleArchiveSession,
  handleReopen,
  handleFork,
  startEditTitle,
  exportChat,
  toggleFind
})
```

And change its template's header line from `v-if="session"` to `v-if="session && !compact"` — in compact mode, `ChatSessionView` renders no header of its own at all; `ChatDock` renders the only one, driven through a `ref` to the mounted `ChatSessionView` instance:

```html
          <ChatSessionHeader
            v-if="dock.mode === 'session' && sessionViewRef?.session"
            :session="sessionViewRef.session"
            :is-owner="sessionViewRef.isOwner"
            compact
            show-back-button
            show-new-chat-button
            @back="dock.showHistory()"
            @new-chat="dock.newChat()"
            @dismiss="dock.closeDock()"
            @close-session="sessionViewRef.handleCloseSession()"
            @archive-session="sessionViewRef.handleArchiveSession()"
            @reopen-session="sessionViewRef.handleReopen()"
            @toggle-scope="sessionViewRef.handleToggleScope($event)"
            @edit-personality="sessionViewRef.handleEditPersonality($event)"
            @edit-mode="sessionViewRef.handleEditMode($event)"
            @start-edit-title="sessionViewRef.startEditTitle()"
            @export-chat="sessionViewRef.exportChat()"
            @toggle-find="sessionViewRef.toggleFind()"
          />
```

Add a `ref` on the `<ChatSessionView>` element further down in the same template and a matching script declaration:

```ts
const sessionViewRef = ref<InstanceType<typeof ChatSessionView> | null>(null)
```

```html
          <ChatSessionView
            v-if="dock.mode === 'session' && dock.activeSessionId"
            ref="sessionViewRef"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
            :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
            :initial-message="dock.pendingMessage?.content ?? null"
            :auto-send-initial="!!dock.pendingMessage"
            :initial-preset-id="dock.pendingMessage?.presetId ?? null"
            :initial-model-id="dock.pendingMessage?.modelId ?? null"
            :initial-effort="dock.pendingMessage?.reasoningEffort ?? null"
            compact
            @initial-message-sent="dock.clearPendingMessage()"
          />
```

(`@session-refreshed` and the old `onSessionRefreshed`/`startEditTitle`/`editTitle`/`titleInputRef`/`submitTitleEdit` script block in `ChatDock.vue` are all deleted outright — dead now that the dock's header is a real bound `ChatSessionHeader` reading `sessionViewRef.session.title` directly instead of a stale local copy.)

- [ ] **Step 7: Full-height toggle + width bump**

In `ChatDock.vue`'s popup `<div>`, bind height/width to the store's `isFullHeight` and bump the base width from `380px` to `440px`:

```html
      <div
        v-if="dock.isOpen"
        ref="popupRef"
        class="fixed z-50 max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
        :class="dock.isFullHeight ? 'w-[440px] h-[calc(100vh-2rem)]' : 'w-[440px] h-[50vh]'"
        :style="dock.isFullHeight ? { left: `${x}px`, top: '1rem' } : { left: `${x}px`, top: `${y}px` }"
      >
```

Add a full-height toggle button in the non-session header row's button cluster, and inside the session-mode `ChatSessionHeader`'s dock context — simplest: add it as a `UButton` sibling right after the `ChatSessionHeader`/placeholder row, inside the same `data-testid="dock-header-row"` wrapper, so it's present in every dock mode:

```html
          <UButton
            :icon="dock.isFullHeight ? 'i-lucide-minimize-2' : 'i-lucide-maximize-2'"
            variant="ghost"
            size="xs"
            :title="dock.isFullHeight ? 'Exit full height' : 'Full height'"
            class="absolute top-2 right-2"
            @click="dock.toggleFullHeight()"
          />
```

(Give the wrapping `data-testid="dock-header-row"` div `class="... relative"` so this `absolute`-positioned button anchors correctly without disturbing the row's own flex layout.)

- [ ] **Step 8: Run the full dock/header/session-view test suite**

Run: `cd src/web-ui && pnpm test ChatSessionHeader ChatSessionView ChatDock`
Expected: all PASS. Fix any test that reached into `ChatSessionView`'s no-longer-rendered internal header while compact — those assertions move to asserting on `ChatDock`'s externally-rendered `ChatSessionHeader` instead.

- [ ] **Step 9: Typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
git add app/components/chat/ChatSessionHeader.vue app/components/chat/ChatSessionView.vue \
  app/components/chat/ChatDock.vue app/stores/chatDock.ts \
  app/components/chat/__tests__/ChatSessionHeader.test.ts app/components/chat/__tests__/ChatDock.test.ts
git commit -m "feat(chat): single-row ChatDock header bound to real session title, full-height toggle, width bump"
```

---

### Task 13: Composer parity — personality/attach-docs/manage before a session exists, drop All-docs checkbox

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatInput.vue`
- Modify: `src/web-ui/app/pages/chats/index.vue` (thread staged personality/docs into `startNewChat`)
- Modify: `src/web-ui/app/stores/chatDock.ts` (thread staged personality/docs into `startNewChat`)
- Test: `src/web-ui/app/components/chat/__tests__/ChatInput.test.ts` (append, or create if it doesn't exist yet — check first)

**Interfaces:**
- `ChatInput` gains: a personality-select `UDropdownMenu` (mirrors the existing `presetMenuItems` pattern), a "Manage personalities" trigger opening `PersonalityManageModal`, and a staged-documents attach button (reuses `ChatDocAttachPicker`'s *search* UI but without a `sessionId` — see below).
- `ChatInput`'s `send` emit gains two more trailing optional args: `personalityId?: string | null` and `stagedDocumentIds?: string[]`.
- The parent (`chats/index.vue`'s `startNewChat`, `chatDock.ts`'s `startNewChat`) passes `personalityId` into `CreateChatSessionRequest` (field already exists) and, after the session is created, calls `AttachDocumentAsync` once per staged doc ID (`ApiRoutes.Chat.sessions.attachDocument`).
- The "All docs" checkbox/toggle is deleted outright from `ChatSessionHeader.vue` (both compact and non-compact) and from `ChatSession`'s UI surface — `SearchAllMyDocs`/`ToggleSearchAllMyDocs` stay on the domain/service/DB unchanged (no migration), simply unreachable from any UI now.

- [ ] **Step 1: Remove the All-docs checkbox from `ChatSessionHeader.vue`**

Delete the `<label>...All docs</label>` block (non-compact) and the `{ label: props.session.searchAllMyDocs ? ... }` entry from the personality group in `compactMenuItems` (compact) — added back in Task 11, now removed again since this task is the one that actually drops it per the addendum decision. Leave `toggleScope`/`handleScopeToggle` code in place only if still referenced elsewhere; if this was their only caller, delete `handleScopeToggle` and the `toggleScope` emit too (grep the file for `toggleScope` after removal — if zero remaining references, remove it; `ChatSessionView.handleToggleScope` similarly becomes unreachable UI-wise but can stay as dead Application-layer plumbing per this task's own scope note, or be removed if you're already touching that file — prefer removing it there too rather than leaving an orphaned handler, since Task 7 already established "no unused method is kept just in case" as this plan's convention).

- [ ] **Step 2: Write the failing `ChatInput` tests**

Check whether `src/web-ui/app/components/chat/__tests__/ChatInput.test.ts` already exists — if so, append; if not, create it following the mocking conventions from `ChatSessionHeader.test.ts` (same `mockNuxtImport('useApi', ...)`/`mockNuxtImport('useToast', ...)` shape):

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatInput from '~/components/chat/ChatInput.vue'

const mockGET = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({ GET: mockGET }))
mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

describe('ChatInput — personality + manage-personalities before a session exists', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: [], error: undefined })
  })

  it('shows a personality picker button', async () => {
    const wrapper = await mountSuspended(ChatInput)
    await flushPromises()
    expect(wrapper.find('[title="Personality"]').exists()).toBe(true)
  })

  it('shows a manage-personalities button', async () => {
    const wrapper = await mountSuspended(ChatInput)
    await flushPromises()
    expect(wrapper.find('[title="Manage personalities"]').exists()).toBe(true)
  })

  it('emits the selected personalityId as the second send argument', async () => {
    mockGET.mockResolvedValueOnce({
      data: [{ id: 'p1', name: 'well', description: null, systemPrompt: '', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null }],
      error: undefined
    })
    const wrapper = await mountSuspended(ChatInput)
    await flushPromises()
    wrapper.vm.selectedPersonalityId = 'p1'
    await wrapper.find('textarea').setValue('hi')
    await wrapper.find('textarea').trigger('keydown', { key: 'Enter' })
    const emitted = wrapper.emitted('send')
    expect(emitted?.[0]?.[3]).toBe('p1')
  })
})
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test ChatInput`
Expected: FAIL — no personality button, no manage button, `send`'s 4th argument doesn't exist.

- [ ] **Step 4: Add personality select + manage trigger to `ChatInput.vue`**

Add state/fetch, mirroring the existing preset pattern:

```ts
import PersonalityManageModal from '~/components/chat/PersonalityManageModal.vue'
import type { AgentPersonalityDto } from '~/types/chat'
```

```ts
const personalities = ref<AgentPersonalityDto[]>([])
const selectedPersonalityId = ref<string | null>(null)
const showManageModal = ref(false)

const selectedPersonalityName = computed(
  () => personalities.value.find(p => p.id === selectedPersonalityId.value)?.name ?? null
)

const personalityMenuItems = computed(() => [[
  {
    label: 'No personality',
    icon: selectedPersonalityId.value === null ? 'i-lucide-check' : undefined,
    onSelect: () => { selectedPersonalityId.value = null }
  },
  ...personalities.value.map(p => ({
    label: p.name,
    icon: selectedPersonalityId.value === p.id ? 'i-lucide-check' : undefined,
    onSelect: () => { selectedPersonalityId.value = p.id }
  })),
  { label: 'Manage personalities…', icon: 'i-lucide-users', onSelect: () => { showManageModal.value = true } }
]])

async function fetchPersonalities() {
  try {
    const { data } = await api.GET<AgentPersonalityDto[]>(ApiRoutes.Chat.personalities.list())
    personalities.value = (data ?? []).filter(p => !p.archivedAt)
  } catch {
    // Degrades to "No personality" — background list fetch, not worth a toast
  }
}

onMounted(fetchPersonalities)
```

Update `submit()` to include the personality id and update the emit signature:

```ts
const emit = defineEmits<{
  send: [
    content: string,
    presetId?: string | null,
    preferredModelId?: string | null,
    reasoningEffort?: string | null,
    personalityId?: string | null
  ]
  cancel: []
}>()
```

```ts
function submit() {
  const trimmed = content.value.trim()
  if (!trimmed || props.disabled) return
  emit('send', trimmed, selectedPresetId.value, selectedModelId.value, selectedEffort.value, selectedPersonalityId.value)
  content.value = ''
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }
}
```

Add the personality picker button and manage-personalities button in the controls row, next to the existing preset button:

```html
          <UDropdownMenu :items="personalityMenuItems">
            <UButton
              icon="i-lucide-user-round"
              :variant="selectedPersonalityId ? 'soft' : 'ghost'"
              :color="selectedPersonalityId ? 'primary' : 'neutral'"
              size="sm"
              :disabled="disabled"
              title="Personality"
            />
          </UDropdownMenu>

          <UButton
            icon="i-lucide-users"
            variant="ghost"
            color="neutral"
            size="sm"
            :disabled="disabled"
            title="Manage personalities"
            @click="showManageModal = true"
          />
```

Update the active-chip block at the top of the template to reflect the real selected personality name instead of the generic "Personality active" string:

```html
    <div
      v-if="selectedPresetName || selectedPersonalityName || personalityId"
      class="mb-2"
    >
      <span class="inline-flex items-center gap-1 text-xs bg-primary/10 text-primary px-2 py-0.5 rounded-full">
        <UIcon
          name="i-lucide-sparkles"
          class="size-3"
        />
        <span>{{ selectedPresetName ? `Preset: ${selectedPresetName}` : (selectedPersonalityName ? `Personality: ${selectedPersonalityName}` : 'Personality active') }}</span>
      </span>
    </div>
```

Mount the modal at the end of the template:

```html
    <PersonalityManageModal
      v-model:open="showManageModal"
      @changed="fetchPersonalities"
    />
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test ChatInput`
Expected: all PASS.

- [ ] **Step 6: Thread the staged personality into session creation**

In `src/web-ui/app/pages/chats/index.vue`, `startNewChat` currently receives `(content, presetId?, modelId?, effort?)` from `ChatInput`'s `send` emit — add the 5th param and pass it into the create request body (find the existing `CreateChatSessionRequest` body construction and add `personalityId`):

```ts
async function startNewChat(content: string, presetId?: string | null, modelId?: string | null, effort?: string | null, personalityId?: string | null) {
  // ...existing guard/loading logic...
  const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), {
    body: {
      title: 'New Chat',
      personalityId: personalityId ?? null,
      preferredModelConfigId: modelId ?? null,
      preferredEffort: effort ?? null
      // ...whatever other fields the existing body already sends...
    }
  })
  // ...existing pendingMessage/prependSession logic...
}
```

(Read the current body of `startNewChat` before editing — this shows the one field to add, `personalityId`, not a full rewrite of the request body; every other field it already sends stays exactly as-is.)

Do the same in `src/web-ui/app/stores/chatDock.ts`'s `startNewChat` action — add the same 5th parameter and the same `personalityId` field to its own session-create call.

- [ ] **Step 7: Full frontend suite, typecheck, lint, commit**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test run
git add app/components/chat/ChatInput.vue app/components/chat/ChatSessionHeader.vue \
  app/pages/chats/index.vue app/stores/chatDock.ts app/components/chat/__tests__/ChatInput.test.ts
git commit -m "feat(chat): personality select + manage-personalities on the composer, drop All-docs checkbox"
```

---

### Task 14: Manual validation matrix update

**Files:**
- Modify: `docs/manual-validation/2026-08-06-web-chat-toolbar-personalities-matrix.md` (append new test cases from Tasks 7-13, same table format as the existing TC-1..TC-6)

**Interfaces:** None — documentation only.

- [ ] **Step 1: Append TC-7 through TC-12**

Add to the existing matrix file: TC-7 (Dismiss vs Close — X never ends the session, kebab "Close chat" shows the confirm dialog and its exact wording), TC-8 (Archive from inside an open chat, both full-page and dock), TC-9 (Reopen a closed session from `/chats` list and from inside the chat), TC-10 (Status filter on `/chats` shows/hides Archived correctly), TC-11 (ChatDock shows exactly one header row with the real title, `< Chat TITLE : + x/⋮` layout, full-height toggle, 440px width), TC-12 (composer on the empty `/chats` state and dock draft mode both offer Personality select, Manage personalities, and the attach-docs button before any session exists; All-docs checkbox is gone everywhere).

- [ ] **Step 2: Commit**

```bash
git add docs/manual-validation/2026-08-06-web-chat-toolbar-personalities-matrix.md
git commit -m "docs: extend manual validation matrix for dismiss/close/archive/reopen + composer parity"
```

---

## Acceptance

```bash
dotnet test
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test run && pnpm build
```

All must pass before this work is considered done. Manual validation matrix (Task 6) should be run through and signed off separately, same as prior chat plans.
