<script setup lang="ts">
import type { components } from '~/types/api'
import { onClickOutside } from '@vueuse/core'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  projects: ProjectListResponse[]
  loading: boolean
}>()

const emit = defineEmits<{
  select: [projectId: string]
  archive: [projectId: string]
  restore: [projectId: string]
  edit: [projectId: string]
}>()

const openMenuId = ref<string | null>(null)
const menuRef = ref<HTMLElement | null>(null)
const menuButtonRef = ref<HTMLElement | null>(null)

function toggleMenu(projectId: string) {
  openMenuId.value = openMenuId.value === projectId ? null : projectId
}

function closeMenu() {
  openMenuId.value = null
}

onClickOutside(menuRef, closeMenu, { ignore: [menuButtonRef] })
</script>

<template>
  <div
    v-if="loading"
    class="flex justify-center items-center p-8 min-h-[200px]"
  >
    <UIcon
      name="i-lucide-loader"
      class="animate-spin size-8"
    />
  </div>

  <div
    v-else-if="projects.length === 0"
    class="text-center p-8 text-muted min-h-[200px] flex items-center justify-center"
  >
    <p>No projects yet. Create your first project!</p>
  </div>

  <div
    v-else
    class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
  >
    <UCard
      v-for="project in projects"
      :key="project.id"
      class="cursor-pointer hover:ring-2 hover:ring-primary transition-shadow"
      :class="project.archivedAt ? 'opacity-60' : ''"
      @click="emit('select', project.id)"
    >
      <template #header>
        <div class="flex items-center justify-between gap-2">
          <div class="flex items-center gap-2 min-w-0">
            <h3 class="font-semibold truncate">
              {{ project.name }}
            </h3>
            <UBadge
              v-if="project.archivedAt"
              variant="subtle"
              size="xs"
              color="neutral"
            >
              Archived
            </UBadge>
            <UBadge
              v-if="project.memberCount"
              variant="subtle"
              size="xs"
              color="info"
            >
              {{ project.memberCount }} member{{ project.memberCount === 1 ? '' : 's' }}
            </UBadge>
          </div>
          <div
            class="relative shrink-0"
            @click.stop
          >
            <span ref="menuButtonRef">
              <UButton
                icon="i-lucide-ellipsis-vertical"
                variant="ghost"
                size="xs"
                @click="toggleMenu(project.id)"
              />
            </span>
            <div
              v-if="openMenuId === project.id"
              ref="menuRef"
              class="absolute right-0 top-full mt-1 bg-white dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700 shadow-lg py-1 z-50 min-w-35"
            >
              <button
                class="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-primary"
                @click="emit('edit', project.id); closeMenu()"
              >
                <UIcon
                  name="i-lucide-pencil"
                  class="size-4"
                />
                Edit project
              </button>
              <button
                v-if="!project.archivedAt"
                class="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-red-600 dark:text-red-400"
                @click="emit('archive', project.id); closeMenu()"
              >
                <UIcon
                  name="i-lucide-archive"
                  class="size-4"
                />
                Archive project
              </button>
              <button
                v-else
                class="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-primary"
                @click="emit('restore', project.id); closeMenu()"
              >
                <UIcon
                  name="i-lucide-archive-restore"
                  class="size-4"
                />
                Restore project
              </button>
            </div>
          </div>
        </div>
      </template>
      <p class="text-sm text-muted line-clamp-2">
        {{ project.description ?? 'No description' }}
      </p>
    </UCard>
  </div>
</template>
