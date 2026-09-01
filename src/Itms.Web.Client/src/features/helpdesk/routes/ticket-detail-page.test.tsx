import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { TicketDetailPage } from '@/features/helpdesk/routes/ticket-detail-page'
import { ApiError } from '@/lib/api/client'
import { Roles } from '@/lib/roles'
import type { AuthenticatedUser, TicketDetail, UserSummary } from '@/lib/api/types'
import type { TicketRead } from '@/features/helpdesk/api/tickets-api'
import { renderWithProviders } from '@/test/render'
import {
  comment,
  historyEntry,
  technicianId,
  ticketDetail,
  ticketId,
} from '@/features/helpdesk/test/ticket-fixtures'

const fetchTicket = vi.fn<() => Promise<TicketRead>>()
const changeTicketStatus = vi.fn()
const assignTicket = vi.fn()
const addTicketComment = vi.fn()
const uploadTicketAttachment = vi.fn()
const fetchAssignableUsers = vi.fn<() => Promise<UserSummary[]>>()
const fetchCurrentUser = vi.fn<() => Promise<AuthenticatedUser | null>>()

vi.mock('@/features/helpdesk/api/tickets-api', () => ({
  fetchTicket: () => fetchTicket(),
  changeTicketStatus: (...args: unknown[]) => changeTicketStatus(...args),
  assignTicket: (...args: unknown[]) => assignTicket(...args),
  addTicketComment: (...args: unknown[]) => addTicketComment(...args),
  uploadTicketAttachment: (...args: unknown[]) => uploadTicketAttachment(...args),
  attachmentDownloadUrl: (ticket: string, attachment: string) =>
    `/api/v1/tickets/${ticket}/attachments/${attachment}`,
  fetchAssignableUsers: () => fetchAssignableUsers(),
  createTicket: vi.fn(),
  fetchTickets: vi.fn(),
  fetchTicketCategories: () => Promise.resolve([]),
  fetchTicketPriorities: () => Promise.resolve([]),
  fetchDepartments: () => Promise.resolve([]),
}))

const toastError = vi.fn()
const toastSuccess = vi.fn()

vi.mock('sonner', () => ({
  toast: {
    error: (message: string, options?: unknown) => toastError(message, options),
    success: (message: string, options?: unknown) => toastSuccess(message, options),
    info: vi.fn(),
  },
}))

vi.mock('@/features/auth/api/auth-api', () => ({
  fetchCurrentUser: () => fetchCurrentUser(),
  login: vi.fn(),
  logout: vi.fn(),
}))

const technician: AuthenticatedUser = {
  id: technicianId,
  userName: 'tech',
  email: 'tech@itms.local',
  displayName: 'Mark Reyes',
  roles: [Roles.technician],
  departmentId: null,
  locationId: null,
}

const endUser: AuthenticatedUser = { ...technician, userName: 'user', roles: [Roles.user] }

const version = '"7"'

function renderDetail(): ReturnType<typeof renderWithProviders> {
  return renderWithProviders(
    <Routes>
      <Route path="/tickets/:id" element={<TicketDetailPage />} />
    </Routes>,
    { route: `/tickets/${ticketId}` },
  )
}

function loads(ticket: TicketDetail, etag: string | null = version): void {
  fetchTicket.mockResolvedValue({ ticket, etag })
}

beforeEach(() => {
  fetchTicket.mockReset()
  changeTicketStatus.mockReset()
  assignTicket.mockReset()
  addTicketComment.mockReset()
  uploadTicketAttachment.mockReset()
  fetchAssignableUsers.mockReset()
  fetchCurrentUser.mockReset()
  toastError.mockReset()
  toastSuccess.mockReset()

  loads(ticketDetail())
  fetchCurrentUser.mockResolvedValue(technician)
  fetchAssignableUsers.mockResolvedValue([
    {
      id: technicianId,
      displayName: 'Mark Reyes',
      email: 'tech@itms.local',
      departmentId: null,
      locationId: null,
      isActive: true,
      roles: [Roles.technician],
    },
  ])
  changeTicketStatus.mockResolvedValue({ number: 'TKT-0001', status: 'InProgress' })
  assignTicket.mockResolvedValue({ number: 'TKT-0001', assigneeName: 'Mark Reyes' })
  addTicketComment.mockResolvedValue(comment())
})

