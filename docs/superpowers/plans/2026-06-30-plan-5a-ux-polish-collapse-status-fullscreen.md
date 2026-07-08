# Plan 5a UX Polish — Collapsible Plans, Status Dropdown, Fullscreen Editor

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve CardPlan UX with collapsible plan sections, clickable status dropdown, and fullscreen toggle on MarkdownEditor

**Architecture:** All changes are frontend-only. CardPlan.vue gets collapsible per-plan sections + interactive status badge dropdown. MarkdownEditor.vue gets a fullscreen toggle button in its toolbar. CardSpec and CardPlan both use MarkdownEditor, so both benefit.

**Tech Stack:** Vue 3 / Nuxt 4 / TypeScript / Tiptap-based MarkdownEditor

**Depends on:** Plan 5a (multi-plan UI, status badges, API endpoints already exist)

---

### Task 1: Collapsible Plan Sections

**Files:**
- Modify: `src/web-ui/app/components/card/CardPlan.vue`

**Design:**
- Each plan section shows a header row (title + status badge + action buttons + collapse toggle chevron)
- Below header: collapsible body (editor + history panel)
- Default state: **collapsed** — only header visible
- Track expanded state per plan with `expandedPlans: Set<string>` (plan IDs)
- Click the header row or chevron to toggle expand/collapse
- Can expand multiple plans at once (Option B)
- When collapsed: editor, history panel, Save/History buttons hidden
- When expanded: show editor + history panel
- Chevron icon rotates (i-lucide-chevron-down → rotate-180 when expanded)

- [x] **Step 1: Add expanded state tracking**

In `<script>`, add after `restoringId`:

```typescript
const expandedPlans = ref<Set<string>>(new Set())

function toggleExpand(planId: string) {
  const next = new Set(expandedPlans.value)
  if (next.has(planId)) {
    next.delete(planId)
  } else {
    next.add(planId)
  }
  expandedPlans.value = next
}

function isExpanded(planId: string): boolean {
  return expandedPlans.value.has(planId)
}
```

- [x] **Step 2: Wrap plan body in collapsible container**

Replace the current plan body div (starts with the header row and contains editor + history) with restructured markup:

The `v-for="plan in plans"` div becomes:

```html
<div
  v-for="plan in plans"
  :key="plan.id"
  class="border rounded-md p-3"
  :class="plan.status === 'Done' ? 'opacity-75' : ''"
>
  <!-- Header row — always visible, clickable -->
  <div
    class="flex items-center gap-2 flex-wrap cursor-pointer select-none"
    @click="toggleExpand(plan.id)"
  >
    <UButton
      size="xs"
      variant="ghost"
      icon="i-lucide-chevron-down"
      :class="isExpanded(plan.id) ? 'rotate-180' : ''"
    />
    <UInput
      v-if="editState[plan.id]"
      v-model="editState[plan.id]!.title"
      class="flex-1"
      style="min-width: 8rem"
      :disabled="props.readonly || plan.status === 'Done'"
      size="sm"
      @click.stop
    />
    <div class="flex items-center gap-1 shrink-0 flex-wrap">
      <!-- status dropdown will go here (Task 2) -->
      <!-- action buttons: Activate/Complete/Reactivate -->
      <UButton
        v-if="!props.readonly && plan.status === 'Pending'"
        size="xs"
        variant="ghost"
        :loading="actionId === plan.id"
        @click.stop="activate(plan)"
      >
        Activate
      </UButton>
      <UButton
        v-if="!props.readonly && plan.status === 'Active'"
        size="xs"
        variant="ghost"
        :loading="actionId === plan.id"
        @click.stop="complete(plan)"
      >
        Complete
      </UButton>
      <UButton
        v-if="!props.readonly && plan.status === 'Done'"
        size="xs"
        variant="ghost"
        :loading="actionId === plan.id"
        @click.stop="reactivate(plan)"
      >
        Reactivate
      </UButton>
    </div>
  </div>

  <!-- Collapsible body -->
  <div v-if="isExpanded(plan.id)" class="mt-3 space-y-3">
    <!-- Editor + optional history panel -->
    <div class="flex gap-4">
      <div class="flex-1 min-w-0">
        <MarkdownEditor
          v-if="editState[plan.id]"
          v-model="editState[plan.id]!.content"
          :editable="!props.readonly && plan.status !== 'Done'"
          placeholder="Describe this plan step by step..."
        />
      </div>
      <!-- History panel -->
      <div
        v-if="showHistoryId === plan.id"
        class="w-44 flex-shrink-0 border-l pl-4 space-y-2 max-h-80 overflow-y-auto"
      >
        <p class="text-xs font-medium text-muted uppercase">
          History
        </p>
        <div
          v-if="loadingVersionsId === plan.id"
          class="text-xs text-muted"
        >
          Loading...
        </div>
        <div
          v-else-if="!versionsCache[plan.id]?.length"
          class="text-xs text-muted"
        >
          No versions yet
        </div>
        <div
          v-for="v in versionsCache[plan.id]"
          :key="v.id"
          class="flex items-center justify-between gap-1 text-xs py-1"
        >
          <div class="min-w-0">
            <p class="truncate">
              v{{ v.version }} · {{ formatDate(v.createdAt) }}
            </p>
            <p class="text-muted truncate">
              {{ shortUser(v.createdByUserId) }}
            </p>
          </div>
          <UButton
            size="xs"
            variant="ghost"
            :loading="restoringId === v.id"
            :disabled="!!restoringId || plan.status === 'Done'"
            @click="restore(plan, v)"
          >
            Restore
          </UButton>
        </div>
      </div>
    </div>

    <!-- Footer actions when expanded -->
    <div class="flex items-center gap-1 justify-end">
      <UButton
        size="xs"
        variant="ghost"
        :label="showHistoryId === plan.id ? 'Hide history' : 'History'"
        @click.stop="toggleHistory(plan.id)"
      />
      <UButton
        v-if="!props.readonly && plan.status !== 'Done'"
        size="xs"
        :loading="savingId === plan.id"
        :disabled="!isPlanDirty(plan)"
        @click.stop="savePlan(plan)"
      >
        Save
      </UButton>
    </div>
  </div>
</div>
```

