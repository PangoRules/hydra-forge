<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'

const props = defineProps<{
  sessionId: string
  currentCardId: string
  sessionOpenCardId: string | null | undefined
  isProjectSession: boolean
}>()

const emit = defineEmits<{ linked: [cardId: string] }>()

const api = useApi()
const toast = useAppToast()
const linking = ref(false)

const isVisible = computed(() =>
  props.isProjectSession
  && !!props.currentCardId
  && props.sessionOpenCardId !== props.currentCardId
)

async function handleLink() {
  if (linking.value) return
  linking.value = true
  try {
    await api.POST(ApiRoutes.Chat.sessions.linkCard(props.sessionId), {
      body: { cardId: props.currentCardId }
    })
    toast.success('Card linked to chat')
    emit('linked', props.currentCardId)
  } catch (err) {
    const msg = err instanceof ApiError ? err.message : 'Failed to link card'
    toast.error(msg)
  } finally {
    linking.value = false
  }
}
</script>

<template>
  <UButton
    v-if="isVisible"
    icon="i-lucide-link"
    variant="ghost"
    size="xs"
    :loading="linking"
    title="Link this card to the current chat session"
    @click="handleLink"
  />
</template>
