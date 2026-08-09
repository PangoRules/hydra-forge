<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import type { PromptPresetDto, PromptPresetGroupDto } from '~/types/chat'

const api = useApi()
const toast = useAppToast()

const groups = ref<PromptPresetGroupDto[]>([])
const presets = ref<PromptPresetDto[]>([])
const selectedGroup = ref<PromptPresetGroupDto | null>(null)

const loading = ref({
  groups: false,
  presets: false
})

const formMode = ref<'create' | 'edit' | null>(null)
const formName = ref('')
const formContent = ref('')
const formGroupId = ref<string | null>(null)
const saving = ref(false)

const dragItem = ref<PromptPresetDto | null>(null)

async function fetchGroups() {
  loading.value.groups = true
  try {
    const { data } = await api.GET<PromptPresetGroupDto[]>(ApiRoutes.Chat.presetGroups.list())
    groups.value = data ?? []
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to load groups')
  } finally {
    loading.value.groups = false
  }
}

async function fetchPresets() {
  loading.value.presets = true
  try {
    const { data } = await api.GET<PromptPresetDto[]>(
      ApiRoutes.Chat.presets.list(selectedGroup.value?.id || undefined)
    )
    presets.value = data ?? []
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to load presets')
  } finally {
    loading.value.presets = false
  }
}

async function createGroup() {
  if (!formName.value.trim()) {
    toast.error('Group name is required')
    return
  }
  saving.value = true
  try {
    await api.POST(ApiRoutes.Chat.presetGroups.create(), {
      body: {
        name: formName.value.trim()
      }
    })
    toast.success('Group created')
    formName.value = ''
    formMode.value = null
    await fetchGroups()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to create group')
  } finally {
    saving.value = false
  }
}

async function updateGroup(group: PromptPresetGroupDto) {
  if (!formName.value.trim()) {
    toast.error('Group name is required')
    return
  }
  saving.value = true
  try {
    await api.PATCH(ApiRoutes.Chat.presetGroups.update(group.id), {
      body: {
        name: formName.value.trim()
      }
    })
    toast.success('Group updated')
    formName.value = ''
    formMode.value = null
    await fetchGroups()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to update group')
  } finally {
    saving.value = false
  }
}

async function archiveGroup(group: PromptPresetGroupDto) {
  try {
    await api.DELETE(ApiRoutes.Chat.presetGroups.archive(group.id))
    toast.success('Group archived')
    await fetchGroups()
    await fetchPresets()
    // If we archived the selected group, clear selection
    if (selectedGroup.value?.id === group.id) {
      selectedGroup.value = null
    }
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to archive group')
  }
}

async function createPreset() {
  if (!formName.value.trim()) {
    toast.error('Preset name is required')
    return
  }
  if (!formContent.value.trim()) {
    toast.error('Preset content is required')
    return
  }
  saving.value = true
  try {
    await api.POST(ApiRoutes.Chat.presets.create(), {
      body: {
        name: formName.value.trim(),
        content: formContent.value,
        groupId: formGroupId.value
      }
    })
    toast.success('Preset created')
    formName.value = ''
    formContent.value = ''
    formGroupId.value = null
    formMode.value = null
    await fetchPresets()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to create preset')
  } finally {
    saving.value = false
  }
}

async function updatePreset(preset: PromptPresetDto) {
  if (!formName.value.trim()) {
    toast.error('Preset name is required')
    return
  }
  if (!formContent.value.trim()) {
    toast.error('Preset content is required')
    return
  }
  saving.value = true
  try {
    await api.PATCH(ApiRoutes.Chat.presets.update(preset.id), {
      body: {
        name: formName.value.trim(),
        content: formContent.value,
        groupId: formGroupId.value
      }
    })
    toast.success('Preset updated')
    formName.value = ''
    formContent.value = ''
    formGroupId.value = null
    formMode.value = null
    await fetchPresets()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to update preset')
  } finally {
    saving.value = false
  }
}

async function archivePreset(preset: PromptPresetDto) {
  try {
    await api.DELETE(ApiRoutes.Chat.presets.archive(preset.id))
    toast.success('Preset archived')
    await fetchPresets()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to archive preset')
  }
}

function startCreateGroup() {
  formMode.value = 'create'
  formName.value = ''
  formContent.value = ''
  formGroupId.value = null
}

function startEditGroup(group: PromptPresetGroupDto) {
  formMode.value = 'edit'
  formName.value = group.name
  formContent.value = ''
  formGroupId.value = null
}

function startCreatePreset() {
  formMode.value = 'create'
  formName.value = ''
  formContent.value = ''
  formGroupId.value = selectedGroup.value?.id || null
}

function startEditPreset(preset: PromptPresetDto) {
  formMode.value = 'edit'
  formName.value = preset.name
  formContent.value = preset.content
  formGroupId.value = preset.groupId
}

function cancelForm() {
  formMode.value = null
  formName.value = ''
  formContent.value = ''
  formGroupId.value = null
}

function handleDrop(preset: PromptPresetDto, newIndex: number) {
  // Update position locally first
  const oldIndex = presets.value.findIndex(p => p.id === preset.id)
  if (oldIndex === -1) return

  const temp = [...presets.value]
  temp.splice(oldIndex, 1)
  temp.splice(newIndex, 0, preset)
  presets.value = temp

  // Update the position on the server
  updatePresetPosition(preset, newIndex)
}

async function updatePresetPosition(preset: PromptPresetDto, newPosition: number) {
  try {
    await api.PATCH(ApiRoutes.Chat.presets.update(preset.id), {
      body: {
        groupId: preset.groupId,
        position: newPosition
      }
    })
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to update preset position')
    // Revert the local change
    await fetchPresets()
  }
}

