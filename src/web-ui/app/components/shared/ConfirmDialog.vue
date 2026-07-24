<script setup lang="ts">
const props = withDefaults(defineProps<{
  open: boolean
  title?: string
  message?: string
  confirmText?: string
  cancelText?: string
  confirmColor?: 'error' | 'primary' | 'warning'
}>(), {
  title: 'Confirm',
  message: 'Are you sure?',
  confirmText: 'Confirm',
  cancelText: 'Cancel',
  confirmColor: 'error'
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  'confirm': []
  'cancel': []
}>()

const isOpen = computed({
  get: () => props.open,
  set: val => emit('update:open', val)
})

function onConfirm() {
  emit('confirm')
  isOpen.value = false
}

function onCancel() {
  emit('cancel')
  isOpen.value = false
}
</script>

<template>
  <UModal v-model:open="isOpen">
    <template #body>
      <div class="p-4">
        <h3 class="text-lg font-semibold mb-2">
          {{ title }}
        </h3>
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ message }}
        </p>
      </div>
    </template>
    <template #footer>
      <div class="flex justify-end gap-3 w-full">
        <UButton
          variant="ghost"
          @click="onCancel"
        >
          {{ cancelText }}
        </UButton>
        <UButton
          :color="confirmColor"
          @click="onConfirm"
        >
          {{ confirmText }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
