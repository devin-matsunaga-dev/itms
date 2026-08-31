/**
 * The one date, time, and duration formatter (DESIGN.md §6). Every timestamp the API
 * returns is UTC (ARCHITECTURE.md §11); every timestamp a person reads is local, and
 * the absolute value stays available on hover wherever a relative one is shown.
 */

/** Parses an API timestamp. Returns null rather than an Invalid Date. */
export function parseTimestamp(value: string | Date | null | undefined): Date | null {
  if (value === null || value === undefined) {
    return null
  }

  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

/** `23 May 2025`. */
export function formatDate(value: string | Date | null | undefined): string {
  const date = parseTimestamp(value)
  if (!date) {
    return '—'
  }

  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(date)
}

/** `10:30 AM`, in the viewer's locale and timezone. */
export function formatTime(value: string | Date | null | undefined): string {
  const date = parseTimestamp(value)
  if (!date) {
    return '—'
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit',
  }).format(date)
}

/** The full absolute value, used as the title on anything rendered relatively. */
export function formatDateTime(value: string | Date | null | undefined): string {
  const date = parseTimestamp(value)
  if (!date) {
    return '—'
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date)
}

/** `Friday` — the weekday line under the page-header date. */
export function formatWeekday(value: string | Date | null | undefined): string {
  const date = parseTimestamp(value)
  if (!date) {
    return '—'
  }

  return new Intl.DateTimeFormat(undefined, { weekday: 'long' }).format(date)
}

const minute = 60_000
const hour = 60 * minute
const day = 24 * hour

/**
 * `2m ago`, `15m ago`, `1h ago`, `3d ago` — the compact form the alert and activity
 * rows use. Anything older than a week reads better as a date.
 */
export function formatRelative(
  value: string | Date | null | undefined,
  now: Date = new Date(),
): string {
  const date = parseTimestamp(value)
  if (!date) {
    return '—'
  }

  const elapsed = now.getTime() - date.getTime()
  if (elapsed < 0) {
    return 'just now'
  }
  if (elapsed < minute) {
    return 'just now'
  }
  if (elapsed < hour) {
    return `${String(Math.floor(elapsed / minute))}m ago`
  }
  if (elapsed < day) {
    return `${String(Math.floor(elapsed / hour))}h ago`
  }
  if (elapsed < 7 * day) {
    return `${String(Math.floor(elapsed / day))}d ago`
  }

  return formatDate(date)
}

/** `4h 20m`, `35m`, `2d 3h` — an elapsed span, for age and SLA columns. */
export function formatDuration(milliseconds: number): string {
  if (!Number.isFinite(milliseconds) || milliseconds < 0) {
    return '—'
  }

  const days = Math.floor(milliseconds / day)
  const hours = Math.floor((milliseconds % day) / hour)
  const minutes = Math.floor((milliseconds % hour) / minute)

  if (days > 0) {
    return hours > 0 ? `${String(days)}d ${String(hours)}h` : `${String(days)}d`
  }
  if (hours > 0) {
    return minutes > 0 ? `${String(hours)}h ${String(minutes)}m` : `${String(hours)}h`
  }
  return `${String(minutes)}m`
}
