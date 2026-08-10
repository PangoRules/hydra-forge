export enum ChatSessionStatus {
  Active = 'Active',
  Closed = 'Closed',
  Archived = 'Archived'
}

export enum MessageRole {
  User = 'User',
  Assistant = 'Assistant',
  System = 'System',
  Tool = 'Tool'
}

export enum AiEditMode {
  PerMutation = 1,
  Blanket = 2
}

export interface ChatMessageDto {
  id: string
  sessionId: string
  role: MessageRole
  content: string
  inputTokens: number
  outputTokens: number
  cachedTokens: number
  cost: number | null
  modelName: string | null
  imagesJson: string | null
  createdAt: string
}

export interface ChatSessionDto {
  id: string
  title: string
  folderId: string | null
  projectId: string | null
  openCardId: string | null
  personalityId: string | null
  personalityArchived: boolean
  status: ChatSessionStatus
  aiEditMode: AiEditMode
  searchAllMyDocs: boolean
  summary: string | null
  preferredModelConfigId: string | null
  preferredEffort: string | null
  createdAt: string
  updatedAt: string
  archivedAt: string | null
}

export interface ChatSessionDetailDto extends ChatSessionDto {
  ownerId: string
  isShared: boolean
  closedAt: string | null
  messages: ChatMessageDto[]
}

export interface ChatSessionPageDto {
  items: ChatSessionDto[]
  totalCount: number
}

export interface AvailableModelDto {
  providerModelConfigId: string
  modelName: string
  providerName: string
  tier: string
  supportsReasoning: boolean
}

export interface PromptPresetDto {
  id: string
  groupId: string | null
  name: string
  content: string
  position?: number
  createdAt: string
  updatedAt: string
  archivedAt: string | null
}

export interface AgentPersonalityDto {
  id: string
  name: string
  description: string | null
  systemPrompt: string
  isDefault: boolean
  createdAt: string
  updatedAt: string
  archivedAt: string | null
}

export interface CreateChatSessionRequest {
  title: string
  folderId?: string | null
  projectId?: string | null
  openCardId?: string | null
  personalityId?: string | null
  aiEditMode?: AiEditMode | null
  searchAllMyDocs?: boolean
  forkedFromSessionId?: string | null
}

export interface CardChatLinkDto {
  id: string
  cardId: string
  chatSessionId: string
  ownerId: string
  ownerUsername: string
  summary: string | null
  createdAt: string
  archivedAt: string | null
}

export interface PromptPresetGroupDto {
  id: string
  name: string
  createdAt: string
  updatedAt: string
  archivedAt: string | null
  presets: PromptPresetDto[]
}

export interface DocumentDto {
  id: string
  title: string
  contentType: string
  language: string | null
  version: number
  createdAt: string
  updatedAt: string
  archivedAt: string | null
}
