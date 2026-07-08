# Editable Columns + Project Templates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users rename/color/WIP-limit/add/delete board columns from the UI (backend CRUD already exists but is unwired), and let new projects pick a column template (Software / General / Blank) instead of always getting a hardcoded dev-pipeline column set.

**Architecture:** Backend adds a non-persisted `ColumnTemplate` enum consumed only at project-creation time (`ProjectService.CreateAsync`); the existing `ColumnsController` CRUD endpoints are reused unchanged. Frontend adds a `useColumnManage` composable (create/update/delete + store sync + toast-on-error) shared by a desktop inline popover (`ColumnHeader.vue`) and a new mobile `ColumnManageModal.vue`, plus a template `USelect` in `ProjectCreateModal.vue`.

**Tech Stack:** .NET 10 / C# (Application layer), xUnit, Nuxt 4 + Nuxt UI v4 + Pinia, Vitest, `@nuxt/test-utils/runtime`, `@vueuse/core` (`onClickOutside`).

## Global Constraints

- Business logic returns `Result<T, Error>` — never throw for expected failures (already true for `ColumnService`; no changes needed there).
- No `var` where the type isn't obvious from the right-hand side.
- `xUnit` only, plain `Assert.*` — no FluentAssertions.
- Every `useApi()` call site wrapped in try/catch (D-40) — never bare `const { error } = await api...` without try/catch.
- No inline API path strings in Vue components/composables/stores — use `ApiRoutes.*` from `app/lib/routes.ts` (all needed routes already exist for Columns; no `routes.ts` changes required).
- No `console.log`/`console.error`/`console.warn` — use `useAppToast()` (this repo's wrapper around Nuxt UI's `useToast()`).
- Comments only when the WHY is non-obvious.

---

### Task 1: Backend — `ColumnTemplate` enum + wire through project creation

**Files:**
- Create: `src/HydraForge.Application/Projects/ColumnTemplate.cs`
- Modify: `src/HydraForge.Application/Projects/ProjectContracts.cs`
- Modify: `src/HydraForge.Application/Projects/ProjectModels.cs`
- Modify: `src/HydraForge.Application/Projects/ProjectService.cs`
- Modify: `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs:21-33`
- Test: `tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs`

**Interfaces:**
- Produces: `ColumnTemplate` enum (`Software = 1, General = 2, Blank = 3`), `ColumnTemplates.Get(ColumnTemplate) : string[]` — both in namespace `HydraForge.Application.Projects`. `CreateProjectCommand` gains `ColumnTemplate Template = ColumnTemplate.General` as its 6th (last) parameter. `CreateProjectRequest` gains `ColumnTemplate Template = ColumnTemplate.General` as its 5th (last) parameter.

- [ ] **Step 1: Write the failing tests**

Replace the existing six-column test and add two more, in `tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs`. Find this existing test:

```csharp
    [Fact]
    public async Task CreateAsync_InsertsSixDefaultColumns()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Columns.Count);
        Assert.Equal("Backlog", result.Value.Columns[0].Name);
        Assert.Equal("Done", result.Value.Columns[5].Name);
    }
```

Replace it with (default template changed from implicit Software to explicit General — this reflects the new default, matching the design decision that General is the non-dev-flavored default):

