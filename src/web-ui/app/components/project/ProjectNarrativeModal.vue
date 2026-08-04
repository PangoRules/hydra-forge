<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'
import { formatDateTime } from '~/lib/date'

type ProjectSnapshotResponse = components['schemas']['ProjectSnapshotResponse']

const props = defineProps<{
  projectId: string
}>()

const emit = defineEmits<{
  close: []
}>()

const api = useApi()
const toast = useAppToast()

const isOpen = ref(true)
const loading = ref(true)
const snapshot = ref<ProjectSnapshotResponse | null>(null)

function closeWithAnimation() {
  if (!isOpen.value) return
  isOpen.value = false
  setTimeout(() => emit('close'), 200)
}

async function fetchSnapshot() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.ProjectSnapshot.get(props.projectId))
    snapshot.value = (data as ProjectSnapshotResponse) ?? null
  } catch {
    toast.error('Failed to load AI narrative')
  } finally {
    loading.value = false
  }
}

onMounted(fetchSnapshot)
</script>

<template>
  <AppModal
    :open="isOpen"
    title="AI Project Narrative"
    @update:open="closeWithAnimation"
    @close="closeWithAnimation"
  >
    <template #body>
      <div
        v-if="loading"
        class="flex justify-center py-8"
      >
        <UIcon
          name="i-lucide-loader-circle"
          class="animate-spin size-6"
        />
      </div>

      <div v-else-if="snapshot?.aiNarrative">
        <p class="text-sm whitespace-pre-wrap">
          {{ snapshot.aiNarrative }}
        </p>
        <p
          v-if="snapshot.aiNarrativeGeneratedAt"
          class="text-xs text-muted mt-4"
        >
          Generated {{ formatDateTime(snapshot.aiNarrativeGeneratedAt) }}
        </p>
      </div>

      <div
        v-else
        class="text-sm text-muted py-4"
      >
        No AI narrative has been generated for this project yet. Narratives are generated nightly.
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton
          variant="ghost"
          @click="closeWithAnimation"
        >
          Close
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
