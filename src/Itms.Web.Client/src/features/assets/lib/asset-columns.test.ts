import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  assetColumns,
  defaultPreferences,
  isVisible,
  readPreferences,
  toggleColumn,
  writePreferences,
  type AssetTablePreferences,
} from './asset-columns'

const storageKey = 'itms.assets.table'

afterEach(() => {
  window.localStorage.clear()
})

describe('assetColumns', () => {
  it('offers neither the asset itself nor the five fields the list contract withholds', () => {
    // The identifying column is not optional: a row with no identifier is not a denser
    // row, it is an unusable one. Cost, notes, barcode, vendor, and the purchase date are
    // not on `AssetListItemResponse` at all (WP-2.3, upheld at the human's direction), so
    // a column for one of them would have nothing behind it.
    const ids = assetColumns.map((column) => column.id)

    for (const withheld of ['asset', 'assetTag', 'cost', 'notes', 'barcode', 'vendor', 'purchase']) {
      expect(ids).not.toContain(withheld)
    }
  })
})

describe('toggleColumn', () => {
  it('flips one column and leaves the rest alone', () => {
    const hidden = toggleColumn(defaultPreferences, 'location')

    expect(isVisible(hidden, 'location')).toBe(false)
    expect(isVisible(hidden, 'status')).toBe(true)
    expect(isVisible(toggleColumn(hidden, 'location'), 'location')).toBe(true)
  })

  it('starts with serial and department hidden and everything else drawn', () => {
    expect(isVisible(defaultPreferences, 'serial')).toBe(false)
    expect(isVisible(defaultPreferences, 'department')).toBe(false)
    expect(isVisible(defaultPreferences, 'warranty')).toBe(true)
  })
})

describe('readPreferences', () => {
  it('returns the defaults when nothing has been stored', () => {
    expect(readPreferences()).toEqual(defaultPreferences)
  })

  it('round-trips what was written', () => {
    const preferences: AssetTablePreferences = { hidden: ['warranty'], density: 'compact' }
    writePreferences(preferences)

    expect(readPreferences()).toEqual(preferences)
  })

  it('drops a column id this build no longer has', () => {
    // Otherwise a renamed or retired column leaves a reader with a preference that hides
    // nothing and can never be cleared from the menu.
    window.localStorage.setItem(
      storageKey,
      JSON.stringify({ hidden: ['serial', 'hostname'], density: 'comfortable' }),
    )

    expect(readPreferences().hidden).toEqual(['serial'])
  })

  it('falls back to the defaults on anything unexpected', () => {
    window.localStorage.setItem(storageKey, 'not json')
    expect(readPreferences()).toEqual(defaultPreferences)

    window.localStorage.setItem(storageKey, '"a string"')
    expect(readPreferences()).toEqual(defaultPreferences)

    window.localStorage.setItem(storageKey, JSON.stringify({ density: 'roomy' }))
    expect(readPreferences().density).toBe('comfortable')
  })

  it('survives a browser that refuses storage', () => {
    // A private window or a browser configured to block site data raises on access rather
    // than returning empty. The table still works; it simply will not remember.
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('denied')
    })
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('denied')
    })

    expect(readPreferences()).toEqual(defaultPreferences)
    expect(() => {
      writePreferences({ hidden: [], density: 'compact' })
    }).not.toThrow()

    getItem.mockRestore()
    setItem.mockRestore()
  })
})

describe('storage key', () => {
  it('is the register’s own, not the ticket queue’s', () => {
    // A technician who runs the queue compact does not thereby want the register compact,
    // and the two tables share no column.
    writePreferences({ hidden: ['status'], density: 'compact' })

    expect(window.localStorage.getItem(storageKey)).not.toBeNull()
    expect(window.localStorage.getItem('itms.tickets.table')).toBeNull()
  })
})
