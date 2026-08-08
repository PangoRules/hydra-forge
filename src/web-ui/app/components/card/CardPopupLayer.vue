<!-- src/web-ui/app/components/card/CardPopupLayer.vue -->
<script setup lang="ts">
const cardPopup = useCardPopupStore()
</script>

<template>
  <ClientOnly>
    <CardPopup
      v-for="cardId in cardPopup.openCardIds"
      :key="cardId"
      :card-id="cardId"
    >
      <!-- Slot intentionally empty — Plan 20 fills this with the migrated CardModal body -->
    </CardPopup>
  </ClientOnly>
</template>
