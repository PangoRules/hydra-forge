<script setup lang="ts">
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'
import Link from '@tiptap/extension-link'
import TaskList from '@tiptap/extension-task-list'
import TaskItem from '@tiptap/extension-task-item'
import { Table } from '@tiptap/extension-table'
import TableRow from '@tiptap/extension-table-row'
import TableHeader from '@tiptap/extension-table-header'
import TableCell from '@tiptap/extension-table-cell'
import { marked } from 'marked'
import { onMounted, onUnmounted } from 'vue'
import { turndownService as turndown, htmlToMarkdown } from '~/lib/document-markdown'

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
const savedOriginalHtml = ref('')
const isFullscreen = ref(false)

function toggleFullscreen() {
  isFullscreen.value = !isFullscreen.value
}

onMounted(() => document.addEventListener('keydown', handleKeydown))
onUnmounted(() => document.removeEventListener('keydown', handleKeydown))

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && isFullscreen.value) {
    isFullscreen.value = false
  }
}

// Copying a .md file's raw text (from a terminal, another editor, a bare-text
// clipboard) carries no HTML on the clipboard at all — ProseMirror's default
// plain-text paste just inserts it as literal characters (one paragraph per
// line, "**bold**" stays literal), which is exactly what corrupted the Phase 7
// docs pasted in from docs/specs. A conservative signal check — real HTML-source
// pastes (copying from a webpage, another rich editor) always carry an HTML
// clipboard entry too, so this only fires for genuine plain-text sources.
const MARKDOWN_SIGNAL = /^#{1,6}\s|^[-*+]\s|^\d+\.\s|^>\s|\*\*[^*]+\*\*|`[^`]+`|^\|.*\|$|^```|\[.+\]\(.+\)/m
function looksLikeMarkdown(text: string): boolean {
  return MARKDOWN_SIGNAL.test(text)
}