describe('TicketDetailPage — reading a ticket', () => {
  it('leads with the subject, the number, and the state of the ticket', async () => {
    loads(ticketDetail({ status: 'InProgress' }))
    renderDetail()

    expect(
      await screen.findByRole('heading', { name: 'Laptop will not connect to Wi-Fi' }),
    ).toBeInTheDocument()
    expect(screen.getByText(/TKT-0001 · Raised by Jane Doe/)).toBeInTheDocument()
    expect(screen.getByText('In progress')).toBeInTheDocument()
    expect(
      screen.getByText('It drops the connection every few minutes in the east wing.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Information Technology')).toBeInTheDocument()
  })

  it('shows a skeleton in the screen’s own shape while it loads', () => {
    fetchTicket.mockReturnValue(new Promise(() => undefined))
    renderDetail()

    expect(screen.getByText('Loading the ticket…')).toBeInTheDocument()
  })

  it('answers a ticket that is not there, or not theirs, with the server’s own answer', async () => {
    // 404 covers three cases deliberately (WP-1.5). The screen does not guess which.
    fetchTicket.mockRejectedValue(new ApiError(404, null, 'No such ticket.'))
    renderDetail()

    expect(await screen.findByText('No such ticket')).toBeInTheDocument()
  })

  it('states what failed and offers a retry when the server does not answer', async () => {
    fetchTicket.mockRejectedValue(new ApiError(500, null, 'boom'))
    renderDetail()

    expect(await screen.findByRole('alert')).toHaveTextContent('The ticket could not be loaded.')
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument()
  })

  it('begins the timeline at the ticket being raised, which writes no history entry', async () => {
    renderDetail()

    // The page subtitle names the requester too, so this asserts inside the timeline
    // rather than on any mention of them.
    const activity = within(await screen.findByRole('list', { name: 'Ticket activity' }))
    expect(activity.getByText(/Raised by/)).toBeInTheDocument()
  })

  it('renders the two lines a resolve wrote as one event', async () => {
    loads(
      ticketDetail({
        status: 'Resolved',
        resolutionNotes: 'Replaced the access point.',
        resolvedAt: '2026-09-01T12:00:00Z',
        history: [
          historyEntry({
            id: 'h-status',
            sequence: 0,
            fromValue: 'InProgress',
            toValue: 'Resolved',
            occurredAt: '2026-09-01T12:00:00Z',
          }),
          historyEntry({
            id: 'h-resolution',
            kind: 'Resolution',
            sequence: 1,
            fromValue: null,
            toValue: 'Replaced the access point.',
            occurredAt: '2026-09-01T12:00:00Z',
          }),
        ],
      }),
    )
    renderDetail()

    const events = await screen.findAllByText('Mark Reyes updated this ticket')
    expect(events).toHaveLength(1)
    // The status name is rendered as a person reads it, not as the wire spells it.
    expect(screen.getByText('In progress')).toBeInTheDocument()
  })

  it('says so when the embedded head does not reach back to creation', async () => {
    loads(ticketDetail({ hasMoreComments: true, comments: [comment()] }))
    renderDetail()

    expect(
      await screen.findByText('Older activity on this ticket is not shown.'),
    ).toBeInTheDocument()
  })
})

