// MemberRole enum: Owner=0, Member=1. openapi-typescript may type this as a
// number or a string depending on the endpoint's serialization — handle both.
export function displayRole(role: number | string | null): string {
  if (role === null) return '—'
  if (typeof role === 'string') return role
  const roles: Record<number, string> = { 0: 'Owner', 1: 'Member' }
  return roles[role] ?? String(role)
}
