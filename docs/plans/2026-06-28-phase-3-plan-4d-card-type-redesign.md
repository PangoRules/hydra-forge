# Card Type Redesign — Plan 4D

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Branch:** `task/phase-3-card-type-redesign`
**Parent branch:** `feat/phase-3-web-ui`
**Prerequisite:** Plan 4A (board filtering) merged. Can run in parallel with 4B/4C.

**Goal:** Replace software-specific card types (Bug, Epic, Spec) with universal ones (Issue, Goal; remove Spec), remove the Epic-only parent restriction so any card can parent any other, and update all UI labels and icons to match.

**Spec:** `docs/specs/2026-06-28-card-type-redesign.md`

**Architecture:** Integer storage values 2 (Bug→Issue) and 5 (Epic→Goal) stay unchanged in the database — only the C# enum names change. Integer 3 (Spec) is retired; existing rows are data-migrated to 5 (Goal) via EF Core SQL migration. `src/web-ui/app/lib/card-type.ts` is the single frontend source of truth for type→label/icon/color mapping; all consuming components import from it. The API uses `JsonStringEnumConverter` — types are always transmitted as strings (e.g. `"Issue"`, `"Goal"`), never integers.

**Tech Stack:** C# / .NET 10, EF Core 9, xUnit, Nuxt 4, TypeScript, Vitest, Tailwind CSS

## Global Constraints

- xUnit only for C# tests — no FluentAssertions, use plain `Assert.*`
- `useApi()` for all HTTP calls — never import openapi-fetch directly
- `useApi()` throws on error — always wrap in try/catch; never check a returned `error` field
- All API path strings go in `src/web-ui/app/lib/routes.ts` as `ApiRoutes.*`
- Entity state transitions only via domain methods — never set `entity.Property = value` directly
- No `console.log/error/warn` in production code — use `useAppToast()` for user feedback
- Use `pnpm` for frontend package operations
- Run commands from repo root unless stated otherwise
- Docker Postgres must be running for EF migrations: `docker compose up -d postgres`

---

## File Map

### Backend — modify
- `src/HydraForge.Domain/Enums/CardType.cs`
- `src/HydraForge.Domain/Entities/ProjectSpace/Card.cs`
- `src/HydraForge.Domain/Common/DomainErrorCodes.cs`
- `src/HydraForge.Application/Cards/CardService.cs`
- `src/HydraForge.Server/Errors/ProblemDetailsMapper.cs`

### Backend — create
- `src/HydraForge.Infrastructure/Migrations/<timestamp>_MigrateSpecCardsToGoal.cs` (generated)

### Frontend — modify
- `src/web-ui/app/lib/card-type.ts`
- `src/web-ui/app/lib/__tests__/card-type.test.ts`
- `src/web-ui/app/components/board/CardCreateModal.vue`
- `src/web-ui/app/components/card/CardMetadata.vue`
- `src/web-ui/app/components/board/BoardCard.vue`
- `src/web-ui/app/components/board/BoardMobileList.vue`
- `src/web-ui/app/types/api.d.ts` (regenerated from OpenAPI)

### Docs — modify
- `docs/data-model.md`
- `docs/glossary.md`
- `docs/functional-spec.md`

---

## Task 1: Domain — rename enum values, remove Epic parent restriction

**Files:**
- Modify: `src/HydraForge.Domain/Enums/CardType.cs`
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Card.cs`
- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`

**Produces:** `CardType.Issue = 2`, `CardType.Goal = 5`, `Card.ValidateParent(...)`, `DomainErrorCodes.Cards.InvalidParent = "CARD_INVALID_PARENT"`

- [ ] **Step 1: Replace `CardType.cs`**

```csharp
namespace HydraForge.Domain.Enums;

public enum CardType
{
    Task = 1,
    Issue = 2,   // was Bug
    // 3 intentionally skipped — was Spec; rows migrated to Goal in MigrateSpecCardsToGoal
    Idea = 4,
    Goal = 5     // was Epic
}
```

- [ ] **Step 2: Rename `ValidateParentEpic` → `ValidateParent` and remove Epic-type check in `Card.cs`**

