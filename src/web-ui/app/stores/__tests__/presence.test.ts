import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { usePresenceStore } from '~/stores/presence'

describe('usePresenceStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts with empty state', () => {
    const store = usePresenceStore()
    expect(store.onlineUsers.size).toBe(0)
    expect(store.focusedCards.size).toBe(0)
  })

  it('addUser adds user to project', () => {
    const store = usePresenceStore()
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn1' })
    expect(store.onlineUsers.get('p1')?.length).toBe(1)
  })

  it('addUser does not duplicate same userId', () => {
    const store = usePresenceStore()
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn1' })
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn2' })
    expect(store.onlineUsers.get('p1')?.length).toBe(1)
  })

  it('removeUser removes by connectionId', () => {
    const store = usePresenceStore()
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn1' })
    store.addUser('p1', { userId: 'u2', username: 'Bob', connectionId: 'conn2' })
    store.removeUser('p1', 'conn1')
    expect(store.onlineUsers.get('p1')?.length).toBe(1)
  })

  it('setCardFocus and clearCardFocus', () => {
    const store = usePresenceStore()
    store.setCardFocus('u1', 'c1')
    expect(store.focusedCards.get('u1')).toBe('c1')
    store.clearCardFocus('u1')
    expect(store.focusedCards.has('u1')).toBe(false)
  })

  it('setProjectUsers replaces all users for project', () => {
    const store = usePresenceStore()
    store.setProjectUsers('p1', [
      { userId: 'u1', username: 'Alice', connectionId: 'conn1' },
      { userId: 'u2', username: 'Bob', connectionId: 'conn2' }
    ])
    expect(store.onlineUsers.get('p1')?.length).toBe(2)
  })
})
