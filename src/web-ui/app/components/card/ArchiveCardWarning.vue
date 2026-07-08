<script setup lang="ts">
defineProps<{
  dependents: { id: string; title: string; type: string }[]
}>()
const emit = defineEmits<{
  confirm: []
  cancel: []
}>()
</script>
<template>
  <UModal :open="true" @close="emit('cancel')">
    <UCard>
      <template #header>
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-alert-triangle" class="size-5 text-warning" />
          <h3 class="font-semibold">Archive Card</h3>
        </div>
      </template>
      <div class="space-y-3">
        <p class="text-sm">This card has relationships with other cards:</p>
        <ul class="space-y-1">
          <li v-for="dep in dependents" :key="dep.id" class="text-sm flex items-center gap-2">
            <UBadge variant="subtle" size="xs">{{ dep.type }}</UBadge>
            {{ dep.title }}
          </li>
        </ul>
        <p class="text-sm text-muted">Archiving this card will not remove relationships but may affect dependent cards. Continue?</p>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton variant="outline" @click="emit('cancel')">Cancel</UButton>
          <UButton color="error" @click="emit('confirm')">Archive</UButton>
        </div>
      </template>
    </UCard>
  </UModal>
</template>