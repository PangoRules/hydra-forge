# Project Management UI — Edit, Creation with Members, List Polish

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Branch:** `task/phase-3-project-management-ui`
**Parent branch:** `feat/phase-3-web-ui`

**Goal:** Complete project management UI by adding an edit modal, member assignment at creation, and a polished project list view.

**Architecture:** Backend changes are minimal — the `UpdateProject` endpoint already exists. `CreateProject` gains an `InitialMemberUsernames` field (looked up via existing `IUserRepository.FindByUsernamesAsync`). The frontend gets two new modals (`ProjectEditModal.vue`, user-search component) and a refreshed `ProjectList.vue` with richer card content.

**Tech Stack:** ASP.NET Core, Nuxt 4, Nuxt UI v4, Pinia

---

## File Map

### Backend
| File | Action | Responsibility |
|------|--------|----------------|
| `src/HydraForge.Application/Projects/ProjectModels.cs` | Modify | Add `InitialMemberUsernames` to `CreateProjectRequest` |
| `src/HydraForge.Application/Projects/ProjectContracts.cs` | Modify | Add `InitialMemberUsernames` to `CreateProjectCommand` |
| `src/HydraForge.Application/Projects/ProjectService.cs` | Modify | Inject `IUserRepository`, loop to add initial members in `CreateAsync` |
| `src/HydraForge.Application/Projects/ProjectModels.cs` | Modify | Add `ProjectDetailResponse` (for edit modal — includes current members) |

### Frontend
| File | Action | Responsibility |
|------|--------|----------------|
| `src/web-ui/app/components/project/ProjectCreateModal.vue` | Modify | Add member picker with username tag input, send `initialMemberUsernames` |
| `src/web-ui/app/components/project/ProjectEditModal.vue` | Create | Edit name/description/git fields for existing project |
| `src/web-ui/app/components/project/ProjectList.vue` | Modify | Add edit button per card, richer card content (avatars, dates, member count, archive badge) |
| `src/web-ui/app/pages/projects/index.vue` | Modify | Wire edit modal, pass project data for editing |
| `src/web-ui/app/lib/routes.ts` | Modify (check) | Verify `ApiRoutes.Projects.update` exists |
| `src/web-ui/app/types/api.d.ts` | Modify | Add `UpdateProjectRequest` if not present, add `initialMemberUsernames` to `CreateProjectRequest` |

---

### Task 1: Backend — Add `InitialMemberUsernames` to Create Project

**Files:**
- Modify: `src/HydraForge.Application/Projects/ProjectModels.cs`
- Modify: `src/HydraForge.Application/Projects/ProjectContracts.cs`
- Modify: `src/HydraForge.Application/Projects/ProjectService.cs`

**Why:** The create flow currently only adds the owner. Project creators should be able to invite existing users at creation time without a separate member-add step.

- [ ] **Step 1: Add `InitialMemberUsernames` to the request DTO**

In `src/HydraForge.Application/Projects/ProjectModels.cs`, add a new optional field to `CreateProjectRequest`:

```csharp
public record CreateProjectRequest(
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider,
    string[]? InitialMemberUsernames // new
);
```

- [ ] **Step 2: Add `InitialMemberUsernames` to the command**

In `src/HydraForge.Application/Projects/ProjectContracts.cs`, add to `CreateProjectCommand`:

```csharp
public record CreateProjectCommand(
    Guid OwnerId,
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider,
    IReadOnlyList<string> InitialMemberUsernames // new, empty list by default
);
```

- [ ] **Step 3: Update `ProjectService` to inject `IUserRepository`**

At `src/HydraForge.Application/Projects/ProjectService.cs`, add `IUserRepository` to the constructor parameters:

```csharp
public class ProjectService(
    IProjectRepository projectRepo,
    IColumnRepository columnRepo,
    IProjectMemberRepository memberRepo,
    IProjectContextSnapshotRepository snapshotRepo,
    IChatArchiveService chatArchiveService,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher,
    IAuditLogWriter auditLogWriter,
    IUserRepository userRepo // new
)
```

- [ ] **Step 4: Add initial member logic in `CreateAsync`**