```csharp
    [Fact]
    public async Task CreateAsync_DefaultTemplate_InsertsFourGeneralColumns()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = DefaultCreateCmd(Guid.NewGuid());

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Columns.Count);
        Assert.Equal("Backlog", result.Value.Columns[0].Name);
        Assert.Equal("In Progress", result.Value.Columns[1].Name);
        Assert.Equal("Review", result.Value.Columns[2].Name);
        Assert.Equal("Done", result.Value.Columns[3].Name);
    }

    [Fact]
    public async Task CreateAsync_SoftwareTemplate_InsertsSixColumns()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = new CreateProjectCommand(Guid.NewGuid(), "Test Project", "A test project", null, null, ColumnTemplate.Software);

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Columns.Count);
        Assert.Equal("Backlog", result.Value.Columns[0].Name);
        Assert.Equal("Spec-ing", result.Value.Columns[1].Name);
        Assert.Equal("Planned", result.Value.Columns[2].Name);
        Assert.Equal("In Dev", result.Value.Columns[3].Name);
        Assert.Equal("In Review", result.Value.Columns[4].Name);
        Assert.Equal("Done", result.Value.Columns[5].Name);
    }

    [Fact]
    public async Task CreateAsync_BlankTemplate_InsertsTwoColumns()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var cmd = new CreateProjectCommand(Guid.NewGuid(), "Test Project", "A test project", null, null, ColumnTemplate.Blank);

        var result = await handler.CreateAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Columns.Count);
        Assert.Equal("To Do", result.Value.Columns[0].Name);
        Assert.Equal("Done", result.Value.Columns[1].Name);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ProjectServiceTests"
```
Expected: compile error (`ColumnTemplate` doesn't exist yet) or, if it happens to compile, `CreateAsync_DefaultTemplate_InsertsFourGeneralColumns` FAILs (still inserts 6 hardcoded columns).

- [ ] **Step 3: Create the `ColumnTemplate` enum + map**

Create `src/HydraForge.Application/Projects/ColumnTemplate.cs`:

```csharp
namespace HydraForge.Application.Projects;

public enum ColumnTemplate
{
    Software = 1,
    General = 2,
    Blank = 3,
}

public static class ColumnTemplates
{
    private static readonly IReadOnlyDictionary<ColumnTemplate, string[]> Map = new Dictionary<
        ColumnTemplate,
        string[]
    >
    {
        [ColumnTemplate.Software] = ["Backlog", "Spec-ing", "Planned", "In Dev", "In Review", "Done"],
        [ColumnTemplate.General] = ["Backlog", "In Progress", "Review", "Done"],
        [ColumnTemplate.Blank] = ["To Do", "Done"],
    };

    public static string[] Get(ColumnTemplate template) => Map[template];
}
```

- [ ] **Step 4: Wire `Template` through the command and request**

In `src/HydraForge.Application/Projects/ProjectContracts.cs`, find:

```csharp
public record CreateProjectCommand(
    Guid OwnerId,
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider
);
```

Replace with:

```csharp
public record CreateProjectCommand(
    Guid OwnerId,
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider,
    ColumnTemplate Template = ColumnTemplate.General
);
```

In `src/HydraForge.Application/Projects/ProjectModels.cs`, find:

```csharp
public record CreateProjectRequest(
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider
);
```

Replace with:

```csharp
public record CreateProjectRequest(
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider,
    ColumnTemplate Template = ColumnTemplate.General
);
```

- [ ] **Step 5: Use the template in `ProjectService.CreateAsync`**

In `src/HydraForge.Application/Projects/ProjectService.cs`, delete the hardcoded array:

```csharp
    private static readonly string[] DefaultColumnNames =
    [
        "Backlog",
        "Spec-ing",
        "Planned",
        "In Dev",
        "In Review",
        "Done",
    ];
```

Find:

```csharp
        var columns = DefaultColumnNames
            .Select(
```

Replace `DefaultColumnNames` with `ColumnTemplates.Get(cmd.Template)`:

```csharp
        var columns = ColumnTemplates
            .Get(cmd.Template)
            .Select(
```

- [ ] **Step 6: Pass `Template` from the controller**

In `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs`, find:

```csharp
        var cmd = new CreateProjectCommand(
            userId,
            request.Name,
            request.Description,
            request.GitRemoteUrl,
            request.GitProvider
        );
```

Replace with:

```csharp
        var cmd = new CreateProjectCommand(
            userId,
            request.Name,
            request.Description,
            request.GitRemoteUrl,
            request.GitProvider,
            request.Template
        );
```

- [ ] **Step 7: Keep the Columns.http smoke test on the Software template**

`src/HydraForge.Server/HttpTests/Columns.http` assumes 6 default columns (Backlog, Spec-ing, Planned, In Dev, In Review, Done) throughout its comments and downstream reorder requests. Since the default template is changing to General (4 columns), pin this smoke test to `Software` explicitly so its existing comments/assertions stay accurate. Find:

```
POST {{baseUrl}}/api/projects HTTP/1.1
Content-Type: application/json
Authorization: Bearer {{adminToken}}
X-Correlation-Id: smoke-test-col-002

{
  "description": "Project for column smoke test",
  "gitProvider": "github",
  "gitRemoteUrl": "https://github.com/test/test.git",
  "name": "Column Smoke Test Project"
}
```

Replace with:

```
POST {{baseUrl}}/api/projects HTTP/1.1
Content-Type: application/json
Authorization: Bearer {{adminToken}}
X-Correlation-Id: smoke-test-col-002

{
  "description": "Project for column smoke test",
  "gitProvider": "github",
  "gitRemoteUrl": "https://github.com/test/test.git",
  "name": "Column Smoke Test Project",
  "template": "Software"
}
```

- [ ] **Step 8: Run tests to verify they pass**

```bash
dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ProjectServiceTests"
```
Expected: PASS (all `ProjectServiceTests`, including the three template tests).

```bash
dotnet build
```
Expected: builds with no errors (confirms `ProjectsController.cs` and any other reference sites still compile).

- [ ] **Step 9: Commit**

```bash
git add src/HydraForge.Application/Projects/ColumnTemplate.cs \
        src/HydraForge.Application/Projects/ProjectContracts.cs \
        src/HydraForge.Application/Projects/ProjectModels.cs \
        src/HydraForge.Application/Projects/ProjectService.cs \
        src/HydraForge.Server/Controllers/Projects/ProjectsController.cs \
        src/HydraForge.Server/HttpTests/Columns.http \
        tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs
git commit -m "feat: add ColumnTemplate enum, default new projects to General columns"
```

---

### Task 2: Regenerate frontend API types

**Files:**
- Modify: `src/web-ui/app/types/api.d.ts` (generated — regenerate, do not hand-edit)

**Interfaces:**
- Consumes: the OpenAPI doc served by the running server at `http://localhost:5000/openapi/v1.json` (reflects Task 1's `CreateProjectRequest.Template` field).
- Produces: updated `components["schemas"]["CreateProjectRequest"]` type including a `template` field, used loosely (via `as`/literal object casts, matching this repo's existing enum-handling convention — see Task 8) by later frontend tasks.

- [ ] **Step 1: Start Postgres + MinIO**

```bash
docker compose up -d postgres minio
```
Expected: both containers report healthy (`docker compose ps`).

- [ ] **Step 2: Start the server in Development**

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HydraForge.Server &
```
Wait until the log shows `Now listening on: http://localhost:5000` (or similar). This may take ~30s on first run (EF Core migrations apply).

- [ ] **Step 3: Regenerate the types**

```bash
cd src/web-ui && pnpm run generate:api-types && cd -
```
Expected: command exits 0, `app/types/api.d.ts` is rewritten.

- [ ] **Step 4: Verify the new field is present**

```bash
grep -A 6 'CreateProjectRequest:' src/web-ui/app/types/api.d.ts
```
Expected: the `template` field appears in the `CreateProjectRequest` schema block. Note: due to a known quirk in this repo (see `BoardColumn.vue`'s comment on `CardType`), enum fields may be typed as `number` here even though the server actually serializes/deserializes them as PascalCase strings (`JsonStringEnumConverter` registered globally in `Program.cs`) — this is expected and does not block Task 8, which sends literal string values the same way `card-type.ts` already does for `CardType`.

- [ ] **Step 5: Stop the server**

```bash
kill %1
```
(Or `Ctrl+C` the foreground job if not backgrounded. Leave Postgres/MinIO running — later frontend tasks don't need them, but it's harmless.)

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/types/api.d.ts
git commit -m "chore: regenerate API types for ColumnTemplate"
```

---

### Task 3: Board store — column mutation actions

**Files:**
- Modify: `src/web-ui/app/stores/board.ts`
- Test: `src/web-ui/app/stores/__tests__/board.test.ts`

**Interfaces:**
- Produces: `addColumn(column: ColumnResponse): void`, `updateColumnInStore(columnId: string, updates: Partial<ColumnResponse>): void`, `removeColumnFromStore(columnId: string): void` — all exported from `useBoardStore()`. Later tasks (`useColumnManage` in Task 4) call these after successful API responses.

- [ ] **Step 1: Write the failing tests**

Add to `src/web-ui/app/stores/__tests__/board.test.ts` (inside the existing `describe('useBoardStore', ...)` block, after the existing tests):

```ts
  it('addColumn appends a column to the end', () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Backlog')]

    board.addColumn(makeColumn('col2', 'Done', 1))

    expect(board.columns.map(c => c.id)).toEqual(['col1', 'col2'])
  })

  it('updateColumnInStore merges updates into the matching column', () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Backlog')]

    board.updateColumnInStore('col1', { name: 'Renamed', color: '#ff0000' })

    expect(board.columns[0]).toMatchObject({ id: 'col1', name: 'Renamed', color: '#ff0000' })
  })

  it('removeColumnFromStore removes the column and its cards map entry', () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Backlog'), makeColumn('col2', 'Done', 1)]
    board.cardsByColumn = new Map([['col1', []], ['col2', []]])

    board.removeColumnFromStore('col1')

    expect(board.columns.map(c => c.id)).toEqual(['col2'])
    expect(board.cardsByColumn.has('col1')).toBe(false)
  })
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd src/web-ui && pnpm vitest run stores/__tests__/board.test.ts
```
Expected: FAIL — `board.addColumn is not a function` (and similarly for the other two).

- [ ] **Step 3: Implement the store actions**

In `src/web-ui/app/stores/board.ts`, find:

```ts
  function setColumnOrder(newOrder: ColumnResponse[]) {
    columns.value = newOrder
  }
```

Add after it:

```ts
  function addColumn(column: ColumnResponse) {
    columns.value = [...columns.value, column]
  }

  function updateColumnInStore(columnId: string, updates: Partial<ColumnResponse>) {
    columns.value = columns.value.map(c => (c.id === columnId ? { ...c, ...updates } : c))
  }

  function removeColumnFromStore(columnId: string) {
    columns.value = columns.value.filter(c => c.id !== columnId)
    cardsByColumn.value.delete(columnId)
  }
```

Find the `return { ... }` block:

```ts
  return {
    project, columns, cardsByColumn, loading, error,
    fetchBoard, moveCard, rollbackMove, addCard, updateCard, removeCard, setColumnOrder,
    boardFilters, visibleColumns,
    members, fetchMembers,
    selectedCardIds, selectedCount, toggleSelectCard, clearSelection
  }
```

Replace with:

```ts
  return {
    project, columns, cardsByColumn, loading, error,
    fetchBoard, moveCard, rollbackMove, addCard, updateCard, removeCard, setColumnOrder,
    addColumn, updateColumnInStore, removeColumnFromStore,
    boardFilters, visibleColumns,
    members, fetchMembers,
    selectedCardIds, selectedCount, toggleSelectCard, clearSelection
  }
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd src/web-ui && pnpm vitest run stores/__tests__/board.test.ts
```
Expected: PASS (all tests in the file, including the three new ones).

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/stores/board.ts src/web-ui/app/stores/__tests__/board.test.ts
git commit -m "feat(web): add column mutation actions to board store"
```

---

### Task 4: `useColumnManage` composable

**Files:**
- Create: `src/web-ui/app/composables/useColumnManage.ts`
- Test: `src/web-ui/app/composables/__tests__/useColumnManage.test.ts`

**Interfaces:**
- Consumes: `ApiRoutes.Columns.create/update/delete(projectId, columnId?)` from `~/lib/routes`; `useApi()`; `useAppToast()`; `useBoardStore().addColumn/updateColumnInStore/removeColumnFromStore` (Task 3).
- Produces: `useColumnManage(projectId: string)` returning `{ createColumn(name: string, color: string | null, wipLimit: number | null): Promise<boolean>, updateColumn(columnId: string, name: string, color: string | null, wipLimit: number | null): Promise<boolean>, deleteColumn(columnId: string): Promise<boolean>, saving: Ref<boolean> }`. Booleans indicate success (`false` on caught error, already toasted). Consumed by Task 6 (`BoardView.vue`) and Task 7 (`ColumnManageModal.vue`).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/composables/__tests__/useColumnManage.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { ApiError } from '~/lib/api-error'

const mockPOST = vi.fn()
const mockPUT = vi.fn()
const mockDELETE = vi.fn()
const mockToastError = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: mockPUT,
  DELETE: mockDELETE
}))

