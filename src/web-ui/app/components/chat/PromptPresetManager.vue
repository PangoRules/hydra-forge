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
  loading.value.presets = true
  try {
    const { data } = await api.GET<PromptPresetDto[]>(
      ApiRoutes.Chat.presets.list(selectedGroup.value?.id || undefined)
    )
    presets.value = data ?? []
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
    await fetchGroups()
  } catch (err) {
    toast.showApiError(err as Error)
  } finally {
    saving.value = false
  }
}

async function updateGroup(_groupId: string) {
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
    await fetchPresets()
  } catch (err) {
    toast.showApiError(err as Error)
  } finally {
    saving.value = false
  }
}

async function updatePreset(_presetId: string) {
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
}

function startEditGroup(group: PromptPresetGroupDto) {
  groupFormMode.value = group.id
  formName.value = group.name
  formContent.value = ''
  formGroupId.value = null
  editingGroupId.value = group.id
}

function startCreatePreset() {
  presetFormMode.value = 'create'
  formName.value = ''
  formContent.value = ''
  formGroupId.value = selectedGroup.value?.id || null
}

function startEditPreset(preset: PromptPresetDto) {
  presetFormMode.value = preset.id
  formName.value = preset.name
  formContent.value = preset.content
  formGroupId.value = preset.groupId
  editingPresetId.value = preset.id
}

function cancelGroupForm() {
  groupFormMode.value = null
  formName.value = ''
  formContent.value = ''
  formGroupId.value = null
}

function cancelPresetForm() {
  presetFormMode.value = null
  formName.value = ''
  formContent.value = ''
  formGroupId.value = null
}

onMounted(() => {
  void fetchGroups()
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
          v-if="groupFormMode !== 'create'"
          icon="i-heroicons-plus-circle"
          label="New Group"
          @click="startCreateGroup"
        />
      </div>

      <div
        v-if="groupFormMode === 'create'"
        class="mb-4"
      >
        <UInput
          v-model="formName"
          placeholder="Group name"
          class="mb-2"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            :loading="saving"
            @click="createGroup"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            @click="cancelGroupForm"
          />
        </div>
      </div>

      <div
        v-else-if="groupFormMode === 'edit'"
        class="mb-4"
      >
        <UInput
          v-model="formName"
          placeholder="Group name"
          class="mb-2"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            :loading="saving"
            @click="updateGroup(editingGroupId!)"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            @click="cancelGroupForm"
          />
        </div>
      </div>

      <div class="space-y-2">
        <div
          v-for="group in groups"
          :key="group.id"
          class="flex justify-between items-center p-3 border rounded-lg hover:bg-gray-50"
          @click="selectedGroup = group"
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
            v-if="presetFormMode !== 'create'"
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

      <div
        v-if="presetFormMode === 'create'"
        class="mb-4"
      >
        <UInput
          v-model="formName"
          placeholder="Preset name"
          class="mb-2"
        />
        <UTextarea
          v-model="formContent"
          placeholder="Preset content"
          class="mb-2"
          :rows="4"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            :loading="saving"
            @click="createPreset"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            @click="cancelPresetForm"
          />
        </div>
      </div>

      <div
        v-else-if="presetFormMode === 'edit'"
        class="mb-4"
      >
        <UInput
          v-model="formName"
          placeholder="Preset name"
          class="mb-2"
        />
        <UTextarea
          v-model="formContent"
          placeholder="Preset content"
          class="mb-2"
          :rows="4"
        />
        <div class="flex gap-2">
          <UButton
            label="Save"
            :loading="saving"
            @click="updatePreset(editingPresetId!)"
          />
          <UButton
            label="Cancel"
            variant="ghost"
            @click="cancelPresetForm"
          />
        </div>
      </div>

      <div class="space-y-2">
        <div
          v-for="preset in presets"
          :key="preset.id"
          class="flex justify-between items-start p-3 border rounded-lg"
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

      <div
        v-if="presets.length === 0"
        class="text-center py-8 text-gray-500"
      >
        No presets in this group. Create one to get started.
      </div>
    </div>
  </div>
</template>