In `src/HydraForge.Domain/Entities/ProjectSpace/Card.cs`, replace the `ValidateParentEpic` method with:

```csharp
public static Error? ValidateParent(Card child, Card parent, IReadOnlyDictionary<Guid, Card>? cardMap = null)
{
    if (child.Id == parent.Id)
        return new Error(DomainErrorCodes.Cards.ParentCycle, "Card cannot be its own parent.");

    if (child.ProjectId != parent.ProjectId)
        return new Error(DomainErrorCodes.Cards.InvalidParent, "Parent card must be in the same project.");

    if (cardMap != null)
    {
        var cycleError = ValidateNoCycle(child.Id, parent.Id, cardMap);
        if (cycleError != null)
            return cycleError;
    }

    return null;
}
```

The `parent.Type != CardType.Epic` check is intentionally deleted — any card type can now be a parent.

- [ ] **Step 3: Rename error code in `DomainErrorCodes.cs`**

In the `Cards` inner class, replace:
```csharp
public const string InvalidParentEpic = "CARD_INVALID_PARENT_EPIC";
```
with:
```csharp
public const string InvalidParent = "CARD_INVALID_PARENT";
```

- [ ] **Step 4: Verify Domain compiles in isolation**

```bash
dotnet build src/HydraForge.Domain 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`. The outer solution will fail (Application/Server still reference old names) — that's expected and fixed in Task 2.

- [ ] **Step 5: Commit**

```bash
git add src/HydraForge.Domain/Enums/CardType.cs \
        src/HydraForge.Domain/Entities/ProjectSpace/Card.cs \
        src/HydraForge.Domain/Common/DomainErrorCodes.cs
git commit -m "feat(domain): rename Bug→Issue, Epic→Goal, retire Spec; open parent restriction"
```

---

## Task 2: Application + Server — update call sites

**Files:**
- Modify: `src/HydraForge.Application/Cards/CardService.cs`
- Modify: `src/HydraForge.Server/Errors/ProblemDetailsMapper.cs`

**Consumes:** `Card.ValidateParent(...)` and `DomainErrorCodes.Cards.InvalidParent` from Task 1

- [ ] **Step 1: Update `CardService.cs` — two call sites**

Line 67: rename `Card.ValidateParentEpic(` → `Card.ValidateParent(`

Line 328: rename `Card.ValidateParentEpic(card, parentCard);` → `Card.ValidateParent(card, parentCard);`

No other logic changes — the parent card null check and card-map lookup around these calls remain.

- [ ] **Step 2: Update `ProblemDetailsMapper.cs` — error code + title**

Replace line 41:
```csharp
DomainErrorCodes.Cards.InvalidParentEpic => (400, "Invalid parent epic"),
```
with:
```csharp
DomainErrorCodes.Cards.InvalidParent => (400, "Invalid parent"),
```

- [ ] **Step 3: Full solution build — must be clean**

```bash
dotnet build 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Run backend tests**

```bash
dotnet test 2>&1 | tail -10
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/HydraForge.Application/Cards/CardService.cs \
        src/HydraForge.Server/Errors/ProblemDetailsMapper.cs
git commit -m "feat(app,server): ValidateParentEpic→ValidateParent call sites + error mapping"
```

---

## Task 3: EF Core migration — retire Spec card type

**Context:** Integer values 2 (Bug→Issue) and 5 (Epic→Goal) are unchanged in the database; only the C# names change. Integer 3 (Spec) is being retired. Any `Cards` rows with `Type = 3` must be moved to `Type = 5` (Goal) so no rows reference the now-undefined value.

**Files:**
- Create: `src/HydraForge.Infrastructure/Migrations/<timestamp>_MigrateSpecCardsToGoal.cs`

**Consumes:** clean build from Task 2, Docker Postgres running

- [ ] **Step 1: Ensure Postgres is running**

```bash
docker compose up -d postgres
```

- [ ] **Step 2: Generate empty migration**

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations add MigrateSpecCardsToGoal \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

Expected: new file created in `src/HydraForge.Infrastructure/Migrations/`. The `Up()` and `Down()` bodies will be empty because the enum integer values haven't changed at the EF model level — that's correct. You'll add SQL manually in the next step.

If you see "Unable to check if the migration has been applied" — that's expected if the DB isn't reachable for the check; the file still generates.

- [ ] **Step 3: Edit the generated migration — add data migration SQL**

Open the newly generated migration file. Add to `Up()` and `Down()`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Spec card type (integer 3) retired — migrate existing rows to Goal (integer 5).
    migrationBuilder.Sql(@"UPDATE ""Cards"" SET ""Type"" = 5 WHERE ""Type"" = 3;");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Cannot distinguish rows that were originally Goal from those migrated from Spec — no-op.
}
```