mockNuxtImport('useAppToast', () => () => ({
  success: vi.fn(),
  error: mockToastError,
  remove: vi.fn(),
  clear: vi.fn()
}))

describe('useColumnManage', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockPOST.mockReset()
    mockPUT.mockReset()
    mockDELETE.mockReset()
    mockToastError.mockReset()
  })

  it('createColumn adds the returned column to the store on success', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    const { useBoardStore } = await import('~/stores/board')
    const newColumn = { id: 'col2', name: 'Done', position: 1, wipLimit: null, color: null }
    mockPOST.mockResolvedValue({ data: newColumn, error: undefined })

    const board = useBoardStore()
    const { createColumn } = useColumnManage('p1')
    const ok = await createColumn('Done', null, null)

    expect(ok).toBe(true)
    expect(board.columns).toContainEqual(newColumn)
  })

  it('createColumn toasts and returns false on failure', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    mockPOST.mockRejectedValue(new ApiError(400, 'VALIDATION_ERROR', 'Bad Request', 'Name required', 'about:blank', 'corr-1'))

    const { createColumn } = useColumnManage('p1')
    const ok = await createColumn('', null, null)

    expect(ok).toBe(false)
    expect(mockToastError).toHaveBeenCalledWith('Bad Request')
  })

  it('deleteColumn removes the column from the store on success', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    const { useBoardStore } = await import('~/stores/board')
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })

    const board = useBoardStore()
    board.columns = [{ id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null }]
    const { deleteColumn } = useColumnManage('p1')
    const ok = await deleteColumn('col1')

    expect(ok).toBe(true)
    expect(board.columns).toEqual([])
  })

  it('deleteColumn toasts the server message and returns false when the column has cards', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    mockDELETE.mockRejectedValue(new ApiError(400, 'COLUMN_DELETE_NON_EMPTY', 'Cannot delete column with cards.', null, 'about:blank', 'corr-2'))

    const { deleteColumn } = useColumnManage('p1')
    const ok = await deleteColumn('col1')

    expect(ok).toBe(false)
    expect(mockToastError).toHaveBeenCalledWith('Cannot delete column with cards.')
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd src/web-ui && pnpm vitest run composables/__tests__/useColumnManage.test.ts
```
Expected: FAIL — cannot find module `~/composables/useColumnManage`.

- [ ] **Step 3: Implement the composable**

Create `src/web-ui/app/composables/useColumnManage.ts`:

```ts
import { useBoardStore } from '~/stores/board'
import { ApiRoutes } from '~/lib/routes'
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']

export function useColumnManage(projectId: string) {
  const boardStore = useBoardStore()
  const api = useApi()
  const toast = useAppToast()
  const saving = ref(false)

  async function createColumn(name: string, color: string | null, wipLimit: number | null): Promise<boolean> {
    saving.value = true
    try {
      const { data } = await api.POST(ApiRoutes.Columns.create(projectId), {
        body: { name, color, wipLimit }
      })
      boardStore.addColumn(data as ColumnResponse)
      return true
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Failed to create column'
      toast.error(message)
      return false
    } finally {
      saving.value = false
    }
  }

  async function updateColumn(columnId: string, name: string, color: string | null, wipLimit: number | null): Promise<boolean> {
    saving.value = true
    try {
      const { data } = await api.PUT(ApiRoutes.Columns.update(projectId, columnId), {
        body: { name, color, wipLimit }
      })
      boardStore.updateColumnInStore(columnId, data as ColumnResponse)
      return true
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Failed to update column'
      toast.error(message)
      return false
    } finally {
      saving.value = false
    }
  }

  async function deleteColumn(columnId: string): Promise<boolean> {
    saving.value = true
    try {
      await api.DELETE(ApiRoutes.Columns.delete(projectId, columnId))
      boardStore.removeColumnFromStore(columnId)
      return true
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Failed to delete column'
      toast.error(message)
      return false
    } finally {
      saving.value = false
    }
  }

  return { createColumn, updateColumn, deleteColumn, saving }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd src/web-ui && pnpm vitest run composables/__tests__/useColumnManage.test.ts
```
Expected: PASS (all 4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/composables/useColumnManage.ts src/web-ui/app/composables/__tests__/useColumnManage.test.ts
git commit -m "feat(web): add useColumnManage composable for column CRUD"
```

---

### Task 5: Desktop — `ColumnHeader.vue` edit/delete UI

**Files:**
- Modify: `src/web-ui/app/components/board/ColumnHeader.vue`
- Test: `src/web-ui/app/components/board/__tests__/ColumnHeader.test.ts`

**Interfaces:**
- Produces: two new emits on `ColumnHeader` — `'update-column': [name: string, color: string | null, wipLimit: number | null]` and `'delete-column': []`. Consumed by Task 6 (`BoardColumn.vue` passthrough).
- Consumes: `ConfirmDialog` (`~/components/shared/ConfirmDialog.vue`, existing), `onClickOutside` from `@vueuse/core` (existing dependency, already used in `BoardMobileList.vue`).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/board/__tests__/ColumnHeader.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ColumnHeader from '~/components/board/ColumnHeader.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

const makeColumn = (overrides = {}) => ({
  id: 'col1',
  name: 'Backlog',
  position: 0,
  wipLimit: null,
  color: null,
  ...overrides
})

describe('ColumnHeader edit/delete', () => {
  it('emits update-column with the edited name on save', async () => {
    const wrapper = await mountSuspended(ColumnHeader, {
      props: { column: makeColumn(), cardCount: 2, includeArchived: false },
      global: { stubs: { ConfirmDialog } }
    })

    await wrapper.find('[data-testid="column-edit-trigger"]').trigger('click')
    await wrapper.find('[data-testid="column-name-input"]').setValue('Renamed')
    await wrapper.find('[data-testid="column-save-trigger"]').trigger('click')

    expect(wrapper.emitted('update-column')?.[0]).toEqual(['Renamed', null, null])
  })

  it('emits delete-column after the delete confirm dialog is confirmed', async () => {
    const wrapper = await mountSuspended(ColumnHeader, {
      props: { column: makeColumn(), cardCount: 0, includeArchived: false },
      global: { stubs: { ConfirmDialog } }
    })

    await wrapper.find('[data-testid="column-edit-trigger"]').trigger('click')
    await wrapper.find('[data-testid="column-delete-trigger"]').trigger('click')
    await (wrapper.vm as any).confirmDelete()

    expect(wrapper.emitted('delete-column')).toBeTruthy()
  })

  it('hides the edit trigger when readonly', async () => {
    const wrapper = await mountSuspended(ColumnHeader, {
      props: { column: makeColumn(), cardCount: 0, includeArchived: false, readonly: true }
    })
    expect(wrapper.find('[data-testid="column-edit-trigger"]').exists()).toBe(false)
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd src/web-ui && pnpm vitest run components/board/__tests__/ColumnHeader.test.ts
```
Expected: FAIL — `[data-testid="column-edit-trigger"]` not found.

- [ ] **Step 3: Implement the edit/delete UI**

In `src/web-ui/app/components/board/ColumnHeader.vue`, replace the `<script setup>` block's imports and add new emits/state. Find:

```ts
<script setup lang="ts">
import type { components } from '~/types/api'
import { CARD_TYPE_FILTER_OPTIONS } from '~/lib/card-type'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  column: ColumnResponse
  cardCount: number
  includeArchived: boolean
  readonly?: boolean
  canMoveLeft?: boolean
  canMoveRight?: boolean
}>()

const isDragging = ref(false)

const emit = defineEmits<{
  'add-card': []
  'filter-type': [value: string | null]
  'filter-archived': [value: boolean]
  'reorder': [draggedColumnId: string, targetColumnId: string]
  'move-left': []
  'move-right': []
}>()
```

Replace with:

```ts
<script setup lang="ts">
import type { components } from '~/types/api'
import { CARD_TYPE_FILTER_OPTIONS } from '~/lib/card-type'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { onClickOutside } from '@vueuse/core'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  column: ColumnResponse
  cardCount: number
  includeArchived: boolean
  readonly?: boolean
  canMoveLeft?: boolean
  canMoveRight?: boolean
}>()

const isDragging = ref(false)

const emit = defineEmits<{
  'add-card': []
  'filter-type': [value: string | null]
  'filter-archived': [value: boolean]
  'reorder': [draggedColumnId: string, targetColumnId: string]
  'move-left': []
  'move-right': []
  'update-column': [name: string, color: string | null, wipLimit: number | null]
  'delete-column': []
}>()
```

Find the end of the drag handlers (before `</script>`):

```ts
function handleDrop(event: DragEvent) {
  if (!event.dataTransfer) return
  const draggedColumnId = event.dataTransfer.getData('text/plain')
  if (draggedColumnId === props.column.id) return
  emit('reorder', draggedColumnId, props.column.id)
}
</script>
```

Replace with:

```ts
function handleDrop(event: DragEvent) {
  if (!event.dataTransfer) return
  const draggedColumnId = event.dataTransfer.getData('text/plain')
  if (draggedColumnId === props.column.id) return
  emit('reorder', draggedColumnId, props.column.id)
}

const showEdit = ref(false)
const editPanelRef = ref<HTMLElement | null>(null)
const editName = ref(props.column.name)
const editColor = ref(props.column.color ?? '#94a3b8')
const editWipLimitStr = ref(props.column.wipLimit != null ? String(props.column.wipLimit) : '')
const showDeleteConfirm = ref(false)

onClickOutside(editPanelRef, () => { showEdit.value = false })

function openEdit() {
  editName.value = props.column.name
  editColor.value = props.column.color ?? '#94a3b8'
  editWipLimitStr.value = props.column.wipLimit != null ? String(props.column.wipLimit) : ''
  showEdit.value = true
}

function saveEdit() {
  if (!editName.value.trim()) return
  const parsed = editWipLimitStr.value.trim() === '' ? null : Number(editWipLimitStr.value)
  const wipLimit = parsed !== null && !Number.isNaN(parsed) && parsed > 0 ? parsed : null
  emit('update-column', editName.value.trim(), editColor.value || null, wipLimit)
  showEdit.value = false
}

function confirmDelete() {
  emit('delete-column')
  showDeleteConfirm.value = false
}
</script>
```

Now the template. Find:

```html
      <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 truncate">
        {{ column.name }}
      </h3>
      <span
        class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5 shrink-0"
      >
        {{ cardCount }}
      </span>
      <UButton
        v-if="canMoveLeft && !readonly"
```

Replace with (inserting the edit trigger + panel between the card count and the move-left button):

```html
      <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 truncate">
        {{ column.name }}
      </h3>
      <span
        class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5 shrink-0"
      >
        {{ cardCount }}
      </span>
      <div
        v-if="!readonly"
        ref="editPanelRef"
        class="relative shrink-0"
      >
        <button
          class="text-gray-300 hover:text-gray-500 opacity-0 group-hover:opacity-100 transition-opacity"
          title="Edit column"
          data-testid="column-edit-trigger"
          @click.stop="showEdit ? (showEdit = false) : openEdit()"
        >
          <UIcon
            name="i-lucide-settings"
            class="size-4"
          />
        </button>
        <div
          v-if="showEdit"
          class="absolute z-20 top-full right-0 mt-1 w-56 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-md shadow-lg p-3 space-y-2"
          @mousedown.stop
        >
          <label class="block text-xs text-gray-500">
            Name
            <input
              v-model="editName"
              data-testid="column-name-input"
              class="mt-0.5 w-full px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
            >
          </label>
          <label class="flex items-center justify-between text-xs text-gray-500">
            Color
            <input
              v-model="editColor"
              type="color"
              class="h-6 w-10 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
            >
          </label>
          <label class="block text-xs text-gray-500">
            WIP limit
            <input
              v-model="editWipLimitStr"
              type="number"
              min="0"
              class="mt-0.5 w-full px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
            >
          </label>
          <div class="flex items-center justify-between pt-1">
            <button
              class="text-xs text-red-500 hover:text-red-600"
              data-testid="column-delete-trigger"
              @click="showDeleteConfirm = true"
            >
              Delete
            </button>
            <UButton
              size="xs"
              data-testid="column-save-trigger"
              @click="saveEdit"
            >
              Save
            </UButton>
          </div>
        </div>
      </div>
      <UButton
        v-if="canMoveLeft && !readonly"
```

Finally, find the end of the template (the closing `</div>` before `</template>`):

```html
    <!-- Row 3: inline search slot -->
    <slot name="filter-row" />
  </div>
</template>
```

Replace with:

```html
    <!-- Row 3: inline search slot -->
    <slot name="filter-row" />

    <ConfirmDialog
      :open="showDeleteConfirm"
      title="Delete column"
      :message="`Delete '${column.name}'? This only works if the column has no cards.`"
      confirm-text="Delete"
      confirm-color="error"
      @update:open="showDeleteConfirm = $event"
      @confirm="confirmDelete"
    />
  </div>
</template>
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd src/web-ui && pnpm vitest run components/board/__tests__/ColumnHeader.test.ts
```
Expected: PASS (all 3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/board/ColumnHeader.vue src/web-ui/app/components/board/__tests__/ColumnHeader.test.ts
git commit -m "feat(web): add rename/color/WIP/delete UI to desktop column header"
```

---

### Task 6: Desktop — wire edit/delete + "Add Column" through `BoardColumn.vue` / `BoardView.vue`

**Files:**
- Modify: `src/web-ui/app/components/board/BoardColumn.vue`
- Modify: `src/web-ui/app/components/board/BoardView.vue`

**Interfaces:**
- Consumes: `ColumnHeader`'s `update-column`/`delete-column` emits (Task 5); `useColumnManage` (Task 4).
- Produces: `BoardColumn` emits `'update-column': [columnId: string, name: string, color: string | null, wipLimit: number | null]` and `'delete-column': [columnId: string]`, consumed by `BoardView.vue`.

- [ ] **Step 1: Passthrough emits in `BoardColumn.vue`**

In `src/web-ui/app/components/board/BoardColumn.vue`, find:

```ts
const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'add-card': [columnId: string]
  'reorder': [draggedColumnId: string, targetColumnId: string]
  'move-left': []
  'move-right': []
}>()
```

Replace with:

```ts
const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'add-card': [columnId: string]
  'reorder': [draggedColumnId: string, targetColumnId: string]
  'move-left': []
  'move-right': []
  'update-column': [columnId: string, name: string, color: string | null, wipLimit: number | null]
  'delete-column': [columnId: string]
}>()
```

Find the `<ColumnHeader ...>` opening tag's event bindings:

```html
      @reorder="(a: string, b: string) => emit('reorder', a, b)"
      @move-left="emit('move-left')"
      @move-right="emit('move-right')"
    >
```

Replace with:

```html
      @reorder="(a: string, b: string) => emit('reorder', a, b)"
      @move-left="emit('move-left')"
      @move-right="emit('move-right')"
      @update-column="(name: string, color: string | null, wipLimit: number | null) => emit('update-column', column.id, name, color, wipLimit)"
      @delete-column="emit('delete-column', column.id)"
    >
```

- [ ] **Step 2: Wire `useColumnManage` + "Add Column" in `BoardView.vue`**

In `src/web-ui/app/components/board/BoardView.vue`, find:

```ts
import type { components } from '~/types/api'
import BoardColumn from '~/components/board/BoardColumn.vue'
import { useColumnReorder } from '~/composables/useColumnReorder'
```

Replace with:

```ts
import type { components } from '~/types/api'
import BoardColumn from '~/components/board/BoardColumn.vue'
import { useColumnReorder } from '~/composables/useColumnReorder'
import { useColumnManage } from '~/composables/useColumnManage'
```

Find:

```ts
const { reorderColumns, moveColumnLeft, moveColumnRight } = useColumnReorder(props.projectId)

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}
</script>
```

Replace with:

```ts
const { reorderColumns, moveColumnLeft, moveColumnRight } = useColumnReorder(props.projectId)
const { createColumn, updateColumn, deleteColumn, saving } = useColumnManage(props.projectId)

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}

const showAddColumn = ref(false)
const newColumnName = ref('')

async function handleAddColumn() {
  if (!newColumnName.value.trim()) return
  const ok = await createColumn(newColumnName.value.trim(), null, null)
  if (ok) {
    newColumnName.value = ''
    showAddColumn.value = false
  }
}
</script>
```

Find the template:

```html
<template>
  <div class="flex gap-4 pb-4 flex-1 min-h-0">
    <BoardColumn
      v-for="(col, idx) in columns"
      :key="col.id"
      :column="col"
      :cards="cardsByColumn.get(col.id) ?? []"
      :project-id="projectId"
      :include-archived="includeArchived"
      :readonly="readonly"
      :can-move-left="idx > 0"
      :can-move-right="idx < columns.length - 1"
      @card-move="handleCardMove"
      @card-click="handleCardClick"
      @add-card="(colId: string) => emit('add-card', colId)"
      @reorder="reorderColumns"
      @move-left="() => moveColumnLeft(col.id)"
      @move-right="() => moveColumnRight(col.id)"
    />
  </div>
</template>
```

Replace with:

```html
<template>
  <div class="flex gap-4 pb-4 flex-1 min-h-0">
    <BoardColumn
      v-for="(col, idx) in columns"
      :key="col.id"
      :column="col"
      :cards="cardsByColumn.get(col.id) ?? []"
      :project-id="projectId"
      :include-archived="includeArchived"
      :readonly="readonly"
      :can-move-left="idx > 0"
      :can-move-right="idx < columns.length - 1"
      @card-move="handleCardMove"
      @card-click="handleCardClick"
      @add-card="(colId: string) => emit('add-card', colId)"
      @reorder="reorderColumns"
      @move-left="() => moveColumnLeft(col.id)"
      @move-right="() => moveColumnRight(col.id)"
      @update-column="(columnId: string, name: string, color: string | null, wipLimit: number | null) => updateColumn(columnId, name, color, wipLimit)"
      @delete-column="(columnId: string) => deleteColumn(columnId)"
    />

    <div
      v-if="!readonly"
      class="shrink-0 w-[220px]"
      data-testid="add-column-widget"
    >
      <UButton
        v-if="!showAddColumn"
        variant="ghost"
        icon="i-lucide-plus"
        @click="showAddColumn = true"
      >
        Add Column
      </UButton>
      <div
        v-else
        class="flex flex-col gap-2 p-2 bg-gray-50 dark:bg-gray-900 rounded-lg"
      >
        <input
          v-model="newColumnName"
          placeholder="Column name"
          data-testid="new-column-name-input"
          class="px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
          @keyup.enter="handleAddColumn"
        >
        <div class="flex gap-2">
          <UButton
            size="xs"
            :loading="saving"
            data-testid="new-column-save-trigger"
            @click="handleAddColumn"
          >
            Add
          </UButton>
          <UButton
            size="xs"
            variant="ghost"
            @click="showAddColumn = false; newColumnName = ''"
          >
            Cancel
          </UButton>
        </div>
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 3: Manual verification (no existing `BoardView` test file to extend — verify via typecheck + the running app)**

```bash
cd src/web-ui && pnpm run typecheck
```
Expected: no new type errors.

Then run the dev stack (`docker compose up -d postgres minio`, `dotnet run --project src/HydraForge.Server` in Development, `cd src/web-ui && pnpm dev`) and in a browser: open a project board, hover a column header, click the gear icon, rename the column, click Save — column renames on the board. Click "Add Column" at the end of the row, type a name, click Add — new column appears. Open the gear on an empty column and click Delete, confirm — column disappears. Try deleting a non-empty column — error toast appears ("Cannot delete column with cards.") and column remains.

- [ ] **Step 4: Commit**

```bash
git add src/web-ui/app/components/board/BoardColumn.vue src/web-ui/app/components/board/BoardView.vue
git commit -m "feat(web): wire column edit/delete/add into desktop board view"
```

---

### Task 7: Mobile — `ColumnManageModal.vue`

**Files:**
- Create: `src/web-ui/app/components/board/ColumnManageModal.vue`
- Modify: `src/web-ui/app/components/board/BoardMobileList.vue`
- Test: `src/web-ui/app/components/board/__tests__/ColumnManageModal.test.ts`

**Interfaces:**
- Consumes: `useColumnManage` (Task 4), `useColumnReorder` (existing), `AppModal`, `ConfirmDialog` (existing shared components).
- Produces: `ColumnManageModal` component with props `{ open: boolean, projectId: string, columns: ColumnResponse[] }` and emit `'update:open': [boolean]`.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/board/__tests__/ColumnManageModal.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import ColumnManageModal from '~/components/board/ColumnManageModal.vue'

const mockPOST = vi.fn()
const mockPUT = vi.fn()
const mockDELETE = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: mockPUT,
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const columns = [
  { id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null },
  { id: 'col2', name: 'Done', position: 1, wipLimit: null, color: null }
]

describe('ColumnManageModal', () => {
  beforeEach(() => {
    mockPOST.mockReset()
    mockPUT.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
  })

  it('renders all columns by name', async () => {
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })
    expect(wrapper.text()).toContain('Backlog')
    expect(wrapper.text()).toContain('Done')
  })

  it('renames a column via PUT and shows it updated', async () => {
    mockPUT.mockResolvedValue({ data: { id: 'col1', name: 'Renamed', position: 0, wipLimit: null, color: null }, error: undefined })
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })

    await wrapper.find('[data-testid="edit-col1"]').trigger('click')
    await wrapper.find('[data-testid="edit-name-col1"]').setValue('Renamed')
    await wrapper.find('[data-testid="save-col1"]').trigger('click')
    await flushPromises()

    expect(mockPUT).toHaveBeenCalledWith('/api/projects/p1/Columns/col1', {
      body: { name: 'Renamed', color: null, wipLimit: null }
    })
  })

  it('deletes a column via DELETE after confirming', async () => {
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })

    await wrapper.find('[data-testid="delete-col2"]').trigger('click')
    await (wrapper.vm as any).confirmDelete()
    await flushPromises()

    expect(mockDELETE).toHaveBeenCalledWith('/api/projects/p1/Columns/col2')
  })

  it('creates a new column via POST', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'col3', name: 'New Col', position: 2, wipLimit: null, color: null }, error: undefined })
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })

    await wrapper.find('[data-testid="new-column-name"]').setValue('New Col')
    await wrapper.find('[data-testid="new-column-add"]').trigger('click')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith('/api/projects/p1/Columns', {
      body: { name: 'New Col', color: null, wipLimit: null }
    })
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd src/web-ui && pnpm vitest run components/board/__tests__/ColumnManageModal.test.ts
```
Expected: FAIL — cannot find module `~/components/board/ColumnManageModal.vue`.

- [ ] **Step 3: Implement `ColumnManageModal.vue`**

Create `src/web-ui/app/components/board/ColumnManageModal.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { useColumnManage } from '~/composables/useColumnManage'
import { useColumnReorder } from '~/composables/useColumnReorder'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import AppModal from '~/components/shared/AppModal.vue'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  open: boolean
  projectId: string
  columns: ColumnResponse[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { createColumn, updateColumn, deleteColumn, saving } = useColumnManage(props.projectId)
const { moveColumnLeft, moveColumnRight } = useColumnReorder(props.projectId)

const editingId = ref<string | null>(null)
const editName = ref('')
const editColor = ref('#94a3b8')
const editWipLimitStr = ref('')
const deleteTargetId = ref<string | null>(null)
const newColumnName = ref('')

function startEdit(column: ColumnResponse) {
  editingId.value = column.id
  editName.value = column.name
  editColor.value = column.color ?? '#94a3b8'
  editWipLimitStr.value = column.wipLimit != null ? String(column.wipLimit) : ''
}

function cancelEdit() {
  editingId.value = null
}

async function saveEdit() {
  if (!editingId.value || !editName.value.trim()) return
  const parsed = editWipLimitStr.value.trim() === '' ? null : Number(editWipLimitStr.value)
  const wipLimit = parsed !== null && !Number.isNaN(parsed) && parsed > 0 ? parsed : null
  const ok = await updateColumn(editingId.value, editName.value.trim(), editColor.value || null, wipLimit)
  if (ok) editingId.value = null
}

async function confirmDelete() {
  if (!deleteTargetId.value) return
  await deleteColumn(deleteTargetId.value)
  deleteTargetId.value = null
}

async function addColumn() {
  if (!newColumnName.value.trim()) return
  const ok = await createColumn(newColumnName.value.trim(), null, null)
  if (ok) newColumnName.value = ''
}
</script>

<template>
  <AppModal
    :open="open"
    title="Manage Columns"
    width="sm:max-w-md"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <div class="space-y-2 p-4">
        <div
          v-for="(column, idx) in columns"
          :key="column.id"
          class="border border-gray-200 dark:border-gray-700 rounded-md p-2"
        >
          <div
            v-if="editingId !== column.id"
            class="flex items-center gap-2"
          >
            <div
              v-if="column.color"
              class="size-3 rounded-full shrink-0"
              :style="{ backgroundColor: column.color }"
            />
            <span class="flex-1 text-sm truncate">{{ column.name }}</span>
            <UButton
              icon="i-lucide-chevron-up"
              size="xs"
              variant="ghost"
              :disabled="idx === 0"
              @click="moveColumnLeft(column.id)"
            />
            <UButton
              icon="i-lucide-chevron-down"
              size="xs"
              variant="ghost"
              :disabled="idx === columns.length - 1"
              @click="moveColumnRight(column.id)"
            />
            <UButton
              icon="i-lucide-pencil"
              size="xs"
              variant="ghost"
              :data-testid="`edit-${column.id}`"
              @click="startEdit(column)"
            />
            <UButton
              icon="i-lucide-trash-2"
              size="xs"
              variant="ghost"
              color="error"
              :data-testid="`delete-${column.id}`"
              @click="deleteTargetId = column.id"
            />
          </div>
          <div
            v-else
            class="space-y-2"
          >
            <input
              v-model="editName"
              :data-testid="`edit-name-${column.id}`"
              class="w-full px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
            >
            <div class="flex items-center gap-2">
              <input
                v-model="editColor"
                type="color"
                class="h-7 w-10 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
              >
              <input
                v-model="editWipLimitStr"
                type="number"
                min="0"
                placeholder="WIP limit"
                class="flex-1 px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
              >
            </div>
            <div class="flex justify-end gap-2">
              <UButton
                size="xs"
                variant="ghost"
                @click="cancelEdit"
              >
                Cancel
              </UButton>
              <UButton
                size="xs"
                :loading="saving"
                :data-testid="`save-${column.id}`"
                @click="saveEdit"
              >
                Save
              </UButton>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-2 pt-2">
          <input
            v-model="newColumnName"
            placeholder="New column name"
            data-testid="new-column-name"
            class="flex-1 px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
            @keyup.enter="addColumn"
          >
          <UButton
            size="xs"
            :loading="saving"
            data-testid="new-column-add"
            @click="addColumn"
          >
            Add
          </UButton>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :open="!!deleteTargetId"
      title="Delete column"
      message="Delete this column? This only works if the column has no cards."
      confirm-text="Delete"
      confirm-color="error"
      @update:open="(v: boolean) => { if (!v) deleteTargetId = null }"
      @confirm="confirmDelete"
    />
  </AppModal>
</template>
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd src/web-ui && pnpm vitest run components/board/__tests__/ColumnManageModal.test.ts
```
Expected: PASS (all 4 tests).

- [ ] **Step 5: Wire the modal into `BoardMobileList.vue`**

Find:

```html
      <UButton
        v-if="!readonly"
        size="sm"
        type="button"
        @click="emit('add-card')"
      >
        <UIcon
          name="i-lucide-plus"
          class="mr-2 hidden sm:inline-block"
        />
        <span>Add card</span>
      </UButton>
    </div>
```

Replace with:

```html
      <UButton
        v-if="!readonly"
        variant="ghost"
        size="sm"
        data-testid="mobile-manage-columns-btn"
        @click="showColumnManage = true"
      >
        Columns
      </UButton>
      <UButton
        v-if="!readonly"
        size="sm"
        type="button"
        @click="emit('add-card')"
      >
        <UIcon
          name="i-lucide-plus"
          class="mr-2 hidden sm:inline-block"
        />
        <span>Add card</span>
      </UButton>
    </div>
```

Find the script's ref declarations, e.g.:

```ts
const showFilters = ref(false)
```

Add after it:

```ts
const showColumnManage = ref(false)
```

Add the import near the other component imports:

```ts
import ColumnManageModal from '~/components/board/ColumnManageModal.vue'
```

Find the end of the template (before the final `</template>`):

```html
    <ConfirmDialog
      v-model:open="showBulkArchiveConfirm"
      title="Archive selected cards"
      :message="`Archive ${board.selectedCount} selected card(s)?`"
      confirm-text="Archive"
      @confirm="archiveSelectedConfirmed"
    />
  </div>
</template>
```

Replace with:

```html
    <ConfirmDialog
      v-model:open="showBulkArchiveConfirm"
      title="Archive selected cards"
      :message="`Archive ${board.selectedCount} selected card(s)?`"
      confirm-text="Archive"
      @confirm="archiveSelectedConfirmed"
    />

    <ColumnManageModal
      v-if="showColumnManage"
      v-model:open="showColumnManage"
      :project-id="projectId"
      :columns="columns"
    />
  </div>
</template>
```

- [ ] **Step 6: Run the existing mobile list test to confirm no regression**

```bash
cd src/web-ui && pnpm vitest run components/board/__tests__/BoardMobileList.test.ts
```
Expected: PASS (unchanged — new button/modal don't affect existing assertions).

- [ ] **Step 7: Commit**

```bash
git add src/web-ui/app/components/board/ColumnManageModal.vue \
        src/web-ui/app/components/board/__tests__/ColumnManageModal.test.ts \
        src/web-ui/app/components/board/BoardMobileList.vue
git commit -m "feat(web): add mobile Manage Columns modal"
```

---

### Task 8: Project creation — column template picker

**Files:**
- Modify: `src/web-ui/app/components/project/ProjectCreateModal.vue`
- Test: `src/web-ui/app/components/project/__tests__/ProjectCreateModal.test.ts`

**Interfaces:**
- Produces: nothing consumed by later tasks — this is the last task. Sends `template: 'Software' | 'General' | 'Blank'` (string literal, matching the `CardType` string-enum convention documented in `~/lib/card-type.ts`, not the raw generated TS type — see Task 2's note) as part of the create-project POST body.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/project/__tests__/ProjectCreateModal.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import ProjectCreateModal from '~/components/project/ProjectCreateModal.vue'

const mockPOST = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

describe('ProjectCreateModal template selection', () => {
  beforeEach(() => {
    mockPOST.mockReset()
  })

  it('defaults to General and sends it on submit', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'p1' }, error: undefined })
    const wrapper = await mountSuspended(ProjectCreateModal, {
      props: { open: true },
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', {}, [this.$slots.body?.(), this.$slots.footer?.()])
            }
          }
        }
      }
    })
    await flushPromises()

    await wrapper.find('input').setValue('New Project')
    await wrapper.findAll('button').find(b => b.text() === 'Create')!.trigger('click')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith('/api/Projects', expect.objectContaining({
      body: expect.objectContaining({ template: 'General' })
    }))
  })

  it('sends the selected template', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'p1' }, error: undefined })
    const wrapper = await mountSuspended(ProjectCreateModal, {
      props: { open: true },
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', {}, [this.$slots.body?.(), this.$slots.footer?.()])
            }
          }
        }
      }
    })
    await flushPromises()

    await wrapper.find('input').setValue('New Project');
    (wrapper.vm as any).columnTemplate = 'Software'
    await wrapper.findAll('button').find(b => b.text() === 'Create')!.trigger('click')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith('/api/Projects', expect.objectContaining({
      body: expect.objectContaining({ template: 'Software' })
    }))
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd src/web-ui && pnpm vitest run components/project/__tests__/ProjectCreateModal.test.ts
```
Expected: FAIL — `body` sent does not include `template`.

- [ ] **Step 3: Add the template select**

In `src/web-ui/app/components/project/ProjectCreateModal.vue`, find:

```ts
const gitProviders = [
  { label: 'GitHub', value: 'github' },
  { label: 'GitLab', value: 'gitlab' },
  { label: 'Gitea', value: 'gitea' },
  { label: 'Self-hosted', value: 'self-hosted' }
]
```

Add after it:

```ts
const columnTemplateOptions = [
  { label: 'General', value: 'General' },
  { label: 'Software', value: 'Software' },
  { label: 'Blank', value: 'Blank' }
]
const columnTemplate = ref('General')
```

Find `resetForm`:

```ts
function resetForm() {
  name.value = ''
  description.value = ''
  gitRemoteUrl.value = ''
  gitProvider.value = undefined
  showAdvanced.value = false
  error.value = null
  selectedMembers.value = []
  searchQuery.value = ''
  searchResults.value = []
}
```

Replace with:

```ts
function resetForm() {
  name.value = ''
  description.value = ''
  gitRemoteUrl.value = ''
  gitProvider.value = undefined
  columnTemplate.value = 'General'
  showAdvanced.value = false
  error.value = null
  selectedMembers.value = []
  searchQuery.value = ''
  searchResults.value = []
}
```

Find the POST body in `handleSubmit`:

```ts
    const { data } = await api.POST(ApiRoutes.Projects.create(), {
      body: {
        name: name.value,
        description: description.value,
        gitRemoteUrl: gitRemoteUrl.value || null,
        gitProvider: gitProvider.value ?? null
      }
    })
