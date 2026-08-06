import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatSessionHeader from '~/components/chat/ChatSessionHeader.vue'
import { AiEditMode, ChatSessionStatus } from '~/types/chat'
import type { ChatSessionDetailDto } from '~/types/chat'

function makeSession(overrides: Partial<ChatSessionDetailDto> = {}): ChatSessionDetailDto {
  return {
    id: 's1',
    title: 'Test Chat',
    folderId: null,
    projectId: null,
    openCardId: null,
    personalityId: null,
    personalityArchived: false,
    status: ChatSessionStatus.Active,
    aiEditMode: AiEditMode.PerMutation,
    searchAllMyDocs: false,
    summary: null,
    preferredModelConfigId: null,
    preferredEffort: null,
    createdAt: '2026-08-01T00:00:00Z',
    updatedAt: '2026-08-01T00:00:00Z',
    archivedAt: null,
    ownerId: 'u1',
    isShared: false,
    closedAt: null,
    messages: [],
    ...overrides
  }
}

describe('ChatSessionHeader — ownership visibility', () => {
  it('owner sees rename pencil, export, and dismiss buttons', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(true)
    expect(wrapper.find('[title="Export chat"]').exists()).toBe(true)
    // Dismiss replaces the old "Close chat" inline button for non-compact mode
    expect(wrapper.find('[title="Dismiss"]').exists()).toBe(true)
  })

  it('non-owner does not see rename pencil or export buttons, but still sees dismiss', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ ownerId: 'u2' }), isOwner: false }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(false)
    expect(wrapper.find('[title="Export chat"]').exists()).toBe(false)
    // Dismiss has no ownership/API-call gate — it just stops showing the chat
    expect(wrapper.find('[title="Dismiss"]').exists()).toBe(true)
  })

  it('shows fork button on shared project chat the caller does not own', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: {
        session: makeSession({ isShared: true, projectId: 'proj1', ownerId: 'u2' }),
        isOwner: false
      }
    })
    await flushPromises()
    expect(wrapper.find('[title="Summarize → start my own"]').exists()).toBe(true)
  })

  it('hides fork button on own shared project chat', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: {
        session: makeSession({ isShared: true, projectId: 'proj1', ownerId: 'u1' }),
        isOwner: true
      }
    })
    await flushPromises()
    expect(wrapper.find('[title="Summarize → start my own"]').exists()).toBe(false)
  })
})

describe('ChatSessionHeader — disabled-when-closed state', () => {
  it('rename pencil is disabled when session is Closed', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Closed }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').attributes('disabled')).toBeDefined()
  })

  it('dismiss button is present regardless of session status', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Closed }), isOwner: true }
    })
    await flushPromises()
    const vm = wrapper.vm as any
    expect(vm.isActive).toBe(false)
    expect(wrapper.find('[title="Dismiss"]').exists()).toBe(true)
  })

  it('reopen button is shown (not archive) for a Closed, non-archived session', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Closed }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Reopen chat"]').exists()).toBe(true)
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(false)
  })

  it('archive button is shown for an Active session (not gated behind Closed)', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Active, projectId: 'p1' }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Archive chat"]').exists()).toBe(true)
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(true)
  })

  it('scope toggle is absent when session is Closed', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Closed }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.text()).not.toContain('All docs')
  })
})

describe('ChatSessionHeader — compact mode (ChatDock)', () => {
  it('still shows the real title when compact (ChatDock owns the single header row)', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ title: 'My Chat' }), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.find('h2').text()).toBe('My Chat')
  })

  it('shows the title when not compact', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ title: 'My Chat' }), isOwner: true, compact: false }
    })
    await flushPromises()
    expect(wrapper.find('h2').text()).toBe('My Chat')
  })

  it('shows a single kebab menu button instead of individual icon buttons when compact', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="More actions"]').exists()).toBe(true)
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(false)
    expect(wrapper.find('[title="Export chat"]').exists()).toBe(false)
  })

  it('close/archive/reopen collapse into the kebab menu when compact — no standalone buttons', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1' }), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(false)
    expect(wrapper.find('[title="Archive chat"]').exists()).toBe(false)
    const items = (wrapper.vm as any).compactMenuItems.flat()
    expect(items.some((i: { label: string }) => i.label === 'Close chat')).toBe(true)
    expect(items.some((i: { label: string }) => i.label === 'Archive chat')).toBe(true)
  })

  it('shows all individual icon buttons when not compact (unchanged behavior)', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: false }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(true)
    expect(wrapper.find('[title="More actions"]').exists()).toBe(false)
  })
})

