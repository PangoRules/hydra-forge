<script setup lang="ts">
import { CHAT_TYPE_BADGE } from '~/lib/chat-type'
import type { ChatType } from '~/lib/chat-type'

const props = defineProps<{
  status: 'Active' | 'Closed' | 'Archived'
  types: Set<ChatType>
  scope: 'mine' | 'participated'
}>()

const emit = defineEmits<{
  'update:status': [value: 'Active' | 'Closed' | 'Archived']
  'update:types': [value: Set<ChatType>]
  'update:scope': [value: 'mine' | 'participated']
}>()

// Linear lifecycle only — 'NonArchived' stays in the backend enum for
// internal use but is not offered as a UI filter.
const statusItems = [
  { label: 'Active', value: 'Active' },
  { label: 'Closed', value: 'Closed' },
  { label: 'Archived', value: 'Archived' }
]

const typeItems = (Object.keys(CHAT_TYPE_BADGE) as ChatType[]).map(value => ({
  label: CHAT_TYPE_BADGE[value].label,
  value
}))

// Local state synced with prop — USelectMenu with multiple uses value array
const localSelectedTypes = ref<{ label: string, value: ChatType }[]>(
  [...props.types].map(v => ({ label: CHAT_TYPE_BADGE[v].label, value: v }))
)

watch(localSelectedTypes, (vals) => {
  if (vals.length === 0) return
  emit('update:types', new Set(vals.map(v => v.value)))
})

watch(() => props.types, (newTypes) => {
  localSelectedTypes.value = [...newTypes].map(v => ({ label: CHAT_TYPE_BADGE[v].label, value: v }))
}, { deep: true })

const typeLabel = computed(() => {
  if (props.types.size === typeItems.length) return `All (${typeItems.length})`
  if (props.types.size === 1) {
    const only = [...props.types][0]!
    return CHAT_TYPE_BADGE[only].label
  }
  return `${props.types.size} selected`
})
</script>

<template>
  <div class="space-y-2">
    <div class="grid grid-cols-2 gap-2">
      <USelect
        :model-value="status"
        :items="statusItems"
        size="xs"
        @update:model-value="(v: unknown) => emit('update:status', v as 'Active' | 'Closed' | 'Archived')"
      />
      <USelectMenu
        v-model="localSelectedTypes"
        :items="typeItems"
        multiple
        by="value"
        size="xs"
      >
        <template #default>
          {{ typeLabel }}
        </template>
      </USelectMenu>
    </div>
    <div class="grid grid-cols-2 gap-0 rounded-md overflow-hidden border border-gray-200 dark:border-gray-700">
      <button
        type="button"
        class="text-xs py-1 transition-colors"
        :class="scope === 'mine' ? 'bg-primary text-white' : 'bg-transparent hover:bg-gray-50 dark:hover:bg-gray-800'"
        @click="emit('update:scope', 'mine')"
      >
        Mine
      </button>
      <button
        type="button"
        class="text-xs py-1 transition-colors border-l border-gray-200 dark:border-gray-700"
        :class="scope === 'participated' ? 'bg-primary text-white' : 'bg-transparent hover:bg-gray-50 dark:hover:bg-gray-800'"
        @click="emit('update:scope', 'participated')"
      >
        Participated
      </button>
    </div>
  </div>
</template>
