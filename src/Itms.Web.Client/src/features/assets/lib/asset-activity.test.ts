import { describe, expect, it } from 'vitest'
import { asset, historyEntry } from '../test/asset-fixtures'
import { buildAssetActivity, changeKindLabels, hasOlderHistory } from './asset-activity'

const subject = asset({ createdAt: '2026-08-01T09:00:00Z', assetTag: 'LAP-0042' })

describe('buildAssetActivity', () => {
  it('groups the lines one operation wrote into one event', () => {
    // Issuing a machine out of stock moves the holder and the status, so
    // `AssetChanges.Between` writes two entries at one instant — and the endpoint's own
    // description says they are meant to be read together, in sequence order.
    const items = buildAssetActivity(subject, [
      historyEntry({ id: 'b', kind: 'Status', fromValue: 'In Stock', toValue: 'Deployed', sequence: 1 }),
      historyEntry({ id: 'a', kind: 'Assignment', fromValue: null, toValue: 'Jane Doe', sequence: 0 }),
    ])

    expect(items).toHaveLength(2)

    const change = items[0]
    expect(change?.kind).toBe('change')
    if (change?.kind !== 'change') {
      throw new Error('expected a change')
    }

    expect(change.entries.map((entry) => entry.id)).toEqual(['a', 'b'])
    expect(change.entries.map((entry) => entry.kind)).toEqual(['Assignment', 'Status'])
  })

  it('keeps a transfer between two people as one line, which is WP-2.2’s own criterion', () => {
    const items = buildAssetActivity(subject, [
      historyEntry({ id: 'a', kind: 'Assignment', fromValue: 'Jane Doe', toValue: 'Mark Reyes' }),
    ])

    const change = items[0]
    if (change?.kind !== 'change') {
      throw new Error('expected a change')
    }

    expect(change.entries).toHaveLength(1)
    expect(change.entries[0]?.fromValue).toBe('Jane Doe')
    expect(change.entries[0]?.toValue).toBe('Mark Reyes')
  })

  it('reads newest first, with the recorded line last', () => {
    const items = buildAssetActivity(subject, [
      historyEntry({ id: 'older', occurredAt: '2026-08-10T09:00:00Z' }),
      historyEntry({ id: 'newer', occurredAt: '2026-08-20T09:00:00Z' }),
    ])

    expect(items.map((item) => item.id)).toEqual(['newer', 'older', `recorded-${subject.id}`])
  })

  it('always has a beginning, because recording an asset writes no history entry', () => {
    // `CreateAssetHandler` audits and raises nothing — a creation has no "before" for
    // `AssetChanges.Between` to diff — so the first line is synthesised rather than a
    // third `AssetChangeKind` being invented server-side.
    const items = buildAssetActivity(subject, [])

    expect(items).toHaveLength(1)
    expect(items[0]?.kind).toBe('recorded')
    expect(items[0]?.at).toBe(subject.createdAt)
  })

  it('puts a change above the recorded line it shares an instant with', () => {
    // An asset issued in the same millisecond it was recorded still happened after being
    // recorded.
    const items = buildAssetActivity(subject, [
      historyEntry({ id: 'a', occurredAt: subject.createdAt }),
    ])

    expect(items.map((item) => item.kind)).toEqual(['change', 'recorded'])
  })

  it('breaks every tie, so two reads of the same data cannot disagree about the order', () => {
    const entries = [
      historyEntry({ id: 'aaa', occurredAt: '2026-08-20T09:00:00Z' }),
      historyEntry({ id: 'zzz', occurredAt: '2026-08-20T09:00:00Z' }),
    ]

    // Version 7 ids are not monotonic within a millisecond, so the same two entries can
    // arrive in either order — and must render in one.
    const forwards = buildAssetActivity(subject, entries).map((item) => item.id)
    const backwards = buildAssetActivity(subject, [...entries].reverse()).map((item) => item.id)

    expect(forwards).toEqual(backwards)
  })

  it('takes the actor and the instant from the first line of the operation', () => {
    const items = buildAssetActivity(subject, [
      historyEntry({ id: 'b', sequence: 1, actorName: 'Mark Reyes' }),
      historyEntry({ id: 'a', sequence: 0, actorName: 'Mark Reyes' }),
    ])

    const change = items[0]
    if (change?.kind !== 'change') {
      throw new Error('expected a change')
    }

    expect(change.actorName).toBe('Mark Reyes')
    expect(change.id).toBe('a+b')
  })
})

describe('hasOlderHistory', () => {
  it('is true only when the server has more than the screen is holding', () => {
    expect(hasOlderHistory(3, 3)).toBe(false)
    expect(hasOlderHistory(50, 62)).toBe(true)
    expect(hasOlderHistory(0, 0)).toBe(false)
  })
})

describe('changeKindLabels', () => {
  it('names both dimensions the server records', () => {
    // Two kinds, not five: SPEC.md §3's five operations move two facts, and a missing key
    // here is a type error rather than a blank row.
    expect(changeKindLabels.Assignment).toBe('Assigned to')
    expect(changeKindLabels.Status).toBe('Status')
  })
})
