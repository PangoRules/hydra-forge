<script setup lang="ts">
import type { ComputedRef } from 'vue'
import { ApiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useAppToast()
const loading = ref(false)

const settings = reactive({
  archivedItemRetentionDays: 730,
  auditLogRetentionDays: 90,
  notificationRetentionDays: 30,
  ntfyServerUrl: '',
  searXngUrl: '',
  brandName: '',
  brandLogoUrl: '',
  aiNarrativeGenerationTimeUtc: null as string | null
})

const saving = reactive({
  retention: false,
  notifications: false,
  search: false,
  branding: false
})

interface SettingsResponse {
  archivedItemRetentionDays: number
  auditLogRetentionDays: number
  notificationRetentionDays: number
  ntfyServerUrl: string | null
  searXngUrl: string | null
  brandName: string | null
  brandLogoUrl: string | null
  aiNarrativeGenerationTimeUtc: string | null
}

async function loadSettings() {
  loading.value = true
  try {
    const { data } = await api.GET<SettingsResponse>(ApiRoutes.Admin.settingsGet())
    if (data) Object.assign(settings, data)
  } catch (e: unknown) {
    toast.error(e instanceof Error ? e.message : 'Failed to load settings')
  } finally {
    loading.value = false
  }
}

function retentionFieldError(value: number): string | undefined {
  return Number.isInteger(value) && value >= 1 ? undefined : 'Must be a whole number of at least 1'
}

function requiredFieldError(value: string | null): string | undefined {
  return value?.trim() ? undefined : 'Required'
}

const archivedItemsError = computed(() => retentionFieldError(settings.archivedItemRetentionDays))
const auditLogError = computed(() => retentionFieldError(settings.auditLogRetentionDays))
const notificationRetentionError = computed(() => retentionFieldError(settings.notificationRetentionDays))
const retentionHasErrors = computed(() => !!(archivedItemsError.value || auditLogError.value || notificationRetentionError.value))

const aiNarrativeTimeModel = computed({
  get: () => {
    if (!settings.aiNarrativeGenerationTimeUtc) return ''
    const parts = settings.aiNarrativeGenerationTimeUtc.split(':')
    return parts.length >= 2 ? `${parts[0]}:${parts[1]}` : ''
  },
  set: (val: string) => {
    settings.aiNarrativeGenerationTimeUtc = val
      ? val.split(':').slice(0, 2).join(':') + ':00'
      : null
  }
})

const ntfyServerUrlError = computed(() => requiredFieldError(settings.ntfyServerUrl))
const searXngUrlError = computed(() => requiredFieldError(settings.searXngUrl))
const brandNameError = computed(() => requiredFieldError(settings.brandName))

const sectionHasErrors: Record<keyof typeof saving, ComputedRef<boolean>> = {
  retention: retentionHasErrors,
  notifications: computed(() => !!ntfyServerUrlError.value),
  search: computed(() => !!searXngUrlError.value),
  branding: computed(() => !!brandNameError.value)
}

// Required-field errors only render once the user has interacted with that
// field (or tried to save) — otherwise every optional-turned-required field
// shows as invalid the instant the page loads with nothing typed in yet.
const touched = reactive({
  ntfyServerUrl: false,
  searXngUrl: false,
  brandName: false
})

const ntfyServerUrlDisplayError = computed(() => touched.ntfyServerUrl ? ntfyServerUrlError.value : undefined)
const searXngUrlDisplayError = computed(() => touched.searXngUrl ? searXngUrlError.value : undefined)
const brandNameDisplayError = computed(() => touched.brandName ? brandNameError.value : undefined)

async function saveSettings(section: keyof typeof saving) {
  if (section === 'notifications') touched.ntfyServerUrl = true
  if (section === 'search') touched.searXngUrl = true
  if (section === 'branding') touched.brandName = true
  if (sectionHasErrors[section].value) return

  saving[section] = true
  try {
    const body: Record<string, unknown> = {}
    if (section === 'retention') {
      body.archivedItemRetentionDays = settings.archivedItemRetentionDays
      body.auditLogRetentionDays = settings.auditLogRetentionDays
      body.notificationRetentionDays = settings.notificationRetentionDays
      body.aiNarrativeGenerationTimeUtc = settings.aiNarrativeGenerationTimeUtc
    } else if (section === 'notifications') {
      body.ntfyServerUrl = settings.ntfyServerUrl
    } else if (section === 'search') {
      body.searXngUrl = settings.searXngUrl
    } else if (section === 'branding') {
      body.brandName = settings.brandName
      body.brandLogoUrl = settings.brandLogoUrl
    }
    await api.PUT(ApiRoutes.Admin.settingsUpdate(), { body })
    toast.success('Settings saved. Changes apply within 5 minutes (cache TTL) or on next housekeeping run.', 6000)
  } catch (e: unknown) {
    toast.error(e instanceof Error ? e.message : 'Save failed')
  } finally {
    saving[section] = false
  }
}

onMounted(() => loadSettings())
</script>

<template>
  <div class="p-6">
    <div class="mb-6">
      <h1 class="text-2xl font-bold">
        System Settings
      </h1>
      <p class="text-sm text-muted">
        Global configuration for this HydraForge instance.
      </p>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <UCard class="lg:col-span-2">
        <template #header>
          <h2 class="font-semibold">
            Retention
          </h2>
          <p class="text-sm text-muted">
            How long archived items, audit logs, and notifications are kept before permanent deletion.
            No background job deletes them yet — these limits take effect once the housekeeping job ships.
          </p>
        </template>

        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <UFormField
            label="Archived Items"
            description="Days"
            :error="archivedItemsError"
          >
            <UInput
              v-model.number="settings.archivedItemRetentionDays"
              type="number"
              class="w-full"
              :min="1"
            />
          </UFormField>
          <UFormField
            label="Audit Log"
            description="Days"
            :error="auditLogError"
          >
            <UInput
              v-model.number="settings.auditLogRetentionDays"
              type="number"
              class="w-full"
              :min="1"
            />
          </UFormField>
          <UFormField
            label="Notifications"
            description="Days"
            :error="notificationRetentionError"
          >
            <UInput
              v-model.number="settings.notificationRetentionDays"
              type="number"
              class="w-full"
              :min="1"
            />
          </UFormField>
        </div>

        <template #footer>
          <div class="flex justify-end">
            <UButton
              label="Save Retention"
              :loading="saving.retention"
              :disabled="retentionHasErrors"
              @click="saveSettings('retention')"
            />
          </div>
        </template>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            Nightly Jobs
          </h2>
          <p class="text-sm text-muted">
            Scheduled background work. A restart is required for a changed time to take effect (Hangfire re-registers the recurring job on startup, not live).
          </p>
        </template>

        <UFormField
          label="AI Narrative Generation Time"
          description="UTC"
        >
          <UInput
            v-model="aiNarrativeTimeModel"
            type="time"
            class="w-full"
          />
        </UFormField>

        <template #footer>
          <div class="flex justify-end">
            <UButton
              label="Save Nightly Jobs"
              :loading="saving.retention"
              @click="saveSettings('retention')"
            />
          </div>
        </template>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            Notifications
          </h2>
          <p class="text-sm text-muted">
            ntfy server used to deliver push notifications.
          </p>
        </template>

        <UFormField
          label="ntfy Server URL"
          :error="ntfyServerUrlDisplayError"
        >
          <UInput
            v-model="settings.ntfyServerUrl"
            placeholder="http://localhost:8083"
            class="w-full"
            @blur="touched.ntfyServerUrl = true"
          />
        </UFormField>

        <template #footer>
          <div class="flex justify-end">
            <UButton
              label="Save Notifications"
              :loading="saving.notifications"
              :disabled="!!ntfyServerUrlError"
              @click="saveSettings('notifications')"
            />
          </div>
        </template>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            Search
          </h2>
          <p class="text-sm text-muted">
            SearXNG instance used for AI web search.
          </p>
        </template>

        <UFormField
          label="SearXNG URL"
          :error="searXngUrlDisplayError"
        >
          <UInput
            v-model="settings.searXngUrl"
            placeholder="http://localhost:8080"
            class="w-full"
            @blur="touched.searXngUrl = true"
          />
        </UFormField>

        <template #footer>
          <div class="flex justify-end">
            <UButton
              label="Save Search"
              :loading="saving.search"
              :disabled="!!searXngUrlError"
              @click="saveSettings('search')"
            />
          </div>
        </template>
      </UCard>

      <UCard class="lg:col-span-2">
        <template #header>
          <h2 class="font-semibold">
            Branding
          </h2>
          <p class="text-sm text-muted">
            Instance name and logo shown across the Web UI and TUI.
          </p>
        </template>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <UFormField
            label="Brand Name"
            :error="brandNameDisplayError"
          >
            <UInput
              v-model="settings.brandName"
              placeholder="HydraForge"
              class="w-full"
              @blur="touched.brandName = true"
            />
          </UFormField>
          <UFormField label="Brand Logo URL">
            <UInput
              v-model="settings.brandLogoUrl"
              placeholder="https://..."
              class="w-full"
            />
          </UFormField>
        </div>

        <template #footer>
          <div class="flex justify-end">
            <UButton
              label="Save Branding"
              :loading="saving.branding"
              :disabled="!!brandNameError"
              @click="saveSettings('branding')"
            />
          </div>
        </template>
      </UCard>
    </div>
  </div>
</template>
