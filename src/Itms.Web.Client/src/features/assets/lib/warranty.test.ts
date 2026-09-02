import { describe, expect, it } from 'vitest'
import {
  criticalDays,
  parseDateOnly,
  readWarranty,
  soonDays,
  warrantyLabel,
  warrantyTone,
} from './warranty'

/** Midday, so a test cannot pass by accident on a boundary the code rounds across. */
const today = new Date(2026, 8, 1, 12, 0, 0)

describe('readWarranty', () => {
  it('counts whole days in the viewer’s own calendar', () => {
    expect(readWarranty('2026-09-13', today).daysRemaining).toBe(12)
    expect(readWarranty('2026-09-01', today).daysRemaining).toBe(0)
    expect(readWarranty('2026-08-30', today).daysRemaining).toBe(-2)
  })

  it('says nothing when no date was recorded', () => {
    // "No warranty recorded" is not "expiring imminently" and not "covered" — it is
    // silence, and the server's filters treat it the same way.
    expect(readWarranty(null, today)).toEqual({
      state: 'none',
      daysRemaining: null,
      expiresAt: null,
    })
    expect(readWarranty(undefined, today).state).toBe('none')
  })

  it('puts each state on the side of the threshold DESIGN.md §4 names', () => {
    // danger under 7, warning under 30 — both exclusive at the top.
    expect(readWarranty(dateIn(-1), today).state).toBe('expired')
    expect(readWarranty(dateIn(0), today).state).toBe('critical')
    expect(readWarranty(dateIn(criticalDays - 1), today).state).toBe('critical')
    expect(readWarranty(dateIn(criticalDays), today).state).toBe('soon')
    expect(readWarranty(dateIn(soonDays - 1), today).state).toBe('soon')
    expect(readWarranty(dateIn(soonDays), today).state).toBe('covered')
  })

  it('agrees with the window the server filters on', () => {
    // WP-2.3: `warrantyExpiringInDays=30` is `today <= expiry <= today + 30`, inclusive at
    // both ends and excluding the already-lapsed. So every row that filter returns reads
    // here as between 0 and 30 days, and nothing it returns reads as expired.
    for (const days of [0, 1, 15, 29, 30]) {
      const warranty = readWarranty(dateIn(days), today)

      expect(warranty.daysRemaining).toBe(days)
      expect(warranty.state).not.toBe('expired')
    }
  })
})

describe('parseDateOnly', () => {
  it('reads a bare date as local midnight, not as UTC', () => {
    // `new Date('2026-09-01')` is UTC midnight, which is the 31st of August for anybody
    // behind Greenwich — and every countdown on the screen would be a day out.
    const date = parseDateOnly('2026-09-01')

    expect(date?.getFullYear()).toBe(2026)
    expect(date?.getMonth()).toBe(8)
    expect(date?.getDate()).toBe(1)
    expect(date?.getHours()).toBe(0)
  })

  it('answers null for anything that is not a date', () => {
    expect(parseDateOnly(null)).toBeNull()
    expect(parseDateOnly(undefined)).toBeNull()
    expect(parseDateOnly('')).toBeNull()
    expect(parseDateOnly('soon')).toBeNull()
  })
})

describe('warrantyLabel', () => {
  it('words each side of today without abbreviating', () => {
    expect(warrantyLabel(readWarranty(dateIn(12), today))).toBe('in 12 days')
    expect(warrantyLabel(readWarranty(dateIn(1), today))).toBe('in 1 day')
    expect(warrantyLabel(readWarranty(dateIn(0), today))).toBe('Expires today')
    expect(warrantyLabel(readWarranty(dateIn(-1), today))).toBe('Expired yesterday')
    expect(warrantyLabel(readWarranty(dateIn(-9), today))).toBe('Expired 9 days ago')
    expect(warrantyLabel(readWarranty(null, today))).toBe('No warranty recorded')
  })
})

describe('warrantyTone', () => {
  it('gives the two urgent states one hue and the rest their own', () => {
    expect(warrantyTone('expired')).toBe('text-danger')
    expect(warrantyTone('critical')).toBe('text-danger')
    expect(warrantyTone('soon')).toBe('text-warning')
    expect(warrantyTone('covered')).toBe('text-body')
    expect(warrantyTone('none')).toBe('text-muted-foreground')
  })
})

/** The `DateOnly` string for a day `offset` days from `today`. */
function dateIn(offset: number): string {
  const date = new Date(today.getFullYear(), today.getMonth(), today.getDate() + offset)
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${String(date.getFullYear())}-${month}-${day}`
}
