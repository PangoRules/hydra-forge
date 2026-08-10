<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { AgentPersonalityDto } from '~/types/chat'

const api = useApi()
const toast = useAppToast()

const personalities = ref<AgentPersonalityDto[]>([])
const loading = ref(false)
const saving = ref(false)

const formMode = ref<'new' | string | null>(null)
const formName = ref('')
const formDescription = ref('')
const formSystemPrompt = ref('')

onMounted(() => void fetchList())

async function fetchList() {
  loading.value = true
  try {
    const { data } = await api.GET<AgentPersonalityDto[]>(ApiRoutes.Chat.personalities.list())
    personalities.value = data ?? []
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to load personalities')
  } finally {
    loading.value = false
  }
}

function startCreate() {
  formMode.value = 'new'
  formName.value = ''
  formDescription.value = ''
  formSystemPrompt.value = ''
}

function startEdit(p: AgentPersonalityDto) {
  formMode.value = p.id
  formName.value = p.name
  formDescription.value = p.description ?? ''
  formSystemPrompt.value = p.systemPrompt
}

function cancelForm() {
  formMode.value = null
}

async function saveForm() {
  if (!formName.value.trim()) {
    toast.error('Name is required')
    return
  }
  saving.value = true
  try {
    if (formMode.value === 'new') {
      await api.POST(ApiRoutes.Chat.personalities.create(), {
        body: {
          name: formName.value.trim(),
          description: formDescription.value.trim() || null,
          systemPrompt: formSystemPrompt.value,
          isDefault: false
        }
      })
      toast.success('Personality created')
    } else if (formMode.value) {
      await api.PATCH(ApiRoutes.Chat.personalities.update(formMode.value), {
        body: {
          name: formName.value.trim(),
          description: formDescription.value.trim() || null,
          systemPrompt: formSystemPrompt.value
        }
      })
      toast.success('Personality updated')
    }
    formMode.value = null
    await fetchList()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to save personality')
  } finally {
    saving.value = false
  }
}

async function archive(p: AgentPersonalityDto) {
  try {
    await api.DELETE(ApiRoutes.Chat.personalities.archive(p.id))
    toast.success(`"${p.name}" archived`)
    await fetchList()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to archive personality')
  }
}

async function setDefault(p: AgentPersonalityDto) {
  try {
    await api.POST(ApiRoutes.Chat.personalities.setDefault(p.id))
    toast.success(`"${p.name}" set as default`)
    await fetchList()
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to set default personality')
  }
}
</script>

<template>
  <div>
    <div class="flex justify-between items-center mb-4">
      <h2 class="text-lg font-semibold">
        Personalities
      </h2>
      <UButton
        v-if="formMode === null"
        data-testid="new-personality"
        icon="i-lucide-plus"
        @click="startCreate"
      >
        New Personality
      </UButton>
    </div>

    <div
      v-if="loading"
      class="flex items-center justify-center py-12"
    >
      <UIcon
        name="i-lucide-loader-circle"
        class="animate-spin size-6 text-muted"
      />
    </div>

    <template v-else-if="formMode === null">
      <div
        v-if="personalities.length === 0"
        class="text-muted text-sm py-8 text-center"
      >
        No personalities yet.
      </div>
      <ul
        v-else
        class="space-y-3"
      >
        <li
          v-for="p in personalities"
          :key="p.id"
          class="p-4 rounded-lg border border-gray-200 dark:border-gray-700"
        >
          <div class="flex items-start gap-3">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-1">
                <span class="font-medium">{{ p.name }}</span>
                <UBadge
                  v-if="p.isDefault"
                  size="xs"
                  color="primary"
                >
                  Default
                </UBadge>
              </div>
              <p
                v-if="p.description"
                class="text-sm text-muted mb-2"
              >
                {{ p.description }}
              </p>
              <p
                class="text-xs text-muted font-mono truncate"
              >
                {{ p.systemPrompt }}
              </p>
            </div>
            <div class="flex items-center gap-1 shrink-0">
              <UButton
                v-if="!p.isDefault"
                :data-testid="`set-default-${p.id}`"
                icon="i-lucide-star"
                variant="ghost"
                size="sm"
                title="Set as default"
                @click.stop="setDefault(p)"
              />
              <UButton
                :data-testid="`edit-${p.id}`"
                icon="i-lucide-pencil"
                variant="ghost"
                size="sm"
                title="Edit"
                @click.stop="startEdit(p)"
              />
              <UButton
                :data-testid="`archive-${p.id}`"
                icon="i-lucide-trash"
                variant="ghost"
                color="error"
                size="sm"
                title="Archive"
                @click.stop="archive(p)"
              />
            </div>
          </div>
        </li>
      </ul>
    </template>

    <template v-else>
      <div class="space-y-4 max-w-2xl">
        <div class="grid grid-cols-2 gap-4">
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium">Name</label>
            <UInput
              v-model="formName"
              data-testid="personality-name-input"
              placeholder="e.g. Senior Developer"
            />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium">Description</label>
            <UInput
              v-model="formDescription"
              data-testid="personality-description-input"
              placeholder="Shown in the personality picker"
            />
          </div>
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium">System prompt</label>
          <UTextarea
            v-model="formSystemPrompt"
            data-testid="personality-prompt-input"
            placeholder="Instructions that define the personality's behavior..."
            :rows="8"
          />
        </div>
        <div class="flex justify-end gap-2">
          <UButton
            variant="ghost"
            color="neutral"
            :disabled="saving"
            @click="cancelForm"
          >
            Cancel
          </UButton>
          <UButton
            data-testid="save-personality"
            :loading="saving"
            @click="saveForm"
          >
            Save
          </UButton>
        </div>
      </div>
    </template>
  </div>
</template>
