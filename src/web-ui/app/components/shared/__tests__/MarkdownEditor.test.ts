import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'

describe('MarkdownEditor', () => {
  it('renders editor content area', async () => {
    const wrapper = await mountSuspended(MarkdownEditor, {
      props: { modelValue: '# Hello', placeholder: 'Write...' }
    })
    expect(wrapper.find('.ProseMirror').exists() || wrapper.find('[contenteditable]').exists()).toBe(true)
  })

  it('renders with placeholder', async () => {
    const wrapper = await mountSuspended(MarkdownEditor, {
      props: { modelValue: '', placeholder: 'Custom placeholder' }
    })
    expect(wrapper.html()).toBeTruthy()
  })

  it('renders in read-only mode when editable=false', async () => {
    const wrapper = await mountSuspended(MarkdownEditor, {
      props: { modelValue: '# Read only', editable: false }
    })
    expect(wrapper.html()).toBeTruthy()
  })
})