In `ProjectService.CreateAsync`, after the snapshot creation (line ~87), add a bulk member-add loop:

```csharp
// Add initial members by username
if (cmd.InitialMemberUsernames.Count > 0)
{
    var usersByUsername = await userRepo.FindByUsernamesAsync(
        cmd.InitialMemberUsernames, ct);
    foreach (var username in cmd.InitialMemberUsernames)
    {
        if (!usersByUsername.TryGetValue(username, out var user))
            continue; // skip unknown usernames (no error — creator made a typo)
        var member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = user.Id,
            Role = MemberRole.Member,
            JoinedAt = DateTime.UtcNow,
        };
        await memberRepo.AddMemberAsync(member, ct);
    }
}
```

Also update the return statement to include all members (not just the owner):

```csharp
// After the initial member loop, collect all members for the DTO
var allMembers = await memberRepo.ListMembersAsync(project.Id, ct);
return Result<ProjectDto>.Success(MapToDto(project, columns, allMembers.ToList()));
```

Replace the current `return Result<ProjectDto>.Success(MapToDto(project, columns, [ownerMember]));` with the above.

- [ ] **Step 5: Register `IUserRepository` in DI (if not already)**

In `src/HydraForge.Server/Program.cs`, verify that `IUserRepository` is registered. It should already be there at line ~122:

```csharp
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
```

If missing, add it.

- [ ] **Step 6: Build and run backend tests**

```bash
dotnet build && dotnet test
```

Expected: 0 errors, all 446+ tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Application/Projects/ProjectModels.cs src/HydraForge.Application/Projects/ProjectContracts.cs src/HydraForge.Application/Projects/ProjectService.cs
git commit -m "feat(api): add initial member usernames to project creation"
```

---

### Task 2: Frontend — Project Edit Modal

**Files:**
- Create: `src/web-ui/app/components/project/ProjectEditModal.vue`
- Modify: `src/web-ui/app/components/project/ProjectList.vue`
- Modify: `src/web-ui/app/pages/projects/index.vue`
- Modify: `src/web-ui/app/lib/routes.ts` (verify `ApiRoutes.Projects.update` exists)

**Why:** The backend `PUT /api/Projects/{id}` endpoint exists but the frontend has no edit UI. Users need to update project name, description, and git details after creation.

- [ ] **Step 1: Verify route constant**

In `src/web-ui/app/lib/routes.ts`, confirm the update route exists:

```ts
update: (projectId: string) => `/api/Projects/${projectId}`,
```

If missing, add it under `ApiRoutes.Projects`.

- [ ] **Step 2: Create `ProjectEditModal.vue`**

Create `src/web-ui/app/components/project/ProjectEditModal.vue`:

```vue
<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

const props = defineProps<{
  open: boolean
  project: { id: string, name: string, description?: string | null, gitRemoteUrl?: string | null, gitProvider?: string | null }
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'updated': []
}>()

const name = ref(props.project.name)
const description = ref(props.project.description ?? '')
const gitRemoteUrl = ref(props.project.gitRemoteUrl ?? '')
const gitProvider = ref<string | undefined>(props.project.gitProvider ?? undefined)
const loading = ref(false)
const error = ref<string | null>(null)

const gitProviders = [
  { label: 'GitHub', value: 'github' },
  { label: 'GitLab', value: 'gitlab' },
  { label: 'Gitea', value: 'gitea' },
  { label: 'Self-hosted', value: 'self-hosted' }
]

const api = useApi()
const toast = useToast()

function onClose() {
  emit('update:open', false)
}

// Sync props when project changes (e.g. opening modal for a different project)
watch(() => props.project.id, () => {
  name.value = props.project.name
  description.value = props.project.description ?? ''
  gitRemoteUrl.value = props.project.gitRemoteUrl ?? ''
  gitProvider.value = props.project.gitProvider ?? undefined
  error.value = null
})

