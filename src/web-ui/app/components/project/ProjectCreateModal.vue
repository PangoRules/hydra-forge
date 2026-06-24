<script setup lang="ts">
const emit = defineEmits<{
  created: []
  close: []
}>()

const name = ref('')
const description = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

const api = useApi()

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    const { error: apiError } = await api.POST('/api/Projects', {
      body: { name: name.value, description: description.value, gitRemoteUrl: null, gitProvider: null }
    })
    if (apiError) throw apiError
    emit('created')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to create project'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <UModal
    :open="true"
    @close="emit('close')"
  >
    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">
          Create Project
        </h2>
      </template>

      <form
        class="space-y-4"
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
          />
        </UFormField>

        <UFormField label="Description">
          <UTextarea
            v-model="description"
            placeholder="Optional description"
          />
        </UFormField>

        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          :title="error"
        />

        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            @click="emit('close')"
          >
            Cancel
          </UButton>
          <UButton
            type="submit"
            :loading="loading"
          >
            Create
          </UButton>
        </div>
      </form>
    </UCard>
  </UModal>
</template>
