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

const turndown = new TurndownService({
  codeBlockStyle: 'fenced',
  headingStyle: 'atx'
})

// Override <br> rule: use bare \n instead of two-trailing-spaces \n
// paired with marked breaks:true, bare \n round-trips cleanly (no whitespace-only lines)
turndown.addRule('lineBreak', {
  filter: ['br'],
  replacement: () => '\n'
})

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
      class: 'focus:outline-none p-3 min-h-[150px] max-h-[400px] overflow-y-auto'
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
    const html = marked.parse(sourceText.value, { async: false, breaks: true }) as string
    const clean = html.trim()
    editor.value.commands.setContent(clean, { emitUpdate: false })
    emit('update:modelValue', clean)
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
        title="Bold"
        :color="isActive('bold') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('bold') }"
        @click="toggleBold"
      />
      <UButton
        icon="i-lucide-italic"
        variant="ghost"
        size="xs"
        title="Italic"
        :color="isActive('italic') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('italic') }"
        @click="toggleItalic"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-heading-1"
        variant="ghost"
        size="xs"
        title="Heading 1"
        :color="isActive('heading', { level: 1 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 1 }) }"
        @click="toggleHeading(1)"
      />
      <UButton
        icon="i-lucide-heading-2"
        variant="ghost"
        size="xs"
        title="Heading 2"
        :color="isActive('heading', { level: 2 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 2 }) }"
        @click="toggleHeading(2)"
      />
      <UButton
        icon="i-lucide-heading-3"
        variant="ghost"
        size="xs"
        title="Heading 3"
        :color="isActive('heading', { level: 3 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 3 }) }"
        @click="toggleHeading(3)"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-list"
        variant="ghost"
        size="xs"
        title="Bullet List"
        :color="isActive('bulletList') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('bulletList') }"
        @click="toggleBulletList"
      />
      <UButton
        icon="i-lucide-list-ordered"
        variant="ghost"
        size="xs"
        title="Ordered List"
        :color="isActive('orderedList') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('orderedList') }"
        @click="toggleOrderedList"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-code"
        variant="ghost"
        size="xs"
        title="Code Block"
        :color="isActive('codeBlock') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('codeBlock') }"
        @click="toggleCodeBlock"
      />
      <UButton
        icon="i-lucide-quote"
        variant="ghost"
        size="xs"
        title="Blockquote"
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
        title="Toggle Markdown Source"
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
      class="markdown-editor-content"
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

<style>
/* Editor content typography — replaces broken prose plugin */
.markdown-editor-content h1 {
  font-size: 1.75rem;
  font-weight: 700;
  line-height: 1.2;
  margin: 1rem 0 0.5rem;
}
.markdown-editor-content h2 {
  font-size: 1.4rem;
  font-weight: 600;
  line-height: 1.25;
  margin: 0.75rem 0 0.4rem;
}
.markdown-editor-content h3 {
  font-size: 1.15rem;
  font-weight: 600;
  line-height: 1.3;
  margin: 0.5rem 0 0.3rem;
}
.markdown-editor-content ul {
  list-style: disc;
  padding-left: 1.5rem;
  margin: 0.25rem 0;
}
.markdown-editor-content ol {
  list-style: decimal;
  padding-left: 1.5rem;
  margin: 0.25rem 0;
}
.markdown-editor-content li {
  margin: 0.15rem 0;
}
.markdown-editor-content p {
  margin: 0.25rem 0;
  line-height: 1.6;
}
.markdown-editor-content blockquote {
  border-left: 3px solid #d1d5db;
  padding-left: 0.75rem;
  margin: 0.5rem 0;
  opacity: 0.8;
}
.dark .markdown-editor-content blockquote {
  border-left-color: #4b5563;
}
.markdown-editor-content pre {
  background: #f3f4f6;
  border-radius: 6px;
  padding: 0.75rem;
  overflow-x: auto;
  margin: 0.5rem 0;
}
.dark .markdown-editor-content pre {
  background: #1f2937;
}
.markdown-editor-content code {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 0.875rem;
}
.markdown-editor-content pre code {
  background: none;
  padding: 0;
}
</style>
