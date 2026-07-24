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
  readonly?: boolean
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

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function isSameAuthor(comment: CommentResponse, prev: CommentResponse | undefined): boolean {
  return !!prev && comment.authorId === prev.authorId
}

function isSameDay(a: string, b: string): boolean {
  const da = new Date(a)
  const db = new Date(b)
  return da.getFullYear() === db.getFullYear()
    && da.getMonth() === db.getMonth()
    && da.getDate() === db.getDate()
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
    const { data } = await api.POST<CommentResponse>(
      ApiRoutes.Comments.create(props.projectId, props.cardId),
      { body: { content } }
    )
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
      class="space-y-0"
    >
      <li
        v-for="(comment, idx) in comments"
        :key="comment.id"
        :class="[isSameAuthor(comment, comments[idx - 1]) ? 'ml-11 -mt-1.5' : 'flex gap-3 mt-3', idx === 0 ? 'mt-0' : '']"
      >
        <template v-if="isSameAuthor(comment, comments[idx - 1])">
          <div class="flex-1 min-w-0 mt-2">
            <div class="flex items-start justify-between gap-2">
              <p class="text-sm wrap-break-word flex-1">
                {{ comment.content }}
              </p>
              <span class="text-xs text-muted whitespace-nowrap shrink-0 ml-2">
                {{ isSameDay(comment.createdAt, comments[idx - 1]!.createdAt) ? formatTime(comment.createdAt) : formatDate(comment.createdAt) }}
              </span>
            </div>
          </div>
        </template>
        <template v-else>
          <UAvatar
            :name="comment.authorUsername"
            size="sm"
            class="shrink-0"
          />
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium">{{ comment.authorUsername }}</span>
              <span class="text-xs text-muted">{{ formatDate(comment.createdAt) }}</span>
            </div>
            <p class="text-sm mt-0.5 wrap-break-word">
              {{ comment.content }}
            </p>
          </div>
        </template>
      </li>
    </ul>

    <form
      v-if="!readonly"
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
