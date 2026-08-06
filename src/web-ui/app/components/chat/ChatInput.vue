<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { AgentPersonalityDto, PromptPresetDto } from '~/types/chat'
import PersonalityManageModal from '~/components/chat/PersonalityManageModal.vue'

const props = withDefaults(
  defineProps<{
    disabled?: boolean
    /** The session's current personality, when one already exists — syncs the
     * picker's selection so it reflects what's actually set, and lets a
     * change made here (mid-conversation) update the live session. Null/
     * absent for a composer with no session yet (personality only takes
     * effect at send time then, via the send emit's 5th argument). */
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
    reasoningEffort?: string | null,
    personalityId?: string | null
  ]
  cancel: []
  /** Fires immediately on selection change (not just bundled into send) so an
   * already-existing session can react right away — mirrors what the old
   * ChatSessionHeader personality select used to do before this picker
   * consolidated into the single composer icon. No-op for callers with no
   * session yet (chats/index.vue empty state, ChatDock draft mode); they just
   * hold the selection locally until the first send creates the session. */
  personalityChanged: [personalityId: string | null]
}>()

const api = useApi()

const content = ref('')
const textareaRef = ref<HTMLTextAreaElement | null>(null)
const presets = ref<PromptPresetDto[]>([])
const selectedPresetId = ref<string | null>(null)
const selectedModelId = ref<string | null>(null)
const selectedEffort = ref<string | null>(null)
const personalities = ref<AgentPersonalityDto[]>([])
const selectedPersonalityId = ref<string | null>(props.personalityId)
const showManageModal = ref(false)

watch(() => props.personalityId, (v) => {
  selectedPersonalityId.value = v
})

watch(selectedPersonalityId, (v, oldValue) => {
  if (oldValue !== v) {
    emit('personalityChanged', v)
  }
})

const selectedPresetName = computed(
  () => presets.value.find(p => p.id === selectedPresetId.value)?.name ?? null
)

const selectedPersonalityName = computed(
  () => personalities.value.find(p => p.id === selectedPersonalityId.value)?.name ?? null
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

const personalityMenuItems = computed(() => [[
  {
    label: 'No personality',
    icon: selectedPersonalityId.value === null ? 'i-lucide-check' : undefined,
    onSelect: () => { selectedPersonalityId.value = null }
  },
  ...personalities.value.map(p => ({
    label: p.name,
    icon: selectedPersonalityId.value === p.id ? 'i-lucide-check' : undefined,
    onSelect: () => { selectedPersonalityId.value = p.id }
  })),
  { label: 'Manage personalities…', icon: 'i-lucide-users', onSelect: () => { showManageModal.value = true } }
]])

async function fetchPresets() {
  try {
    const { data } = await api.GET<PromptPresetDto[]>(ApiRoutes.Chat.presets.list())
    presets.value = data ?? []
  } catch {
    // Preset picker degrades to "No preset" — not worth a toast for a background list fetch
  }
}

async function fetchPersonalities() {
  try {
    const { data } = await api.GET<AgentPersonalityDto[]>(ApiRoutes.Chat.personalities.list())
    personalities.value = (data ?? []).filter(p => !p.archivedAt)
  } catch {
    // Degrades to "No personality" — background list fetch, not worth a toast
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
  emit('send', trimmed, selectedPresetId.value, selectedModelId.value, selectedEffort.value, selectedPersonalityId.value)
  content.value = ''
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }
}

onMounted(() => {
  void fetchPresets()
  void fetchPersonalities()
})

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

defineExpose({ setContent, selectedPersonalityId, personalityMenuItems })
</script>

<template>
  <div class="border-t border-gray-200 dark:border-gray-700 p-4">
    <!-- Active preset/personality chip -->
    <div
      v-if="selectedPresetName || selectedPersonalityName || personalityId"
      class="mb-2"
    >
      <span class="inline-flex items-center gap-1 text-xs bg-primary/10 text-primary px-2 py-0.5 rounded-full">
        <UIcon
          name="i-lucide-sparkles"
          class="size-3"
        />
        <span>{{ selectedPresetName ? `Preset: ${selectedPresetName}` : (selectedPersonalityName ? `Personality: ${selectedPersonalityName}` : 'Personality active') }}</span>
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
          <UDropdownMenu :items="personalityMenuItems">
            <UButton
              icon="i-lucide-user-round"
              :variant="selectedPersonalityId ? 'soft' : 'ghost'"
              :color="selectedPersonalityId ? 'primary' : 'neutral'"
              size="sm"
              :disabled="disabled"
              title="Personality"
            />
          </UDropdownMenu>

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

    <PersonalityManageModal
      v-model:open="showManageModal"
      @changed="fetchPersonalities"
    />
  </div>
</template>
