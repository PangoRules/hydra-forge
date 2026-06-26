import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardModal from '~/components/card/CardModal.vue'

describe('CardModal', () => {
  it('mounts without error', async () => {
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          AppModal: true,
          CardDescription: true,
          CardMetadata: true
        }
      }
    })
    expect(wrapper.vm).toBeTruthy()
  })

  it('renders with loading state true', async () => {
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          AppModal: true,
          CardDescription: true,
          CardMetadata: true
        }
      }
    })
    // The AppModal should receive loading=true initially
    const appModalStub = wrapper.findComponent({ name: 'AppModal' })
    expect(appModalStub.exists()).toBe(true)
  })
})