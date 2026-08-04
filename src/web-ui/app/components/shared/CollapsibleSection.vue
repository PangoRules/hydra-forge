<script setup lang="ts">
/**
 * Generic collapsible group: header row (title + optional badge + optional
 * trailing action slot) toggling a body slot. Shared by CollapsibleFilterPanel
 * and any grouped-list page (e.g. AI Feature Routing) that needs the same
 * expand/collapse chrome without re-implementing it per page.
 */
const props = withDefaults(defineProps<{
  title: string
  badge?: string | number | null
  defaultOpen?: boolean
  open?: boolean
}>(), {
  badge: null,
  defaultOpen: false,
  open: undefined
})

const emit = defineEmits<{
  'update:open': [boolean]
}>()

const internalOpen = ref(props.defaultOpen)
const isOpen = computed({
  get: () => props.open ?? internalOpen.value,
  set: (val: boolean) => {
    internalOpen.value = val
    emit('update:open', val)
  }
})
</script>

<template>
  <div class="rounded-lg border border-muted">
    <div class="flex w-full items-center justify-between p-3 gap-2">
      <button
        type="button"
        class="flex flex-1 items-center gap-2 text-left min-w-0"
        :aria-expanded="isOpen"
        @click="isOpen = !isOpen"
      >
        <UIcon
          name="i-lucide-chevron-right"
          class="size-4 shrink-0 transition-transform"
          :class="isOpen ? 'rotate-90' : ''"
        />
        <span class="text-xs font-semibold uppercase tracking-wide text-muted truncate">
          {{ title }}
        </span>
        <UBadge
          v-if="badge !== null"
          size="sm"
          color="primary"
          variant="subtle"
        >
          {{ badge }}
        </UBadge>
      </button>
      <slot name="actions" />
    </div>

    <div
      v-if="isOpen"
      class="px-3 pb-3 space-y-3"
    >
      <slot />
    </div>
  </div>
</template>
