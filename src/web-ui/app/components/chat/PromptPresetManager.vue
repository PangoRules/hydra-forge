<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { PromptPresetDto, PromptPresetGroupDto } from '~/types/chat'

const api = useApi()
const toast = useAppToast()

const groups = ref<PromptPresetGroupDto[]>([])
const presets = ref<PromptPresetDto[]>([])
const selectedGroup = ref<PromptPresetGroupDto | null>(null)
const editingGroupId = ref<string | null>(null)
const editingPresetId = ref<string | null>(null)

const loading = ref({
  groups: false,
  presets: false
})

const groupFormMode = ref<'create' | 'edit' | string | null>(null)
const presetFormMode = ref<'create' | 'edit' | string | null>(null)
const formName = ref('')
const formContent = ref('')
const formGroupId = ref<string | null>(null)
const saving = ref(false)
const presetFetchCounter = ref(0)

async function fetchGroups() {
  loading.value.groups = true
  try {
    const { data } = await api.GET<PromptPresetGroupDto[]>(ApiRoutes.Chat.presetGroups.list())
    groups.value = data ?? []
  } catch (err) {
    toast.showApiError(err as Error)
  } finally {
    loading.value.groups = false
  }
}

async function fetchPresets() {
  const currentCounter = ++presetFetchCounter.value
  loading.value.presets = true

  try {
    const { data } = await api.GET<PromptPresetDto[]>(
      ApiRoutes.Chat.presets.list(selectedGroup.value?.id || undefined)
    )
    // Discard stale responses
    if (currentCounter === presetFetchCounter.value) {
      presets.value = data ?? []
    }
  } catch (err) {
    toast.showApiError(err as Error)
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
    groupFormMode.value = null
    presetFormMode.value = null
    await fetchGroups()
  } catch (err) {
    toast.showApiError(err as Error)
  } finally {
    saving.value = false
  }
}

async function updateGroup() {
  if (!formName.value.trim()) {
    toast.error('Group name is required')
    return
  }
  saving.value = true
  try {
    await api.PATCH(ApiRoutes.Chat.presetGroups.update(editingGroupId.value!), {
      body: {
        name: formName.value.trim()
      }
    })
    toast.success('Group updated')
    formName.value = ''
    groupFormMode.value = null
    presetFormMode.value = null
    await fetchGroups()
  } catch (err) {
    toast.showApiError(err as Error)
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
    toast.showApiError(err as Error)
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
    presetFormMode.value = null
    groupFormMode.value = null
    await fetchGroups()
    await fetchPresets()
  } catch (err) {
    toast.showApiError(err as Error)
  } finally {
    saving.value = false
  }
}

async function updatePreset() {
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
    await api.PATCH(ApiRoutes.Chat.presets.update(editingPresetId.value!), {
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
    presetFormMode.value = null
    groupFormMode.value = null
    await fetchGroups()
    await fetchPresets()
  } catch (err) {
    toast.showApiError(err as Error)
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
    toast.showApiError(err as Error)
  }
}

function startCreateGroup() {
  groupFormMode.value = 'create'
  formName.value = ''
  formContent.value = ''
  formGroupId.value = null
  presetFormMode.value = null
}

function startEditGroup(group: PromptPresetGroupDto) {
  groupFormMode.value = 'edit'
  formName.value = group.name
  formContent.value = ''
  formGroupId.value = null
  editingGroupId.value = group.id
  presetFormMode.value = null
}

function startCreatePreset() {
  presetFormMode.value = 'create'
  formName.value = ''
  formContent.value = ''
  formGroupId.value = selectedGroup.value?.id || null
  groupFormMode.value = null
}

function startEditPreset(preset: PromptPresetDto) {
  presetFormMode.value = 'edit'
  formName.value = preset.name
  formContent.value = preset.content
  formGroupId.value = preset.groupId
  editingPresetId.value = preset.id
  groupFormMode.value = null
}

function cancelGroupForm() {
  groupFormMode.value = null
  formName.value = ''
  formContent.value = ''
  formGroupId.value = null
  presetFormMode.value = null
}

function cancelPresetForm() {
  presetFormMode.value = null
  formName.value = ''
  formContent.value = ''
  formGroupId.value = null
  groupFormMode.value = null
}

function handlePresetDragStart(index: number, event: DragEvent) {
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', index.toString())
  }
}

