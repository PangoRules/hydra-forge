import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { reactive, nextTick } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import type { VueWrapper } from '@vue/test-utils'

const mockCloseAll = vi.fn()

// Mutable reactive store/route state — mirrors the pattern in chatDock.test.ts
// and ChatDock.test.ts for mocking Pinia stores + useRoute in component tests.
const storeState = reactive({
  openCardIds: [] as string[],
  closeAll: mockCloseAll
})

const routeState = reactive({ path: '/projects/proj1', params: { id: 'proj1' } as Record<string, string> })

mockNuxtImport('useCardPopupStore', () => () => storeState)
mockNuxtImport('useRoute', () => () => routeState)

import CardPopupLayer from '~/components/card/CardPopupLayer.vue'

async function mount(): Promise<VueWrapper> {
  return await mountSuspended(CardPopupLayer, { global: { stubs: { CardPopup: true } } }) as unknown as VueWrapper
}

describe('CardPopupLayer', () => {
  let wrapper: VueWrapper | null = null

  beforeEach(() => {
    mockCloseAll.mockReset()
    storeState.openCardIds = []
    routeState.path = '/projects/proj1'
    routeState.params = { id: 'proj1' }
  })

  afterEach(() => {
    wrapper?.unmount()
    wrapper = null
  })

  it('does not close cards on mount when the route is already on a project board', async () => {
    storeState.openCardIds = ['card-1']
    wrapper = await mount()
    expect(mockCloseAll).not.toHaveBeenCalled()
  })

  it('closes stale restored cards on mount when not on any project route', async () => {
    storeState.openCardIds = ['card-1', 'card-2']
    routeState.path = '/chats'
    routeState.params = {}
    wrapper = await mount()
    expect(mockCloseAll).toHaveBeenCalledTimes(1)
  })

  it('does not close on mount when there are no open cards, regardless of route', async () => {
    storeState.openCardIds = []
    routeState.path = '/admin'
    routeState.params = {}
    wrapper = await mount()
    expect(mockCloseAll).not.toHaveBeenCalled()
  })

  it('closes all cards when navigating to a different project', async () => {
    storeState.openCardIds = ['card-1']
    wrapper = await mount()
    expect(mockCloseAll).not.toHaveBeenCalled()

    routeState.path = '/projects/proj2'
    routeState.params = { id: 'proj2' }
    await nextTick()
    expect(mockCloseAll).toHaveBeenCalledTimes(1)
  })

  it('closes all cards when leaving the project board entirely', async () => {
    storeState.openCardIds = ['card-1']
    wrapper = await mount()

    routeState.path = '/projects'
    routeState.params = {}
    await nextTick()
    expect(mockCloseAll).toHaveBeenCalledTimes(1)
  })

  it('does not close cards when switching tabs within the same project route (path unchanged)', async () => {
    storeState.openCardIds = ['card-1']
    wrapper = await mount()

    // Board/docs/chats tabs are client-side state on the same /projects/[id]
    // route — the path itself never changes, so re-triggering reactivity on
    // the same path must not close anything.
    routeState.path = '/projects/proj1'
    routeState.params = { id: 'proj1' }
    await nextTick()
    expect(mockCloseAll).not.toHaveBeenCalled()
  })
})
