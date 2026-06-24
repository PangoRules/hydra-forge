<script setup lang="ts">
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'
import TurndownService from 'turndown'
import { marked } from 'marked'

const props = withDefaults(defineProps<{
  modelValue: string
  placeholder?: string
  editable?: boolean
  showToolbar?: boolean
  showSourceToggle?: boolean
}>(), {
  placeholder: 'Write something...',
  editable: true,
  showToolbar: true,
  showSourceToggle: true
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const sourceMode = ref(false)
const sourceText = ref('')
const editorReady = ref(false)

const turndown = new TurndownService()

const editor = useEditor({
  content: props.modelValue,
  editable: props.editable,
  extensions: [
    StarterKit.configure({
      heading: { levels: [1, 2, 3] }
    }),
    Placeholder.configure({ placeholder: props.placeholder })
  ],
  editorProps: {
    attributes: {
      class: 'prose prose-sm max-w-none focus:outline-none p-3 min-h-[150px] max-h-[400px] overflow-y-auto'
    }
  },
  onUpdate({ editor: ed }) {
    if (!sourceMode.value) {
      emit('update:modelValue', ed.getHTML())
    }
  },
  onCreate() {
    editorReady.value = true
  }
})

watch(() => props.modelValue, (val) => {
  if (editor.value && editor.value.getHTML() !== val && !sourceMode.value) {
    editor.value.commands.setContent(val, { emitUpdate: false })
  }
})

watch(() => props.editable, (val) => {
  editor.value?.setEditable(val)
})

// Sync source text with editor when switching to source mode
watch(sourceMode, (isSource) => {
  if (!editor.value) return
  if (isSource) {
    sourceText.value = turndown.turndown(editor.value.getHTML())
  } else {
    // Switching back: convert markdown to HTML, set in editor, emit
    const html = marked.parse(sourceText.value, { async: false }) as string
    editor.value.commands.setContent(html, { emitUpdate: false })
    emit('update:modelValue', html)
  }
})

onBeforeUnmount(() => {
  editor.value?.destroy()
})

// Toolbar actions
function toggleBold() {
  editor.value?.chain().focus().toggleBold().run()
}
function toggleItalic() {
  editor.value?.chain().focus().toggleItalic().run()
}
function toggleHeading(level: 1 | 2 | 3) {
  editor.value?.chain().focus().toggleHeading({ level }).run()
}
function toggleBulletList() {
  editor.value?.chain().focus().toggleBulletList().run()
}
function toggleOrderedList() {
  editor.value?.chain().focus().toggleOrderedList().run()
}
function toggleCodeBlock() {
  editor.value?.chain().focus().toggleCodeBlock().run()
}
function toggleBlockquote() {
  editor.value?.chain().focus().toggleBlockquote().run()
}

function isActive(name: string, attrs?: Record<string, unknown>): boolean {
  return editor.value?.isActive(name, attrs) ?? false
}

function toggleSource() {
  sourceMode.value = !sourceMode.value
}
</script>

<template>
  <div :class="{ 'border rounded-md': editable }">
    <!-- Toolbar -->
    <div
      v-if="editable && showToolbar && !sourceMode"
      class="flex items-center gap-0.5 px-2 py-1.5 border-b bg-gray-50 dark:bg-gray-800 rounded-t-md overflow-x-auto"
    >
      <UButton
        icon="i-lucide-bold"
        variant="ghost"
        size="xs"
        :color="isActive('bold') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('bold') }"
        @click="toggleBold"
      />
      <UButton
        icon="i-lucide-italic"
        variant="ghost"
        size="xs"
        :color="isActive('italic') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('italic') }"
        @click="toggleItalic"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-heading-1"
        variant="ghost"
        size="xs"
        :color="isActive('heading', { level: 1 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 1 }) }"
        @click="toggleHeading(1)"
      />
      <UButton
        icon="i-lucide-heading-2"
        variant="ghost"
        size="xs"
        :color="isActive('heading', { level: 2 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 2 }) }"
        @click="toggleHeading(2)"
      />
      <UButton
        icon="i-lucide-heading-3"
        variant="ghost"
        size="xs"
        :color="isActive('heading', { level: 3 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 3 }) }"
        @click="toggleHeading(3)"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-list"
        variant="ghost"
        size="xs"
        :color="isActive('bulletList') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('bulletList') }"
        @click="toggleBulletList"
      />
      <UButton
        icon="i-lucide-list-ordered"
        variant="ghost"
        size="xs"
        :color="isActive('orderedList') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('orderedList') }"
        @click="toggleOrderedList"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-code"
        variant="ghost"
        size="xs"
        :color="isActive('codeBlock') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('codeBlock') }"
        @click="toggleCodeBlock"
      />
      <UButton
        icon="i-lucide-quote"
        variant="ghost"
        size="xs"
        :color="isActive('blockquote') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('blockquote') }"
        @click="toggleBlockquote"
      />

      <div class="flex-1" />

      <UButton
        v-if="showSourceToggle"
        icon="i-lucide-code-xml"
        variant="ghost"
        size="xs"
        label="Source"
        @click="toggleSource"
      />
    </div>

    <!-- Source mode bar (when in source mode) -->
    <div
      v-if="editable && showToolbar && sourceMode && showSourceToggle"
      class="flex items-center px-2 py-1.5 border-b bg-gray-50 dark:bg-gray-800 rounded-t-md"
    >
      <span class="text-xs text-muted font-mono">Markdown</span>
      <div class="flex-1" />
      <UButton
        icon="i-lucide-eye"
        variant="ghost"
        size="xs"
        label="Preview"
        @click="toggleSource"
      />
    </div>

    <!-- WYSIWYG editor -->
    <EditorContent
      v-show="!sourceMode"
      :editor="editor"
    />

    <!-- Source textarea -->
    <textarea
      v-if="sourceMode"
      v-model="sourceText"
      class="w-full p-3 font-mono text-sm resize-none focus:outline-none bg-transparent"
      :class="{ 'min-h-[150px] max-h-[400px]': true }"
      :placeholder="props.placeholder"
    />
  </div>
</template>
