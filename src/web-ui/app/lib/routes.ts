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
  Chats: '/chats',
  Projects: {
    List: '/projects',
    Board: (projectId: string) => `/projects/${projectId}/board`
  },
  Admin: {
    Home: '/admin',
    Users: '/admin/users',
    Settings: '/admin/settings',
    AuditLog: '/admin/audit-log',
    Providers: '/admin/providers',
    ProviderModels: '/admin/provider-models',
    Routing: '/admin/routing',
    Usage: '/admin/usage'
  },
  Account: {
    Usage: '/account/usage'
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

  Account: {
    usage: () => '/api/account/usage'
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
    auditLog: () => '/api/admin/audit-log',
    providers: {
      list: () => '/api/admin/providers',
      create: () => '/api/admin/providers',
      detail: (id: string) => `/api/admin/providers/${id}`,
      update: (id: string) => `/api/admin/providers/${id}`,
      disable: (id: string) => `/api/admin/providers/${id}`,
      delete: (id: string) => `/api/admin/providers/${id}/permanent`,
      probeModels: (id: string) => `/api/admin/providers/${id}/models`,
      listModels: (id: string) => `/api/admin/providers/${id}/models/configured`,
      createModel: (id: string) => `/api/admin/providers/${id}/models`,
      updateModel: (id: string, modelId: string) => `/api/admin/providers/${id}/models/${modelId}`,
      deleteModel: (id: string, modelId: string) => `/api/admin/providers/${id}/models/${modelId}`
    },
    routing: {
      list: () => '/api/admin/routing',
      update: (feature: string) => `/api/admin/routing/${feature}`,
      setAllowedModels: (feature: string) => `/api/admin/routing/${feature}/allowed-models`
    },
    usage: {
      tokens: (params: string) => `/api/admin/usage/tokens?${params}`,
      images: (params: string) => `/api/admin/usage/images?${params}`
    },
    userBudget: {
      get: (userId: string) => `/api/admin/users/${userId}/budget`,
      update: (userId: string) => `/api/admin/users/${userId}/budget`
    }
  },

  Chat: {
    sessions: {
      list: (folderId?: string, projectId?: string, before?: string, beforeId?: string, limit = 20) =>
        `/api/chat/sessions?${folderId ? `folderId=${folderId}&` : ''}${projectId ? `projectId=${projectId}&` : ''}${before ? `before=${before}&` : ''}${beforeId ? `beforeId=${beforeId}&` : ''}limit=${limit}`,
      create: () => '/api/chat/sessions',
      detail: (sessionId: string) => `/api/chat/sessions/${sessionId}`,
      update: (sessionId: string) => `/api/chat/sessions/${sessionId}`,
      close: (sessionId: string) => `/api/chat/sessions/${sessionId}/close`,
      archive: (sessionId: string) => `/api/chat/sessions/${sessionId}`,
      attachDocument: (sessionId: string) => `/api/chat/sessions/${sessionId}/documents`,
      listDocuments: (sessionId: string) => `/api/chat/sessions/${sessionId}/documents`,
      detachDocument: (sessionId: string, documentId: string) =>
        `/api/chat/sessions/${sessionId}/documents/${documentId}`,
      permission: (sessionId: string) => `/api/chat/sessions/${sessionId}/permission`,
      messages: (sessionId: string, before?: string, beforeId?: string, limit = 50) =>
        `/api/chat/sessions/${sessionId}/messages?${before ? `before=${before}&` : ''}${beforeId ? `beforeId=${beforeId}&` : ''}limit=${limit}`,
      sendMessage: (sessionId: string) => `/api/chat/sessions/${sessionId}/messages`,
      generateReply: (sessionId: string, messageId: string) =>
        `/api/chat/sessions/${sessionId}/messages/${messageId}/generate`,
      rollbackMessage: (sessionId: string, messageId: string) =>
        `/api/chat/sessions/${sessionId}/messages/${messageId}/rollback`
    },
    folders: {
      list: (projectId?: string) => `/api/chat/folders${projectId ? `?projectId=${projectId}` : ''}`,
      create: () => '/api/chat/folders',
      detail: (folderId: string) => `/api/chat/folders/${folderId}`,
      update: (folderId: string) => `/api/chat/folders/${folderId}`,
      archive: (folderId: string) => `/api/chat/folders/${folderId}`
    },
    presets: {
      list: (groupId?: string) => `/api/chat/presets${groupId ? `?groupId=${groupId}` : ''}`,
      create: () => '/api/chat/presets',
      detail: (presetId: string) => `/api/chat/presets/${presetId}`,
      update: (presetId: string) => `/api/chat/presets/${presetId}`,
      archive: (presetId: string) => `/api/chat/presets/${presetId}`
    },
    presetGroups: {
      list: () => '/api/chat/preset-groups',
      create: () => '/api/chat/preset-groups',
      detail: (groupId: string) => `/api/chat/preset-groups/${groupId}`,
      update: (groupId: string) => `/api/chat/preset-groups/${groupId}`,
      archive: (groupId: string) => `/api/chat/preset-groups/${groupId}`
    },
    personalities: {
      list: () => '/api/chat/personalities',
      create: () => '/api/chat/personalities',
      detail: (personalityId: string) => `/api/chat/personalities/${personalityId}`,
      update: (personalityId: string) => `/api/chat/personalities/${personalityId}`,
      archive: (personalityId: string) => `/api/chat/personalities/${personalityId}`,
      setDefault: (personalityId: string) => `/api/chat/personalities/${personalityId}/default`
    },
    cardLinks: {
      byCard: (cardId: string) => `/api/cards/${cardId}/chat-links`,
      archive: (linkId: string) => `/api/chat/card-links/${linkId}`
    },
    documents: {
      list: (q?: string) => `/api/Documents${q ? `?q=${encodeURIComponent(q)}` : ''}`,
      create: () => '/api/Documents',
      detail: (documentId: string) => `/api/Documents/${documentId}`,
      archive: (documentId: string) => `/api/Documents/${documentId}`
    },
    search: (q: string, projectId?: string) =>
      `/api/chat/search?q=${encodeURIComponent(q)}${projectId ? `&projectId=${projectId}` : ''}`
  },

  Llm: {
    models: (feature: string) => `/api/llm/models?feature=${feature}`
  }
} as const
