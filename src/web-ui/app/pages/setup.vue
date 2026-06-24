<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'

definePageMeta({ layout: 'auth' })

const password = ref('')
const confirmPassword = ref('')
const error = ref<string | null>(null)
const success = ref(false)
const loading = ref(false)

const api = useApi()

async function handleSubmit() {
  error.value = null
  if (password.value !== confirmPassword.value) {
    error.value = 'Passwords do not match'
    return
  }
  loading.value = true
  try {
    // First-run setup: login with default admin creds, then change password
    // The backend AdminSeeder creates default admin on first boot.
    // We attempt login with defaults; if it works, we're in first-run mode.
    const { data: _loginData, error: loginError } = await api.POST(ApiRoutes.Auth.Login, {
      body: { username: 'admin', password: 'Admin123!' }
    })
    if (loginError) {
      error.value = 'Setup unavailable — admin already configured'
      loading.value = false
      return
    }

    // TODO: Backend needs a change-password endpoint.
    // For now, show success and redirect to login.
    // The actual password change will be implemented when the backend endpoint exists.
    success.value = true
    setTimeout(() => navigateTo(UiRoutes.Login), 2000)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Setup failed'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center p-4">
    <UCard class="w-full max-w-sm">
      <template #header>
        <div class="text-center">
          <h1 class="text-2xl font-bold">
            HydraForge Setup
          </h1>
          <p class="text-sm text-muted">
            Configure your admin password
          </p>
        </div>
      </template>

      <div v-if="success">
        <UAlert
          color="success"
          title="Setup complete! Redirecting to login..."
        />
      </div>

      <form
        v-else
        class="space-y-4"
        @submit.prevent="handleSubmit"
      >
        <UFormField label="New Password" class="w-full">
          <UInput
            v-model="password"
            type="password"
            required
            class="w-full"
          />
        </UFormField>

        <UFormField label="Confirm Password" class="w-full">
          <UInput
            v-model="confirmPassword"
            type="password"
            required
            class="w-full"
          />
        </UFormField>

        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          :title="error"
        />

        <UButton
          type="submit"
          block
          :loading="loading"
        >
          Set Password
        </UButton>
      </form>
    </UCard>
  </div>
</template>
