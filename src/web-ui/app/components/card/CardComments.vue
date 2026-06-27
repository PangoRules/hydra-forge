<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

interface CommentResponse {
  id: string
  cardId: string
  authorId: string
  authorUsername: string
  content: string
  createdAt: string
  updatedAt: string
  archivedAt: string | null
  mentionedUserIds: string[]
}

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const api = useApi()
const toast = useAppToast()

const comments = ref<CommentResponse[]>([])
const loading = ref(true)
const newContent = ref('')
const posting = ref(false)

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}

async function fetchComments() {
  loading.value = true
  try {
    const { data } = await api.GET<{ comments: CommentResponse[] }>(
      ApiRoutes.Comments.list(props.projectId, props.cardId)
    )
    comments.value = data?.comments ?? []
  } catch {
    toast.error('Failed to load comments')
  } finally {
    loading.value = false
  }
}

async function postComment() {
  const content = newContent.value.trim()
  if (!content) return
  posting.value = true
  try {
    const { data, error: apiError } = await api.POST<CommentResponse>(
      ApiRoutes.Comments.create(props.projectId, props.cardId),
      { body: { content } }
    )
    if (apiError) throw apiError
    comments.value.push(data as CommentResponse)
    newContent.value = ''
  } catch {
    toast.error('Failed to post comment')
  } finally {
    posting.value = false
  }
}

onMounted(() => fetchComments())
</script>

<template>
  <div class="space-y-4">
    <h3 class="font-medium text-sm">
      Comments
    </h3>

    <div
      v-if="loading"
      class="py-4 text-center text-sm text-muted"
    >
      Loading...
    </div>

    <div
      v-else-if="comments.length === 0"
      class="py-4 text-center text-sm text-muted"
    >
      No comments yet
    </div>

    <ul
      v-else
      class="space-y-3"
    >
      <li
        v-for="comment in comments"
        :key="comment.id"
        class="flex gap-3"
      >
        <UAvatar
          :name="comment.authorUsername"
          size="sm"
          class="flex-shrink-0"
        />
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2">
            <span class="text-sm font-medium">{{ comment.authorUsername }}</span>
            <span class="text-xs text-muted">{{ formatDate(comment.createdAt) }}</span>
          </div>
          <p class="text-sm mt-0.5 break-words">
            {{ comment.content }}
          </p>
        </div>
      </li>
    </ul>

    <form
      class="flex gap-2"
      @submit.prevent="postComment"
    >
      <UTextarea
        v-model="newContent"
        placeholder="Write a comment..."
        size="sm"
        class="flex-1"
        :disabled="posting"
        :rows="2"
      />
      <UButton
        type="submit"
        size="sm"
        :loading="posting"
        :disabled="!newContent.trim() || posting"
      >
        Post
      </UButton>
    </form>
  </div>
</template>
