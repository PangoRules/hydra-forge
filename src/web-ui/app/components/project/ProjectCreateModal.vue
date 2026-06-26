<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'created': []
  'close': []
}>()

const name = ref('')
const description = ref('')
const gitRemoteUrl = ref('')
const gitProvider = ref<string | undefined>(undefined)
const showAdvanced = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)

const gitProviders = [
  { label: 'GitHub', value: 'github' },
  { label: 'GitLab', value: 'gitlab' },
  { label: 'Gitea', value: 'gitea' },
  { label: 'Self-hosted', value: 'self-hosted' }
]

const api = useApi()

function onClose() {
  emit('update:open', false)
  emit('close')
}

function resetForm() {
  name.value = ''
  description.value = ''
  gitRemoteUrl.value = ''
  gitProvider.value = undefined
  showAdvanced.value = false
  error.value = null
}

watch(() => props.open, (val) => {
  if (!val) resetForm()
})

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    const { error: apiError } = await api.POST(ApiRoutes.Projects.create(), {
      body: {
        name: name.value,
        description: description.value,
        gitRemoteUrl: gitRemoteUrl.value || null,
        gitProvider: gitProvider.value ?? null
      }
    })
    if (apiError) throw apiError
    onClose()
    emit('created')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to create project'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppModal
    :open="open"
    title="Create Project"
    :loading="loading"
    :error="error"
    width="sm:max-w-lg"
    @update:open="emit('update:open', $event)"
    @close="onClose"
  >
    <template #body>
      <form
        class="space-y-4 p-4"
        @submit.prevent="handleSubmit"
      >
        <UFormField
          label="Project Name"
          required
        >
          <UInput
            v-model="name"
            placeholder="My Project"
            required
            class="w-full"
          />
        </UFormField>

        <UFormField
          label="Description"
          class="w-full"
        >
          <UTextarea
            v-model="description"
            placeholder="Optional description"
            class="w-full"
          />
        </UFormField>

        <!-- Advanced -->
        <div>
          <UButton
            variant="ghost"
            size="sm"
            class="mb-2"
            @click="showAdvanced = !showAdvanced"
          >
            <UIcon
              :name="showAdvanced ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
              class="size-4"
            />
            <span>Advanced</span>
          </UButton>

          <div
            v-if="showAdvanced"
            class="space-y-4"
          >
            <UFormField
              label="Git Remote URL"
              class="w-full"
            >
              <UInput
                v-model="gitRemoteUrl"
                placeholder="https://github.com/user/repo.git"
                class="w-full"
              />
            </UFormField>

            <UFormField
              label="Git Provider"
              class="w-full"
            >
              <div class="relative">
                <USelect
                  v-model="gitProvider"
                  :items="gitProviders"
                  placeholder="Select provider"
                  class="w-full"
                />
                <UButton
                  v-if="gitProvider"
                  variant="ghost"
                  size="sm"
                  class="absolute inset-y-0 right-6 px-2 hover:!bg-transparent text-gray-400 hover:text-gray-600"
                  icon="i-lucide-x"
                  @click="gitProvider = undefined"
                />
              </div>
            </UFormField>
          </div>
        </div>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          @click="onClose"
        >
          Cancel
        </UButton>
        <UButton
          type="submit"
          :loading="loading"
          @click="handleSubmit"
        >
          Create
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
