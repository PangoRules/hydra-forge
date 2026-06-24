<script setup lang="ts">
const props = withDefaults(defineProps<{
  open: boolean
  title?: string
  loading?: boolean
  error?: string | null
  /** Tailwind max-width class e.g. 'sm:max-w-4xl' */
  width?: string
  /** Show default close button in header */
  showClose?: boolean
}>(), {
  showClose: true,
  width: 'sm:max-w-lg'
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  'close': []
}>()

const isOpen = computed({
  get: () => props.open,
  set: val => emit('update:open', val)
})

function onClose() {
  emit('close')
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') onClose()
}
</script>

<template>
  <UModal
    v-model:open="isOpen"
    :ui="{ content: width }"
  >
    <div
      class="flex flex-col max-h-[85vh]"
      @keydown="onKeydown"
    >
      <!-- Header -->
      <div
        v-if="title || $slots.header || showClose"
        class="flex items-center justify-between p-4 border-b"
      >
        <h2
          v-if="title"
          class="text-lg font-semibold truncate"
        >
          {{ title }}
        </h2>
        <slot name="header" />
        <UButton
          v-if="showClose"
          icon="i-lucide-x"
          variant="ghost"
          size="sm"
          @click="onClose"
        />
      </div>

      <!-- Loading -->
      <div
        v-if="loading"
        class="flex items-center justify-center p-8"
      >
        <UIcon
          name="i-lucide-loader"
          class="animate-spin size-8"
        />
      </div>

      <!-- Error -->
      <UAlert
        v-else-if="error"
        color="error"
        :title="error"
        class="m-4"
      />

      <!-- Body -->
      <div
        v-else
        class="flex-1 overflow-y-auto"
      >
        <slot name="body" />
      </div>

      <!-- Footer -->
      <div
        v-if="$slots.footer"
        class="p-4 border-t"
      >
        <slot name="footer" />
      </div>
    </div>
  </UModal>
</template>
