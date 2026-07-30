import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import UserResetPasswordModal from '~/components/admin/UserResetPasswordModal.vue'

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

describe('UserResetPasswordModal', () => {
  beforeEach(() => {
    mockPOST.mockReset()
  })

  it('renders a labeled New Password field', async () => {
    const wrapper = await mountSuspended(UserResetPasswordModal, {
      props: { userId: 'u1' },
      global: { stubs: { AppModal: appModalStub } }
    })
    expect(wrapper.text()).toContain('New Password')
  })

  it('submits the new password to the reset endpoint for the given user and emits reset', async () => {
    mockPOST.mockResolvedValue({ data: {}, error: undefined })
    const wrapper = await mountSuspended(UserResetPasswordModal, {
      props: { userId: 'u1' },
      global: { stubs: { AppModal: appModalStub } }
    })

    await wrapper.find('input[type="password"]').setValue('NewSecret123!')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(
      '/api/admin/users/u1/reset-password',
      { body: { newPassword: 'NewSecret123!' } }
    )
    expect(wrapper.emitted('reset')).toBeTruthy()
  })

  it('does not emit reset when the request fails', async () => {
    mockPOST.mockRejectedValue(new Error('Password too weak'))
    const wrapper = await mountSuspended(UserResetPasswordModal, {
      props: { userId: 'u1' },
      global: { stubs: { AppModal: appModalStub } }
    })

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.emitted('reset')).toBeFalsy()
  })
})
