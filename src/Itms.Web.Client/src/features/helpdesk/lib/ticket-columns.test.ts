import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  defaultPreferences,
  isVisible,
  readPreferences,
  ticketColumns,
  toggleColumn,
  writePreferences,
} from './ticket-columns'

const key = 'itms.tickets.table'

beforeEach(() => {
  window.localStorage.clear()
})

afterEach(() => {
  vi.restoreAllMocks()
  window.localStorage.clear()
})

describe('toggleColumn', () => {
  it('hides a visible column and shows a hidden one', () => {
    const hidden = toggleColumn(defaultPreferences, 'status')
    expect(isVisible(hidden, 'status')).toBe(false)

    const shown = toggleColumn(hidden, 'status')
    expect(isVisible(shown, 'status')).toBe(true)
  })

  it('leaves the density alone', () => {
    expect(toggleColumn({ hidden: [], density: 'compact' }, 'sla').density).toBe('compact')
  })
})

describe('readPreferences', () => {
  it('gives a reader who has chosen nothing the defaults', () => {
    expect(readPreferences()).toEqual(defaultPreferences)
  })

  it('round-trips what was written', () => {
    writePreferences({ hidden: ['department', 'updated'], density: 'compact' })

    expect(readPreferences()).toEqual({ hidden: ['department', 'updated'], density: 'compact' })
  })

  it('drops a column id this build no longer has', () => {
    // Otherwise a renamed or retired column leaves a preference hiding nothing that the
    // menu can never clear.
    window.localStorage.setItem(key, JSON.stringify({ hidden: ['status', 'phlogiston'] }))

    expect(readPreferences().hidden).toEqual(['status'])
  })

  it('falls back to the defaults on anything it cannot read', () => {
    for (const stored of ['not json at all', '"a string"', 'null', '[]']) {
      window.localStorage.setItem(key, stored)
      expect(readPreferences()).toEqual(
        stored === '[]' ? { hidden: defaultPreferences.hidden, density: 'comfortable' } : defaultPreferences,
      )
    }
  })

  it('survives a browser that refuses storage entirely', () => {
    // A private window, cleared site data, or a browser configured to block site data
    // throws on access rather than returning empty.
    vi.spyOn(window.localStorage, 'getItem').mockImplementation(() => {
      throw new Error('denied')
    })

    expect(readPreferences()).toEqual(defaultPreferences)
  })
})

describe('writePreferences', () => {
  it('says nothing when storage refuses — the table still works, it just forgets', () => {
    vi.spyOn(window.localStorage, 'setItem').mockImplementation(() => {
      throw new Error('quota')
    })

    expect(() => {
      writePreferences({ hidden: [], density: 'compact' })
    }).not.toThrow()
  })
})

describe('ticketColumns', () => {
  it('does not offer the identifying column, which a row cannot do without', () => {
    const ids = ticketColumns.map((column) => column.id)

    expect(ids).not.toContain('ticket')
    expect(ids).not.toContain('subject')
  })
})
