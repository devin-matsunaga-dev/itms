import { describe, expect, it } from 'vitest'
import { endOfLocalDay, kpiTiles, openStatuses } from './ticket-kpis'
import { parseTicketQuery } from './ticket-query'

const dayEnd = '2026-09-01T15:59:59.999Z'

describe('kpiTiles', () => {
  it('offers the four the mockup names, in DESIGN.md §4’s tint order', () => {
    expect(kpiTiles(dayEnd).map((tile) => tile.id)).toEqual([
      'open',
      'unassigned',
      'overdue',
      'dueToday',
    ])
  })

  it('links each tile to a query the list endpoint understands', () => {
    // A tile whose link the queue cannot parse lands somebody on an unfiltered screen
    // showing a different number from the one they clicked.
    for (const tile of kpiTiles(dayEnd)) {
      const query = parseTicketQuery(new URLSearchParams(tile.query))
      expect(query.sort).toBe('Priority')
      expect(query.pageSize).toBe(25)
    }
  })

  it('counts the same four statuses as open that the server does', () => {
    const query = parseTicketQuery(
      new URLSearchParams(kpiTiles(dayEnd).find((tile) => tile.id === 'open')?.query ?? ''),
    )

    expect(query.status).toEqual([...openStatuses])
  })

  it('asks the unassigned tile only for work still in play', () => {
    const query = parseTicketQuery(
      new URLSearchParams(kpiTiles(dayEnd).find((tile) => tile.id === 'unassigned')?.query ?? ''),
    )

    expect(query.unassigned).toBe(true)
    expect(query.status).toEqual([...openStatuses])
  })

  it('sends the overdue tile to the breached SLA view', () => {
    const query = parseTicketQuery(
      new URLSearchParams(kpiTiles(dayEnd).find((tile) => tile.id === 'overdue')?.query ?? ''),
    )

    expect(query.slaState).toBe('Breached')
  })

  it('carries the viewer’s own day boundary on the due-today tile', () => {
    const tile = kpiTiles(dayEnd).find((entry) => entry.id === 'dueToday')

    expect(tile?.query).toContain(encodeURIComponent(dayEnd))
  })
})

describe('endOfLocalDay', () => {
  it('is the last millisecond of the day the viewer is standing in', () => {
    const end = new Date(endOfLocalDay(new Date('2026-09-01T04:00:00Z')))

    expect(end.getHours()).toBe(23)
    expect(end.getMinutes()).toBe(59)
    expect(end.getSeconds()).toBe(59)
  })

  it('does not move for two instants on the same local day', () => {
    const morning = new Date('2026-09-01T00:30:00')
    const evening = new Date('2026-09-01T20:30:00')

    expect(endOfLocalDay(morning)).toBe(endOfLocalDay(evening))
  })
})
