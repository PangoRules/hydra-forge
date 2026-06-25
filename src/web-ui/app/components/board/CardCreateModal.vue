<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  projectId: string
  columns: ColumnResponse[]
  preselectedColumnId?: string
}>()

const emit = defineEmits<{
  close: []
  created: []
}>()

const isOpen = ref(true)
const api = useApi()
const toast = useToast()

const title = ref('')
const description = ref('')
const cardType = ref(0)
const columnId = ref(props.preselectedColumnId ?? '')
const saving = ref(false)

const canSave = computed(() => title.value.trim().length > 0 && columnId.value.length > 0)

async function handleCreate() {
  if (!canSave.value) return
  saving.value = true
  const { error } = await api.POST(ApiRoutes.Cards.create(props.projectId), {
    body: {
      columnId: columnId.value,
      title: title.value.trim(),
      description: description.value,
      type: cardType.value
    }
  })
  saving.value = false
  if (error) {
    toast.add({ title: 'Failed to create card', color: 'error' })
  } else {
    toast.add({ title: 'Card created', color: 'success' })
    emit('created')
    closeWithAnimation()
  }
}

function closeWithAnimation() {
  isOpen.value = false
  setTimeout(() => emit('close'), 200)
}
</script>

<template>
  <AppModal
    :open="isOpen"
    title="Create card"
    width="sm:max-w-lg"
    @update:open="closeWithAnimation"
    @close="closeWithAnimation"
  >
    <template #body>
      <div class="space-y-4 p-1">
        <div>
          <label class="block text-sm font-medium mb-1">Title *</label>
          <input
            v-model="title"
            class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
            placeholder="Card title"
            @keydown.enter="handleCreate"
          />
        </div>
        <div>
          <label class="block text-sm font-medium mb-1">Description</label>
          <textarea
            v-model="description"
            class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary min-h-[80px]"
            placeholder="Optional description"
          />
        </div>
        <div class="flex gap-4">
          <div class="flex-1">
            <label class="block text-sm font-medium mb-1">Type</label>
            <select
              v-model="cardType"
              class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
            >
              <option :value="0">Task</option>
              <option :value="1">Bug</option>
              <option :value="2">Epic</option>
              <option :value="3">Spec</option>
              <option :value="4">Idea</option>
            </select>
          </div>
          <div class="flex-1">
            <label class="block text-sm font-medium mb-1">Column *</label>
            <select
              v-model="columnId"
              class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
              :disabled="!!preselectedColumnId"
            >
              <option
                v-for="col in columns"
                :key="col.id"
                :value="col.id"
              >
                {{ col.name }}
              </option>
            </select>
          </div>
        </div>
      </div>
    </template>
    <template #footer>
      <div class="flex justify-end gap-3 w-full">
        <UButton variant="ghost" @click="closeWithAnimation">
          Cancel
        </UButton>
        <UButton
          :disabled="!canSave"
          :loading="saving"
          color="primary"
          @click="handleCreate"
        >
          Create
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
