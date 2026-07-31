<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { components } from '~/types/api'
import DataTable from '~/components/shared/DataTable.vue'
import ProjectCard from '~/components/project/ProjectCard.vue'
import { formatDateOnly } from '~/lib/date'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  projects: ProjectListResponse[]
  loading: boolean
  page: number
  pageSize: number
  totalCount: number
  fillHeight?: boolean
}>()

const emit = defineEmits<{
  'select': [projectId: string]
  'edit': [projectId: string]
  'toggle-archive': [project: { id: string, name: string, archivedAt: string | null }]
  'update:page': [number]
  'update:pageSize': [number]
}>()

const columns: TableColumn<ProjectListResponse>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'memberCount', header: 'Members' },
  { accessorKey: 'myRole', header: 'My Role' },
  { accessorKey: 'createdAt', header: 'Created' },
  { id: 'actions', header: '' }
]

function displayRole(role: number | string | null): string {
  if (role === null) return '—'
  if (typeof role === 'string') return role
  // MemberRole enum: Owner=0, Member=1 — but check the actual enum values
  const roles: Record<number, string> = { 0: 'Owner', 1: 'Member' }
  return roles[role] ?? String(role)
}
</script>

<template>
  <DataTable
    :data="projects"
    :columns="columns"
    :loading="loading"
    :page="page"
    :page-size="pageSize"
    :total-count="totalCount"
    :page-size-options="[5, 10, 15]"
    :row-key="(item: ProjectListResponse) => item.id"
    :fill-height="fillHeight"
    selectable
    @update:page="emit('update:page', $event)"
    @update:page-size="emit('update:pageSize', $event)"
    @select="(item) => emit('select', item.id)"
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
    <template #myRole-cell="{ row }">
      {{ displayRole(row.original.myRole) }}
    </template>
    <template #createdAt-cell="{ row }">
      {{ formatDateOnly(row.original.createdAt) }}
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

    <template #card="{ item }">
      <ProjectCard
        :project="item"
        @select="emit('select', $event)"
        @toggle-archive="emit('toggle-archive', $event)"
        @edit="emit('edit', $event)"
      />
    </template>
  </DataTable>
</template>