async function handlePresetDrop(dragIndex: number, event: DragEvent) {
  if (!event.dataTransfer) return

  const dropIndex = parseInt(event.dataTransfer.getData('text/plain') || '0', 10)
  if (dragIndex === dropIndex) return

  const draggedPreset = presets.value[dragIndex]
  if (!draggedPreset) return

  const [removed] = presets.value.splice(dragIndex, 1)
  presets.value.splice(dropIndex, 0, removed ?? draggedPreset)

  // Update positions
  for (let i = 0; i < presets.value.length; i++) {
    presets.value[i]!.position = i
  }

  // Send PATCH to update positions
  const movedPreset = presets.value[dropIndex]
  if (!movedPreset) return

  try {
    await api.PATCH(ApiRoutes.Chat.presets.update(movedPreset.id), {
      body: { position: dropIndex }
    })
  } catch (err) {
    toast.showApiError(err as Error)
  }
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
        <h2 class="text-xl font-bold">
          Prompt Preset Groups
        </h2>
        <UButton
          v-if="groupFormMode !== 'create' && presetFormMode !== 'create'"
          icon="i-heroicons-plus-circle"
          label="New Group"
          data-testid="new-group"
          :disabled="loading.groups || loading.presets"
          @click="startCreateGroup"
        />
      </div>

      <div
        v-if="groupFormMode === 'create'"
        class="mb-4"
      >
        <label
          for="group-name-input"
          class="block text-sm font-medium text-gray-700 mb-1"
        >Group name</label>
        <UInput
          id="group-name-input"
          v-model="formName"
          placeholder="Group name"
          data-testid="group-name-input"
          class="mb-2"
          :aria-busy="saving"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            data-testid="save-group"
            :loading="saving"
            :disabled="loading.groups || loading.presets"
            @click="createGroup"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            :disabled="loading.groups || loading.presets"
            @click="cancelGroupForm"
          />
        </div>
      </div>

      <div
        v-else-if="groupFormMode === 'edit'"
        class="mb-4"
      >
        <label
          for="group-name-input-edit"
          class="block text-sm font-medium text-gray-700 mb-1"
        >Group name</label>
        <UInput
          id="group-name-input-edit"
          v-model="formName"
          placeholder="Group name"
          class="mb-2"
          :aria-busy="saving"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            :loading="saving"
            :disabled="loading.groups || loading.presets"
            @click="updateGroup()"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            :disabled="loading.groups || loading.presets"
            @click="cancelGroupForm"
          />
        </div>
      </div>

      <div class="space-y-2">
        <div
          v-for="group in groups"
          :key="group.id"
          role="button"
          tabindex="0"
          class="flex justify-between items-center p-3 border rounded-lg hover:bg-gray-50 cursor-pointer"
          :class="{ 'bg-blue-50': selectedGroup?.id === group.id }"
          :data-testid="`group-row-${group.id}`"
          @click="selectedGroup = group"
          @keydown.enter="selectedGroup = group"
          @keydown.space="selectedGroup = group"
        >
          <div class="flex-1">
            <h3 class="font-medium">
              {{ group.name }}
            </h3>
            <p class="text-sm text-gray-500">
              {{ group.presets.length }} preset<span v-if="group.presets.length !== 1">s</span>
            </p>
          </div>
          <div class="flex gap-2">
            <UButton
              icon="i-heroicons-pencil"
              variant="ghost"
              size="sm"
              :aria-label="`Edit ${group.name}`"
              @click.stop="startEditGroup(group)"
            />
            <UButton
              icon="i-heroicons-trash"
              variant="ghost"
              size="sm"
              :data-testid="`archive-group-${group.id}`"
              :aria-label="`Archive ${group.name}`"
              @click.stop="archiveGroup(group)"
            />
          </div>
        </div>
      </div>

      <div
        v-if="groups.length === 0"
        class="text-center py-8 text-gray-500"
      >
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
            v-if="presetFormMode !== 'create' && groupFormMode !== 'create'"
            icon="i-heroicons-plus-circle"
            label="New Preset"
            data-testid="new-preset"
            :disabled="loading.groups || loading.presets"
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

      <div
        v-if="presetFormMode === 'create'"
        class="mb-4"
      >
        <label
          for="preset-name-input"
          class="block text-sm font-medium text-gray-700 mb-1"
        >Preset name</label>
        <UInput
          id="preset-name-input"
          v-model="formName"
          placeholder="Preset name"
          data-testid="preset-name-input"
          class="mb-2"
          :aria-busy="saving"
        />
        <label
          for="preset-content-input"
          class="block text-sm font-medium text-gray-700 mb-1"
        >Preset content</label>
        <UTextarea
          id="preset-content-input"
          v-model="formContent"
          placeholder="Preset content"
          data-testid="preset-content-input"
          class="mb-2"
          :rows="4"
          :aria-busy="saving"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            data-testid="save-preset"
            :loading="saving"
            :disabled="loading.groups || loading.presets"
            @click="createPreset"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            :disabled="loading.groups || loading.presets"
            @click="cancelPresetForm"
          />
        </div>
      </div>

      <div
        v-else-if="presetFormMode === 'edit'"
        class="mb-4"
      >
        <label
          for="preset-name-input-edit"
          class="block text-sm font-medium text-gray-700 mb-1"
        >Preset name</label>
        <UInput
          id="preset-name-input-edit"
          v-model="formName"
          placeholder="Preset name"
          data-testid="preset-name-input"
          class="mb-2"
          :aria-busy="saving"
        />
        <label
          for="preset-content-input-edit"
          class="block text-sm font-medium text-gray-700 mb-1"
        >Preset content</label>
        <UTextarea
          id="preset-content-input-edit"
          v-model="formContent"
          placeholder="Preset content"
          data-testid="preset-content-input"
          class="mb-2"
          :rows="4"
          :aria-busy="saving"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            data-testid="save-preset"
            :loading="saving"
            :disabled="loading.groups || loading.presets"
            @click="updatePreset()"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            :disabled="loading.groups || loading.presets"
            @click="cancelPresetForm"
          />
        </div>
      </div>

      <div class="space-y-2">
        <div
          v-for="(preset, index) in presets"
          :key="preset.id"
          class="flex justify-between items-start p-3 border rounded-lg"
          draggable="true"
          @dragstart="handlePresetDragStart(index, $event)"
          @dragover.prevent
          @drop="handlePresetDrop(index, $event)"
        >
          <div class="flex-1">
            <h3 class="font-medium">
              {{ preset.name }}
            </h3>
            <p class="text-sm text-gray-700 mt-1 line-clamp-2">
              {{ preset.content }}
            </p>
          </div>
          <div class="flex gap-2 ml-2">
            <UButton
              icon="i-heroicons-pencil"
              variant="ghost"
              size="sm"
              :data-testid="`edit-preset-${preset.id}`"
              :aria-label="`Edit ${preset.name}`"
              @click.stop="startEditPreset(preset)"
            />
            <UButton
              icon="i-heroicons-trash"
              variant="ghost"
              size="sm"
              :data-testid="`archive-preset-${preset.id}`"
              :aria-label="`Archive ${preset.name}`"
              @click.stop="archivePreset(preset)"
            />
          </div>
        </div>
      </div>

      <div
        v-if="presets.length === 0"
        class="text-center py-8 text-gray-500"
      >
        No presets in this group. Create one to get started.
      </div>
    </div>
  </div>
</template>
