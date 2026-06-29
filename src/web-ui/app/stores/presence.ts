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
    onlineUsers.value.set(projectId, users)
  }

  function addUser(projectId: string, user: PresenceUser) {
    const users = onlineUsers.value.get(projectId) ?? []
    if (!users.find(u => u.userId === user.userId)) {
      onlineUsers.value.set(projectId, [...users, user])
    }
  }

  function removeUser(projectId: string, connectionId: string) {
    const users = onlineUsers.value.get(projectId) ?? []
    onlineUsers.value.set(projectId, users.filter(u => u.connectionId !== connectionId))
  }

  function setCardFocus(userId: string, cardId: string) {
    focusedCards.value.set(userId, cardId)
  }

  function clearCardFocus(userId: string) {
    focusedCards.value.delete(userId)
  }

  return { onlineUsers, focusedCards, setProjectUsers, addUser, removeUser, setCardFocus, clearCardFocus }
})
