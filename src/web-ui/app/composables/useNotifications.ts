import { useApi } from '~/composables/useApi'
import { ApiRoutes } from '~/lib/routes'

export interface NotificationItem {
  id: string
  title: string
  body: string | null
  cardId: string | null
  projectId: string | null
  actionUrl: string | null
  isRead: boolean
  createdAt: string
}

export function useNotifications() {
  const api = useApi()
  const unreadCount = useState<number>('notifications:unreadCount', () => 0)
  const notifications = useState<NotificationItem[]>('notifications:notifications', () => [])
  const showPanel = useState<boolean>('notifications:showPanel', () => false)

  async function fetchUnreadCount(): Promise<void> {
    try {
      const { data } = await api.GET<{ count: number }>(ApiRoutes.Notifications.unreadCount())
      unreadCount.value = data?.count ?? 0
    } catch {
      // Silently fail — bell just shows 0
    }
  }

  async function fetchNotifications(skip = 0, take = 20): Promise<void> {
    try {
      const { data } = await api.GET<NotificationItem[]>(ApiRoutes.Notifications.list(skip, take))
      notifications.value = data ?? []
    } catch {
      // Silently fail
    }
  }

  async function markAsRead(notificationId: string): Promise<void> {
    try {
      await api.POST(ApiRoutes.Notifications.markRead(notificationId))
      const notif = notifications.value.find(n => n.id === notificationId)
      if (notif && !notif.isRead) {
        notif.isRead = true
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }
    } catch {
      // Silently fail
    }
  }

  async function markAllAsRead(): Promise<void> {
    try {
      await api.POST(ApiRoutes.Notifications.markAllRead())
      // eslint-disable-next-line @stylistic/max-statements-per-line
      for (const n of notifications.value) { n.isRead = true }
      unreadCount.value = 0
    } catch {
      // Silently fail
    }
  }

  function onNotificationReceived(notification: NotificationItem): void {
    unreadCount.value++
    if (showPanel.value) {
      notifications.value.unshift(notification)
    }
  }

  return {
    unreadCount,
    notifications,
    showPanel,
    fetchUnreadCount,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    onNotificationReceived
  }
}
