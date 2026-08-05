<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { PromptPresetDto } from '~/types/chat'

const props = withDefaults(
  defineProps<{
    disabled?: boolean
    personalityId?: string | null
    feature?: string
    initialModelId?: string | null
    initialEffort?: string | null
  }>(),
  {
    disabled: false,
    personalityId: null,
    feature: 'PersonalChat',
    initialModelId: null,
    initialEffort: null
  }
)

const emit = defineEmits<{
  send: [
    content: string,
    presetId?: string | null,
    preferredModelId?: string | null,
    reasoningEffort?: string | null
  ]
  cancel: []
}>()

const api = useApi()

const content = ref('')
const textareaRef = ref<HTMLTextAreaElement | null>(null)
const presets = ref<PromptPresetDto[]>([])
const selectedPresetId = ref<string | null>(null)
const selectedModelId = ref<string | null>(null)
const selectedEffort = ref<string | null>(null)

const selectedPresetName = computed(
  () => presets.value.find(p => p.id === selectedPresetId.value)?.name ?? null
)

const presetMenuItems = computed(() => [[
  {
    label: 'No preset',
    icon: selectedPresetId.value === null ? 'i-lucide-check' : undefined,
    onSelect: () => { selectedPresetId.value = null }
  },
  ...presets.value.map(p => ({
    label: p.name,
    description: p.content.length > 60 ? `${p.content.slice(0, 60)}…` : p.content,
    icon: selectedPresetId.value === p.id ? 'i-lucide-check' : undefined,
    onSelect: () => { selectedPresetId.value = p.id }
  }))
]])

async function fetchPresets() {
  try {
    const { data } = await api.GET<PromptPresetDto[]>(ApiRoutes.Chat.presets.list())
    presets.value = data ?? []
  } catch {
    // Preset picker degrades to "No preset" — not worth a toast for a background list fetch
  }
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    submit()
  }
}

function submit() {
  const trimmed = content.value.trim()
  if (!trimmed || props.disabled) return
  emit('send', trimmed, selectedPresetId.value, selectedModelId.value, selectedEffort.value)
  content.value = ''
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }
}

onMounted(fetchPresets)

function setContent(text: string) {
  content.value = text
  nextTick(() => {
    if (textareaRef.value) {
      textareaRef.value.style.height = 'auto'
      textareaRef.value.style.height = Math.min(textareaRef.value.scrollHeight, 160) + 'px'
      textareaRef.value.focus()
    }
  })
}

defineExpose({ setContent })
</script>

<template>
  <div class="border-t border-gray-200 dark:border-gray-700 p-4">
    <!-- Active preset/personality chip -->
    <div
      v-if="selectedPresetName || personalityId"
      class="mb-2"
    >
      <span class="inline-flex items-center gap-1 text-xs bg-primary/10 text-primary px-2 py-0.5 rounded-full">
        <UIcon
          name="i-lucide-sparkles"
          class="size-3"
        />
        <span>{{ selectedPresetName ? `Preset: ${selectedPresetName}` : 'Personality active' }}</span>
      </span>
    </div>

    <!-- Composer card -->
    <div class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 overflow-hidden">
      <div class="relative">
        <textarea
          ref="textareaRef"
          v-model="content"
          class="w-full resize-none bg-transparent px-4 pt-3 pb-1 text-sm focus-visible:outline-none min-h-[44px] max-h-40"
          :disabled="disabled"
          placeholder="Message the AI... (Enter to send, Shift+Enter for newline)"
          rows="1"
          @keydown="handleKeydown"
          @input="(e: Event) => {
            const el = e.target as HTMLTextAreaElement
            el.style.height = 'auto'
            el.style.height = Math.min(el.scrollHeight, 160) + 'px'
          }"
        />

        <!-- Cancel button shown when streaming -->
        <slot name="cancel" />
      </div>

      <!-- Controls row -->
      <div class="flex items-center justify-between gap-2 px-2 pb-2 pt-1">
        <div class="flex items-center gap-1.5">
          <!-- Image attach: disabled until a chat-image upload endpoint exists (server ImageBlock is StorageKey/MediaType, not a client-uploadable shape yet) -->
          <UButton
            icon="i-lucide-image"
            variant="ghost"
            color="neutral"
            size="sm"
            disabled
            title="Image attachments aren't available yet"
            class="shrink-0"
          />

          <ChatModelPicker
            v-model="selectedModelId"
            v-model:effort="selectedEffort"
            :feature="feature"
            :disabled="disabled"
            :initial-model-id="initialModelId"
            :initial-effort="initialEffort"
          />
        </div>

        <div class="flex items-center gap-1.5">
          <UDropdownMenu :items="presetMenuItems">
            <UButton
              icon="i-lucide-sparkles"
              :variant="selectedPresetId ? 'soft' : 'ghost'"
              :color="selectedPresetId ? 'primary' : 'neutral'"
              size="sm"
              :disabled="disabled"
              title="Prompt preset"
            />
          </UDropdownMenu>

          <UButton
            icon="i-lucide-send"
            size="sm"
            class="shrink-0"
            :disabled="disabled || !content.trim()"
            @click="submit"
          />
        </div>
      </div>
    </div>
  </div>
</template>
