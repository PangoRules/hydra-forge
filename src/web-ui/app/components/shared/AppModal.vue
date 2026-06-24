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
    <!-- Header — renders in UModal's #header slot -->
    <template #header>
      <div
        v-if="title || $slots.header || showClose"
        class="flex items-center gap-2 w-full"
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
          class="ml-auto"
          @click="onClose"
        />
      </div>
    </template>

    <!-- Body — renders in UModal's #body slot -->
    <template #body>
      <div @keydown="onKeydown">
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

        <!-- Content -->
        <div v-else>
          <slot name="body" />
        </div>
      </div>
    </template>

    <!-- Footer — renders in UModal's #footer slot -->
    <template #footer>
      <div
        v-if="$slots.footer"
        class="flex justify-end gap-3"
      >
        <slot name="footer" />
      </div>
    </template>
  </UModal>
</template>
