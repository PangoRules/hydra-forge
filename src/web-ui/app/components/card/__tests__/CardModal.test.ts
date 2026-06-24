import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardModal from '~/components/card/CardModal.vue'

describe('CardModal', () => {
  it('shows loading spinner initially', async () => {
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  it('renders modal container', async () => {
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.find('.flex.flex-col').exists()).toBe(true)
  })
})
