import { useCallback, useState } from 'react'
import {
  readPreferences,
  toggleColumn,
  writePreferences,
  type AssetColumnId,
  type AssetDensity,
  type AssetTablePreferences,
} from '../lib/asset-columns'

export interface AssetTablePreferencesController {
  readonly preferences: AssetTablePreferences
  readonly toggle: (id: AssetColumnId) => void
  readonly setDensity: (density: AssetDensity) => void
}

/**
 * The reader's own column and density choices for the register, remembered per browser.
 *
 * Plain `useState` seeded lazily, following the queue's controller: nothing here runs
 * before paint, so there is no reason for the module-level store the colour scheme needs.
 * Storage is written through on every change and is allowed to fail — a private window
 * leaves the table working and forgetful, which is the right trade for a preference about
 * row height.
 */
export function useAssetTablePreferences(): AssetTablePreferencesController {
  const [preferences, setPreferences] = useState<AssetTablePreferences>(readPreferences)

  const update = useCallback(
    (change: (current: AssetTablePreferences) => AssetTablePreferences) => {
      setPreferences((current) => {
        const next = change(current)
        writePreferences(next)
        return next
      })
    },
    [],
  )

  const toggle = useCallback(
    (id: AssetColumnId) => {
      update((current) => toggleColumn(current, id))
    },
    [update],
  )

  const setDensity = useCallback(
    (density: AssetDensity) => {
      update((current) => ({ ...current, density }))
    },
    [update],
  )

  return { preferences, toggle, setDensity }
}