```

Replace with:

```ts
    const { data } = await api.POST(ApiRoutes.Projects.create(), {
      body: {
        name: name.value,
        description: description.value,
        gitRemoteUrl: gitRemoteUrl.value || null,
        gitProvider: gitProvider.value ?? null,
        template: columnTemplate.value
      }
    })
```

Now the template. Find:

```html
        <UFormField
          label="Description"
          class="w-full"
        >
          <UTextarea
            v-model="description"
            placeholder="Optional description"
            class="w-full"
          />
        </UFormField>

        <!-- Members -->
```

Replace with:

```html
        <UFormField
          label="Description"
          class="w-full"
        >
          <UTextarea
            v-model="description"
            placeholder="Optional description"
            class="w-full"
          />
        </UFormField>

        <UFormField
          label="Board Columns"
          class="w-full"
        >
          <USelect
            v-model="columnTemplate"
            :items="columnTemplateOptions"
            class="w-full"
          />
        </UFormField>

        <!-- Members -->
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd src/web-ui && pnpm vitest run components/project/__tests__/ProjectCreateModal.test.ts
```
Expected: PASS (both tests).

- [ ] **Step 5: Run the full frontend test suite**

```bash
cd src/web-ui && pnpm test
```
Expected: PASS (no regressions across the whole suite — this is the last task, good point to confirm everything from Tasks 3-8 is still green together).

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/components/project/ProjectCreateModal.vue \
        src/web-ui/app/components/project/__tests__/ProjectCreateModal.test.ts
git commit -m "feat(web): add column template picker to project creation"
```

---

## Post-plan verification

After all 8 tasks:

```bash
dotnet build
dotnet test
cd src/web-ui && pnpm run typecheck && pnpm test
```

All should pass with zero regressions. Then manually verify per Task 6 Step 3 (desktop) and additionally on mobile viewport: open a board on a narrow window, tap "Columns" in the toolbar, rename/reorder/delete/add a column in the modal, confirm changes reflect on the board underneath after closing.
