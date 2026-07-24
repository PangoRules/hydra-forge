export function formatDueDate(dueAt: string | null | undefined): string | null {
  if (!dueAt) return null
  return new Date(dueAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

export function isOverdue(dueAt: string | null | undefined): boolean {
  if (!dueAt) return false
  return new Date(dueAt) < new Date()
}
