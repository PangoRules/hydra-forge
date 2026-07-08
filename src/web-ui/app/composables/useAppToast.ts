/**
 * Standardized toast helper wrapping Nuxt UI v4 `useToast()`.
 *
 * Ensures every toast has explicit `type: 'foreground'` so auto-dismiss works
 * (Reka UI's ToastRoot pauses on hover by default when type="foreground").
 * Call sites never repeat `duration: 4000` — success/failure defaults live here.
 *
 * Usage:
 *   const toast = useAppToast()
 *   toast.success('Card archived')           // green, 4s
 *   toast.error('Failed to archive')         // red, 6s
 *   toast.success('Moved 3 cards', 2000)     // green, 2s
 *   toast.showApiError(error)                // red, 6s, with correlationId copy button
 */

import { ApiError } from '~/lib/api-error'

type ToastFn = (title: string, durationOverride?: number) => void

export function useAppToast() {
  const { add, remove, clear } = useToast()

  const show = (title: string, color: 'success' | 'error', dur: number) => {
    add({
      title,
      color,
      type: 'foreground' as const,
      duration: dur,
      close: true
    })
  }

  const success: ToastFn = (title, durationOverride) => {
    show(title, 'success', durationOverride ?? 4000)
  }

  const error: ToastFn = (title, durationOverride) => {
    show(title, 'error', durationOverride ?? 6000)
  }

  const showApiError = (error: ApiError | Error) => {
    if (error instanceof ApiError) {
      // For ApiError, show the title and add a copy button for correlationId
      add({
        title: error.title,
        color: 'error',
        type: 'foreground' as const,
        duration: 6000,
        close: true,
        actions: [
          {
            label: 'Copy Correlation ID',
            onClick: async () => {
              try {
                await navigator.clipboard.writeText(error.correlationId)
                add({
                  title: 'Correlation ID copied',
                  color: 'success',
                  type: 'foreground' as const,
                  duration: 2000
                })
              } catch {
                add({
                  title: 'Failed to copy — ID: ' + error.correlationId.slice(0, 8) + '.',
                  color: 'error',
                  type: 'foreground' as const,
                  duration: 4000
                })
              }
            }
          }
        ]
      })
    } else {
      // For generic Error, just show the message
      show(error.message, 'error', 6000)
    }
  }

  return { success, error, showApiError, remove, clear }
}
