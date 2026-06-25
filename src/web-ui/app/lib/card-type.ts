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
 * Select / filter options in render order (All first, then each type).
 * The "All" entry carries `value: null`.
 */
export const CARD_TYPE_OPTIONS: { label: string, value: number | null }[] = [
  { label: 'All', value: null },
  { label: 'Task', value: 0 },
  { label: 'Bug', value: 1 },
  { label: 'Epic', value: 2 },
  { label: 'Spec', value: 3 },
  { label: 'Idea', value: 4 }
]

/**
 * Normalize a card type value to its API string representation.
 * Handles both numeric enum values (from backend) and pre-resolved strings.
 */
export function toTypeString(type: number | string): string {
  return typeof type === 'string' ? type : (CARD_TYPE_MAP[type] ?? 'Task')
}
