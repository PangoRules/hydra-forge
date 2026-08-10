<script setup lang="ts">
import { UiRoutes, ApiRoutes } from '~/lib/routes'
import type { DocumentDto } from '~/types/chat'
import type { TableColumn } from '@nuxt/ui'
import DocumentUploader from '~/components/chat/DocumentUploader.vue'
import { formatDateOnly } from '~/lib/date'

definePageMeta({ middleware: ['auth'] })

const api = useApi()
const toast = useAppToast()

const documents = ref<DocumentDto[]>([])
const loading = ref(false)
const archiving = ref(new Set<string>())

async function loadDocuments() {
  loading.value = true
  try {
    const { data } = await api.GET<DocumentDto[]>(ApiRoutes.Chat.documents.list())
    documents.value = data ?? []
  } catch (err) {
    toast.showApiError(err as Error)
  } finally {
    loading.value = false
  }
}

async function archiveDocument(doc: DocumentDto) {
  archiving.value.add(doc.id)
  try {
    await api.DELETE(ApiRoutes.Chat.documents.archive(doc.id))
    toast.success(`"${doc.title}" archived`)
    await loadDocuments()
  } catch (err) {
    toast.showApiError(err as Error)
  } finally {
    archiving.value.delete(doc.id)
  }
}

const columns: TableColumn<DocumentDto>[] = [
  {
    accessorKey: 'title',
    header: 'Title'
  },
  {
    accessorKey: 'contentType',
    header: 'Type'
  },
  {
    accessorKey: 'version',
    header: 'Version'
  },
  {
    accessorKey: 'updatedAt',
    header: 'Last Modified',
    cell: ({ row }) => formatDateOnly(row.original.updatedAt)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => {
      return h('button', {
        type: 'button',
        class: 'text-red-500 hover:text-red-700 text-sm',
        disabled: archiving.value.has(row.original.id),
        onClick: () => archiveDocument(row.original)
      }, 'Archive')
    }
  }
]

// Load on mount
onMounted(() => {
  void loadDocuments()
})
</script>

<template>
  <div class="flex flex-col h-full">
    <!-- Header -->
    <div class="shrink-0 flex items-center gap-3 p-4 border-b border-gray-200 dark:border-gray-700">
      <UButton
        icon="i-lucide-arrow-left"
        variant="ghost"
        color="neutral"
        size="sm"
        aria-label="Back to chats"
        :to="UiRoutes.Chats"
      />
      <h1 class="text-lg font-bold">
        My Documents
      </h1>
    </div>

    <!-- Scrollable content -->
    <div class="flex-1 min-h-0 overflow-y-auto p-4 space-y-6">
      <!-- Upload area -->
      <section>
        <h2 class="text-sm font-semibold text-muted mb-2 uppercase tracking-wide">
          Upload
        </h2>
        <DocumentUploader @uploaded="loadDocuments" />
      </section>

      <!-- Document list -->
      <section>
        <h2 class="text-sm font-semibold text-muted mb-2 uppercase tracking-wide">
          Documents
        </h2>

        <!-- Loading -->
        <div
          v-if="loading"
          class="flex justify-center py-12"
        >
          <UIcon
            name="i-lucide-loader-circle"
            class="size-6 animate-spin text-muted"
          />
        </div>

        <!-- Empty state -->
        <div
          v-else-if="documents.length === 0"
          class="text-center py-12 text-muted"
        >
          <UIcon
            name="i-lucide-file-x"
            class="size-10 mx-auto mb-3"
          />
          <p class="text-sm">
            No documents yet.
          </p>
          <p class="text-xs mt-1">
            Upload a text, markdown, code, CSV, or HTML file to get started.
          </p>
        </div>

        <!-- Mobile: card grid -->
        <div
          v-else
          :class="`grid gap-3 grid-cols-1 md:hidden`"
        >
          <div
            v-for="doc in documents"
            :key="doc.id"
            class="rounded-lg border border-gray-200 dark:border-gray-700 p-4 space-y-2"
          >
            <div class="flex items-start justify-between gap-2">
              <h3 class="font-medium text-sm text-foreground break-words">
                {{ doc.title }}
              </h3>
              <span class="shrink-0 text-xs text-muted font-mono">
                v{{ doc.version }}
              </span>
            </div>
            <div class="flex items-center gap-2 flex-wrap">
              <UBadge
                :label="doc.contentType"
                color="neutral"
                variant="soft"
                size="sm"
              />
              <span class="text-xs text-muted">
                {{ formatDateOnly(doc.updatedAt) }}
              </span>
            </div>
            <button
              type="button"
              class="text-red-500 hover:text-red-700 text-sm min-h-8"
              :disabled="archiving.has(doc.id)"
              @click="archiveDocument(doc)"
            >
              Archive
            </button>
          </div>
        </div>

        <!-- Desktop: table -->
        <div
          v-if="!loading && documents.length > 0"
          :class="`hidden md:block`"
        >
          <UTable
            :data="documents"
            :columns="columns"
            :row-key="(row: DocumentDto) => row.id"
          />
        </div>
      </section>
    </div>
  </div>
</template>