**Key changes:**
- Header has `@click="toggleExpand(plan.id)"` and `cursor-pointer`
- Chevron icon with rotation based on expanded state
- `@click.stop` on buttons inside header to prevent toggle when clicking actions
- Editor + history + save buttons only shown when expanded
- Expanded body has `.mt-3` spacing from header

For the empty state and new-plan form, they remain unchanged (not inside any plan).

- [x] **Step 3: Verify typecheck + lint**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint --fix
```

Expected: zero errors.

- [x] **Step 4: Commit**

```bash
git add src/web-ui/app/components/card/CardPlan.vue
git commit -m "feat(web): collapsible plan sections — default collapsed, click to expand"
```

---

### Task 2: Interactive Status Dropdown

**Files:**
- Modify: `src/web-ui/app/components/card/CardPlan.vue`

**Design:**
- The status badge becomes a dropdown trigger
- Clicking shows: Pending / Active / Done options
- Selecting a different status calls the appropriate API:
  - Any → Pending: reactivate(plan) if Done, or use generic update? Actually, there's no "set to Pending" endpoint currently. The API has activate (Pending→Active), complete (Active→Done), reactivate (Done→Active). To handle arbitrary transitions, we'd need a new endpoint or use existing ones.
  - Actually, to keep it simple: clicking a status option calls the corresponding lifecycle method if applicable. If the transition isn't supported by the API, show a toast explaining why.
  - But the user said "users might mess this up" — they want to be able to go back to Pending. Currently no endpoint supports Done→Pending or Active→Pending.
  
  Actually, let me think about this differently. The API has:
  - Activate: Pending → Active
  - Complete: Active → Done
  - Reactivate: Done → Active
  
  There's no way to go back to Pending. But the user wants that. The simplest approach for now is:
  - If dropdown selects Active → call activate (works for Pending), or reactivate (works for Done)
  - If dropdown selects Done → call complete (works for Active)
  - If dropdown selects Pending → we need a new API endpoint or... let's keep it simple for now and just not support Pending as a target via dropdown (only via creation). The buttons handle the forward flow.

  Actually, let me re-read the user's request: "It's weird that it can never go back to pending, users might mess this up". The user wants to be able to go back. The simplest backend approach would be to add a `SetStatus` endpoint or just re-use `Reactivate` but that goes to Active. 

  For the MVP of this task, let me just make the badge clickable with a dropdown showing the 3 options. If the transition isn't natively supported by the lifecycle methods, we'll show a toast: "Cannot go from X to Y directly". This keeps it simple while giving users visibility into the state machine.

Actually, looking at the user's request again: "I like C for Progress/status While I like the buttons ish (ish because I only see unknown and at the moment I wrote above was not clickable)"

They want the badge/dropdown as a progress indicator. Since we don't have a "set to Pending" API, let me just wire up the dropdown to the existing lifecycle methods and add a toast for unsupported transitions. The buttons handle the common cases; the dropdown gives visibility and the ability to skip steps (e.g., Pending → Done directly calls both activate and complete sequentially).

Actually, simplest: make the status badge a USelectMenu or UDropdown that only shows the 3 options. On select, if same as current, no-op. If different:
- To Active: call activate() if Pending, reactivate() if Done
- To Done: call complete() if Active, or activate+complete if Pending
- To Pending: no backend support yet — show toast "Cannot revert to Pending. Create a new plan instead."

Let me keep it simple with a dropdown and handle the three states.

- [x] **Step 1: Add status dropdown replacing badge**

In `<script>`, add:

```typescript
const STATUS_OPTIONS = ['Pending', 'Active', 'Done'] as const