async function handleSubmit() {
  if (!name.value.trim()) return
  error.value = null
  loading.value = true
  try {
    const { error: apiError } = await api.PUT(ApiRoutes.Projects.update(props.project.id), {
      body: {
        name: name.value.trim(),
        description: description.value.trim() || null,
        gitRemoteUrl: gitRemoteUrl.value || null,
        gitProvider: gitProvider.value ?? null
      }
    })
    if (apiError) throw apiError
    toast.add({ title: 'Project updated', color: 'success' })
    emit('updated')
    onClose()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to update project'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppModal
    :open="open"
    title="Edit Project"
    :loading="loading"
    :error="error"
    width="sm:max-w-lg"
    @update:open="emit('update:open', $event)"
    @close="onClose"
  >
    <template #body>
      <form class="space-y-4 p-4" @submit.prevent="handleSubmit">
        <UFormField label="Project Name" required>
          <UInput v-model="name" placeholder="My Project" required class="w-full" />
        </UFormField>

        <UFormField label="Description" class="w-full">
          <UTextarea v-model="description" placeholder="Optional description" class="w-full" />
        </UFormField>

        <UFormField label="Git Remote URL" class="w-full">
          <UInput v-model="gitRemoteUrl" placeholder="https://github.com/user/repo.git" class="w-full" />
        </UFormField>

        <UFormField label="Git Provider" class="w-full">
          <div class="relative">
            <USelect v-model="gitProvider" :items="gitProviders" placeholder="Select provider" class="w-full" />
            <UButton
              v-if="gitProvider"
              variant="ghost"
              size="sm"
              class="absolute inset-y-0 right-6 px-2 hover:!bg-transparent text-gray-400 hover:text-gray-600"
              icon="i-lucide-x"
              @click="gitProvider = undefined"
            />
          </div>
        </UFormField>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="outline" @click="onClose">Cancel</UButton>
        <UButton type="submit" :loading="loading" :disabled="!name.trim()">Save</UButton>
      </div>
    </template>
  </AppModal>
</template>
```

- [ ] **Step 3: Update `ProjectList.vue` — add edit button**

In `src/web-ui/app/components/project/ProjectList.vue`, add an `edit` emit and a pencil button on each card:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  projects: ProjectListResponse[]
  loading: boolean
}>()

const emit = defineEmits<{
  select: [projectId: string]
  edit: [project: ProjectListResponse]
}>()
</script>

<template>
  <div v-if="loading" class="flex justify-center p-8">
    <UIcon name="i-lucide-loader" class="animate-spin size-8" />
  </div>

  <div v-else-if="projects.length === 0" class="text-center p-8 text-muted">
    <p>No projects yet. Create your first project!</p>
  </div>

  <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
    <UCard
      v-for="project in projects"
      :key="project.id"
      class="cursor-pointer hover:ring-2 hover:ring-primary transition-shadow relative"
      @click="emit('select', project.id)"
    >
      <template #header>
        <div class="flex items-center justify-between">
          <h3 class="font-semibold truncate">{{ project.name }}</h3>
          <UButton
            icon="i-lucide-pencil"
            variant="ghost"
            size="xs"
            class="shrink-0"
            @click.stop="emit('edit', project)"
          />
        </div>
      </template>
      <p class="text-sm text-muted line-clamp-2 mb-2">
        {{ project.description ?? 'No description' }}
      </p>
      <div class="flex items-center justify-between text-xs text-muted">
        <span>{{ new Date(project.createdAt).toLocaleDateString() }}</span>
        <span>{{ project.memberCount }} member{{ project.memberCount !== 1 ? 's' : '' }}</span>
      </div>
    </UCard>
  </div>
</template>
```

- [ ] **Step 4: Wire edit modal in projects page**

In `src/web-ui/app/pages/projects/index.vue`, add the edit modal and wire events:

```vue
<script setup lang="ts">
// ... existing imports ...

type ProjectListResponse = components['schemas']['ProjectListResponse']

const projects = ref<ProjectListResponse[]>([])
const loading = ref(true)
const showCreateModal = ref(false)
const showEditModal = ref(false)
const editingProject = ref<ProjectListResponse | null>(null)

// ... existing api, toast, fetchProjects, onProjectSelect ...

function onProjectEdit(project: ProjectListResponse) {
  editingProject.value = project
  showEditModal.value = true
}

function onProjectUpdated() {
  showEditModal.value = false
  editingProject.value = null
  fetchProjects()
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 max-w-6xl mx-auto">
    <!-- header ... -->

    <ProjectList
      :projects="projects"
      :loading="loading"
      @select="onProjectSelect"
      @edit="onProjectEdit"
    />

    <ProjectCreateModal
      v-model:open="showCreateModal"
      @created="onProjectCreated"
    />

    <ProjectEditModal
      v-if="editingProject"
      :open="showEditModal"
      :project="editingProject"
      @update:open="showEditModal = $event"
      @updated="onProjectUpdated"
    />
  </div>
</template>
```

- [ ] **Step 5: Run typecheck and tests**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```

Expected: 0 errors, all 67+ tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/components/project/ProjectEditModal.vue src/web-ui/app/components/project/ProjectList.vue src/web-ui/app/pages/projects/index.vue
git commit -m "feat(ui): add project edit modal with name/description/git fields"
```

---

### Task 3: Frontend — Member Picker in Create Modal

**Files:**
- Modify: `src/web-ui/app/components/project/ProjectCreateModal.vue`

**Why:** The create modal now supports `initialMemberUsernames`. Users should be able to type existing usernames to add members at project creation.

- [ ] **Step 1: Add username tag input to create modal**

In `src/web-ui/app/components/project/ProjectCreateModal.vue`, add a username tag picker below the description field:

```vue
<script setup lang="ts">
// ... existing imports ...

const name = ref('')
const description = ref('')
const gitRemoteUrl = ref('')
const gitProvider = ref<string | undefined>(undefined)
const showAdvanced = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)
const newMemberUsername = ref('')
const initialMemberUsernames = ref<string[]>([])

// ... existing methods ...

function addMemberUsername() {
  const username = newMemberUsername.value.trim()
  if (username && !initialMemberUsernames.value.includes(username)) {
    initialMemberUsernames.value.push(username)
  }
  newMemberUsername.value = ''
}

function removeMemberUsername(username: string) {
  initialMemberUsernames.value = initialMemberUsernames.value.filter(u => u !== username)
}

function resetForm() {
  name.value = ''
  description.value = ''
  gitRemoteUrl.value = ''
  gitProvider.value = undefined
  showAdvanced.value = false
  error.value = null
  initialMemberUsernames.value = []
  newMemberUsername.value = ''
}
</script>
```

In the template, add the member picker between the Description and Advanced sections:

```vue
        <!-- Members -->
        <div>
          <label class="block text-sm font-medium mb-1.5">Initial Members</label>
          <div class="flex flex-wrap gap-1.5 mb-2">
            <span
              v-for="username in initialMemberUsernames"
              :key="username"
              class="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-full bg-primary/10 text-primary"
            >
              {{ username }}
              <button class="hover:text-red-500 leading-none" @click="removeMemberUsername(username)">×</button>
            </span>
            <span v-if="initialMemberUsernames.length === 0" class="text-xs text-gray-400">None</span>
          </div>
          <div class="flex gap-2">
            <UInput
              v-model="newMemberUsername"
              placeholder="Username"
              class="flex-1"
              @keydown.enter.prevent="addMemberUsername"
            />
            <UButton variant="soft" size="sm" :disabled="!newMemberUsername.trim()" @click="addMemberUsername">
              Add
            </UButton>
          </div>
          <p class="text-xs text-muted mt-1">Type existing usernames and click Add to invite members at creation.</p>
        </div>
```

Update the `handleSubmit` body to include `initialMemberUsernames`:

```ts
const { error: apiError } = await api.POST(ApiRoutes.Projects.create(), {
  body: {
    name: name.value,
    description: description.value,
    gitRemoteUrl: gitRemoteUrl.value || null,
    gitProvider: gitProvider.value ?? null,
    initialMemberUsernames: initialMemberUsernames.value.length > 0
      ? initialMemberUsernames.value
      : null
  }
})
```

- [ ] **Step 2: Run typecheck and lint**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/web-ui/app/components/project/ProjectCreateModal.vue
git commit -m "feat(ui): add member username picker to project creation modal"
```

---

### Task 4: Frontend — Project List Polish

**Files:**
- Modify: `src/web-ui/app/components/project/ProjectList.vue`

**Why:** The current project list shows a plain card with name and description. Enhance with visual polish: archive badge, member count with avatars, relative dates, better hover states.

- [ ] **Step 1: Refine the card template**

Replace the `ProjectList.vue` template with a richer card layout. The edit button from Task 2 Step 3 stays. Add:
- Archive badge for archived projects
- Member count with avatar initials
- Relative creation date ("2 weeks ago")

```vue
<template>
  <div v-if="loading" class="flex justify-center p-8">
    <UIcon name="i-lucide-loader" class="animate-spin size-8" />
  </div>

  <div v-else-if="projects.length === 0" class="text-center p-8 text-muted">
    <p>No projects yet. Create your first project!</p>
  </div>

  <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
    <UCard
      v-for="project in projects"
      :key="project.id"
      class="cursor-pointer hover:ring-2 hover:ring-primary transition-all hover:shadow-md relative group"
      @click="emit('select', project.id)"
    >
      <template #header>
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2 min-w-0">
            <h3 class="font-semibold truncate">{{ project.name }}</h3>
            <UBadge
              v-if="project.archivedAt"
              variant="subtle"
              color="neutral"
              size="sm"
            >
              Archived
            </UBadge>
          </div>
          <UButton
            icon="i-lucide-pencil"
            variant="ghost"
            size="xs"
            class="shrink-0 opacity-0 group-hover:opacity-100 transition-opacity"
            @click.stop="emit('edit', project)"
          />
        </div>
      </template>

      <p class="text-sm text-muted line-clamp-2 mb-3">
        {{ project.description ?? 'No description' }}
      </p>

      <div class="flex items-center justify-between text-xs text-muted">
        <span class="flex items-center gap-1">
          <UIcon name="i-lucide-calendar" class="size-3" />
          {{ formatRelativeDate(project.createdAt) }}
        </span>
        <span class="flex items-center gap-1">
          <UIcon name="i-lucide-users" class="size-3" />
          {{ project.memberCount }} {{ project.memberCount === 1 ? 'member' : 'members' }}
        </span>
      </div>
    </UCard>
  </div>
</template>
```

Add a `formatRelativeDate` helper in the script:

```ts
function formatRelativeDate(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24))
  if (diffDays === 0) return 'Today'
  if (diffDays === 1) return 'Yesterday'
  if (diffDays < 30) return `${diffDays}d ago`
  const diffMonths = Math.floor(diffDays / 30)
  if (diffMonths < 12) return `${diffMonths}mo ago`
  const diffYears = Math.floor(diffMonths / 12)
  return `${diffYears}y ago`
}
```

- [ ] **Step 2: Run typecheck and tests**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```

