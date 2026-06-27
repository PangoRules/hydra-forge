<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

interface CardRelationshipDto {
  id: string
  sourceCardId: string
  targetCardId: string
  sourceCardNumber: number
  sourceCardTitle: string
  targetCardNumber: number
  targetCardTitle: string
  type: number
  createdAt: string
  createdByUserId: string
  archivedAt: string | null
}

const props = defineProps<{
  cardId: string
  projectId: string
  readonly?: boolean
}>()

const relationships = ref<CardRelationshipDto[]>([])
const loading = ref(true)

const api = useApi()
const toast = useAppToast()

const relationshipLabel: Record<number, string> = {
  1: 'Blocked by',
  2: 'Precedes',
  3: 'Relates to'
}

const relationshipColor: Record<number, string> = {
  1: 'error',
  2: 'warning',
  3: 'neutral'
}

function relatedTitle(rel: CardRelationshipDto): string {
  return rel.sourceCardId === props.cardId
    ? `#${rel.targetCardNumber} ${rel.targetCardTitle}`
    : `#${rel.sourceCardNumber} ${rel.sourceCardTitle}`
}

type BadgeColor = 'neutral' | 'primary' | 'info' | 'success' | 'warning' | 'error'

function badgeColor(rel: CardRelationshipDto): BadgeColor {
  return relationshipColor[rel.type] as BadgeColor
}

async function fetchRelationships() {
  loading.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Relationships.list(props.projectId, props.cardId))
    if (error) throw error
    relationships.value = (data as { relationships: CardRelationshipDto[] })?.relationships ?? []
  } catch {
    toast.error('Failed to load dependencies')
  } finally {
    loading.value = false
  }
}

onMounted(() => fetchRelationships())
</script>

<template>
  <div class="space-y-2">
    <p class="text-xs font-medium text-muted uppercase">
      Dependencies
    </p>

    <div class="space-y-1 max-h-48 overflow-y-auto">
      <div
        v-if="loading"
        class="text-xs text-muted"
      >
        Loading...
      </div>
      <div
        v-else-if="relationships.length === 0"
        class="text-xs text-muted"
      >
        No dependencies
      </div>

      <div
        v-for="rel in relationships"
        :key="rel.id"
        class="flex items-center gap-2 text-sm"
      >
        <UBadge
          variant="subtle"
          size="xs"
          :color="badgeColor(rel)"
        >
          {{ relationshipLabel[rel.type] }}
        </UBadge>
        <span class="truncate text-xs">{{ relatedTitle(rel) }}</span>
      </div>
    </div>
  </div>
</template>
