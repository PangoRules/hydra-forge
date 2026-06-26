import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardDescription from '~/components/card/CardDescription.vue'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'
import { ApiRoutes } from '~/lib/routes'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockPUT = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: vi.fn(),
  PUT: mockPUT,
  DELETE: vi.fn()
}))

mockNuxtImport('useBoardStore', () => () => ({
  updateCard: vi.fn()
}))

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1',
    projectId: 'p1',
    columnId: 'col1',
    cardNumber: 1,
    title: 'Test',
    description: 'Initial description',
    type: 0,
    position: 0,
    dueAt: null,
    version: 1,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
    movedAt: '2024-01-01T00:00:00Z',
    archivedAt: null,
    parentCardId: null,
    assignees: [],
    watchers: [],
    ...overrides
  }
}

describe('CardDescription', () => {
  beforeEach(() => {
    mockPUT.mockReset()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders description label', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard(), projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Description')
  })

  it('renders markdown editor with card description', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard(), projectId: 'p1' }
    })
    expect(wrapper.findComponent(MarkdownEditor).exists()).toBe(true)
  })

  it('uses the version from the card prop on save, not a value cached at mount', async () => {
    mockPUT.mockResolvedValue({ data: makeCard({ version: 99 }), error: undefined })
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard({ version: 7 }), projectId: 'p1' }
    })

    await wrapper.setProps({ card: makeCard({ version: 42 }) })

    await wrapper.findComponent(MarkdownEditor).vm.$emit('update:modelValue', 'edited')
    await vi.advanceTimersByTimeAsync(2000)
    await flushPromises()

    expect(mockPUT).toHaveBeenCalledWith(
      ApiRoutes.Cards.update('p1', 'c1'),
      expect.objectContaining({ body: expect.objectContaining({ version: 42 }) })
    )
  })

  it('emits update:card with the server response after a successful save', async () => {
    const updated = makeCard({ version: 2, description: 'edited' })
    mockPUT.mockResolvedValue({ data: updated, error: undefined })
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard(), projectId: 'p1' }
    })

    await wrapper.findComponent(MarkdownEditor).vm.$emit('update:modelValue', 'edited')
    const saveBtn = wrapper.findAll('button').find(b => b.text() === 'Save')!
    await saveBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('update:card')).toBeTruthy()
    expect(wrapper.emitted('update:card')![0]).toEqual([updated])
  })
})
