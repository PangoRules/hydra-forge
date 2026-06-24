<script setup lang="ts">
import { UiRoutes } from '~/lib/routes'

definePageMeta({ layout: 'auth' })

const username = ref('')
const password = ref('')
const error = ref<string | null>(null)
const loading = ref(false)

const { login } = useAuth()

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    await login(username.value, password.value)
    await navigateTo(UiRoutes.Projects)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Login failed'
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
            HydraForge
          </h1>
          <p class="text-sm text-muted">
            Sign in to your workspace
          </p>
        </div>
      </template>

      <form
        class="space-y-4"
        @submit.prevent="handleSubmit"
      >
        <UFormField label="Username" class="w-full">
          <UInput
            v-model="username"
            autocomplete="username"
            required
            class="w-full"
          />
        </UFormField>

        <UFormField label="Password" class="w-full">
          <UInput
            v-model="password"
            type="password"
            autocomplete="current-password"
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
          Sign in
        </UButton>
      </form>
    </UCard>
  </div>
</template>
