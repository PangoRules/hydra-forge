import { describe, it, expect, vi, afterEach } from 'vitest'
import { formatDueDate, isOverdue } from '~/lib/date'

describe('date', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns null for a missing due date', () => {
    expect(formatDueDate(null)).toBeNull()
    expect(formatDueDate(undefined)).toBeNull()
  })

  it('formats an ISO date as short month + day', () => {
    // Use a date at noon UTC to avoid timezone edge cases
    expect(formatDueDate('2024-03-15T12:00:00Z')).toBe('Mar 15')
  })

  it('is not overdue when there is no due date', () => {
    expect(isOverdue(null)).toBe(false)
  })

  it('is overdue when the due date is in the past', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2024-06-01T00:00:00Z'))
    expect(isOverdue('2024-01-01T00:00:00Z')).toBe(true)
  })

  it('is not overdue when the due date is in the future', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2024-01-01T00:00:00Z'))
    expect(isOverdue('2024-06-01T00:00:00Z')).toBe(false)
  })
})