import { describe, expect, it } from 'vitest'
import { slaMeter } from './sla-meter'
import { sla } from '../test/ticket-fixtures'

/** A 480-minute (8h) resolution target running from 09:00. */
const at = (iso: string): Date => new Date(iso)

describe('slaMeter', () => {
  it('is empty at the moment the ticket is raised', () => {
    const meter = slaMeter(sla({ resolutionDueAt: '2026-09-01T17:00:00Z' }), at('2026-09-01T09:00:00Z'))

    expect(meter.fraction).toBe(0)
    expect(meter.remaining).toBe('8h left')
  })

  it('is half full halfway through the target', () => {
    const meter = slaMeter(sla({ resolutionDueAt: '2026-09-01T17:00:00Z' }), at('2026-09-01T13:00:00Z'))

    expect(meter.fraction).toBeCloseTo(0.5)
    expect(meter.remaining).toBe('4h left')
  })

  it('pins at full once the deadline has passed, and says how far over', () => {
    const meter = slaMeter(
      sla({ resolutionDueAt: '2026-09-01T17:00:00Z', resolutionState: 'Breached' }),
      at('2026-09-01T18:30:00Z'),
    )

    expect(meter.fraction).toBe(1)
    expect(meter.remaining).toBe('Overdue 1h')
    expect(meter.bar).toContain('danger')
  })

  it('reads a parked clock at the instant it was parked, not at now', () => {
    // WP-1.8 freezes the deadline for the length of a Waiting period. A meter measured
    // against `now` would fill while nobody is able to work on the ticket.
    const parked = sla({
      resolutionDueAt: '2026-09-01T17:00:00Z',
      pausedAt: '2026-09-01T11:00:00Z',
      isPaused: true,
    })

    const soonAfter = slaMeter(parked, at('2026-09-01T11:30:00Z'))
    const muchLater = slaMeter(parked, at('2026-09-02T09:00:00Z'))

    expect(soonAfter.fraction).toBeCloseTo(0.25)
    expect(muchLater.fraction).toBeCloseTo(0.25)
    expect(muchLater.remaining).toBe('6h left')
    expect(muchLater.paused).toBe(true)
  })

  it('treats a finished clock as run out rather than as time remaining', () => {
    for (const state of ['Met', 'Stopped'] as const) {
      const meter = slaMeter(
        sla({ resolutionDueAt: '2026-09-01T17:00:00Z', resolutionState: state }),
        at('2026-09-01T10:00:00Z'),
      )

      expect(meter.fraction).toBe(1)
      // Nothing to count down; the pill's word is what the cell shows instead.
      expect(meter.remaining).toBeNull()
    }
  })

  it('never reports a negative or an overflowing bar', () => {
    const early = slaMeter(sla({ resolutionDueAt: '2026-09-09T17:00:00Z' }), at('2026-09-01T09:00:00Z'))
    const late = slaMeter(
      sla({ resolutionDueAt: '2026-09-01T10:00:00Z', resolutionState: 'Breached' }),
      at('2026-09-30T09:00:00Z'),
    )

    expect(early.fraction).toBeGreaterThanOrEqual(0)
    expect(late.fraction).toBeLessThanOrEqual(1)
  })

  it('names the state in words, so the bar is never the only signal', () => {
    expect(slaMeter(sla({ resolutionState: 'Approaching' }), at('2026-09-01T09:00:00Z')).label).toBe(
      'Due soon',
    )
    expect(slaMeter(sla({ resolutionState: 'Breached' }), at('2026-09-01T09:00:00Z')).label).toBe(
      'Overdue',
    )
  })

  it('rounds a span to one unit, and never says “0m left”', () => {
    const meter = slaMeter(
      sla({ resolutionDueAt: '2026-09-01T09:00:30Z' }),
      at('2026-09-01T09:00:00Z'),
    )

    expect(meter.remaining).toBe('1m left')
  })
})
