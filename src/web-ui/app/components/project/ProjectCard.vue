<script setup lang="ts">
import type { components } from '~/types/api'
import { onClickOutside } from '@vueuse/core'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  project: ProjectListResponse
}>()

const emit = defineEmits<{
  select: [projectId: string]
  archive: [projectId: string]
  restore: [projectId: string]
  edit: [projectId: string]
}>()

const showMenu = ref(false)
const menuRef = ref<HTMLElement | null>(null)
const menuButtonRef = ref<HTMLElement | null>(null)

function toggleMenu() {
  showMenu.value = !showMenu.value
}

function closeMenu() {
  showMenu.value = false
}

onClickOutside(menuRef, closeMenu, { ignore: [menuButtonRef] })
</script>

<template>
  <UCard
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
              @click="toggleMenu"
            />
          </span>
          <div
            v-if="showMenu"
            ref="menuRef"
            class="absolute right-0 top-full mt-1 bg-white dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700 shadow-lg py-1 z-50 min-w-35"
          >
            <button
              class="w-full text-left px-3 py-1.5 text-xs hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-primary"
              @click="emit('edit', project.id); closeMenu()"
            >
              <UIcon
                name="i-lucide-pencil"
                class="size-3.5"
              />
              Edit
            </button>
            <button
              v-if="!project.archivedAt"
              class="w-full text-left px-3 py-1.5 text-xs hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-red-600 dark:text-red-400"
              @click="emit('archive', project.id); closeMenu()"
            >
              <UIcon
                name="i-lucide-archive"
                class="size-3.5"
              />
              Archive
            </button>
            <button
              v-else
              class="w-full text-left px-3 py-1.5 text-xs hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-primary"
              @click="emit('restore', project.id); closeMenu()"
            >
              <UIcon
                name="i-lucide-archive-restore"
                class="size-3.5"
              />
              Restore
            </button>
          </div>
        </div>
      </div>
    </template>
    <p class="text-sm text-muted line-clamp-2">
      {{ project.description ?? 'No description' }}
    </p>
  </UCard>
</template>
