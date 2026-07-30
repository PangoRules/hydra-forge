<script setup lang="ts">
import { UiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

await navigateTo(UiRoutes.Chats, { redirectCode: 302 })
</script>

<template>
  <div />
</template>
