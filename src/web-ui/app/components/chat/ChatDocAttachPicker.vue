<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

interface DocItem {
  id: string
  title: string
}

const props = defineProps<{
  sessionId: string
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'picked': [documentId: string]
}>()

const api = useApi()
const toast = useAppToast()

const isOpen = computed({
  get: () => props.open,
  set: val => emit('update:open', val)
})

const search = ref('')
const docs = ref<DocItem[]>([])
const loading = ref(false)
const picking = ref(false)

watch([isOpen, search], async ([open, q]) => {
  if (!open) return
  await fetchDocs(q)
}, { immediate: true })

async function fetchDocs(q?: string) {
  loading.value = true
  try {
    const { data } = await api.GET<DocItem[]>(ApiRoutes.Chat.documents.list(q))
    docs.value = data ?? []
  } catch {
    docs.value = []
  } finally {
    loading.value = false
  }
}

async function pickDoc(doc: DocItem) {
  picking.value = true
  try {
    await api.POST(ApiRoutes.Chat.sessions.attachDocument(props.sessionId), {
      body: { documentId: doc.id }
    })
    toast.success(`"${doc.title}" attached`)
    isOpen.value = false
    emit('picked', doc.id)
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to attach document')
  } finally {
    picking.value = false
  }
}
</script>

<template>
  <AppModal
    v-model:open="isOpen"
    title="Attach Document"
    width="sm:max-w-xl"
  >
    <template #body>
      <div class="space-y-3">
        <!-- Search -->
        <div class="relative">
          <UIcon
            name="i-lucide-search"
            class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted"
          />
          <input
            v-model="search"
            type="text"
            placeholder="Search documents..."
            class="w-full pl-9 pr-4 py-2 text-sm border border-gray-200 dark:border-gray-700 rounded-lg bg-transparent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          >
        </div>

        <!-- Loading -->
        <div
          v-if="loading"
          class="flex items-center justify-center py-8"
        >
          <UIcon
            name="i-lucide-loader-circle"
            class="animate-spin size-6 text-muted"
          />
        </div>

        <!-- Empty -->
        <p
          v-else-if="docs.length === 0"
          class="text-sm text-muted text-center py-6"
        >
          {{ search ? 'No documents match your search' : 'No documents found' }}
        </p>

        <!-- Doc list -->
        <ul
          v-else
          class="divide-y divide-gray-100 dark:divide-gray-800 max-h-72 overflow-y-auto"
        >
          <li
            v-for="doc in docs"
            :key="doc.id"
          >
            <button
              class="w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors rounded-lg"
              :disabled="picking"
              @click="pickDoc(doc)"
            >
              <UIcon
                name="i-lucide-file-text"
                class="size-4 shrink-0 text-muted"
              />
              <span class="text-sm truncate">{{ doc.title }}</span>
            </button>
          </li>
        </ul>
      </div>
    </template>
  </AppModal>
</template>
