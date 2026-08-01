import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import CardModal from '~/components/card/CardModal.vue'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))

// API sends CardType as a string (JsonStringEnumConverter) — the generated
// CardResponse['type'] is typed as number, but the real runtime value is one
// of 'Task' | 'Issue' | 'Idea' | 'Goal'. Cast through unknown like CardModal.vue does.
function makeCard(type: string): CardResponse {
  return {
    id: 'c1',
    projectId: 'p1',
    columnId: 'col1',
    cardNumber: 1,
    title: 'Test card',
    description: '',
    type: type as unknown as CardResponse['type'],
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
    relationshipBadges: [],
    relationshipCount: 0,
    parentCard: null,
    childCount: 0
  }
}

async function mountForType(type: string) {
  mockGET.mockResolvedValue({ data: makeCard(type), error: undefined })
  const wrapper = await mountSuspended(CardModal, {
    props: { cardId: 'c1', projectId: 'p1' },
    global: {
      stubs: {
        AppModal: {
          render() {
            return h('div', { 'data-testid': 'app-modal' }, this.$slots.body?.())
          }
        },
        CardDescription: true,
        CardMetadata: true,
        CardChecklist: true,
        CardComments: true,
        CardAttachments: true,
        CardDependencies: true,
        CardSpec: true,
        CardPlan: true
      }
    }
  })
  await flushPromises()
  ;(wrapper.vm as any).activeTab = 'docs'
  await flushPromises()
  return wrapper
}

describe('CardModal Docs tab visibility (real component, not duplicated constants)', () => {
  beforeEach(() => {
    mockGET.mockReset()
  })

  it('Goal: Docs tab present, Spec labeled Specification, Plan present', async () => {
    const wrapper = await mountForType('Goal')
    expect((wrapper.vm as any).hasDocsTab).toBe(true)
    expect(wrapper.find('card-spec-stub').exists()).toBe(true)
    expect(wrapper.find('card-spec-stub').attributes('doctype')).toBe('Specification')
    expect(wrapper.find('card-plan-stub').exists()).toBe(true)
  })

  it('Idea: Docs tab present, Spec labeled Concept, no Plan', async () => {
    const wrapper = await mountForType('Idea')
    expect((wrapper.vm as any).hasDocsTab).toBe(true)
    expect(wrapper.find('card-spec-stub').exists()).toBe(true)
    expect(wrapper.find('card-spec-stub').attributes('doctype')).toBe('Concept')
    expect(wrapper.find('card-plan-stub').exists()).toBe(false)
  })

  it('Issue: Docs tab present, Spec labeled Report, Plan present', async () => {
    const wrapper = await mountForType('Issue')
    expect((wrapper.vm as any).hasDocsTab).toBe(true)
    expect(wrapper.find('card-spec-stub').exists()).toBe(true)
    expect(wrapper.find('card-spec-stub').attributes('doctype')).toBe('Report')
    expect(wrapper.find('card-plan-stub').exists()).toBe(true)
  })

  it('Task: Docs tab present, no Spec, Plan present', async () => {
    const wrapper = await mountForType('Task')
    expect((wrapper.vm as any).hasDocsTab).toBe(true)
    expect(wrapper.find('card-spec-stub').exists()).toBe(false)
    expect(wrapper.find('card-plan-stub').exists()).toBe(true)
  })
})
