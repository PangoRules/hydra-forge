<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { onClickOutside } from '@vueuse/core'

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
  refreshKey?: number
}>()

watch(() => props.refreshKey, fetchComments)

const api = useApi()
const toast = useAppToast()
const board = useBoardStore()
const authStore = useAuthStore()

const comments = ref<CommentResponse[]>([])
const loading = ref(true)
const newContent = ref('')
const posting = ref(false)

// @mention suggestion dropdown — mirrors the backend's MentionExtractor charset
// ([A-Za-z0-9_.-]) so a candidate the user picks always parses server-side.
const textareaRef = ref()
const mentionQuery = ref<string | null>(null)
const mentionStart = ref(0)
const mentionActiveIndex = ref(0)
const mentionBoxRef = ref<HTMLElement | null>(null)

const mentionCandidates = computed(() => {
  if (mentionQuery.value === null) return []
  const q = mentionQuery.value.toLowerCase()
  return board.members
    .filter(m => m.userId !== authStore.user?.userId && m.username.toLowerCase().startsWith(q))
    .slice(0, 5)
})

function getNativeTextarea(): HTMLTextAreaElement | null {
  const el = textareaRef.value?.$el ?? textareaRef.value
  return el?.querySelector?.('textarea') ?? null
}

function updateMentionQuery() {
  const el = getNativeTextarea()
  if (!el) return
  const caret = el.selectionStart ?? newContent.value.length
  const upToCaret = newContent.value.slice(0, caret)
  const match = upToCaret.match(/(?:^|\s)@([A-Za-z0-9_.-]*)$/)
  if (match) {
    mentionQuery.value = match[1] ?? ''
    mentionStart.value = caret - (match[1]?.length ?? 0) - 1
    mentionActiveIndex.value = 0
  } else {
    mentionQuery.value = null
  }
}

function selectMention(username: string) {
  const queryLength = mentionQuery.value?.length ?? 0
  const before = newContent.value.slice(0, mentionStart.value)
  const after = newContent.value.slice(mentionStart.value + 1 + queryLength)
  newContent.value = `${before}@${username} ${after}`
  mentionQuery.value = null

  nextTick(() => {
    const el = getNativeTextarea()
    if (!el) return
    const caret = before.length + username.length + 2
    el.focus()
    el.setSelectionRange(caret, caret)
  })
}

function onTextareaKeydown(e: KeyboardEvent) {
  if (mentionCandidates.value.length === 0) return
  if (e.key === 'ArrowDown') {
    e.preventDefault()
    mentionActiveIndex.value = (mentionActiveIndex.value + 1) % mentionCandidates.value.length
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    mentionActiveIndex.value = (mentionActiveIndex.value - 1 + mentionCandidates.value.length) % mentionCandidates.value.length
  } else if (e.key === 'Enter' || e.key === 'Tab') {
    e.preventDefault()
    selectMention(mentionCandidates.value[mentionActiveIndex.value]!.username)
  } else if (e.key === 'Escape') {
    mentionQuery.value = null
  }
}

onClickOutside(mentionBoxRef, () => {
  mentionQuery.value = null
})

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
      <div class="relative flex-1">
        <UTextarea
          ref="textareaRef"
          v-model="newContent"
          placeholder="Write a comment... (@ to mention)"
          size="sm"
          class="w-full"
          :disabled="posting"
          :rows="2"
          @keydown="onTextareaKeydown"
          @keyup="updateMentionQuery"
          @click="updateMentionQuery"
        />
        <div
          v-if="mentionCandidates.length > 0"
          ref="mentionBoxRef"
          class="absolute bottom-full left-0 mb-1 w-48 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-md shadow-lg py-1 z-10"
        >
          <button
            v-for="(member, idx) in mentionCandidates"
            :key="member.userId"
            type="button"
            class="w-full text-left px-3 py-1.5 text-sm flex items-center gap-2"
            :class="idx === mentionActiveIndex ? 'bg-primary/10 text-primary' : 'hover:bg-gray-100 dark:hover:bg-gray-700'"
            @mousedown.prevent="selectMention(member.username)"
          >
            <UAvatar
              :name="member.username"
              size="xs"
            />
            {{ member.username }}
          </button>
        </div>
      </div>
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
