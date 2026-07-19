<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { components } from '~/types/api'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  projects: ProjectListResponse[]
  loading: boolean
}>()

const emit = defineEmits<{
  'select': [projectId: string]
  'edit': [projectId: string]
  'toggle-archive': [project: { id: string, name: string, archivedAt: string | null }]
}>()

const columns: TableColumn<ProjectListResponse>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'memberCount', header: 'Members' },
  { accessorKey: 'myRole', header: 'My Role' },
  { accessorKey: 'createdAt', header: 'Created' },
  { id: 'actions', header: '' }
]

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString()
}
</script>

<template>
  <UTable
    :data="projects"
    :columns="columns"
    :loading="loading"
    class="w-full"
    @select="(_e, row) => emit('select', row.original.id)"
  >
    <template #name-cell="{ row }">
      <div class="flex items-center gap-2">
        <span class="font-medium">{{ row.original.name }}</span>
        <UBadge
          v-if="row.original.archivedAt"
          variant="subtle"
          size="xs"
          color="neutral"
        >
          Archived
        </UBadge>
      </div>
    </template>
    <template #createdAt-cell="{ row }">
      {{ formatDate(row.original.createdAt) }}
    </template>
    <template #actions-cell="{ row }">
      <div
        class="flex justify-end gap-1"
        @click.stop
      >
        <UButton
          icon="i-lucide-pencil"
          variant="ghost"
          size="xs"
          :data-testid="`edit-${row.original.id}`"
          @click="emit('edit', row.original.id)"
        />
        <UButton
          :icon="row.original.archivedAt ? 'i-lucide-archive-restore' : 'i-lucide-archive'"
          variant="ghost"
          size="xs"
          :data-testid="`toggle-archive-${row.original.id}`"
          @click="emit('toggle-archive', { id: row.original.id, name: row.original.name, archivedAt: row.original.archivedAt })"
        />
      </div>
    </template>
  </UTable>
</template>
