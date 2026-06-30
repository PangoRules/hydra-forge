import { defineStore } from 'pinia'

interface PresenceUser {
  userId: string
  username: string
  connectionId: string
}

export const usePresenceStore = defineStore('presence', () => {
  const onlineUsers = ref<Map<string, PresenceUser[]>>(new Map())
  const focusedCards = ref<Map<string, string>>(new Map()) // userId → cardId

  function setProjectUsers(projectId: string, users: PresenceUser[]) {
    const updated = new Map(onlineUsers.value)
    updated.set(projectId, users)
    onlineUsers.value = updated
  }

  function addUser(projectId: string, user: PresenceUser) {
    const updated = new Map(onlineUsers.value)
    const users = updated.get(projectId) ?? []
    if (!users.find(u => u.userId === user.userId)) {
      updated.set(projectId, [...users, user])
      onlineUsers.value = updated
    }
  }

  function removeUser(projectId: string, connectionId: string) {
    const updated = new Map(onlineUsers.value)
    const users = updated.get(projectId) ?? []
    updated.set(projectId, users.filter(u => u.connectionId !== connectionId))
    onlineUsers.value = updated
  }

  function setCardFocus(userId: string, cardId: string) {
    const updated = new Map(focusedCards.value)
    updated.set(userId, cardId)
    focusedCards.value = updated
  }

  function clearCardFocus(userId: string) {
    const updated = new Map(focusedCards.value)
    updated.delete(userId)
    focusedCards.value = updated
  }

  function clearAllFocusedCards() {
    focusedCards.value = new Map()
  }

  return { onlineUsers, focusedCards, setProjectUsers, addUser, removeUser, setCardFocus, clearCardFocus, clearAllFocusedCards }
})
