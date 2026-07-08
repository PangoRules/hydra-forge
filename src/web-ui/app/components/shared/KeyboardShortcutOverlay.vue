<script setup lang="ts">
import { useKeyboard } from '~/composables/useKeyboard'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  close: []
}>()

const { getAllShortcuts } = useKeyboard()

const show = computed({
  get: () => props.open,
  set: (val) => {
    if (!val) emit('close')
  }
})

// Group shortcuts by scope
const shortcutsByScope = computed(() => {
  const allShortcuts = getAllShortcuts() || []
  const groups: Record<string, { key: string, description: string }[]> = {}

  for (const shortcut of allShortcuts) {
    if (!groups[shortcut.scope]) {
      groups[shortcut.scope] = []
    }
    groups[shortcut.scope]!.push({ key: shortcut.key, description: shortcut.description })
  }

  return groups
})
</script>

<template>
  <UModal
    v-model:open="show"
    title="Keyboard Shortcuts"
  >
    <template #body>
      <div class="space-y-6">
        <div
          v-for="(shortcuts, scope) in shortcutsByScope"
          :key="scope"
          class="space-y-3"
        >
          <h3 class="font-medium">
            {{ scope }} Shortcuts
          </h3>
          <ul class="space-y-2 text-sm">
            <li
              v-for="shortcut in (shortcuts || [])"
              :key="shortcut.key"
              class="flex items-center gap-2"
            >
              <kbd class="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded">
                {{ shortcut.key }}
              </kbd>
              <span>{{ shortcut.description }}</span>
            </li>
          </ul>
        </div>
      </div>
    </template>
  </UModal>
</template>