Expected: 0 errors, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/web-ui/app/components/project/ProjectList.vue
git commit -m "feat(ui): polish project list cards with badges, member count, relative dates"
```

---

### Task 5: Update API Types

**Files:**
- Modify: `src/web-ui/app/types/api.d.ts`

**Why:** The `CreateProjectRequest` schema now includes `initialMemberUsernames`. The auto-generated types need updating.

- [ ] **Step 1: Update `CreateProjectRequest` type**

In `src/web-ui/app/types/api.d.ts`, locate `CreateProjectRequest` (around line 2552+) and add:

```ts
CreateProjectRequest: {
  name: string;
  description: string | null;
  gitRemoteUrl: string | null;
  gitProvider: string | null;
  initialMemberUsernames?: string[] | null;
}
```

Also verify `UpdateProjectRequest` exists (it should from the existing backend — add if missing):

```ts
UpdateProjectRequest: {
  name: string;
  description: string | null;
  gitRemoteUrl: string | null;
  gitProvider: string | null;
}
```

- [ ] **Step 2: Verify with typecheck**

```bash
cd src/web-ui && pnpm typecheck
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/web-ui/app/types/api.d.ts
git commit -m "fix(types): add initialMemberUsernames and UpdateProjectRequest to API types"
```

---

### Verification

```bash
dotnet build && dotnet test
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```

Expected: 0 errors, all 446+ .NET tests and 67+ web tests pass.