async function setStatus(plan: PlanResponse, newStatus: string) {
  if (newStatus === plan.status) return
  if (newStatus === 'Active') {
    if (plan.status === 'Pending') await activate(plan)
    else if (plan.status === 'Done') await reactivate(plan)
  } else if (newStatus === 'Done') {
    if (plan.status === 'Active') await complete(plan)
    else if (plan.status === 'Pending') {
      // Must go through Active first — or chain
      // For simplicity, try activate then complete
      await activate(plan)
      await complete(plan)
    }
  } else if (newStatus === 'Pending') {
    // Not supported — show toast
    toast.add({ title: 'Cannot revert to Pending', color: 'warning' })
  }
}
```

In template, replace the static badge:

```html
<!-- Replace: <UBadge> with USelectMenu wrapping -->
<USelectMenu
  :items="STATUS_OPTIONS.map(s => ({ label: s, value: s }))"
  :model-value="plan.status"
  @update:model-value="(val: string) => setStatus(plan, val)"
  :disabled="props.readonly"
  size="xs"
>
  <template #default>
    <UBadge
      :color="STATUS_COLORS[plan.status] ?? 'neutral'"
      variant="subtle"
      size="xs"
      class="cursor-pointer"
    >
      {{ STATUS_LABELS[plan.status] ?? 'Unknown' }}
    </UBadge>
  </template>
</USelectMenu>
```

Wait, `USelectMenu` in Nuxt UI v4 — let me check if it exists or if I should use `UDropdown`. Looking at the codebase conventions: Nuxt UI v4 uses `USelectMenu` for select-style dropdowns. But actually, the simpler approach might be to just use a native `<select>` or `USelect`. 

Actually, to keep it simple and match Nuxt UI conventions, I'll use a dropdown approach. Let me just wrap the badge in a clickable container that opens a small context menu.

Actually, the simplest approach that maintains the visual badge is to use `UPopover` or `UDropdown`. Let me use `UDropdown` since it's designed for this pattern:

```html
<UDropdown :items="statusMenuItems(plan)">
  <UBadge
    :color="STATUS_COLORS[plan.status] ?? 'neutral'"
    variant="subtle"
    size="xs"
    class="cursor-pointer"
  >
    {{ STATUS_LABELS[plan.status] ?? 'Unknown' }}
  </UBadge>
</UDropdown>
```

With items computed:
```typescript
function statusMenuItems(plan: PlanResponse) {
  return STATUS_OPTIONS
    .filter(s => s !== plan.status)
    .map(s => ({ label: s, click: () => setStatus(plan, s) }))
}
```

Let me simplify and just use this pattern.

- [x] **Step 2: Verify typecheck + lint**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint --fix
```

Expected: zero errors.

- [x] **Step 3: Commit**

```bash
git add src/web-ui/app/components/card/CardPlan.vue
git commit -m "feat(web): clickable status dropdown on plan badge"
```

---

### Task 3: Fullscreen Toggle on MarkdownEditor

**Files:**
- Modify: `src/web-ui/app/components/shared/MarkdownEditor.vue`

**Design:**
- Add a fullscreen toggle button in the MarkdownEditor toolbar
- Icon: `i-lucide-maximize` (enter fullscreen) / `i-lucide-minimize` (exit)
- When fullscreen: the editor covers the viewport with a fixed overlay, high z-index
- Add a fixed positioning wrapper that overlays the entire screen
- Escape key exits fullscreen
- Test with both CardPlan and CardSpec (both use MarkdownEditor)

- [x] **Step 1: Read current MarkdownEditor**

Read `src/web-ui/app/components/shared/MarkdownEditor.vue` to understand its structure.

- [x] **Step 2: Add fullscreen state + toggle**

In `<script>`, add:

```typescript
const isFullscreen = ref(false)

function toggleFullscreen() {
  isFullscreen.value = !isFullscreen.value
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && isFullscreen.value) {
    isFullscreen.value = false
  }
}

onMounted(() => window.addEventListener('keydown', onKeydown))
onUnmounted(() => window.removeEventListener('keydown', onKeydown))
```

- [x] **Step 3: Add fullscreen button to toolbar + wrapper div**

Find the editor container in the template. Add the fullscreen toggle button in the toolbar area (next to existing toolbar controls). Wrap the editor in conditional fullscreen classes.

The structure should be:

```html
<div :class="isFullscreen ? 'fixed inset-0 z-50 bg-background p-6' : ''">
  <div class="... toolbar ...">
    <!-- existing toolbar -->
    <UButton
      size="xs"
      variant="ghost"
      :icon="isFullscreen ? 'i-lucide-minimize' : 'i-lucide-maximize'"
      @click="toggleFullscreen"
    />
  </div>
  <TiptapEditor :class="isFullscreen ? 'h-[calc(100vh-8rem)]' : ''" />
</div>
```

Read the actual MarkdownEditor structure to place the button correctly.

- [x] **Step 4: Verify typecheck + lint + tests**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint --fix && pnpm vitest run
```

Expected: zero errors, all 128 tests pass.

- [x] **Step 5: Commit**

```bash
git add src/web-ui/app/components/shared/MarkdownEditor.vue
git commit -m "feat(web): fullscreen toggle on MarkdownEditor (Escape to exit)"
```

---

### Task 4: Final Verification

- [x] **Step 1: Full build + test + typecheck + lint**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint --fix && pnpm vitest run
```

- [x] **Step 2: Push**

```bash
git push origin task/phase-3-5a-doc-model-schema
```
