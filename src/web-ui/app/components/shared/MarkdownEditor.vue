<script setup lang="ts">
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'

const props = withDefaults(defineProps<{
  modelValue: string
  placeholder?: string
  editable?: boolean
}>(), {
  placeholder: 'Write something...',
  editable: true
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const editor = useEditor({
  content: props.modelValue,
  editable: props.editable,
  extensions: [
    StarterKit,
    Placeholder.configure({ placeholder: props.placeholder })
  ],
  onUpdate({ editor }) {
    emit('update:modelValue', editor.getHTML())
  }
})

watch(() => props.modelValue, (val) => {
  if (editor.value && editor.value.getHTML() !== val) {
    editor.value.commands.setContent(val, { emitUpdate: false })
  }
})

watch(() => props.editable, (val) => {
  editor.value?.setEditable(val)
})

onBeforeUnmount(() => {
  editor.value?.destroy()
})
</script>

<template>
  <div
    v-if="editor"
    class="prose prose-sm max-w-none"
    :class="{ 'border rounded-md p-3 min-h-[100px]': editable }"
  >
    <EditorContent :editor="editor" />
  </div>
</template>
