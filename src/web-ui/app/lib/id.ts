/**
 * `crypto.randomUUID()` only exists in a secure context — HTTPS, or the
 * special-cased `http://localhost` (Chrome grants that one hostname an
 * exception). A deployment reached over plain HTTP by IP (e.g. a Tailscale
 * address like `http://100.69.37.61:8080`) gets no exception, so the method
 * doesn't exist there and calling it throws `TypeError: crypto.randomUUID is
 * not a function` — synchronously, before anything else in the same function
 * runs. Use this for temporary client-side ids (optimistic UI state, always
 * replaced by the server's real id once a request succeeds) instead —
 * cryptographic randomness isn't needed for a value that never leaves the
 * browser tab.
 */
export function randomId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}
