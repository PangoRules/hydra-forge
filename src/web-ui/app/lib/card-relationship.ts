/**
 * Mirrors the TUI's CardRelationshipIndicatorHelper + BoardRenderer.FormatBadgeLine —
 * same verb/ordering per RelationshipType + direction, so both clients read the same way.
 */
export type RelationshipType = 'BlockedBy' | 'Precedes' | 'Relates' | 'SpawnedFrom'

export interface RelationshipBadgeStyle {
  icon: string
  colorClass: string
  verb: string
}

const STYLES: Record<RelationshipType, { icon: string, colorClass: string, verbs: [string, string] }> = {
  BlockedBy: { icon: 'i-lucide-ban', colorClass: 'text-error', verbs: ['blocks', 'blocked by'] },
  Precedes: { icon: 'i-lucide-fast-forward', colorClass: 'text-warning', verbs: ['precedes', 'preceded by'] },
  SpawnedFrom: { icon: 'i-lucide-sprout', colorClass: 'text-info', verbs: ['spawned from', 'spawned'] },
  Relates: { icon: 'i-lucide-link', colorClass: 'text-muted', verbs: ['relates', 'relates'] }
}

/** Same ordering as the TUI: blocking relationships surface first, informational ones last. */
export const RELATIONSHIP_TYPE_ORDER: Record<RelationshipType, number> = {
  BlockedBy: 0,
  Precedes: 1,
  SpawnedFrom: 2,
  Relates: 3
}

export function formatRelationshipBadge(type: RelationshipType, isSource: boolean): RelationshipBadgeStyle {
  const style = STYLES[type] ?? STYLES.Relates
  return {
    icon: style.icon,
    colorClass: style.colorClass,
    verb: isSource ? style.verbs[0] : style.verbs[1]
  }
}
