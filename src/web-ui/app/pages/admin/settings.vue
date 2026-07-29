<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()
const loading = ref(false)

const settings = reactive({
  archivedItemRetentionDays: 730,
  auditLogRetentionDays: 90,
  notificationRetentionDays: 30,
  ntfyServerUrl: '',
  searXngUrl: '',
  brandName: '',
  brandLogoUrl: ''
})

interface SettingsResponse {
  archivedItemRetentionDays: number
  auditLogRetentionDays: number
  notificationRetentionDays: number
  ntfyServerUrl: string | null
  searXngUrl: string | null
  brandName: string | null
  brandLogoUrl: string | null
}

async function loadSettings() {
  loading.value = true
  try {
    const data = await api.GET<SettingsResponse>(ApiRoutes.Admin.settingsGet())
    Object.assign(settings, data)
  } catch (e: unknown) {
    toast.add({ title: e instanceof Error ? e.message : 'Failed to load settings', color: 'error' })
  } finally {
    loading.value = false
  }
}

async function saveSettings(section: string) {
  try {
    const body: Record<string, unknown> = {}
    if (section === 'retention') {
      body.archivedItemRetentionDays = settings.archivedItemRetentionDays
      body.auditLogRetentionDays = settings.auditLogRetentionDays
      body.notificationRetentionDays = settings.notificationRetentionDays
    } else if (section === 'notifications') {
      body.ntfyServerUrl = settings.ntfyServerUrl || null
    } else if (section === 'search') {
      body.searXngUrl = settings.searXngUrl || null
    } else if (section === 'branding') {
      body.brandName = settings.brandName || null
      body.brandLogoUrl = settings.brandLogoUrl || null
    }
    await api.PUT(ApiRoutes.Admin.settingsUpdate(), { body })
    toast.add({ title: 'Settings saved. Changes apply within 5 minutes.', color: 'success' })
  } catch (e: unknown) {
    toast.add({ title: e instanceof Error ? e.message : 'Save failed', color: 'error' })
  }
}

onMounted(() => loadSettings())
</script>

<template>
  <div class="p-6 max-w-2xl">
    <h1 class="text-2xl font-bold mb-6">
      System Settings
    </h1>

    <!-- Retention -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">
        Retention
      </h2>
      <div class="space-y-3">
        <UInput
          v-model.number="settings.archivedItemRetentionDays"
          label="Archived Item Retention (days)"
          type="number"
        />
        <UInput
          v-model.number="settings.auditLogRetentionDays"
          label="Audit Log Retention (days)"
          type="number"
        />
        <UInput
          v-model.number="settings.notificationRetentionDays"
          label="Notification Retention (days)"
          type="number"
        />
      </div>
      <UButton
        label="Save Retention"
        class="mt-3"
        @click="saveSettings('retention')"
      />
    </section>

    <!-- Notifications -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">
        Notifications
      </h2>
      <UInput
        v-model="settings.ntfyServerUrl"
        label="ntfy Server URL"
        placeholder="http://localhost:8083"
      />
      <UButton
        label="Save Notifications"
        class="mt-3"
        @click="saveSettings('notifications')"
      />
    </section>

    <!-- Search -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">
        Search
      </h2>
      <UInput
        v-model="settings.searXngUrl"
        label="SearXNG URL"
        placeholder="http://localhost:8080"
      />
      <UButton
        label="Save Search"
        class="mt-3"
        @click="saveSettings('search')"
      />
    </section>

    <!-- Branding -->
    <section class="mb-8">
      <h2 class="text-lg font-semibold mb-3">
        Branding
      </h2>
      <UInput
        v-model="settings.brandName"
        label="Brand Name"
        placeholder="HydraForge"
      />
      <UInput
        v-model="settings.brandLogoUrl"
        label="Brand Logo URL"
        placeholder="https://..."
      />
      <UButton
        label="Save Branding"
        class="mt-3"
        @click="saveSettings('branding')"
      />
    </section>
  </div>
</template>
