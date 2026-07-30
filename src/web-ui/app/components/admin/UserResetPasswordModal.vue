<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

const props = defineProps<{
  userId: string
}>()

const emit = defineEmits<{
  close: []
  reset: []
}>()

const isOpen = ref(true)
const newPassword = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

const api = useApi()

/** Close with animation: set isOpen=false (triggers UModal scale-out 200ms), then emit close */
function closeWithAnimation() {
  if (!isOpen.value) return
  isOpen.value = false
  setTimeout(() => emit('close'), 200)
}

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    await api.POST(ApiRoutes.Admin.userResetPassword(props.userId), {
      body: { newPassword: newPassword.value }
    })
    emit('reset')
    closeWithAnimation()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to reset password'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppModal
    :open="isOpen"
    title="Reset Password"
    :loading="loading"
    :error="error"
    width="sm:max-w-sm"
    @update:open="closeWithAnimation"
    @close="closeWithAnimation"
  >
    <template #body>
      <form
        class="p-4"
        @submit.prevent="handleSubmit"
      >
        <UFormField
          label="New Password"
          required
        >
          <UInput
            v-model="newPassword"
            type="password"
            required
            class="w-full"
            autofocus
          />
        </UFormField>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          @click="closeWithAnimation"
        >
          Cancel
        </UButton>
        <UButton
          type="submit"
          :loading="loading"
          @click="handleSubmit"
        >
          Reset
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
