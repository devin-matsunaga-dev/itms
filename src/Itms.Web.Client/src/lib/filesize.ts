/** Byte counts as a person reads them. The API sends `byteLength` as a plain integer. */

const units = ['B', 'KB', 'MB', 'GB'] as const

/**
 * `842 B`, `12.4 KB`, `3.1 MB`.
 *
 * Binary steps, because a file size is what an operating system reports and every one of
 * them counts in 1024s. One decimal above a kilobyte, none below — "842.0 B" is noise.
 */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) {
    return '—'
  }

  let value = bytes
  let unit = 0

  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit += 1
  }

  return unit === 0
    ? `${String(Math.round(value))} ${units[unit]}`
    : `${value.toFixed(1)} ${units[unit]}`
}