- [ ] **Step 4: Verify build still clean after editing**

```bash
dotnet build 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Apply migration**

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef database update \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

Expected: lists the new migration as applied and prints `Done.`

- [ ] **Step 6: Commit**

```bash
git add src/HydraForge.Infrastructure/Migrations/
git commit -m "feat(db): data migration — retire Spec (type 3), move rows to Goal (type 5)"
```

---

## Task 4: Frontend — update `card-type.ts` and its tests (TDD)

**Files:**
- Modify: `src/web-ui/app/lib/__tests__/card-type.test.ts`
- Modify: `src/web-ui/app/lib/card-type.ts`

**Produces:**
- `CARD_TYPE_OPTIONS`: `Task(0)`, `Issue(1)`, `Goal(2)`, `Idea(3)`
- `CARD_TYPE_FILTER_OPTIONS`: All / Task / Issue / Goal / Idea (no Spec)
- `cardTypeToApiString`, `cardTypeOption` work with new type names

**Context on the `value` field:** The numeric `value` in `CARD_TYPE_OPTIONS` is a zero-based UI index used to identify a selected type in dropdowns. It is NOT the C# enum integer. The API transmits type names as strings via `JsonStringEnumConverter` — so `cardType.value = 1` → `toTypeString(1)` = `"Issue"` → API body gets `type: "Issue"`. The `apiValue` field is what the API actually sends/receives.

- [ ] **Step 1: Replace the test file with failing tests**

```ts
import { describe, it, expect } from 'vitest'
import { CARD_TYPE_OPTIONS, CARD_TYPE_FILTER_OPTIONS, cardTypeToApiString, cardTypeOption } from '~/lib/card-type'

describe('card-type', () => {
  it('has four options: Task, Issue, Goal, Idea', () => {
    expect(CARD_TYPE_OPTIONS.map(o => o.label)).toEqual(['Task', 'Issue', 'Goal', 'Idea'])
  })

  it('filter options include All first', () => {
    expect(CARD_TYPE_FILTER_OPTIONS[0].label).toBe('All')
    expect(CARD_TYPE_FILTER_OPTIONS[0].value).toBeNull()
  })

  it('filter options do not include Spec, Bug, or Epic', () => {
    const labels = CARD_TYPE_FILTER_OPTIONS.map(o => o.label)
    expect(labels).not.toContain('Spec')
    expect(labels).not.toContain('Bug')
    expect(labels).not.toContain('Epic')
  })

  it('index 1 maps to Issue', () => {
    expect(cardTypeToApiString(1)).toBe('Issue')
  })

  it('index 2 maps to Goal', () => {
    expect(cardTypeToApiString(2)).toBe('Goal')
  })

  it('passes through a known string type unchanged', () => {
    expect(cardTypeToApiString('Issue')).toBe('Issue')
    expect(cardTypeToApiString('Goal')).toBe('Goal')
  })

  it('defaults unknown numeric index to Task', () => {
    expect(cardTypeToApiString(99)).toBe('Task')
  })

  it('resolves Issue option at index 1', () => {
    expect(cardTypeOption(1).label).toBe('Issue')
    expect(cardTypeOption(1).color).toBe('error')
    expect(cardTypeOption(1).icon).toBe('i-lucide-bug')
  })

  it('resolves Goal option at index 2', () => {
    expect(cardTypeOption(2).label).toBe('Goal')
    expect(cardTypeOption(2).color).toBe('primary')
    expect(cardTypeOption(2).icon).toBe('i-lucide-layers')
  })

  it('resolves by API string', () => {
    expect(cardTypeOption('Goal').label).toBe('Goal')
    expect(cardTypeOption('Issue').label).toBe('Issue')
  })
})
```

- [ ] **Step 2: Run — confirm failures**

```bash
cd src/web-ui && pnpm test --run app/lib/__tests__/card-type.test.ts 2>&1 | tail -15
```

Expected: multiple failures (CARD_TYPE_OPTIONS still has old values).

- [ ] **Step 3: Replace `card-type.ts`**

```ts
/** UI display index → API string name. Zero-based; NOT the C# enum integer. */
export const CARD_TYPE_MAP: Record<number, string> = {
  0: 'Task',
  1: 'Issue',
  2: 'Goal',
  3: 'Idea'
}

