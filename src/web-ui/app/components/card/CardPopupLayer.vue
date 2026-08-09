<!-- src/web-ui/app/components/card/CardPopupLayer.vue -->
<script setup lang="ts">
const cardPopup = useCardPopupStore()
const boardStore = useBoardStore()
const route = useRoute()

// Card popups only make sense while viewing the project board that owns
// them (board/docs/chats are one route with client-side tabs, so switching
// tabs never fires this). Leaving that project entirely — a different
// project's board, the projects list, anywhere else — closes them all.
const currentProjectId = computed(() => {
  const match = route.path.match(/^\/projects\/([^/]+)/)
  return match ? match[1] : null
})

watch(currentProjectId, (newId, oldId) => {
  if (newId !== oldId) cardPopup.closeAll()
})

// Cards restored from localStorage on a hard reload are only valid if the
// reload landed back on a project board — if it landed anywhere else
// (projects list, /chats, admin), there's nothing to reconcile them against.
onMounted(() => {
  if (currentProjectId.value === null && cardPopup.openCardIds.length > 0) {
    cardPopup.closeAll()
  }
})

function handleArchived() {
  if (currentProjectId.value) boardStore.fetchBoard(currentProjectId.value)
}

function handleRestored() {
  if (currentProjectId.value) boardStore.fetchBoard(currentProjectId.value)
}
</script>

<template>
  <ClientOnly>
    <CardPopup
      v-for="cardId in cardPopup.openCardIds"
      :key="cardId"
      :card-id="cardId"
      @archived="handleArchived"
      @restored="handleRestored"
    />
  </ClientOnly>
</template>
