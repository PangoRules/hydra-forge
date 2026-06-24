<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

// Redirect to projects or login
await navigateTo('/projects', { redirectCode: 302 })
</script>

<template>
  <div />
</template>
