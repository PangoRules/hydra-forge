/**
 * Centralized route constants for UI pages and API endpoints.
 * Use these instead of inline magic strings to keep routes consistent
 * and easy to update when endpoints or page paths change.
 *
 * API routes use helper functions that return full paths with interpolated IDs.
 * This avoids string concatenation errors and makes refactoring safer.
 *
 * Usage:
 *   api.GET(ApiRoutes.Projects.list())
 *   api.POST(ApiRoutes.Projects.create(), { body: { name: '...' } })
 *   api.GET(ApiRoutes.Projects.detail(projectId))
 */

export const UiRoutes = {
  Login: '/login',
  Setup: '/setup',
  Projects: {
    List: '/projects',
    Board: (projectId: string) => `/projects/${projectId}/board`
  },
  Admin: {
    Home: '/admin',
    Users: '/admin/users',
    Projects: '/admin/projects',
    Settings: '/admin/settings',
    AuditLog: '/admin/audit-log'
  }
} as const

export const ApiRoutes = {
  Auth: {
    Login: '/api/Auth/login',
    refresh: '/api/Auth/refresh'
  },

  Users: {
    search: (query: string, limit = 10, excludeProjectId?: string) =>
      `/api/Users/search?q=${encodeURIComponent(query)}&limit=${limit}${excludeProjectId ? `&excludeProjectId=${excludeProjectId}` : ''}`
  },

  Projects: {
    list: () => '/api/Projects',
    create: () => '/api/Projects',
    detail: (projectId: string) => `/api/Projects/${projectId}`,
    update: (projectId: string) => `/api/Projects/${projectId}`,
    toggleArchive: (projectId: string) => `/api/Projects/${projectId}/toggle-archive`,
    members: (projectId: string) => `/api/Projects/${projectId}/members`,
    member: (projectId: string, memberId: string) => `/api/Projects/${projectId}/members/${memberId}`
  },

  Columns: {
    list: (projectId: string) => `/api/projects/${projectId}/Columns`,
    create: (projectId: string) => `/api/projects/${projectId}/Columns`,
    detail: (projectId: string, columnId: string) => `/api/projects/${projectId}/Columns/${columnId}`,
    update: (projectId: string, columnId: string) => `/api/projects/${projectId}/Columns/${columnId}`,
    delete: (projectId: string, columnId: string) => `/api/projects/${projectId}/Columns/${columnId}`,
    reorder: (projectId: string) => `/api/projects/${projectId}/Columns/reorder`
  },

  Cards: {
    list: (projectId: string) => `/api/projects/${projectId}/Cards`,
    create: (projectId: string) => `/api/projects/${projectId}/Cards`,
    detail: (projectId: string, cardIdOrNumber: string) => `/api/projects/${projectId}/Cards/${cardIdOrNumber}`,
    update: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}`,
    move: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}/move`,
    assignees: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}/assignees`,
    removeAssignee: (projectId: string, cardId: string, assigneeUserId: string) =>
      `/api/projects/${projectId}/Cards/${cardId}/assignees/${assigneeUserId}`,
    archive: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}/archive`,
    restore: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}/restore`,
    delete: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}`,
    watch: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}/watch`
  },

  Checklist: {
    list: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/CardChecklist`,
    create: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/CardChecklist`,
    item: (projectId: string, cardId: string, itemId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/CardChecklist/${itemId}`,
    reorder: (projectId: string, cardId: string, itemId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/CardChecklist/${itemId}/reorder`,
    toggle: (projectId: string, cardId: string, itemId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/CardChecklist/${itemId}/toggle`
  },

  Comments: {
    list: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/CardComments`,
    create: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/CardComments`,
    comment: (projectId: string, cardId: string, commentId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/CardComments/${commentId}`
  },

  Attachments: {
    list: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/attachments`,
    upload: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/attachments`,
    download: (projectId: string, cardId: string, attachmentId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/attachments/${attachmentId}`,
    delete: (projectId: string, cardId: string, attachmentId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/attachments/${attachmentId}`
  },

  Relationships: {
    list: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/CardRelationships`,
    create: (projectId: string, cardId: string) => `/api/projects/${projectId}/cards/${cardId}/CardRelationships`,
    archiveImpact: (projectId: string, cardId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/CardRelationships/archive-impact`,
    archiveWithRelationships: (projectId: string, cardId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/CardRelationships/archive-with-relationships`,
    relationship: (projectId: string, cardId: string, relationshipId: string) =>
      `/api/projects/${projectId}/cards/${cardId}/CardRelationships/${relationshipId}`
  },

  Specs: {
    forCard: (projectId: string, cardId: string) => `/api/projects/${projectId}/Specs/cards/${cardId}`,
    detail: (projectId: string, specId: string) => `/api/projects/${projectId}/Specs/${specId}`,
    restore: (projectId: string, specId: string) => `/api/projects/${projectId}/Specs/${specId}/restore`,
    versions: (projectId: string, specId: string) => `/api/projects/${projectId}/Specs/${specId}/versions`
  },

  Plans: {
    forCard: (projectId: string, cardId: string) => `/api/projects/${projectId}/Plans/cards/${cardId}`,
    detail: (projectId: string, planId: string) => `/api/projects/${projectId}/Plans/${planId}`,
    restore: (projectId: string, planId: string) => `/api/projects/${projectId}/Plans/${planId}/restore`,
    versions: (projectId: string, planId: string) => `/api/projects/${projectId}/Plans/${planId}/versions`,
    updateStatus: (projectId: string, planId: string) => `/api/projects/${projectId}/Plans/${planId}/status` as const
  },

  ProjectSnapshot: {
    get: (projectId: string) => `/api/projects/${projectId}/ProjectSnapshot`
  },

  Notifications: {
    list: (skip = 0, take = 20) => `/api/Notifications?skip=${skip}&take=${take}`,
    unreadCount: () => '/api/Notifications/unread-count',
    markRead: (id: string) => `/api/Notifications/${id}/read`,
    markAllRead: () => '/api/Notifications/read-all'
  },

  Admin: {
    usersList: (skip = 0, take = 20, search?: string) =>
      `/api/admin/users?skip=${skip}&take=${take}${search ? `&search=${encodeURIComponent(search)}` : ''}`,
    userGet: (userId: string) => `/api/admin/users/${userId}`,
    userCreate: () => '/api/admin/users',
    userDisable: (userId: string) => `/api/admin/users/${userId}/disable`,
    userEnable: (userId: string) => `/api/admin/users/${userId}/enable`,
    userResetPassword: (userId: string) => `/api/admin/users/${userId}/reset-password`,
    userRole: (userId: string) => `/api/admin/users/${userId}/role`,
    projectsList: (skip = 0, take = 20, search?: string) =>
      `/api/admin/projects?skip=${skip}&take=${take}${search ? `&search=${encodeURIComponent(search)}` : ''}`,
    projectGet: (projectId: string) => `/api/admin/projects/${projectId}`,
    settingsGet: () => '/api/admin/settings',
    settingsUpdate: () => '/api/admin/settings',
    auditLog: () => '/api/admin/audit-log'
  }
} as const
