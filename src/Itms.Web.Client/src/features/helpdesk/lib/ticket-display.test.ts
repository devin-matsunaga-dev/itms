import { describe, expect, it } from 'vitest'
import type { SlaState, TicketStatus } from '@/lib/api/types'
import {
  priorityDot,
  slaLabels,
  slaStateOrder,
  slaTones,
  statusLabels,
  statusOrder,
  statusTones,
} from './ticket-display'

/**
 * The seven statuses and five SLA states the contract declares. Written out rather than
 * derived, so adding one server-side fails here — an unmapped status would otherwise
 * render as an unstyled blank and nobody would find out from a test.
 */
const everyStatus: TicketStatus[] = [
  'New',
  'Assigned',
  'InProgress',
  'Waiting',
  'Resolved',
  'Closed',
  'Cancelled',
]

const everySlaState: SlaState[] = ['Pending', 'Approaching', 'Breached', 'Met', 'Stopped']

describe('the status map', () => {
  it('names and colours every status the API can return', () => {
    for (const status of everyStatus) {
      expect(statusLabels[status]).toBeTruthy()
      expect(statusTones[status].fill).toBeTruthy()
      expect(statusTones[status].dot).toBeTruthy()
    }
  })

  it('offers them in the workflow’s own order', () => {
    expect([...statusOrder]).toEqual(everyStatus)
  })

  it('says "In progress" rather than the wire’s "InProgress"', () => {
    expect(statusLabels.InProgress).toBe('In progress')
  })

  it('holds DESIGN.md §2’s hues', () => {
    expect(statusTones.New.dot).toBe('bg-primary')
    expect(statusTones.InProgress.dot).toBe('bg-warning')
    expect(statusTones.Waiting.dot).toBe('bg-teal')
    expect(statusTones.Resolved.dot).toBe('bg-violet')
    expect(statusTones.Closed.dot).toBe('bg-neutral-chart')
    expect(statusTones.Cancelled.dot).toBe('bg-muted-foreground')
  })

  it('gives every status a dark-mode fill, because §5 checks both schemes', () => {
    for (const status of everyStatus) {
      expect(statusTones[status].fill).toContain('dark:')
    }
  })
})

describe('the priority map', () => {
  it('holds DESIGN.md §2’s four hues, keyed on the immutable code', () => {
    expect(priorityDot('critical')).toBe('bg-critical')
    expect(priorityDot('high')).toBe('bg-danger')
    expect(priorityDot('medium')).toBe('bg-warning')
    expect(priorityDot('low')).toBe('bg-success')
  })

  it('does not care how the code is cased', () => {
    expect(priorityDot('CRITICAL')).toBe('bg-critical')
  })

  it('gives an administrator’s own priority a hue nothing else claims', () => {
    // Better an unmapped priority reads as unmapped than as somebody else's severity.
    expect(priorityDot('after-hours')).toBe('bg-muted-foreground')
  })
})

describe('the SLA map', () => {
  it('names and colours every state', () => {
    for (const state of everySlaState) {
      expect(slaLabels[state]).toBeTruthy()
      expect(slaTones[state].fill).toContain('dark:')
    }
  })

  it('offers every state to the filter, hardest first', () => {
    expect([...slaStateOrder].sort()).toEqual([...everySlaState].sort())
    expect(slaStateOrder[0]).toBe('Breached')
  })

  it('says what a person means rather than what the enum says', () => {
    expect(slaLabels.Breached).toBe('Overdue')
    expect(slaLabels.Approaching).toBe('Due soon')
    expect(slaLabels.Pending).toBe('On track')
  })
})
