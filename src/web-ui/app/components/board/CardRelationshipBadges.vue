<script setup lang="ts">
defineProps<{
  badges: {
    relatedCardId: string
    cardNumber: number
    title: string
    icon: string
    colorClass: string
    verb: string
  }[]
  overflow: number
}>()
</script>

<template>
  <div class="flex flex-col gap-0.5 min-w-0 w-full">
    <span class="text-[10px] text-gray-400 leading-none">Relations:</span>
    <div
      v-for="badge in badges"
      :key="badge.relatedCardId"
      class="text-xs flex items-center gap-1 min-w-0"
      :class="badge.colorClass"
      :title="`${badge.verb} #${badge.cardNumber} ${badge.title}`"
    >
      <UIcon
        :name="badge.icon"
        class="size-3 shrink-0"
      />
      <span class="truncate">{{ badge.verb }} #{{ badge.cardNumber }} {{ badge.title }}</span>
    </div>
    <span
      v-if="overflow > 0"
      class="text-[10px] text-gray-400"
    >+{{ overflow }} more</span>
  </div>
</template>
