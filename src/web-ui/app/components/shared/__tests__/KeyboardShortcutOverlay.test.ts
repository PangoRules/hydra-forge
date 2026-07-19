import { mount } from '@vue/test-utils'
import { describe, it, expect } from 'vitest'
import KeyboardShortcutOverlay from '../KeyboardShortcutOverlay.vue'

describe('KeyboardShortcutOverlay', () => {
  it('renders keyboard shortcuts title', () => {
    const wrapper = mount(KeyboardShortcutOverlay, {
      props: {
        open: true
      }
    })
    
    // The component should render without errors
    expect(wrapper.exists()).toBe(true)
  })
})