describe('ChatSessionHeader — dismiss vs close/archive', () => {
  it('emits dismiss (not close) when the X button is clicked', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true }
    })
    await flushPromises()
    await wrapper.find('[title="Dismiss"]').trigger('click')
    expect(wrapper.emitted('dismiss')).toBeTruthy()
    expect(wrapper.emitted('close')).toBeFalsy()
  })

  it('selecting Close chat from the kebab does not emit closeSession directly — it is gated behind confirmation', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1' }), isOwner: true, compact: true }
    })
    await flushPromises()
    const closeItem = wrapper.vm.compactMenuItems.flat().find((i: { label: string }) => i.label === 'Close chat') as { onSelect: () => void }
    closeItem.onSelect()
    await flushPromises()
    // The menu item only opens the confirm dialog (showCloseConfirm = true) —
    // the real session-close emit only fires once the dialog's own Confirm
    // button is clicked (ConfirmDialog.onConfirm → @confirm="confirmClose").
    expect(wrapper.emitted('closeSession')).toBeFalsy()
  })

  it('every compact menu item has an icon', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    const items = wrapper.vm.compactMenuItems.flat()
    expect(items.length).toBeGreaterThan(0)
    expect(items.every((i: { icon?: string }) => !!i.icon)).toBe(true)
  })

})

describe('ChatSessionHeader — close button gating by chat type', () => {
  it('hides the inline Close button for normal chats (no projectId)', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: null }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(false)
  })

  it('shows the inline Close button for project chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1' }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(true)
  })

  it('shows the inline Close button for card chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1', openCardId: 'c1' }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(true)
  })

  it('excludes Close from the compact kebab menu for normal chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: null }), isOwner: true, compact: true }
    })
    await flushPromises()
    const items = (wrapper.vm as unknown as {
      compactMenuItems: Array<Array<{ label: string }>>
    }).compactMenuItems
    expect(items.flat().map(i => i.label)).not.toContain('Close chat')
  })

  it('includes Close in the compact kebab menu for project chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1' }), isOwner: true, compact: true }
    })
    await flushPromises()
    const items = (wrapper.vm as unknown as {
      compactMenuItems: Array<Array<{ label: string }>>
    }).compactMenuItems
    expect(items.flat().map(i => i.label)).toContain('Close chat')
  })
})

describe('ChatSessionHeader — dock chrome (back/new-chat/full-height)', () => {
  it('shows a back button only when showBackButton is true', async () => {
    const withBack = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true, showBackButton: true }
    })
    await flushPromises()
    expect(withBack.find('[title="Back to history"]').exists()).toBe(true)

    const withoutBack = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true }
    })
    await flushPromises()
    expect(withoutBack.find('[title="Back to history"]').exists()).toBe(false)
  })

  it('emits back and newChat from their respective buttons', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true, showBackButton: true, showNewChatButton: true }
    })
    await flushPromises()
    await wrapper.find('[title="Back to history"]').trigger('click')
    await wrapper.find('[title="New chat"]').trigger('click')
    expect(wrapper.emitted('back')).toBeTruthy()
    expect(wrapper.emitted('newChat')).toBeTruthy()
  })

  it('shows the full-height toggle only when showFullHeightToggle is true, and reflects isFullHeight', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true, compact: true, showFullHeightToggle: true, isFullHeight: true }
    })
    await flushPromises()
    const toggle = wrapper.find('[title="Exit full height"]')
    expect(toggle.exists()).toBe(true)
    await toggle.trigger('click')
    expect(wrapper.emitted('toggleFullHeight')).toBeTruthy()
  })
})
