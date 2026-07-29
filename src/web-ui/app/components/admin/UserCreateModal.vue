<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'created': []
  'close': []
}>()

const username = ref('')
const password = ref('')
const firstName = ref('')
const lastName = ref('')
const email = ref('')
const isAdmin = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)

const api = useApi()

function onClose() {
  emit('update:open', false)
  emit('close')
}

function resetForm() {
  username.value = ''
  password.value = ''
  firstName.value = ''
  lastName.value = ''
  email.value = ''
  isAdmin.value = false
  error.value = null
}

watch(() => props.open, (val) => {
  if (!val) resetForm()
})

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    await api.POST(ApiRoutes.Admin.userCreate(), {
      body: {
        username: username.value,
        password: password.value,
        name: firstName.value,
        lastName: lastName.value,
        email: email.value,
        isAdmin: isAdmin.value
      }
    })
    onClose()
    emit('created')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to create user'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppModal
    :open="open"
    title="Create User"
    :loading="loading"
    :error="error"
    width="sm:max-w-lg"
    @update:open="emit('update:open', $event)"
    @close="onClose"
  >
    <template #body>
      <form
        class="space-y-4 p-4"
        @submit.prevent="handleSubmit"
      >
        <UFormField
          label="Username"
          required
        >
          <UInput
            v-model="username"
            placeholder="jdoe"
            required
            class="w-full"
          />
        </UFormField>

        <UFormField
          label="Password"
          required
        >
          <UInput
            v-model="password"
            type="password"
            required
            class="w-full"
          />
        </UFormField>

        <div class="grid grid-cols-2 gap-4">
          <UFormField label="First Name">
            <UInput
              v-model="firstName"
              class="w-full"
            />
          </UFormField>
          <UFormField label="Last Name">
            <UInput
              v-model="lastName"
              class="w-full"
            />
          </UFormField>
        </div>

        <UFormField
          label="Email"
          required
        >
          <UInput
            v-model="email"
            type="email"
            required
            class="w-full"
          />
        </UFormField>

        <UCheckbox
          v-model="isAdmin"
          label="Admin"
        />
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          @click="onClose"
        >
          Cancel
        </UButton>
        <UButton
          type="submit"
          :loading="loading"
          @click="handleSubmit"
        >
          Create
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