const editor = useEditor({
  content: props.modelValue,
  editable: props.editable,
  extensions: [
    StarterKit.configure({
      heading: { levels: [1, 2, 3] },
      // StarterKit bundles its own Link since v3 — disabled in favor of the
      // explicitly configured instance below (openOnClick/autolink/rel), else
      // both register under the same name ("Duplicate extension names" warning).
      link: false
    }),
    Placeholder.configure({ placeholder: props.placeholder }),
    Link.configure({
      openOnClick: false,
      autolink: true,
      HTMLAttributes: { rel: 'noopener noreferrer nofollow', target: '_blank' }
    }),
    TaskList,
    TaskItem.configure({ nested: true }),
    Table.configure({ resizable: false }),
    TableRow,
    TableHeader,
    TableCell
  ],
  editorProps: {
    attributes: {
      class: 'focus-visible:outline-2 focus-visible:outline-primary p-3 min-h-[180px]'
    },
    handlePaste(view, event) {
      const clipboard = event.clipboardData
      if (!clipboard) return false
      const text = clipboard.getData('text/plain')
      // If the clipboard also carries an HTML representation, this came from a
      // rich source (a webpage, another WYSIWYG editor) — TipTap's default HTML
      // paste already handles that correctly. Only plain-text-only sources need
      // markdown parsing.
      if (!text || clipboard.getData('text/html') || !looksLikeMarkdown(text)) return false

      const html = marked.parse(text, { async: false, breaks: true, gfm: true }) as string
      return view.pasteHTML(html.trim(), event)
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
    savedOriginalHtml.value = editor.value.getHTML()
    sourceText.value = turndown.turndown(savedOriginalHtml.value)
  } else {
    // If source text unchanged, restore original HTML — no lossy round-trip
    if (sourceText.value === turndown.turndown(savedOriginalHtml.value)) {
      editor.value.commands.setContent(savedOriginalHtml.value, { emitUpdate: false })
      return
    }
    // User edited source: convert markdown → HTML, emit to trigger save
    const html = marked.parse(sourceText.value, { async: false, breaks: true, gfm: true }) as string
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
function toggleStrike() {
  editor.value?.chain().focus().toggleStrike().run()
}
function toggleInlineCode() {
  editor.value?.chain().focus().toggleCode().run()
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
function toggleTaskList() {
  editor.value?.chain().focus().toggleTaskList().run()
}
function toggleCodeBlock() {
  editor.value?.chain().focus().toggleCodeBlock().run()
}
function toggleBlockquote() {
  editor.value?.chain().focus().toggleBlockquote().run()
}
function insertHorizontalRule() {
  editor.value?.chain().focus().setHorizontalRule().run()
}
function insertTable() {
  editor.value?.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()
}
function addTableRow() {
  editor.value?.chain().focus().addRowAfter().run()
}
function addTableColumn() {
  editor.value?.chain().focus().addColumnAfter().run()
}
function deleteTable() {
  editor.value?.chain().focus().deleteTable().run()
}

// Link popover — prefills with the current mark's href when the cursor is
// already inside a link (edit-in-place), blank otherwise (insert new).
const linkPopoverOpen = ref(false)
const linkUrlInput = ref('')

function openLinkPopover() {
  linkUrlInput.value = (editor.value?.getAttributes('link').href as string | undefined) ?? ''
  linkPopoverOpen.value = true
}
function applyLink() {
  const url = linkUrlInput.value.trim()
  if (!url) {
    editor.value?.chain().focus().unsetLink().run()
  } else {
    editor.value?.chain().focus().extendMarkRange('link').setLink({ href: url }).run()
  }
  linkPopoverOpen.value = false
}
function removeLink() {
  editor.value?.chain().focus().unsetLink().run()
  linkPopoverOpen.value = false
}

function isActive(name: string, attrs?: Record<string, unknown>): boolean {
  return editor.value?.isActive(name, attrs) ?? false
}

function toggleSource() {
  sourceMode.value = !sourceMode.value
}

// CardSpec/CardPlan read this for "Export as Markdown" — the editor's own
// modelValue is HTML (its native authoring format); Markdown is derived
// on demand via the same Turndown pipeline the Source toggle already uses,
// rather than duplicating conversion logic at each call site.
defineExpose({
  getMarkdown: () => htmlToMarkdown(editor.value?.getHTML() ?? '')
})
</script>

<template>
  <div :class="isFullscreen ? 'fixed inset-0 z-[100] bg-default flex flex-col' : 'border rounded-md'">
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
        :class="{ 'bg-primary/10': isActive('bold'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleBold"
      />
      <UButton
        icon="i-lucide-italic"
        variant="ghost"
        size="xs"
        title="Italic"
        :color="isActive('italic') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('italic'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleItalic"
      />
      <UButton
        icon="i-lucide-strikethrough"
        variant="ghost"
        size="xs"
        title="Strikethrough"
        :color="isActive('strike') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('strike'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleStrike"
      />
      <UButton
        icon="i-lucide-code"
        variant="ghost"
        size="xs"
        title="Inline Code"
        :color="isActive('code') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('code'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleInlineCode"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-heading-1"
        variant="ghost"
        size="xs"
        title="Heading 1"
        :color="isActive('heading', { level: 1 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 1 }), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleHeading(1)"
      />
      <UButton
        icon="i-lucide-heading-2"
        variant="ghost"
        size="xs"
        title="Heading 2"
        :color="isActive('heading', { level: 2 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 2 }), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleHeading(2)"
      />
      <UButton
        icon="i-lucide-heading-3"
        variant="ghost"
        size="xs"
        title="Heading 3"
        :color="isActive('heading', { level: 3 }) ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('heading', { level: 3 }), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleHeading(3)"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-list"
        variant="ghost"
        size="xs"
        title="Bullet List"
        :color="isActive('bulletList') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('bulletList'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleBulletList"
      />
      <UButton
        icon="i-lucide-list-ordered"
        variant="ghost"
        size="xs"
        title="Ordered List"
        :color="isActive('orderedList') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('orderedList'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleOrderedList"
      />
      <UButton
        icon="i-lucide-list-todo"
        variant="ghost"
        size="xs"
        title="Task List"
        :color="isActive('taskList') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('taskList'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleTaskList"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UButton
        icon="i-lucide-square-code"
        variant="ghost"
        size="xs"
        title="Code Block"
        :color="isActive('codeBlock') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('codeBlock'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleCodeBlock"
      />
      <UButton
        icon="i-lucide-quote"
        variant="ghost"
        size="xs"
        title="Blockquote"
        :color="isActive('blockquote') ? 'primary' : 'neutral'"
        :class="{ 'bg-primary/10': isActive('blockquote'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleBlockquote"
      />
      <UButton
        icon="i-lucide-minus"
        variant="ghost"
        size="xs"
        title="Horizontal Rule"
        color="neutral"
        class="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1"
        @click="insertHorizontalRule"
      />

      <span class="w-px h-4 bg-gray-300 dark:bg-gray-600 mx-1" />

      <UPopover
        v-model:open="linkPopoverOpen"
        :content="{ align: 'start' }"
      >
        <UButton
          icon="i-lucide-link"
          variant="ghost"
          size="xs"
          title="Link"
          :color="isActive('link') ? 'primary' : 'neutral'"
          :class="{ 'bg-primary/10': isActive('link'), 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
          @click="openLinkPopover"
        />
        <template #content>
          <div class="flex items-center gap-1 p-2">
            <UInput
              v-model="linkUrlInput"
              placeholder="https://..."
              size="xs"
              autofocus
              class="w-56"
              @keydown.enter="applyLink"
            />
            <UButton
              size="xs"
              @click="applyLink"
            >
              Apply
            </UButton>
            <UButton
              v-if="isActive('link')"
              size="xs"
              variant="ghost"
              color="error"
              @click="removeLink"
            >
              Remove
            </UButton>
          </div>
        </template>
      </UPopover>

      <template v-if="isActive('table')">
        <UButton
          icon="i-lucide-rows-3"
          variant="ghost"
          size="xs"
          title="Add Row"
          color="neutral"
          class="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1"
          @click="addTableRow"
        />
        <UButton
          icon="i-lucide-columns-3"
          variant="ghost"
          size="xs"
          title="Add Column"
          color="neutral"
          class="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1"
          @click="addTableColumn"
        />
        <UButton
          icon="i-lucide-table-2"
          variant="ghost"
          size="xs"
          title="Delete Table"
          color="error"
          class="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1"
          @click="deleteTable"
        />
      </template>
      <UButton
        v-else
        icon="i-lucide-table"
        variant="ghost"
        size="xs"
        title="Insert Table"
        color="neutral"
        class="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1"
        @click="insertTable"
      />

      <div class="flex-1" />

      <UButton
        v-if="showSourceToggle"
        icon="i-lucide-code-xml"
        variant="ghost"
        size="xs"
        title="Toggle Markdown Source"
        label="Source"
        :class="{ 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleSource"
      />
      <UButton
        :icon="isFullscreen ? 'i-lucide-minimize' : 'i-lucide-maximize'"
        variant="ghost"
        size="xs"
        :title="isFullscreen ? 'Exit fullscreen' : 'Fullscreen'"
        @click="toggleFullscreen"
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
        :class="{ 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1': true }"
        @click="toggleSource"
      />
    </div>

    <!-- WYSIWYG editor -->
    <div :class="isFullscreen ? 'flex-1 flex flex-col overflow-y-auto' : 'max-h-[280px] overflow-y-auto'">
      <EditorContent
        v-show="!sourceMode"
        :editor="editor"
        :class="isFullscreen ? 'markdown-editor-content markdown-editor-fullscreen' : 'markdown-editor-content'"
        role="textbox"
        aria-multiline="true"
        aria-label="Markdown editor"
      />

      <!-- Source textarea -->
      <textarea
        v-if="sourceMode"
        v-model="sourceText"
        class="w-full p-3 font-mono text-sm leading-loose resize-none focus-visible:outline-2 focus-visible:outline-primary bg-transparent min-h-[180px]"
        :placeholder="props.placeholder"
        :class="isFullscreen ? 'h-full' : ''"
      />
    </div>
  </div>
</template>

<style>
/* Fullscreen: editor fills available height, scrolls when content overflows */
.markdown-editor-fullscreen {
  flex: 1;
  display: flex;
  flex-direction: column;
}
.markdown-editor-fullscreen .ProseMirror {
  flex: 1;
  min-height: unset;
}

/* Editor content typography — replaces broken prose plugin */
.markdown-editor-content {
  line-height: 1.625;
}
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
.markdown-editor-content hr {
  border: none;
  border-top: 1px solid #d1d5db;
  margin: 1rem 0;
}
.dark .markdown-editor-content hr {
  border-top-color: #4b5563;
}
.markdown-editor-content a {
  color: var(--ui-primary);
  text-decoration: underline;
}
.markdown-editor-content s {
  opacity: 0.7;
}
/* Task list — tiptap renders <ul data-type="taskList"><li data-checked="true|false"> */
.markdown-editor-content ul[data-type='taskList'] {
  list-style: none;
  padding-left: 0.25rem;
}
.markdown-editor-content ul[data-type='taskList'] li {
  display: flex;
  align-items: flex-start;
  gap: 0.4rem;
}
.markdown-editor-content ul[data-type='taskList'] li > label {
  margin-top: 0.2rem;
  user-select: none;
}
.markdown-editor-content ul[data-type='taskList'] li > div {
  flex: 1;
}
.markdown-editor-content table {
  border-collapse: collapse;
  margin: 0.5rem 0;
  width: 100%;
}
.markdown-editor-content th,
.markdown-editor-content td {
  border: 1px solid #d1d5db;
  padding: 0.35rem 0.5rem;
  text-align: left;
}
.dark .markdown-editor-content th,
.dark .markdown-editor-content td {
  border-color: #4b5563;
}
.markdown-editor-content th {
  background: #f3f4f6;
  font-weight: 600;
}
.dark .markdown-editor-content th {
  background: #1f2937;
}
</style>
