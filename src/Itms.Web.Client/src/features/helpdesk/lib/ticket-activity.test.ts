import { describe, expect, it } from 'vitest'
import { buildActivity, hasOlderActivity } from './ticket-activity'
import { comment, historyEntry, ticketDetail } from '../test/ticket-fixtures'

describe('buildActivity — the merged timeline', () => {
  it('gives a ticket with no history and no comments the line saying it was raised', () => {
    const items = buildActivity(ticketDetail())

    expect(items).toHaveLength(1)
    expect(items[0]).toMatchObject({
      kind: 'raised',
      at: '2026-09-01T09:00:00Z',
      actorName: 'Jane Doe',
    })
  })

  it('reads newest first, because the payload carries only the head of each list', () => {
    const items = buildActivity(
      ticketDetail({
        history: [historyEntry({ id: 'h1', occurredAt: '2026-09-01T10:00:00Z' })],
        comments: [comment({ id: 'c1', createdAt: '2026-09-01T11:00:00Z' })],
      }),
    )

    expect(items.map((item) => item.kind)).toEqual(['comment', 'change', 'raised'])
  })

  it('renders the two lines a resolve writes as one event, in the order it wrote them', () => {
    // WP-1.4: resolving writes the status move and the resolution at one instant, and
    // added `sequence` because version 7 ids are not monotonic inside a millisecond.
    const items = buildActivity(
      ticketDetail({
        history: [
          historyEntry({
            id: 'h-resolution',
            kind: 'Resolution',
            sequence: 1,
            fromValue: null,
            toValue: 'Replaced the access point.',
            occurredAt: '2026-09-01T12:00:00Z',
          }),
          historyEntry({
            id: 'h-status',
            kind: 'Status',
            sequence: 0,
            fromValue: 'InProgress',
            toValue: 'Resolved',
            occurredAt: '2026-09-01T12:00:00Z',
          }),
        ],
      }),
    )

    const changes = items.filter((item) => item.kind === 'change')
    expect(changes).toHaveLength(1)
    expect(changes[0]?.entries.map((entry) => entry.id)).toEqual(['h-status', 'h-resolution'])
  })

  it('keeps two changes at different instants apart', () => {
    const items = buildActivity(
      ticketDetail({
        history: [
          historyEntry({ id: 'h2', occurredAt: '2026-09-01T12:00:00Z' }),
          historyEntry({ id: 'h1', occurredAt: '2026-09-01T10:00:00Z' }),
        ],
      }),
    )

    expect(items.filter((item) => item.kind === 'change')).toHaveLength(2)
  })

  it('orders a change before a comment that shares its instant, and never varies', () => {
    const detail = ticketDetail({
      history: [historyEntry({ id: 'h1', occurredAt: '2026-09-01T10:00:00Z' })],
      comments: [comment({ id: 'c1', createdAt: '2026-09-01T10:00:00Z' })],
    })

    const once = buildActivity(detail).map((item) => item.id)
    const twice = buildActivity(detail).map((item) => item.id)

    // A list whose order changes between two reads of the same data is what WP-1.4's
    // `sequence` and WP-1.5's id tie-break both came from.
    expect(once).toEqual(twice)
    expect(once[0]).toBe('h1')
    expect(once[1]).toBe('c1')
  })

  it('puts the raised line last even when a comment shares the creation instant', () => {
    const items = buildActivity(
      ticketDetail({ comments: [comment({ createdAt: '2026-09-01T09:00:00Z' })] }),
    )

    expect(items.at(-1)?.kind).toBe('raised')
  })

  it('reports the gap when the embedded head does not reach back to creation', () => {
    expect(hasOlderActivity(ticketDetail())).toBe(false)
    expect(hasOlderActivity(ticketDetail({ hasMoreHistory: true }))).toBe(true)
    expect(hasOlderActivity(ticketDetail({ hasMoreComments: true }))).toBe(true)
  })
})
