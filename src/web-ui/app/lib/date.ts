export function formatDueDate(dueAt: string | null | undefined): string | null {
  if (!dueAt) return null
  return new Date(dueAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

export function isOverdue(dueAt: string | null | undefined): boolean {
  if (!dueAt) return false
  return new Date(dueAt) < new Date()
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString()
}

// Compact, date-only — for table columns where a full timestamp would be too wide.
export function formatDateOnly(iso: string): string {
  return new Date(iso).toLocaleDateString()
}

export function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}