function handleDragStart(preset: PromptPresetDto, event: DragEvent) {
  dragItem.value = preset
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
  }
}

function handleDragOver(preset: PromptPresetDto, event: DragEvent) {
  event.preventDefault()
}

function handleDropOnPreset(preset: PromptPresetDto, event: DragEvent) {
  event.preventDefault()
  if (!dragItem.value) return

  const oldIndex = presets.value.findIndex(p => p.id === dragItem.value?.id)
  const newIndex = presets.value.findIndex(p => p.id === preset.id)
  
  if (oldIndex !== -1 && newIndex !== -1 && oldIndex !== newIndex) {
    handleDrop(dragItem.value, newIndex)
  }
  
  dragItem.value = null
}

onMounted(() => {
  void fetchGroups()
  void fetchPresets()
})

watch(selectedGroup, () => {
  void fetchPresets()
})
</script>

<template>
  <div class="flex flex-col md:flex-row gap-6">
    <!-- Groups panel -->
    <div class="md:w-1/3">
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-xl font-bold">Prompt Preset Groups</h2>
        <UButton
          v-if="formMode !== 'create'"
          icon="i-heroicons-plus-circle"
          label="New Group"
          @click="startCreateGroup"
        />
      </div>

      <div v-if="formMode === 'create'" class="mb-4">
        <UInput v-model="formName" placeholder="Group name" class="mb-2" />
        <div class="flex gap-2">
          <UButton label="Save" @click="createGroup" :loading="saving" />
          <UButton label="Cancel" @click="cancelForm" variant="ghost" />
        </div>
      </div>

      <div v-else-if="formMode === 'edit'" class="mb-4">
        <UInput v-model="formName" placeholder="Group name" class="mb-2" />
        <div class="flex gap-2">
          <UButton label="Save" @click="updateGroup(selectedGroup!)" :loading="saving" />
          <UButton label="Cancel" @click="cancelForm" variant="ghost" />
        </div>
      </div>

      <div class="space-y-2">
        <div
          v-for="group in groups"
          :key="group.id"
          class="flex justify-between items-center p-3 border rounded-lg hover:bg-gray-50"
        >
          <div class="flex-1">
            <h3 class="font-medium">{{ group.name }}</h3>
            <p class="text-sm text-gray-500">
              {{ group.presets.length }} preset<span v-if="group.presets.length !== 1">s</span>
            </p>
          </div>
          <div class="flex gap-2">
            <UButton
              icon="i-heroicons-pencil"
              variant="ghost"
              size="sm"
              @click="startEditGroup(group)"
            />
            <UButton
              icon="i-heroicons-trash"
              variant="ghost"
              size="sm"
              @click="archiveGroup(group)"
            />
          </div>
        </div>
      </div>

      <div v-if="groups.length === 0" class="text-center py-8 text-gray-500">
        No groups yet. Create one to get started.
      </div>
    </div>

    <!-- Presets panel -->
    <div class="md:w-2/3">
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-xl font-bold">
          {{ selectedGroup ? selectedGroup.name : 'Ungrouped' }} Presets
        </h2>
        <div>
          <UButton
            v-if="formMode !== 'create'"
            icon="i-heroicons-plus-circle"
            label="New Preset"
            @click="startCreatePreset"
          />
          <UButton
            v-if="selectedGroup"
            icon="i-heroicons-arrow-left"
            label="Back to all groups"
            variant="ghost"
            @click="selectedGroup = null"
          />
        </div>
      </div>

      <div v-if="formMode === 'create'" class="mb-4">
        <UInput v-model="formName" placeholder="Preset name" class="mb-2" />
        <UTextarea v-model="formContent" placeholder="Preset content" class="mb-2" rows="4" />
        <div class="flex gap-2">
          <UButton label="Save" @click="createPreset" :loading="saving" />
          <UButton label="Cancel" @click="cancelForm" variant="ghost" />
        </div>
      </div>

      <div v-else-if="formMode === 'edit'" class="mb-4">
        <UInput v-model="formName" placeholder="Preset name" class="mb-2" />
        <UTextarea v-model="formContent" placeholder="Preset content" class="mb-2" rows="4" />
        <div class="flex gap-2">
          <UButton label="Save" @click="updatePreset(selectedGroup!)" :loading="saving" />
          <UButton label="Cancel" @click="cancelForm" variant="ghost" />
        </div>
      </div>

      <div class="space-y-2">
        <div
          v-for="(preset, index) in presets"
          :key="preset.id"
          class="flex justify-between items-start p-3 border rounded-lg"
          draggable="true"
          @dragstart="handleDragStart(preset, $event)"
          @dragover="handleDragOver(preset, $event)"
          @drop="handleDropOnPreset(preset, $event)"
        >
          <div class="flex-1">
            <h3 class="font-medium">{{ preset.name }}</h3>
            <p class="text-sm text-gray-700 mt-1 line-clamp-2">{{ preset.content }}</p>
          </div>
          <div class="flex gap-2 ml-2">
            <UButton
              icon="i-heroicons-pencil"
              variant="ghost"
              size="sm"
              @click="startEditPreset(preset)"
            />
            <UButton
              icon="i-heroicons-trash"
              variant="ghost"
              size="sm"
              @click="archivePreset(preset)"
            />
          </div>
        </div>
      </div>

      <div v-if="presets.length === 0" class="text-center py-8 text-gray-500">
        No presets in this group. Create one to get started.
      </div>
    </div>
  </div>
</template>