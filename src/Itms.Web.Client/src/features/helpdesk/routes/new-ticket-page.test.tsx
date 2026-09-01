import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { NewTicketPage } from '@/features/helpdesk/routes/new-ticket-page'
import { ApiError } from '@/lib/api/client'
import { Roles } from '@/lib/roles'
import type {
  AuthenticatedUser,
  CreateTicketRequest,
  Department,
  TicketCategory,
  TicketPriority,
  UserSummary,
} from '@/lib/api/types'
import { renderWithProviders } from '@/test/render'
import { ticketDetail } from '@/features/helpdesk/test/ticket-fixtures'

const createTicket = vi.fn<(request: CreateTicketRequest) => Promise<unknown>>()
const fetchCurrentUser = vi.fn<() => Promise<AuthenticatedUser | null>>()
const fetchAssignableUsers = vi.fn<() => Promise<UserSummary[]>>()

const categories: TicketCategory[] = [
  {
    id: 'cat-network',
    name: 'Network',
    description: null,
    sortOrder: 1,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
]

const priorities: TicketPriority[] = [
  {
    id: 'pri-high',
    code: 'high',
    name: 'High',
    description: null,
    rank: 2,
    responseTargetMinutes: 30,
    resolutionTargetMinutes: 480,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
]

const departments: Department[] = [
  {
    id: 'dep-it',
    name: 'Information Technology',
    code: 'IT',
    description: null,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
]

vi.mock('@/features/helpdesk/api/tickets-api', () => ({
  createTicket: (request: CreateTicketRequest) => createTicket(request),
  fetchTicketCategories: (): Promise<TicketCategory[]> => Promise.resolve(categories),
  fetchTicketPriorities: (): Promise<TicketPriority[]> => Promise.resolve(priorities),
  fetchDepartments: (): Promise<Department[]> => Promise.resolve(departments),
  fetchAssignableUsers: () => fetchAssignableUsers(),
  fetchTicket: vi.fn(),
  fetchTickets: vi.fn(),
  changeTicketStatus: vi.fn(),
  assignTicket: vi.fn(),
  addTicketComment: vi.fn(),
  uploadTicketAttachment: vi.fn(),
  attachmentDownloadUrl: () => '',
}))

const toastError = vi.fn()
const toastSuccess = vi.fn()

vi.mock('sonner', () => ({
  toast: {
    error: (message: string, options?: unknown) => toastError(message, options),
    success: (message: string) => toastSuccess(message),
    info: vi.fn(),
  },
}))

vi.mock('@/features/auth/api/auth-api', () => ({
  fetchCurrentUser: () => fetchCurrentUser(),
  login: vi.fn(),
  logout: vi.fn(),
}))

const technician: AuthenticatedUser = {
  id: '11111111-1111-1111-1111-111111111111',
  userName: 'tech',
  email: 'tech@itms.local',
  displayName: 'Mark Reyes',
  roles: [Roles.technician],
  departmentId: null,
  locationId: null,
}

const endUser: AuthenticatedUser = { ...technician, userName: 'user', roles: [Roles.user] }

function Address(): React.JSX.Element {
  const location = useLocation()
  return <output data-testid="path">{location.pathname}</output>
}

function renderForm(): ReturnType<typeof renderWithProviders> {
  return renderWithProviders(
    <>
      <Routes>
        <Route path="/tickets/new" element={<NewTicketPage />} />
        <Route path="/tickets/:id" element={<p>the ticket</p>} />
      </Routes>
      <Address />
    </>,
    { route: '/tickets/new' },
  )
}

const path = (): string => screen.getByTestId('path').textContent ?? ''

beforeEach(() => {
  createTicket.mockReset()
  fetchCurrentUser.mockReset()
  fetchAssignableUsers.mockReset()
  toastError.mockReset()
  toastSuccess.mockReset()

  fetchCurrentUser.mockResolvedValue(technician)
  fetchAssignableUsers.mockResolvedValue([])
  createTicket.mockResolvedValue(ticketDetail())
})

describe('NewTicketPage', () => {
  it('labels the field SPEC.md calls the title, whatever the wire calls it', async () => {
    renderForm()

    // The entity and the event call it `Subject`, frozen in Itms.Contracts since WP-0.3.
    // The mismatch is internal and stops at this label.
    expect(await screen.findByLabelText(/^Title/)).toBeInTheDocument()
  })

  it('will not submit an empty form, and says what each field needs', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.click(await screen.findByRole('button', { name: /create ticket/i }))

    expect(await screen.findByText('Enter a title for the ticket.')).toBeInTheDocument()
    expect(screen.getByText('Describe what is wrong.')).toBeInTheDocument()
    expect(screen.getByText('Choose a category.')).toBeInTheDocument()
    expect(screen.getByText('Choose a priority.')).toBeInTheDocument()
    expect(createTicket).not.toHaveBeenCalled()
  })

  it('shows an end user their own name as the requester, and will not let them change it', async () => {
    fetchCurrentUser.mockResolvedValue(endUser)
    renderForm()

    // Shown rather than hidden, so the form reads the same for everybody and says why the
    // field is fixed. Not the enforcement — a User naming somebody else is refused with a
    // 403 (WP-1.5) and would be if this form were hand-crafted.
    const requester = await screen.findByLabelText('Requester')
    // Awaited: the field renders immediately and the name arrives with /auth/me.
    await waitFor(() => {
      expect(requester).toHaveValue('Mark Reyes (you)')
    })
    expect(requester).toHaveAttribute('readonly')
  })

  it('lets somebody working the queue file on another person’s behalf', async () => {
    renderForm()

    // Re-queried inside the wait, not captured before it: the field starts as the
    // read-only box and is replaced by a real control once /auth/me says the caller works
    // the queue, so a reference taken early points at a detached element.
    await waitFor(() => {
      expect(screen.getByLabelText('Requester')).not.toHaveAttribute('readonly')
    })
    expect(screen.getByLabelText('Department')).toBeInTheDocument()
  })

  it('explains the requester field rather than leaving it unexplained', async () => {
    renderForm()

    expect(
      await screen.findByRole('button', { name: /about the requester field/i }),
    ).toBeInTheDocument()
  })

  it('says where attachments go instead of offering a control that cannot work', async () => {
    // The API attaches only to a ticket that already exists (WP-1.7).
    renderForm()

    expect(await screen.findByText(/attachments are added on the ticket itself/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /add attachment/i })).not.toBeInTheDocument()
  })

  it('offers a way back to the queue from the header', async () => {
    renderForm()

    expect(await screen.findByRole('link', { name: /back to tickets/i })).toHaveAttribute(
      'href',
      '/tickets',
    )
  })

  it('maps a server field error back onto the field that caused it', async () => {
    const person = userEvent.setup()
    createTicket.mockRejectedValue(
      new ApiError(
        400,
        {
          code: 'helpdesk.category_retired',
          errors: { categoryId: ['That ticket category has been retired. Choose another.'] },
        } as never,
        'retired',
      ),
    )
    renderForm()

    await person.type(await screen.findByLabelText(/^Title/), 'Printer jam')
    await person.type(screen.getByLabelText(/^Description/), 'Tray two keeps jamming.')
    await submitWithPickers(person)

    expect(
      await screen.findByText('That ticket category has been retired. Choose another.'),
    ).toBeInTheDocument()
    expect(toastError).not.toHaveBeenCalled()
  })

  it('falls back to a toast when the failure names no field', async () => {
    const person = userEvent.setup()
    createTicket.mockRejectedValue(new ApiError(500, null, 'boom'))
    renderForm()

    await person.type(await screen.findByLabelText(/^Title/), 'Printer jam')
    await person.type(screen.getByLabelText(/^Description/), 'Tray two keeps jamming.')
    await submitWithPickers(person)

    await waitFor(() => {
      expect(toastError).toHaveBeenCalledWith('The ticket could not be raised.', expect.anything())
    })
  })

  it('goes straight to the ticket it just raised', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.type(await screen.findByLabelText(/^Title/), 'Printer jam')
    await person.type(screen.getByLabelText(/^Description/), 'Tray two keeps jamming.')
    await submitWithPickers(person)

    await waitFor(() => {
      expect(createTicket).toHaveBeenCalledWith({
        subject: 'Printer jam',
        description: 'Tray two keeps jamming.',
        categoryId: 'cat-network',
        priorityId: 'pri-high',
        requesterId: null,
        departmentId: null,
      })
    })

    await waitFor(() => {
      expect(path()).toBe('/tickets/ticket-1')
    })
    expect(toastSuccess).toHaveBeenCalledWith('TKT-0001 raised.')
  })
})

/**
 * Picks a category and a priority, then submits.
 *
 * The two pickers are Base UI popups; opening one and choosing an option is the same
 * three clicks every time, and what each test is actually about is what reaches the API
 * afterwards.
 */
async function submitWithPickers(person: ReturnType<typeof userEvent.setup>): Promise<void> {
  await person.click(screen.getByLabelText(/^Category/))
  await person.click(await screen.findByRole('option', { name: 'Network' }))

  await person.click(screen.getByLabelText(/^Priority/))
  await person.click(await screen.findByRole('option', { name: 'High' }))

  await person.click(screen.getByRole('button', { name: /create ticket/i }))
}
