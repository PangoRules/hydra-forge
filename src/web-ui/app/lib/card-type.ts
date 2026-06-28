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
 * API string value for the parent card type filter.
 * Used in CardCreateModal to populate the Parent dropdown.
 */
export const PARENT_CARD_API_VALUE = 'Epic' as const

/**
 * Filter options with "All" first (value: null for untyped cards).
 */
export const CARD_TYPE_FILTER_OPTIONS = [
  { label: 'All', value: null },
  { label: 'Task', value: 'Task' },
  { label: 'Bug', value: 'Bug' },
  { label: 'Epic', value: 'Epic' },
  { label: 'Spec', value: 'Spec' },
  { label: 'Idea', value: 'Idea' }
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

/** Map CARD_TYPE_OPTION color name to a Tailwind text color class. */
export function cardTypeColorClass(option: { color: string }): string {
  switch (option.color) {
    case 'error': return 'text-red-500'
    case 'warning': return 'text-amber-500'
    case 'info': return 'text-blue-500'
    case 'primary': return 'text-primary'
    default: return 'text-gray-400'
  }
}
