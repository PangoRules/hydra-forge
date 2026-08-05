<script setup lang="ts">
// Providers and Provider Models were merged onto one page/nav item — this route
// stays only so old bookmarks/links still land somewhere.
import { UiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

navigateTo(UiRoutes.Admin.Providers, { replace: true })
</script>

<template>
  <div />
</template>
