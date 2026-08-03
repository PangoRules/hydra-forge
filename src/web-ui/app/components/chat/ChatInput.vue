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
  send: [content: string, images?: { url: string, base64?: string }[], presetId?: string | null]
  cancel: []
}>()

const content = ref('')
const attachedImages = ref<{ url: string, base64?: string }[]>([])
const textareaRef = ref<HTMLTextAreaElement | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    submit()
  }
}

function submit() {
  const trimmed = content.value.trim()
  if (!trimmed || props.disabled) return
  emit('send', trimmed, attachedImages.value.length > 0 ? attachedImages.value : undefined, props.presetId)
  content.value = ''
  for (const img of attachedImages.value) URL.revokeObjectURL(img.url)
  attachedImages.value = []
}

function removeImage(idx: number) {
  const [removed] = attachedImages.value.splice(idx, 1)
  if (removed) URL.revokeObjectURL(removed.url)
}

function handleFileChange(e: Event) {
  const input = e.target as HTMLInputElement
  if (!input.files) return

  for (const file of Array.from(input.files)) {
    if (!file.type.startsWith('image/')) continue
    const reader = new FileReader()
    reader.onload = (ev) => {
      const base64 = (ev.target?.result as string).split(',')[1]
      attachedImages.value.push({
        url: URL.createObjectURL(file),
        base64
      })
    }
    reader.readAsDataURL(file)
  }

  // Reset so same file can be re-selected
  input.value = ''
}

onUnmounted(() => {
  for (const img of attachedImages.value) URL.revokeObjectURL(img.url)
})
</script>

<template>
  <div class="border-t border-gray-200 dark:border-gray-700 p-4">
    <!-- Image previews -->
    <div
      v-if="attachedImages.length > 0"
      class="flex flex-wrap gap-2 mb-3"
    >
      <div
        v-for="(img, idx) in attachedImages"
        :key="idx"
        class="relative group"
      >
        <img
          :src="img.base64 ? `data:image/png;base64,${img.base64}` : img.url"
          class="h-16 w-16 object-cover rounded-md border border-gray-200 dark:border-gray-700"
        >
        <button
          class="absolute -top-1.5 -right-1.5 bg-red-500 text-white rounded-full w-5 h-5 flex items-center justify-center text-xs opacity-0 group-hover:opacity-100 transition-opacity"
          @click="removeImage(idx)"
        >
          ×
        </button>
      </div>
    </div>

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
      <!-- Image attach button -->
      <UButton
        icon="i-lucide-image"
        variant="ghost"
        size="sm"
        :disabled="disabled"
        class="shrink-0"
        @click="fileInputRef?.click()"
      />
      <input
        ref="fileInputRef"
        type="file"
        accept="image/*"
        multiple
        class="hidden"
        @change="handleFileChange"
      >

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
