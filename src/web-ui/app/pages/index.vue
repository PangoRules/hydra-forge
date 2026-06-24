<script setup lang="ts">
import { UiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

await navigateTo(UiRoutes.Projects, { redirectCode: 302 })
</script>

<template>
  <div />
</template>
