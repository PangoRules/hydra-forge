/** UI display index → API string name. Zero-based; NOT the C# enum integer. */
export const CARD_TYPE_MAP: Record<number, string> = {
  0: 'Task',
  1: 'Issue',
  2: 'Goal',
  3: 'Idea'
}

/** Lucide icon name for each UI index. */
export const CARD_TYPE_ICONS: Record<number, string> = {
  0: 'i-lucide-square-check',
  1: 'i-lucide-bug',
  2: 'i-lucide-layers',
  3: 'i-lucide-lightbulb'
}

/**
 * Full metadata per type. `value` is the zero-based UI dropdown index;
 * `apiValue` is the string sent to / received from the API.
 */
export const CARD_TYPE_OPTIONS = [
  { value: 0, apiValue: 'Task',  label: 'Task',  color: 'neutral', icon: 'i-lucide-square-check' },
  { value: 1, apiValue: 'Issue', label: 'Issue', color: 'error',   icon: 'i-lucide-bug' },
  { value: 2, apiValue: 'Goal',  label: 'Goal',  color: 'primary', icon: 'i-lucide-layers' },
  { value: 3, apiValue: 'Idea',  label: 'Idea',  color: 'warning', icon: 'i-lucide-lightbulb' },
] as const

/** @deprecated — parent type is no longer restricted; any card can be a parent. */
export const PARENT_CARD_API_VALUE = ''

/** Per-column filter options. Values are API strings; null means "All". */
export const CARD_TYPE_FILTER_OPTIONS = [
  { label: 'All',   value: null },
  { label: 'Task',  value: 'Task' },
  { label: 'Issue', value: 'Issue' },
  { label: 'Goal',  value: 'Goal' },
  { label: 'Idea',  value: 'Idea' },
] as const

/**
 * Resolve a card type to its API string name.
 * Accepts the zero-based UI index (number) or an already-resolved API string.
 */
export function toTypeString(type: number | string): string {
  return typeof type === 'string' ? type : (CARD_TYPE_MAP[type] ?? 'Task')
}

/** Alias for toTypeString — matches existing import pattern across components. */
export const cardTypeToApiString = toTypeString

/** Full option object for a given UI index or API string. */
export function cardTypeOption(type: number | string) {
  const apiValue = toTypeString(type)
  return CARD_TYPE_OPTIONS.find(o => o.apiValue === apiValue) ?? CARD_TYPE_OPTIONS[0]
}

/** Tailwind text-color class for a card type option's color name. */
export function cardTypeColorClass(option: { color: string }): string {
  switch (option.color) {
    case 'error':   return 'text-red-500'
    case 'warning': return 'text-amber-500'
    case 'info':    return 'text-blue-500'
    case 'primary': return 'text-primary'
    default:        return 'text-gray-400'
  }
}