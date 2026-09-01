import { useCallback, useState } from 'react'
import {
  readPreferences,
  toggleColumn,
  writePreferences,
  type TicketColumnId,
  type TicketDensity,
  type TicketTablePreferences,
} from '../lib/ticket-columns'

export interface TablePreferencesController {
  readonly preferences: TicketTablePreferences
  readonly toggle: (id: TicketColumnId) => void
  readonly setDensity: (density: TicketDensity) => void
}

/**
 * The reader's own column and density choices, remembered per browser.
 *
 * Plain `useState` seeded lazily rather than the module-level store `lib/theme.ts` uses:
 * the theme has to be the same object an inline script in `index.html` already agreed
 * with before React mounted, and nothing here runs before paint. One screen owns this.
 *
 * Storage is written through on every change and is allowed to fail — a private window
 * or a browser refusing site data leaves the table working and forgetful, which is the
 * right trade for a preference about row height.
 */
export function useTablePreferences(): TablePreferencesController {
  const [preferences, setPreferences] = useState<TicketTablePreferences>(readPreferences)

  const update = useCallback(
    (change: (current: TicketTablePreferences) => TicketTablePreferences) => {
      setPreferences((current) => {
        const next = change(current)
        writePreferences(next)
        return next
      })
    },
    [],
  )

  const toggle = useCallback(
    (id: TicketColumnId) => {
      update((current) => toggleColumn(current, id))
    },
    [update],
  )

  const setDensity = useCallback(
    (density: TicketDensity) => {
      update((current) => ({ ...current, density }))
    },
    [update],
  )

  return { preferences, toggle, setDensity }
}
