<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'
import type { AgentPersonalityDto } from '~/types/chat'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'changed': []
}>()

const api = useApi()
const toast = useAppToast()

const personalities = ref<AgentPersonalityDto[]>([])
const loading = ref(false)

// null = list view, 'new' = create form, otherwise the id being edited
const formMode = ref<'new' | string | null>(null)
const formName = ref('')
const formDescription = ref('')
const formSystemPrompt = ref('')
const saving = ref(false)

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

watch(() => props.open, (isOpen) => {
  if (isOpen) void fetchList()
  else formMode.value = null
}, { immediate: true })

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
    emit('changed')
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
    emit('changed')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to archive personality')
  }
}

async function setDefault(p: AgentPersonalityDto) {
  try {
    await api.POST(ApiRoutes.Chat.personalities.setDefault(p.id))
    toast.success(`"${p.name}" set as default`)
    await fetchList()
    emit('changed')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to set default personality')
  }
}
</script>

<template>
  <AppModal
    :open="open"
    title="Manage personalities"
    width="sm:max-w-2xl"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <div
        v-if="loading"
        class="flex items-center justify-center py-8"
      >
        <UIcon
          name="i-lucide-loader-circle"
          class="animate-spin size-6 text-muted"
        />
      </div>

      <template v-else-if="formMode === null">
        <div class="flex justify-end mb-3">
          <UButton
            data-testid="new-personality"
            icon="i-lucide-plus"
            size="xs"
            @click="startCreate"
          >
            New
          </UButton>
        </div>
        <ul class="space-y-2">
          <li
            v-for="p in personalities"
            :key="p.id"
            class="flex items-center gap-2 p-2 rounded border border-gray-200 dark:border-gray-700"
          >
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <span class="font-medium text-sm truncate">{{ p.name }}</span>
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
                class="text-xs text-muted truncate"
              >
                {{ p.description }}
              </p>
            </div>
            <UButton
              v-if="!p.isDefault"
              :data-testid="`set-default-${p.id}`"
              icon="i-lucide-star"
              variant="ghost"
              size="xs"
              title="Set as default"
              @click="setDefault(p)"
            />
            <UButton
              icon="i-lucide-pencil"
              variant="ghost"
              size="xs"
              title="Edit"
              @click="startEdit(p)"
            />
            <UButton
              :data-testid="`archive-${p.id}`"
              icon="i-lucide-archive"
              variant="ghost"
              color="error"
              size="xs"
              title="Archive"
              @click="archive(p)"
            />
          </li>
        </ul>
      </template>

      <template v-else>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-medium text-muted">Name</label>
              <UInput
                v-model="formName"
                data-testid="personality-name-input"
                placeholder="e.g. Senior Developer"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-medium text-muted">Description</label>
              <UInput
                v-model="formDescription"
                data-testid="personality-description-input"
                placeholder="Shown in the personality picker"
              />
            </div>
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-muted">System prompt</label>
            <UTextarea
              v-model="formSystemPrompt"
              data-testid="personality-prompt-input"
              placeholder="Instructions that define the personality's behavior..."
              :rows="6"
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
    </template>
  </AppModal>
</template>
