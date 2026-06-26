/** Card type names indexed by their numeric enum value (mirrors backend CardType). */
export const CARD_TYPE_MAP: Record<number, string> = {
  0: 'Task',
  1: 'Bug',
  2: 'Epic',
  3: 'Spec',
  4: 'Idea'
}

/** Lucide icon name for each card type. */
export const CARD_TYPE_ICONS: Record<number, string> = {
  0: 'i-lucide-square',
  1: 'i-lucide-bug',
  2: 'i-lucide-layers',
  3: 'i-lucide-file-text',
  4: 'i-lucide-lightbulb'
}

/**
 * Select / filter options with full metadata for each type.
 * Includes apiValue, color, and icon for UI rendering.
 */
export const CARD_TYPE_OPTIONS = [
  { value: 0, apiValue: 'Task', label: 'Task', color: 'neutral', icon: 'i-lucide-square-check' },
  { value: 1, apiValue: 'Bug', label: 'Bug', color: 'error', icon: 'i-lucide-bug' },
  { value: 2, apiValue: 'Epic', label: 'Epic', color: 'primary', icon: 'i-lucide-layers' },
  { value: 3, apiValue: 'Spec', label: 'Spec', color: 'info', icon: 'i-lucide-file-text' },
  { value: 4, apiValue: 'Idea', label: 'Idea', color: 'warning', icon: 'i-lucide-lightbulb' }
] as const

/**
 * Filter options with "All" first (value: null for untyped cards).
 */
export const CARD_TYPE_FILTER_OPTIONS = [
  { label: 'All', value: null },
  { label: 'Task', value: 0 },
  { label: 'Bug', value: 1 },
  { label: 'Epic', value: 2 },
  { label: 'Spec', value: 3 },
  { label: 'Idea', value: 4 }
] as const

/**
 * Normalize a card type value to its API string representation.
 * Handles both numeric enum values (from backend) and pre-resolved strings.
 */
export function toTypeString(type: number | string): string {
  return typeof type === 'string' ? type : (CARD_TYPE_MAP[type] ?? 'Task')
}

/** Alias for toTypeString — matches Plan 4's Task 16 import pattern. */
export const cardTypeToApiString = toTypeString

/** Full option object for a given type value (value, apiValue, label, color, icon). */
export function cardTypeOption(type: number | string) {
  const apiValue = toTypeString(type)
  return CARD_TYPE_OPTIONS.find(o => o.apiValue === apiValue) ?? CARD_TYPE_OPTIONS[0]
}
