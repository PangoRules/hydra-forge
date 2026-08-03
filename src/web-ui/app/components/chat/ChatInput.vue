<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    disabled?: boolean
    personalityId?: string | null
    presetId?: string | null
  }>(),
  {
    disabled: false,
    personalityId: null,
    presetId: null
  }
)

const emit = defineEmits<{
  send: [content: string, presetId?: string | null]
  cancel: []
}>()

const content = ref('')
const textareaRef = ref<HTMLTextAreaElement | null>(null)

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    submit()
  }
}

function submit() {
  const trimmed = content.value.trim()
  if (!trimmed || props.disabled) return
  emit('send', trimmed, props.presetId)
  content.value = ''
}
</script>

<template>
  <div class="border-t border-gray-200 dark:border-gray-700 p-4">
    <!-- Active preset/personality chip -->
    <div
      v-if="presetId || personalityId"
      class="mb-2"
    >
      <span class="inline-flex items-center gap-1 text-xs bg-primary/10 text-primary px-2 py-0.5 rounded-full">
        <UIcon
          name="i-lucide-sparkles"
          class="size-3"
        />
        <span>{{ presetId ? 'Preset active' : 'Personality active' }}</span>
      </span>
    </div>

    <!-- Input row -->
    <div class="flex items-end gap-2">
      <!-- Image attach: disabled until a chat-image upload endpoint exists (server ImageBlock is StorageKey/MediaType, not a client-uploadable shape yet) -->
      <UButton
        icon="i-lucide-image"
        variant="ghost"
        size="sm"
        disabled
        title="Image attachments aren't available yet"
        class="shrink-0"
      />

      <!-- Textarea -->
      <div class="flex-1 relative">
        <textarea
          ref="textareaRef"
          v-model="content"
          class="w-full resize-none rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 px-4 py-2.5 pr-10 text-sm focus-visible:outline-2 focus-visible:outline-primary min-h-[44px] max-h-40"
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

      <!-- Send button -->
      <UButton
        icon="i-lucide-send"
        size="sm"
        class="shrink-0"
        :disabled="disabled || !content.trim()"
        @click="submit"
      />
    </div>
  </div>
</template>