/** Lucide icon name for each UI index. */
export const CARD_TYPE_ICONS: Record<number, string> = {
  0: 'i-lucide-square-check',
  1: 'i-lucide-bug',
  2: 'i-lucide-layers',
  3: 'i-lucide-lightbulb'
}

/**
 * Full metadata per type. `value` is the zero-based UI dropdown index;
 * `apiValue` is the string sent to / received from the API.
 */
export const CARD_TYPE_OPTIONS = [
  { value: 0, apiValue: 'Task',  label: 'Task',  color: 'neutral', icon: 'i-lucide-square-check' },
  { value: 1, apiValue: 'Issue', label: 'Issue', color: 'error',   icon: 'i-lucide-bug' },
  { value: 2, apiValue: 'Goal',  label: 'Goal',  color: 'primary', icon: 'i-lucide-layers' },
  { value: 3, apiValue: 'Idea',  label: 'Idea',  color: 'warning', icon: 'i-lucide-lightbulb' },
] as const

/** Per-column filter options. Values are API strings; null means "All". */
export const CARD_TYPE_FILTER_OPTIONS = [
  { label: 'All',   value: null },
  { label: 'Task',  value: 'Task' },
  { label: 'Issue', value: 'Issue' },
  { label: 'Goal',  value: 'Goal' },
  { label: 'Idea',  value: 'Idea' },
] as const

/**
 * Resolve a card type to its API string name.
 * Accepts the zero-based UI index (number) or an already-resolved API string.
 */
export function toTypeString(type: number | string): string {
  return typeof type === 'string' ? type : (CARD_TYPE_MAP[type] ?? 'Task')
}

/** Alias for toTypeString — matches existing import pattern across components. */
export const cardTypeToApiString = toTypeString

/** Full option object for a given UI index or API string. */
export function cardTypeOption(type: number | string) {
  const apiValue = toTypeString(type)
  return CARD_TYPE_OPTIONS.find(o => o.apiValue === apiValue) ?? CARD_TYPE_OPTIONS[0]
}

/** Tailwind text-color class for a card type option's color name. */
export function cardTypeColorClass(option: { color: string }): string {
  switch (option.color) {
    case 'error':   return 'text-red-500'
    case 'warning': return 'text-amber-500'
    case 'info':    return 'text-blue-500'
    case 'primary': return 'text-primary'
    default:        return 'text-gray-400'
  }
}
```

- [ ] **Step 4: Run tests — must pass**

```bash
cd src/web-ui && pnpm test --run app/lib/__tests__/card-type.test.ts 2>&1 | tail -10
```

Expected: all tests pass.

- [ ] **Step 5: Typecheck**

```bash
cd src/web-ui && pnpm typecheck 2>&1 | tail -5
```

Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/lib/card-type.ts \
        src/web-ui/app/lib/__tests__/card-type.test.ts
git commit -m "feat(ui): card-type.ts — Bug→Issue, Epic→Goal, remove Spec"
```

---

## Task 5: Frontend — update `CardCreateModal.vue`

**Files:**
- Modify: `src/web-ui/app/components/board/CardCreateModal.vue`

**Consumes:** updated `CARD_TYPE_OPTIONS` from Task 4

Changes:
- `selectedParentEpicId` ref → `selectedParentId`
- `epicCards` ref → `parentCandidates`
- `fetchEpics()` → `fetchParentCandidates()` — remove `?type=Epic` filter, fetch all project cards
- "Parent Epic" label → "Parent"
- Iteration var `epic` → `card` in template loop

