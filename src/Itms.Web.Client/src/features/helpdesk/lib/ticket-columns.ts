/**
 * Which columns the queue shows, and how tightly it packs them.
 *
 * **Why this is not in the URL, unlike every filter.** CONVENTIONS.md and DESIGN.md §6
 * ask a list screen to keep filter, sort, and page state in the address so a view is
 * linkable — because those describe *which rows*, which is the thing somebody sends to a
 * colleague. Hidden columns and row density describe how one person likes to read,
 * travel badly in a link, and would make two addresses for the same query. So they are a
 * per-browser preference, held where the colour scheme is already held.
 *
 * Storage is best-effort in both directions. A private window, cleared site data, or a
 * browser configured to refuse storage all raise on access rather than returning empty,
 * so every read and write is guarded and the defaults are what a failure produces.
 */

/** A column the reader may turn off. The first two are not offered — see `optional`. */
export type TicketColumnId =
  | 'requester'
  | 'assignee'
  | 'department'
  | 'category'
  | 'priority'
  | 'status'
  | 'created'
  | 'sla'
  | 'updated'

export interface TicketColumn {
  readonly id: TicketColumnId
  readonly label: string
}

/**
 * Every optional column, in the order the table lays them out.
 *
 * The ticket itself — number, subject, and the created caption under them — is not on
 * this list. A row with no identifier is not a denser row, it is an unusable one.
 */
export const ticketColumns: readonly TicketColumn[] = [
  { id: 'status', label: 'Status' },
  { id: 'priority', label: 'Priority' },
  { id: 'requester', label: 'Requester' },
  { id: 'assignee', label: 'Assignee' },
  { id: 'department', label: 'Department' },
  { id: 'category', label: 'Category' },
  { id: 'created', label: 'Age' },
  { id: 'sla', label: 'SLA' },
  { id: 'updated', label: 'Updated' },
]

/** How tightly rows pack. `comfortable` is DESIGN.md §4's 44px row. */
export type TicketDensity = 'comfortable' | 'compact'

export interface TicketTablePreferences {
  readonly hidden: readonly TicketColumnId[]
  readonly density: TicketDensity
}

/**
 * What a reader who has never chosen sees.
 *
 * `category` is hidden by default: it is the one column of the nine that a technician
 * triaging a queue almost never sorts or scans by, and nine columns already wrap at the
 * 1280px floor DESIGN.md §6 sets.
 */
export const defaultPreferences: TicketTablePreferences = {
  hidden: ['category'],
  density: 'comfortable',
}

const storageKey = 'itms.tickets.table'

const columnIds = new Set<string>(ticketColumns.map((column) => column.id))

/** True when the column should be rendered. */
export function isVisible(
  preferences: TicketTablePreferences,
  id: TicketColumnId,
): boolean {
  return !preferences.hidden.includes(id)
}

/** The same preferences with one column flipped. */
export function toggleColumn(
  preferences: TicketTablePreferences,
  id: TicketColumnId,
): TicketTablePreferences {
  const hidden = preferences.hidden.includes(id)
    ? preferences.hidden.filter((column) => column !== id)
    : [...preferences.hidden, id]

  return { ...preferences, hidden }
}

/**
 * Reads the stored preferences, falling back to the defaults on anything unexpected.
 *
 * Unknown column ids are dropped rather than kept: a build that renames or retires a
 * column would otherwise leave a reader with a preference that hides nothing and can
 * never be cleared from the menu.
 */
export function readPreferences(): TicketTablePreferences {
  let raw: string | null = null

  try {
    raw = window.localStorage.getItem(storageKey)
  } catch {
    return defaultPreferences
  }

  if (raw === null) {
    return defaultPreferences
  }

  try {
    const parsed: unknown = JSON.parse(raw)
    if (typeof parsed !== 'object' || parsed === null) {
      return defaultPreferences
    }

    const record = parsed as Partial<Record<keyof TicketTablePreferences, unknown>>

    const hidden = Array.isArray(record.hidden)
      ? record.hidden.filter(
          (value): value is TicketColumnId =>
            typeof value === 'string' && columnIds.has(value),
        )
      : defaultPreferences.hidden

    const density: TicketDensity = record.density === 'compact' ? 'compact' : 'comfortable'

    return { hidden, density }
  } catch {
    return defaultPreferences
  }
}

/** Stores the preferences. A browser that refuses is not an error the reader can act on. */
export function writePreferences(preferences: TicketTablePreferences): void {
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(preferences))
  } catch {
    // Nothing to do and nothing worth saying: the table still works, it simply will not
    // remember. The same call `lib/theme.ts` makes.
  }
}
