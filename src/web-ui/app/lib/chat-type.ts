/**
 * Chat type is never stored on the DTO — it is derived from which optional
 * scope FKs are set. A card chat always lives inside a project, so projectId
 * is implied when openCardId is set.
 */
export type ChatType = 'normal' | 'project' | 'card'

export function getChatType(session: { projectId: string | null, openCardId: string | null }): ChatType {
  if (session.projectId && session.openCardId) return 'card'
  if (session.projectId) return 'project'
  return 'normal'
}

/** Nuxt UI UBadge color + label per chat type. */
export const CHAT_TYPE_BADGE: Record<ChatType, { label: string, color: 'neutral' | 'primary' | 'success' }> = {
  normal: { label: 'Chat', color: 'neutral' },
  project: { label: 'Project', color: 'primary' },
  card: { label: 'Card', color: 'success' }
}