describe('TicketDetailPage — transitions', () => {
  it('renders only what the server said the ticket may move to', async () => {
    loads(ticketDetail({ status: 'InProgress', allowedNextStatuses: ['Waiting', 'Resolved'] }))
    renderDetail()

    expect(await screen.findByRole('button', { name: 'Put on hold' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Resolve' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Close' })).not.toBeInTheDocument()
  })

  it('does not offer New as a transition, because that is the unassign operation', async () => {
    // WP-1.6's trap: `Assigned → New` is a real edge in the state machine and the status
    // endpoint refuses it outright. A button here would 409 every time it was pressed.
    loads(
      ticketDetail({
        status: 'Assigned',
        assigneeId: technicianId,
        assigneeName: 'Mark Reyes',
        allowedNextStatuses: ['New', 'InProgress', 'Cancelled'],
      }),
    )
    renderDetail()

    expect(await screen.findByRole('button', { name: 'Start work' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^New$/ })).not.toBeInTheDocument()
  })

  it('sends the version it read the ticket at, so a stale move is refused before it happens', async () => {
    const person = userEvent.setup()
    loads(ticketDetail({ status: 'Assigned', allowedNextStatuses: ['InProgress'] }))
    renderDetail()

    await person.click(await screen.findByRole('button', { name: 'Start work' }))

    await waitFor(() => {
      expect(changeTicketStatus).toHaveBeenCalledWith(ticketId, 'InProgress', null, version)
    })
  })

  it('refuses to resolve without saying what was done', async () => {
    const person = userEvent.setup()
    loads(ticketDetail({ status: 'InProgress', allowedNextStatuses: ['Resolved'] }))
    renderDetail()

    // The first click opens the dialog; while it is open the page behind it leaves the
    // accessibility tree, so the second `Resolve` is the dialog's own confirm.
    await person.click(await screen.findByRole('button', { name: 'Resolve' }))
    await person.click(await screen.findByRole('button', { name: 'Resolve' }))

    expect(
      await screen.findByText('Describe what was done to resolve the ticket.'),
    ).toBeInTheDocument()
    expect(changeTicketStatus).not.toHaveBeenCalled()
  })

  it('carries the resolution notes with the move once they are written', async () => {
    const person = userEvent.setup()
    loads(ticketDetail({ status: 'InProgress', allowedNextStatuses: ['Resolved'] }))
    renderDetail()

    await person.click(await screen.findByRole('button', { name: 'Resolve' }))
    await person.type(
      await screen.findByLabelText(/resolution notes/i),
      'Replaced the access point.',
    )
    await person.click(await screen.findByRole('button', { name: 'Resolve' }))

    await waitFor(() => {
      expect(changeTicketStatus).toHaveBeenCalledWith(
        ticketId,
        'Resolved',
        'Replaced the access point.',
        version,
      )
    })
  })

  it('tells somebody the ticket moved under them rather than that their move failed', async () => {
    const person = userEvent.setup()
    changeTicketStatus.mockRejectedValue(
      new ApiError(412, { code: 'helpdesk.ticket_conflict' } as never, 'stale'),
    )
    loads(ticketDetail({ status: 'Assigned', allowedNextStatuses: ['InProgress'] }))
    renderDetail()

    await person.click(await screen.findByRole('button', { name: 'Start work' }))

    await waitFor(() => {
      expect(toastError).toHaveBeenCalledWith(
        'This ticket changed while you were reading it.',
        expect.anything(),
      )
    })
  })
})

describe('TicketDetailPage — the conversation', () => {
  it('offers a technician the internal note, clearly marked', async () => {
    renderDetail()

    expect(
      await screen.findByText(/internal note — the requester cannot see this/i),
    ).toBeInTheDocument()
  })

  it('does not offer an end user an audience they cannot write to', async () => {
    fetchCurrentUser.mockResolvedValue(endUser)
    renderDetail()

    expect(await screen.findByLabelText('Add a comment')).toBeInTheDocument()
    expect(
      screen.queryByText(/internal note — the requester cannot see this/i),
    ).not.toBeInTheDocument()
  })

  it('posts what was typed, as a public comment by default', async () => {
    const person = userEvent.setup()
    renderDetail()

    await person.type(await screen.findByLabelText('Add a comment'), 'Rebooted the switch.')
    await person.click(screen.getByRole('button', { name: /post comment/i }))

    await waitFor(() => {
      expect(addTicketComment).toHaveBeenCalledWith(ticketId, 'Rebooted the switch.', false)
    })
  })

  it('marks an internal note in the thread so its audience is never in doubt', async () => {
    loads(ticketDetail({ comments: [comment({ isInternal: true, body: 'Escalating to the ISP.' })] }))
    renderDetail()

    expect(await screen.findByText('Internal note')).toBeInTheDocument()
    expect(screen.getByText('Escalating to the ISP.')).toBeInTheDocument()
  })
})

describe('TicketDetailPage — assignment', () => {
  it('gives somebody working the queue a control, and an end user the name only', async () => {
    loads(ticketDetail({ assigneeId: technicianId, assigneeName: 'Mark Reyes' }))
    renderDetail()

    expect(await screen.findByLabelText('Assignee')).toBeInTheDocument()

    fetchCurrentUser.mockResolvedValue(endUser)
    renderDetail()

    await waitFor(() => {
      expect(screen.getAllByText('Mark Reyes').length).toBeGreaterThan(0)
    })
  })
})
