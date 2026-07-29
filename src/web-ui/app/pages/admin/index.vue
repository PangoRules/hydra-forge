<script setup lang="ts">
import { UiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })
const user = useAuthStore().user
if (!user?.isAdmin) navigateTo('/projects')
else navigateTo(UiRoutes.Admin.Users)
</script>

<template>
  <div />
</template>
