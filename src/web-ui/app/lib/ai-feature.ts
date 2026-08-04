export const AI_FEATURE_LABELS: Record<string, string> = {
  PersonalChat: 'Personal Chat',
  ProjectChat: 'Project Chat',
  DeepResearch: 'Deep Research',
  AgentPipeline: 'Agent Pipeline',
  MemoryExtraction: 'Memory Extraction',
  NotesClassification: 'Notes Classification',
  DocumentEditing: 'Document Editing',
  CardReview: 'Card Review',
  ImageChat: 'Image Chat',
  ImageDocument: 'Image Document',
  ImageGalleryEditor: 'Image Gallery Editor',
  ProjectNarrative: 'Project Narrative (nightly)',
  DocumentEmbedding: 'Document Embedding',
  ChatTitle: 'Chat Title'
}

export type LlmTier = 'Economy' | 'Standard' | 'Premium'

export const TIER_OPTIONS: { label: string, value: LlmTier }[] = [
  { label: 'Economy', value: 'Economy' },
  { label: 'Standard', value: 'Standard' },
  { label: 'Premium', value: 'Premium' }
]

export const MAX_TIER_OPTIONS: { label: string, value: LlmTier | 'Locked' }[] = [
  { label: 'Economy', value: 'Economy' },
  { label: 'Standard', value: 'Standard' },
  { label: 'Premium', value: 'Premium' },
  { label: 'Locked (default only)', value: 'Locked' }
]
