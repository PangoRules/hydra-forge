<script setup lang="ts">
import CollapsibleSection from '~/components/shared/CollapsibleSection.vue'

/**
 * Collapsed-by-default filter container used above admin data tables (Usage,
 * Audit Log, ...). Collapsed state gives the table more vertical room when
 * filters aren't in active use; the badge keeps active filters visible even
 * while collapsed so nothing silently stays applied out of sight.
 */
const props = withDefaults(defineProps<{
  activeCount?: number
  defaultOpen?: boolean
}>(), {
  activeCount: 0,
  defaultOpen: false
})

const emit = defineEmits<{
  reset: []
}>()

const open = ref(props.defaultOpen || props.activeCount > 0)

watch(() => props.activeCount, (count) => {
  if (count > 0) open.value = true
})
</script>

<template>
  <CollapsibleSection
    title="Filters"
    :badge="activeCount > 0 ? `${activeCount} active` : null"
    :open="open"
    @update:open="open = $event"
  >
    <template #actions>
      <UButton
        v-if="activeCount > 0"
        size="xs"
        color="neutral"
        variant="ghost"
        icon="i-lucide-x"
        label="Reset filters"
        @click="emit('reset')"
      />
    </template>

    <slot />
  </CollapsibleSection>
</template>