- [ ] **Step 1: Update refs, function, and handler in the `<script setup>` block**

Replace the four relevant lines (ref declarations + function + onMounted):

```ts
const selectedParentId = ref<string | undefined>()
const parentCandidates = ref<CardResponse[]>([])

async function fetchParentCandidates() {
  try {
    const { data } = await api.GET<{ cards: CardResponse[] }>(ApiRoutes.Cards.list(props.projectId))
    parentCandidates.value = data?.cards ?? []
  } catch {
    parentCandidates.value = []
  }
}

onMounted(() => fetchParentCandidates())
```

In `handleCreate`, update the parent block:
```ts
if (selectedParentId.value) {
  body.parentCardId = selectedParentId.value
}
```

- [ ] **Step 2: Update the parent section in the template**

```html
<div>
  <label class="block text-sm font-medium mb-1">Parent</label>
  <select
    v-model="selectedParentId"
    class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
  >
    <option :value="undefined">
      None
    </option>
    <option
      v-for="card in parentCandidates"
      :key="card.id"
      :value="card.id"
    >
      {{ card.title }}
    </option>
  </select>
</div>
```

- [ ] **Step 3: Typecheck**

```bash
cd src/web-ui && pnpm typecheck 2>&1 | tail -5
```

Expected: no errors.

- [ ] **Step 4: Visual check**

Start dev server (`cd src/web-ui && pnpm dev`), open a project board, click "+ Add Card":
- Type dropdown shows: Task, Issue, Goal, Idea (not Bug / Epic / Spec)
- Parent section label reads "Parent" (not "Parent Epic")
- Parent dropdown lists all cards in the project, not filtered to Epic type
- Creating a card of any type with any parent works

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/board/CardCreateModal.vue
git commit -m "feat(ui): CardCreateModal — Parent Epic→Parent, fetch all cards as parent candidates"
```

---

## Task 6: Frontend — add Parent field to `CardMetadata.vue`

**Files:**
- Modify: `src/web-ui/app/components/card/CardMetadata.vue`

**Consumes:** updated `cardTypeToApiString` from Task 4

The metadata panel already handles `parentCardId` as a pass-through in `persistCardFields`. This task adds a visible parent field with inline editing — matching the pattern of the existing Type and Column fields.

- [ ] **Step 1: Extend `persistCardFields` to accept `parentCardId`**

Replace the current signature and body of `persistCardFields`:

```ts
async function persistCardFields(fields: {
  type?: string
  dueAt?: string | null
  parentCardId?: string | null
}) {
  try {
    const { data } = await api.PUT(ApiRoutes.Cards.update(props.projectId, props.card.id), {
      body: {
        title: props.card.title,
        description: props.card.description,
        type: fields.type ?? cardTypeToApiString(props.card.type),
        version: props.card.version,
        parentCardId: fields.parentCardId !== undefined ? fields.parentCardId : props.card.parentCardId,
        dueAt: fields.dueAt !== undefined ? fields.dueAt : props.card.dueAt
      }
    })
    if (data) emit('update:card', data as CardResponse)
    return true
  } catch {
    toast.error('Failed to update card')
    return false
  }
}
```

- [ ] **Step 2: Add parent refs and handlers**

Add after the existing `savingColumn` ref:

```ts
const parentCandidates = ref<CardResponse[]>([])
const savingParent = ref(false)

const parentCard = computed(() => {
  if (!props.card.parentCardId) return null
  for (const cards of board.cardsByColumn.values()) {
    const found = cards.find(c => c.id === props.card.parentCardId)
    if (found) return found
  }
  return null
})

async function fetchParentCandidates() {
  try {
    const { data } = await api.GET<{ cards: CardResponse[] }>(ApiRoutes.Cards.list(props.projectId))
    parentCandidates.value = (data?.cards ?? []).filter(c => c.id !== props.card.id)
  } catch {
    parentCandidates.value = []
  }
}

