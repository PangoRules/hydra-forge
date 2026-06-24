/**
 * Centralized route constants for UI pages and API endpoints.
 * Use these instead of inline magic strings to keep routes consistent
 * and easy to update when endpoints or page paths change.
 */

export const UiRoutes = {
  Login: '/login',
  Setup: '/setup',
  Projects: '/projects'
} as const

export const ApiRoutes = {
  Auth: {
    Login: '/api/Auth/login'
  }
} as const
