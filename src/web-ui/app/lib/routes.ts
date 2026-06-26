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
  }
} as const

export const ApiRoutes = {
  Auth: {
    Login: '/api/Auth/login',
    refresh: '/api/Auth/refresh'
  },

  Projects: {
    list: () => '/api/Projects',
    create: () => '/api/Projects',
    detail: (projectId: string) => `/api/Projects/${projectId}`,
    update: (projectId: string) => `/api/Projects/${projectId}`,
    archive: (projectId: string) => `/api/Projects/${projectId}/archive`,
    restore: (projectId: string) => `/api/Projects/${projectId}/restore`,
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
    delete: (projectId: string, cardId: string) => `/api/projects/${projectId}/Cards/${cardId}`
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
    versions: (projectId: string, planId: string) => `/api/projects/${projectId}/Plans/${planId}/versions`
  },

  ProjectSnapshot: {
    get: (projectId: string) => `/api/projects/${projectId}/ProjectSnapshot`
  }
} as const