async function handleParentChange(value: string) {
  if (props.isArchived) return
  const newParentId = value === '' ? null : value
  if (newParentId === (props.card.parentCardId ?? null)) return
  savingParent.value = true
  await persistCardFields({ parentCardId: newParentId })
  savingParent.value = false
}
```

Add `onMounted` call (or extend existing if one is already there):

```ts
onMounted(() => {
  if (!props.isArchived) fetchParentCandidates()
})
```

- [ ] **Step 3: Add Parent section to template, after the Column section**

```html
<!-- Parent -->
<div>
  <p class="text-xs font-medium text-muted uppercase mb-1">
    Parent
  </p>
  <select
    v-if="!isArchived"
    class="w-full px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
    :value="card.parentCardId ?? ''"
    :disabled="savingParent"
    @change="(e: Event) => handleParentChange((e.target as HTMLSelectElement).value)"
  >
    <option value="">
      None
    </option>
    <option
      v-for="c in parentCandidates"
      :key="c.id"
      :value="c.id"
    >
      {{ c.title }}
    </option>
  </select>
  <span
    v-else
    class="text-sm text-muted"
  >
    {{ parentCard?.title ?? (card.parentCardId ? card.parentCardId.slice(0, 8) : 'None') }}
  </span>
</div>
```

- [ ] **Step 4: Typecheck**

```bash
cd src/web-ui && pnpm typecheck 2>&1 | tail -5
```

- [ ] **Step 5: Visual check**

Open a card's detail modal. The metadata panel should show a "Parent" row. Changing the parent dropdown should save immediately and update the card.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/components/card/CardMetadata.vue
git commit -m "feat(ui): CardMetadata — add parent display + inline edit"
```

---

## Task 7: Frontend — remove hardcoded "Epic" from board card views

**Files:**
- Modify: `src/web-ui/app/components/board/BoardCard.vue`
- Modify: `src/web-ui/app/components/board/BoardMobileList.vue`
- Modify: `src/web-ui/app/components/board/__tests__/BoardCard.test.ts`

Any card type can now be a parent. The "Epic" badge shown on child cards should say "Parent" and the tooltip should be generic.

- [ ] **Step 1: Update `BoardCard.vue` — parent badge**

Find the `v-if="card.parentCardId"` span (around line 255) and replace:

```html
<span
  v-if="card.parentCardId"
  class="text-xs text-primary flex items-center gap-1"
  title="Has a parent card"
>
  <UIcon name="i-lucide-layers" class="size-3" />
  Parent
</span>
```

- [ ] **Step 2: Update `BoardMobileList.vue` — parent row**

Find the `v-if="card.parentCardId"` paragraph (around line 607) and replace:

```html
<!-- Row 5: parent link -->
<p
  v-if="card.parentCardId"
  class="text-xs mt-1 text-primary flex items-center gap-1"
>
  <UIcon name="i-lucide-layers" class="size-3" />
  Parent
</p>
```

- [ ] **Step 3: Update `BoardCard.test.ts` — update stale comment**

Line 116: change the inline comment from `// Bug type` to `// Issue type` (type: 1 now resolves to Issue):

```ts
props: { card: makeCard({ type: 1 }), projectId: 'p1' } // Issue type
```

- [ ] **Step 4: Typecheck + full frontend test run**

```bash
cd src/web-ui && pnpm typecheck && pnpm test --run 2>&1 | tail -15
```

Expected: no type errors, all tests pass.

- [ ] **Step 5: Visual check**

On the board, a card that has a parent should display "Parent" (with the layers icon) — not "Epic". Type icons and colors for Issue/Goal/Task/Idea should be correct. Per-column type filter dropdown in column headers should show: Task, Issue, Goal, Idea.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/components/board/BoardCard.vue \
        src/web-ui/app/components/board/BoardMobileList.vue \
        src/web-ui/app/components/board/__tests__/BoardCard.test.ts
