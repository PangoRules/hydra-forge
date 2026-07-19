<script setup lang="ts">
import { useKeyboard } from '~/composables/keyboard/useKeyboard'

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

// Map raw key names to human-readable display
const KEY_LABELS: Record<string, string> = {
  'ArrowRight': 'Ctrl+Shift+→',
  'ArrowLeft': 'Ctrl+Shift+←',
  'ArrowUp': 'Ctrl+Shift+↑',
  'ArrowDown': 'Ctrl+Shift+↓',
  'Enter': '↵',
  '?': '?'
}

function keyLabel(raw: string): string {
  return KEY_LABELS[raw] ?? raw
}

// Group shortcuts by scope, deduplicate by key+description
const shortcutsByScope = computed(() => {
  const allShortcuts = getAllShortcuts() || []
  const groups: Record<string, { key: string, label: string, description: string }[]> = {}
  const seen = new Set<string>()

  for (const shortcut of allShortcuts) {
    const dedupKey = `${shortcut.scope}:${shortcut.key}:${shortcut.description}`
    if (seen.has(dedupKey)) continue
    seen.add(dedupKey)

    if (!groups[shortcut.scope]) {
      groups[shortcut.scope] = []
    }
    groups[shortcut.scope]!.push({
      key: shortcut.key,
      label: keyLabel(shortcut.key),
      description: shortcut.description
    })
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
          class="space-y-2"
        >
          <h3 class="font-semibold text-sm text-gray-500 dark:text-gray-400 uppercase tracking-wider">
            {{ scope }}
          </h3>
          <table class="w-full text-sm">
            <thead class="sr-only">
              <tr>
                <th>Shortcut</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="shortcut in (shortcuts || [])"
                :key="shortcut.key"
                class="border-b border-gray-100 dark:border-gray-800 last:border-0"
              >
                <td class="py-1.5 pr-4 align-middle">
                  <kbd class="inline-block px-2 py-0.5 bg-gray-100 dark:bg-gray-700 rounded text-xs font-mono whitespace-nowrap">
                    {{ shortcut.label }}
                  </kbd>
                </td>
                <td class="py-1.5 align-middle text-gray-700 dark:text-gray-300">
                  {{ shortcut.description }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </UModal>
</template>
