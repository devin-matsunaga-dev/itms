import { describe, expect, it } from 'vitest'
import {
  formatDate,
  formatDuration,
  formatRelative,
  formatTime,
  formatWeekday,
  parseTimestamp,
} from '@/lib/datetime'

describe('parseTimestamp', () => {
  it('returns null for a missing value', () => {
    expect(parseTimestamp(null)).toBeNull()
    expect(parseTimestamp(undefined)).toBeNull()
  })

  it('returns null rather than an Invalid Date for junk', () => {
    expect(parseTimestamp('not a date')).toBeNull()
  })

  it('reads the UTC timestamps the API returns', () => {
    expect(parseTimestamp('2026-05-23T10:30:00Z')?.toISOString()).toBe('2026-05-23T10:30:00.000Z')
  })
})

describe('formatting', () => {
  it('renders a placeholder rather than throwing on a missing value', () => {
    expect(formatDate(null)).toBe('—')
    expect(formatTime(null)).toBe('—')
    expect(formatWeekday(null)).toBe('—')
  })

  it('renders the date in the viewer local timezone', () => {
    // Constructed locally, so the assertion holds whatever timezone the run is in.
    const local = new Date(2025, 4, 23, 10, 30)
    expect(formatDate(local)).toContain('2025')
    expect(formatWeekday(local)).toBe('Friday')
  })
})

describe('formatRelative', () => {
  const now = new Date('2026-05-23T12:00:00Z')

  it.each([
    ['2026-05-23T11:58:00Z', '2m ago'],
    ['2026-05-23T11:45:00Z', '15m ago'],
    ['2026-05-23T11:00:00Z', '1h ago'],
    ['2026-05-20T12:00:00Z', '3d ago'],
  ])('renders %s as %s', (value, expected) => {
    expect(formatRelative(value, now)).toBe(expected)
  })

  it('collapses anything under a minute to "just now"', () => {
    expect(formatRelative('2026-05-23T11:59:30Z', now)).toBe('just now')
  })

  it('falls back to an absolute date beyond a week', () => {
    expect(formatRelative('2026-04-01T12:00:00Z', now)).toContain('2026')
  })
})

describe('formatDuration', () => {
  it.each([
    [35 * 60_000, '35m'],
    [4 * 3_600_000 + 20 * 60_000, '4h 20m'],
    [2 * 86_400_000 + 3 * 3_600_000, '2d 3h'],
    [3 * 3_600_000, '3h'],
  ])('renders %d ms as %s', (milliseconds, expected) => {
    expect(formatDuration(milliseconds)).toBe(expected)
  })

  it('refuses a negative span rather than inventing one', () => {
    expect(formatDuration(-1)).toBe('—')
  })
})