git commit -m "feat(ui): BoardCard/BoardMobileList — replace 'Epic' parent badge with 'Parent'"
```

---

## Task 8: Regenerate `api.d.ts`

**Files:**
- Modify: `src/web-ui/app/types/api.d.ts`

After backend changes the OpenAPI schema reflects the renamed `CardType` enum values. Regenerating `api.d.ts` keeps the generated types in sync.

- [ ] **Step 1: Start the server with the new code**

```bash
dotnet run --project src/HydraForge.Server &
```

Wait until you see `Application started` in the output. Verify the OpenAPI doc is available:

```bash
curl -s http://localhost:5000/openapi/v1.json | head -5
```

Expected: JSON output starting with `{"openapi":`.

- [ ] **Step 2: Check for a generation script**

```bash
grep -i "openapi\|generate\|schema" src/web-ui/package.json
```

If a script exists (e.g. `pnpm run generate:api`), use it. Otherwise continue with the manual command below.

- [ ] **Step 3: Regenerate**

```bash
cd src/web-ui && npx openapi-typescript http://localhost:5000/openapi/v1.json -o app/types/api.d.ts
```

- [ ] **Step 4: Typecheck after regeneration**

```bash
cd src/web-ui && pnpm typecheck 2>&1 | tail -10
```

Fix any errors caused by type shape changes. Most likely `CardType` may change from `number` to a string union — if that happens, update component code that passes `card.type` as a number to use `cardTypeOption(card.type).apiValue` instead.

- [ ] **Step 5: Full test run**

```bash
cd src/web-ui && pnpm test --run 2>&1 | tail -10
```

Expected: all tests pass.

- [ ] **Step 6: Stop the background server, commit**

```bash
kill %1 2>/dev/null; git add src/web-ui/app/types/api.d.ts
git commit -m "chore(ui): regenerate api.d.ts after CardType rename"
```

---

## Task 9: Update docs

**Files:**
- Modify: `docs/data-model.md`
- Modify: `docs/glossary.md`
- Modify: `docs/functional-spec.md`

- [ ] **Step 1: Update `docs/data-model.md` — CardType enum table**

Find the CardType enum section and replace with:

```markdown
| Value | Name  | Description                               |
|-------|-------|-------------------------------------------|
| 1     | Task  | Unit of work                              |
| 2     | Issue | Problem, concern, or question             |
| 4     | Idea  | Suggestion; may become a Goal or Task     |
| 5     | Goal  | Significant objective; groups child cards |
```

Add a note: `Value 3 (Spec) was retired in migration MigrateSpecCardsToGoal; existing rows moved to Goal (5).`

- [ ] **Step 2: Update `docs/glossary.md`**

Remove entries for **Bug**, **Epic**, **Spec (card type)**.

Add/update:
- **Goal** — Card type (integer 5). A significant objective that groups child work. Formerly "Epic". Any card can be a child of a Goal — the Epic-only parent restriction is removed.
- **Issue** — Card type (integer 2). A problem, concern, or question. Formerly "Bug".
- **Idea** — Card type (integer 4). A suggestion that may later become a Goal or Task.
- **Parent** — A card that another card is linked to via `parentCardId`. Any card type can be a parent; cycle detection prevents loops. Formerly restricted to Epic-type cards only.

- [ ] **Step 3: Update `docs/functional-spec.md`**

Search for "FR-6" and "FR-10" (card type requirements). Update the type list to: **Task, Issue, Goal, Idea**. Replace any "Epic" parent requirement with: "Any card can parent any other card in the same project; cycles are rejected."

- [ ] **Step 4: Commit**

```bash
git add docs/data-model.md docs/glossary.md docs/functional-spec.md
git commit -m "docs: data-model, glossary, functional-spec — card type redesign"
```

---

## Done Criteria

- [ ] `dotnet build` — `0 Error(s)`
- [ ] `dotnet test` — all pass
- [ ] `cd src/web-ui && pnpm test --run` — all pass
- [ ] `cd src/web-ui && pnpm typecheck` — no errors
- [ ] Type dropdown in CardCreateModal shows: Task, Issue, Goal, Idea (no Bug / Epic / Spec)
- [ ] Per-column type filter in ColumnHeader shows: Task, Issue, Goal, Idea
- [ ] Parent dropdown in CardCreateModal lists all project cards (not filtered to Epic)
- [ ] CardMetadata panel shows a "Parent" row — editable for non-archived cards
- [ ] Board card badge reads "Parent" (not "Epic") when `parentCardId` is set
- [ ] EF migration applied — `Cards` table has no rows with `Type = 3`
- [ ] `docs/data-model.md`, `docs/glossary.md`, `docs/functional-spec.md` updated
