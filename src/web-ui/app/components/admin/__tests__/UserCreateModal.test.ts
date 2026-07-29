import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import UserCreateModal from '~/components/admin/UserCreateModal.vue'

const mockPOST = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: vi.fn(),
  PATCH: vi.fn(),
  DELETE: vi.fn()
}))

const appModalStub = defineComponent({
  render() {
    return h('div', { 'data-testid': 'app-modal' }, [
      this.$slots.body?.(),
      this.$slots.footer?.()
    ])
  }
})

describe('UserCreateModal', () => {
  beforeEach(() => {
    mockPOST.mockReset()
  })

  it('renders a labeled field for every input', async () => {
    const wrapper = await mountSuspended(UserCreateModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    const text = wrapper.text()
    expect(text).toContain('Username')
    expect(text).toContain('Password')
    expect(text).toContain('First Name')
    expect(text).toContain('Last Name')
    expect(text).toContain('Email')
    expect(text).toContain('Admin')
  })

  it('submits the form fields to the user-create endpoint and emits created', async () => {
    mockPOST.mockResolvedValue({ data: {}, error: undefined })
    const wrapper = await mountSuspended(UserCreateModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })

    await wrapper.find('input[placeholder="jdoe"]').setValue('jdoe')
    await wrapper.find('input[type="password"]').setValue('Secret123!')
    await wrapper.find('input[type="email"]').setValue('jdoe@example.com')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(
      '/api/admin/users',
      expect.objectContaining({
        body: expect.objectContaining({
          username: 'jdoe',
          password: 'Secret123!',
          email: 'jdoe@example.com'
        })
      })
    )
    expect(wrapper.emitted('created')).toBeTruthy()
  })

  it('does not emit created when the request fails', async () => {
    mockPOST.mockRejectedValue(new Error('Username already taken'))
    const wrapper = await mountSuspended(UserCreateModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.emitted('created')).toBeFalsy()
  })
})
