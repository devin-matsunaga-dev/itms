import { describe, expect, it } from 'vitest'
import { transitionActions } from './ticket-transitions'

describe('transitionActions — which moves become buttons', () => {
  it('offers what the server allowed, in workflow order', () => {
    const actions = transitionActions('InProgress', ['Waiting', 'Resolved', 'Cancelled'])

    expect(actions.map((action) => action.status)).toEqual(['Waiting', 'Resolved', 'Cancelled'])
  })

  it('never renders New, because that is the unassign operation and not a transition', () => {
    // WP-1.6 made `Assigned → New` a real edge so unassignment writes its history line
    // like every other move. The status endpoint refuses it outright with
    // `helpdesk.unassign_to_return_to_new`; a button here would 409 every time.
    const actions = transitionActions('Assigned', ['New', 'InProgress', 'Cancelled'])

    expect(actions.map((action) => action.status)).toEqual(['InProgress', 'Cancelled'])
  })

  it('never renders Assigned, because a ticket is assigned by being given to somebody', () => {
    const actions = transitionActions('New', ['Assigned', 'Cancelled'])

    expect(actions.map((action) => action.status)).toEqual(['Cancelled'])
  })

  it('renders nothing from a terminal state', () => {
    expect(transitionActions('Closed', [])).toEqual([])
    expect(transitionActions('Cancelled', [])).toEqual([])
  })

  it('survives an absent list rather than assuming the whole table', () => {
    expect(transitionActions('New', undefined)).toEqual([])
  })

  it('words the same destination by where the ticket came from', () => {
    const from = (status: Parameters<typeof transitionActions>[0]): string | undefined =>
      transitionActions(status, ['InProgress'])[0]?.label

    expect(from('Assigned')).toBe('Start work')
    expect(from('Waiting')).toBe('Resume')
    expect(from('Resolved')).toBe('Reopen')
  })

  it('gives resolving and holding each their own note, and nothing else one', () => {
    // Both are required non-blank by the server and rejected on any other destination, so
    // the field the dialog collects has to match the destination exactly.
    const actions = transitionActions('InProgress', ['Waiting', 'Resolved', 'Cancelled'])

    expect(actions.find((action) => action.status === 'Resolved')?.note?.field).toBe(
      'resolutionNotes',
    )
    expect(actions.find((action) => action.status === 'Waiting')?.note?.field).toBe('holdReason')
    expect(actions.find((action) => action.status === 'Cancelled')?.note).toBeNull()
  })

  it('asks for a hold reason in the words the server refuses without', () => {
    const hold = transitionActions('InProgress', ['Waiting'])[0]

    expect(hold?.note?.required).toBe('Say what the ticket is waiting on.')
    expect(hold?.note?.label).toBe('Reason for the hold')
  })

  it('marks the two one-way moves as needing confirmation, and cancelling as destructive', () => {
    const actions = transitionActions('Resolved', ['Closed', 'Cancelled'])

    expect(actions.find((action) => action.status === 'Closed')).toMatchObject({
      confirms: true,
      destructive: false,
    })
    expect(actions.find((action) => action.status === 'Cancelled')).toMatchObject({
      confirms: true,
      destructive: true,
    })
  })
})
