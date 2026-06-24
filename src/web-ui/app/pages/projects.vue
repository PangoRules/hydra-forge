<script setup lang="ts">
import { UiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

const auth = useAuth()
</script>

<template>
  <div class="p-8">
    <h1 class="text-2xl font-bold mb-4">Projects</h1>
    <p class="text-muted">
      Authenticated as <strong>{{ auth.isAuthenticated ? 'yes' : 'no' }}</strong>
    </p>
    <p class="text-sm text-muted mt-2">
      Project list and board coming in Plan 2.
    </p>
  </div>
</template>