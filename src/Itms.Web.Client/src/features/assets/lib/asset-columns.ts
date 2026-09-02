/**
 * Which columns the register shows, and how tightly it packs them.
 *
 * **Why this is not in the URL, unlike every filter.** DESIGN.md §4 draws the line and
 * `ticket-columns.ts` argues it at length: filters, sorting, and paging describe *which
 * rows*, which is the thing somebody sends to a colleague, so they live in the address.
 * Hidden columns and row density describe how one person likes to read, travel badly in a
 * link, and would make two addresses for the same query — so they are a per-browser
 * preference, held where the colour scheme is already held.
 *
 * A second storage key rather than a shared one: a technician who runs the ticket queue
 * compact does not thereby want the asset register compact, and the two tables do not
 * share a single column between them.
 *
 * Storage is best-effort in both directions. A private window, cleared site data, or a
 * browser configured to refuse storage all raise on access rather than returning empty,
 * so every read and write is guarded and the defaults are what a failure produces.
 */

/** A column the reader may turn off. The asset itself is not offered — see `assetColumns`. */
export type AssetColumnId =
  | 'status'
  | 'type'
  | 'serial'
  | 'holder'
  | 'department'
  | 'location'
  | 'warranty'
  | 'updated'

export interface AssetColumn {
  readonly id: AssetColumnId
  readonly label: string
}

/**
 * Every optional column, in the order the table lays them out.
 *
 * The asset itself — the tag, the name under it, and the recorded caption — is not on
 * this list. A row with no identifier is not a denser row, it is an unusable one.
 *
 * **Cost, notes, barcode, vendor, and the purchase date are not here either, and that is
 * deliberate.** WP-2.3 kept all five off `AssetListItemResponse` and recorded that adding
 * one would be a decision for this package rather than something it discovers; at the
 * human's direction that decision is *no*. An inventory list is the thing somebody
 * screenshots, and a cost column would put the commercial value of the estate in it.
 * They are on the detail screen, where one asset is being read on purpose.
 */
export const assetColumns: readonly AssetColumn[] = [
  { id: 'status', label: 'Status' },
  { id: 'type', label: 'Type' },
  { id: 'serial', label: 'Serial number' },
  { id: 'holder', label: 'Assigned to' },
  { id: 'department', label: 'Department' },
  { id: 'location', label: 'Location' },
  { id: 'warranty', label: 'Warranty' },
  { id: 'updated', label: 'Updated' },
]

/** How tightly rows pack. `comfortable` is DESIGN.md §4's 44px row. */
export type AssetDensity = 'comfortable' | 'compact'

export interface AssetTablePreferences {
  readonly hidden: readonly AssetColumnId[]
  readonly density: AssetDensity
}

/**
 * What a reader who has never chosen sees: six columns and the identifying one.
 *
 * `serial` and `department` start hidden. A serial number is a key equipment is *looked
 * up* by — the search box already matches it — rather than one a register is scanned by,
 * and it is the widest low-information column on the row. A department duplicates the
 * location for most of an estate, and the location is the one a technician walking to a
 * machine needs. Both are one click away in the Columns menu.
 */
export const defaultPreferences: AssetTablePreferences = {
  hidden: ['serial', 'department'],
  density: 'comfortable',
}

const storageKey = 'itms.assets.table'

const columnIds = new Set<string>(assetColumns.map((column) => column.id))

/** True when the column should be rendered. */
export function isVisible(preferences: AssetTablePreferences, id: AssetColumnId): boolean {
  return !preferences.hidden.includes(id)
}

/** The same preferences with one column flipped. */
export function toggleColumn(
  preferences: AssetTablePreferences,
  id: AssetColumnId,
): AssetTablePreferences {
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
export function readPreferences(): AssetTablePreferences {
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

    const record = parsed as Partial<Record<keyof AssetTablePreferences, unknown>>

    const hidden = Array.isArray(record.hidden)
      ? record.hidden.filter(
          (value): value is AssetColumnId => typeof value === 'string' && columnIds.has(value),
        )
      : defaultPreferences.hidden

    const density: AssetDensity = record.density === 'compact' ? 'compact' : 'comfortable'

    return { hidden, density }
  } catch {
    return defaultPreferences
  }
}

/** Stores the preferences. A browser that refuses is not an error the reader can act on. */
export function writePreferences(preferences: AssetTablePreferences): void {
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(preferences))
  } catch {
    // Nothing to do and nothing worth saying: the table still works, it simply will not
    // remember. The same call `lib/theme.ts` and the ticket queue both make.
  }
}
