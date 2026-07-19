<script setup lang="ts">
const props = defineProps<{
  search: string
  role: string
  sortBy: string
  sortDescending: boolean
  showArchived: boolean
}>()

const emit = defineEmits<{
  'update:search': [string]
  'update:role': [string]
  'update:sortBy': [string]
  'update:sortDescending': [boolean]
  'update:showArchived': [boolean]
}>()

const roleOptions = [
  { label: 'All roles', value: 'all' },
  { label: 'Owner', value: 'Owner' },
  { label: 'Member', value: 'Member' }
]

const sortOptions = [
  { label: 'Newest first', value: 'CreatedAt:true' },
  { label: 'Oldest first', value: 'CreatedAt:false' },
  { label: 'Name (A-Z)', value: 'Name:false' },
  { label: 'Name (Z-A)', value: 'Name:true' },
  { label: 'Recently updated', value: 'UpdatedAt:true' }
]

const sortValue = computed({
  get: () => `${props.sortBy}:${props.sortDescending}`,
  set: (val: string) => {
    const [field, desc] = val.split(':')
    emit('update:sortBy', field!)
    emit('update:sortDescending', desc === 'true')
  }
})

function handleRoleUpdate(event: string) {
  emit('update:role', event === 'all' ? '' : String(event))
}
</script>

<template>
  <div class="flex flex-wrap items-center gap-3">
    <UInput
      :model-value="search"
      placeholder="Search projects..."
      icon="i-lucide-search"
      class="flex-1 min-w-[200px]"
      data-testid="project-search-input"
      @update:model-value="emit('update:search', String($event))"
    />
    <USelect
      :model-value="role"
      :items="roleOptions"
      class="w-36"
      data-testid="project-role-select"
      @update:model-value="handleRoleUpdate($event)"
    />
    <USelect
      v-model="sortValue"
      :items="sortOptions"
      class="w-44"
      data-testid="project-sort-select"
    />
    <div class="flex items-center gap-2">
      <USwitch
        :model-value="showArchived"
        @update:model-value="emit('update:showArchived', Boolean($event))"
      />
      <span class="text-sm text-muted">Show archived</span>
    </div>
  </div>
</template>
