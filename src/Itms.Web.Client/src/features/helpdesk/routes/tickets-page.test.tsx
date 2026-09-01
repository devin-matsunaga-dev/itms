import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { TicketsPage } from '@/features/helpdesk/routes/tickets-page'
import { ApiError } from '@/lib/api/client'
import { Roles } from '@/lib/roles'
import type {
  AuthenticatedUser,
  Department,
  PagedTickets,
  TicketCategory,
  TicketListItem,
  TicketPriority,
  UserSummary,
} from '@/lib/api/types'
import type { TicketQuery } from '@/features/helpdesk/lib/ticket-query'
import { renderWithProviders } from '@/test/render'

const fetchTickets = vi.fn<(query: TicketQuery) => Promise<PagedTickets>>()
const fetchCurrentUser = vi.fn<() => Promise<AuthenticatedUser | null>>()
const fetchAssignableUsers = vi.fn<() => Promise<UserSummary[]>>()

vi.mock('@/features/helpdesk/api/tickets-api', () => ({
  fetchTickets: (query: TicketQuery) => fetchTickets(query),
  fetchTicketCategories: (): Promise<TicketCategory[]> => Promise.resolve(categories),
  fetchTicketPriorities: (): Promise<TicketPriority[]> => Promise.resolve(priorities),
  fetchDepartments: (): Promise<Department[]> => Promise.resolve(departments),
  fetchAssignableUsers: () => fetchAssignableUsers(),
}))

vi.mock('@/features/auth/api/auth-api', () => ({
  fetchCurrentUser: () => fetchCurrentUser(),
  login: vi.fn(),
  logout: vi.fn(),
}))

const me = '11111111-1111-1111-1111-111111111111'

const technician: AuthenticatedUser = {
  id: me,
  userName: 'tech',
  email: 'tech@itms.local',
  displayName: 'Mark Reyes',
  roles: [Roles.technician],
  departmentId: null,
  locationId: null,
}

const endUser: AuthenticatedUser = { ...technician, userName: 'user', roles: [Roles.user] }

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

function ticket(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 'ticket-1',
    number: 'TKT-0001',
    subject: 'Laptop will not connect to Wi-Fi',
    status: 'New',
    categoryId: 'cat-network',
    categoryName: 'Network',
    priorityId: 'pri-high',
    priorityName: 'High',
    priorityCode: 'high',
    priorityRank: 2,
    requesterId: 'requester-1',
    requesterName: 'Jane Doe',
    departmentId: 'dep-it',
    departmentName: 'Information Technology',
    assigneeId: null,
    assigneeName: null,
    createdAt: '2026-08-31T09:00:00Z',
    updatedAt: '2026-08-31T09:00:00Z',
    dueAt: '2026-09-01T09:00:00Z',
    sla: {
      responseTargetMinutes: 30,
      responseDueAt: '2026-08-31T09:30:00Z',
      responseWarnAt: '2026-08-31T09:24:00Z',
      respondedAt: null,
      resolutionTargetMinutes: 480,
      resolutionDueAt: '2026-09-01T09:00:00Z',
      resolutionWarnAt: '2026-09-01T02:36:00Z',
      resolvedAt: null,
      pausedAt: null,
      responseState: 'Pending',
      resolutionState: 'Pending',
      isPaused: false,
      pausedSeconds: 0,
    },
    ...overrides,
  }
}

function page(items: TicketListItem[], total = items.length, pageNumber = 1): PagedTickets {
  return {
    items,
    total,
    page: pageNumber,
    pageSize: 25,
    totalPages: Math.max(1, Math.ceil(total / 25)),
    hasNextPage: pageNumber * 25 < total,
  }
}

/** Reports the address the screen has navigated to, so the URL can be asserted on. */
function Address(): React.JSX.Element {
  const location = useLocation()
  return (
    <>
      <output data-testid="address">{location.search}</output>
      <output data-testid="path">{location.pathname}</output>
    </>
  )
}

function renderQueue(route = '/tickets') {
  return renderWithProviders(
    <>
      <Routes>
        <Route path="/tickets" element={<TicketsPage />} />
      </Routes>
      <Address />
    </>,
    { route },
  )
}

/** The query the screen last asked the API for. */
function lastQuery(): TicketQuery {
  const call = fetchTickets.mock.calls.at(-1)
  if (call === undefined) {
    throw new Error('The queue was never requested.')
  }
  return call[0]
}

const address = () => screen.getByTestId('address').textContent ?? ''
const path = () => screen.getByTestId('path').textContent ?? ''

beforeEach(() => {
  window.localStorage.clear()
  fetchTickets.mockReset()
  fetchCurrentUser.mockReset()
  fetchAssignableUsers.mockReset()

  fetchTickets.mockResolvedValue(page([ticket()]))
  fetchCurrentUser.mockResolvedValue(technician)
  fetchAssignableUsers.mockResolvedValue([
    { ...technician, isActive: true, roles: [Roles.technician] } as UserSummary,
  ])
})

describe('TicketsPage — the queue', () => {
  it('renders a row per ticket in the reference table treatment', async () => {
    renderQueue()

    const row = (await screen.findByText('TKT-0001')).closest('tr')
    expect(row).not.toBeNull()

    const cells = within(row as HTMLElement)
    expect(cells.getByText('Laptop will not connect to Wi-Fi')).toBeInTheDocument()
    expect(cells.getByText('Jane Doe')).toBeInTheDocument()
    expect(cells.getByText('High')).toBeInTheDocument()
    expect(cells.getByText('New')).toBeInTheDocument()
    expect(cells.getByText('Unassigned')).toBeInTheDocument()
  })

  it('shows a skeleton in the table’s own shape while the page loads', () => {
    fetchTickets.mockReturnValue(new Promise(() => undefined))
    renderQueue()

    expect(screen.getByLabelText('Loading tickets')).toBeInTheDocument()
  })

  it('states what failed and offers a retry', async () => {
    fetchTickets.mockRejectedValue(new ApiError(500, null, 'boom'))
    renderQueue()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The ticket queue could not be loaded.',
    )
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument()
  })

  it('offers ticket creation from the screen the ticket belongs to', async () => {
    fetchTickets.mockResolvedValue(page([]))
    renderQueue()

    // The header action, and the empty state offering the same thing a second time.
    expect(await screen.findByText('No tickets yet')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /new ticket/i })).toHaveAttribute(
      'href',
      '/tickets/new',
    )
    expect(screen.getByRole('button', { name: /create the first ticket/i })).toBeInTheDocument()
  })

  it('takes the empty state’s own action to the create form', async () => {
    const person = userEvent.setup()
    fetchTickets.mockResolvedValue(page([]))
    renderQueue()

    await person.click(await screen.findByRole('button', { name: /create the first ticket/i }))

    expect(path()).toBe('/tickets/new')
  })

  it('distinguishes an empty queue from a queue nothing matches', async () => {
    fetchTickets.mockResolvedValue(page([]))
    renderQueue('/tickets?status=Closed&sort=Priority&direction=Ascending&pageSize=25')

    expect(await screen.findByText('No tickets match these filters')).toBeInTheDocument()
    // The filter bar's control, and the empty state offering the same way out again —
    // in the same words, because it is the same action.
    expect(screen.getAllByRole('button', { name: /clear all/i })).toHaveLength(2)
  })

  it('opens the detail screen for the ticket whose number was clicked', async () => {
    const person = userEvent.setup()
    renderQueue()

    await person.click(await screen.findByRole('button', { name: 'TKT-0001' }))

    // The row navigates by id, not by number: the number is what a person reads and the
    // id is what the API is keyed on.
    expect(path()).toBe('/tickets/ticket-1')
  })
})

describe('TicketsPage — the URL is the state', () => {
  it('writes the queue’s own ordering into a bare address', async () => {
    renderQueue()

    await waitFor(() => {
      expect(address()).toContain('sort=Priority')
    })
    expect(address()).toContain('direction=Ascending')
  })

  it('asks the API for exactly what the address says', async () => {
    renderQueue(
      '/tickets?status=New&status=Waiting&priorityId=pri-high&slaState=Breached' +
        '&sort=DueAt&direction=Descending&page=2&pageSize=50',
    )

    await waitFor(() => {
      expect(fetchTickets).toHaveBeenCalled()
    })

    const query = lastQuery()
    expect(query.status).toEqual(['New', 'Waiting'])
    expect(query.priorityId).toBe('pri-high')
    expect(query.slaState).toBe('Breached')
    expect(query.sort).toBe('DueAt')
    expect(query.direction).toBe('Descending')
    expect(query.page).toBe(2)
    expect(query.pageSize).toBe(50)
  })

  it('puts a new sort in the address, so the view stays linkable', async () => {
    const person = userEvent.setup()
    renderQueue()
    await screen.findByText('TKT-0001')

    await person.click(screen.getByRole('button', { name: /^ticket$/i }))

    await waitFor(() => {
      expect(address()).toContain('sort=Number')
    })
  })

  it('reverses the column it is already sorted on', async () => {
    const person = userEvent.setup()
    renderQueue()
    await screen.findByText('TKT-0001')

    await person.click(screen.getByRole('button', { name: /^priority$/i }))

    await waitFor(() => {
      expect(address()).toContain('direction=Descending')
    })
    expect(address()).toContain('sort=Priority')
  })

  it('reports the ordering to a screen reader as well as to the eye', async () => {
    renderQueue()
    await screen.findByText('TKT-0001')

    const header = screen.getByRole('columnheader', { name: /priority/i })
    expect(header).toHaveAttribute('aria-sort', 'ascending')
  })

  it('writes a filter chosen from the bar into the address', async () => {
    const person = userEvent.setup()
    renderQueue()
    await screen.findByText('TKT-0001')

    // The whole point of the bar: no draft state, no apply button — picking a value is
    // a navigation, so the address and the table cannot come apart.
    await person.click(screen.getByLabelText('Priority'))
    await person.click(await screen.findByRole('option', { name: 'High' }))

    await waitFor(() => {
      expect(address()).toContain('priorityId=pri-high')
    })
    expect(lastQuery().priorityId).toBe('pri-high')
  })

  it('carries a repeated status filter, because "open" is four statuses', async () => {
    const person = userEvent.setup()
    renderQueue()
    await screen.findByText('TKT-0001')

    await person.click(screen.getByLabelText('Status'))
    await person.click(await screen.findByRole('option', { name: 'New' }))
    await person.click(await screen.findByRole('option', { name: 'In progress' }))

    await waitFor(() => {
      expect(lastQuery().status).toEqual(['New', 'InProgress'])
    })
    expect(address()).toContain('status=New&status=InProgress')
  })

  it('pages forward without losing the filters', async () => {
    const person = userEvent.setup()
    fetchTickets.mockResolvedValue(page([ticket()], 60))
    renderQueue('/tickets?status=New&sort=Priority&direction=Ascending&pageSize=25')
    await screen.findByText('TKT-0001')

    await person.click(screen.getByRole('button', { name: /next page/i }))

    await waitFor(() => {
      expect(address()).toContain('page=2')
    })
    expect(address()).toContain('status=New')
  })

  it('returns to the first page when a filter changes', async () => {
    const person = userEvent.setup()
    fetchTickets.mockResolvedValue(page([ticket()], 60, 3))
    renderQueue('/tickets?status=New&page=3&sort=Priority&direction=Ascending&pageSize=25')
    await screen.findByText('TKT-0001')

    await person.click(screen.getAllByRole('button', { name: /clear all/i })[0] as HTMLElement)

    await waitFor(() => {
      expect(address()).not.toContain('page=3')
    })
    expect(address()).not.toContain('status=New')
  })
})

describe('TicketsPage — the built-in views', () => {
  it('offers the three WP-1.9 names', async () => {
    renderQueue()
    const views = await screen.findByRole('group', { name: /saved views/i })

    expect(within(views).getByRole('button', { name: 'My tickets' })).toBeInTheDocument()
    expect(within(views).getByRole('button', { name: 'Unassigned' })).toBeInTheDocument()
    expect(within(views).getByRole('button', { name: 'Overdue' })).toBeInTheDocument()
  })

  it('writes a technician’s "My tickets" as an assignee filter', async () => {
    const person = userEvent.setup()
    renderQueue()
    await screen.findByText('TKT-0001')

    await person.click(screen.getByRole('button', { name: 'My tickets' }))

    await waitFor(() => {
      expect(address()).toContain(`assigneeId=${me}`)
    })
    expect(address()).not.toContain('requesterId')
  })

  it('writes an end user’s "My tickets" as a requester filter instead', async () => {
    const person = userEvent.setup()
    fetchCurrentUser.mockResolvedValue(endUser)
    renderQueue()
    await screen.findByText('TKT-0001')

    await person.click(screen.getByRole('button', { name: 'My tickets' }))

    await waitFor(() => {
      expect(address()).toContain(`requesterId=${me}`)
    })
    expect(address()).not.toContain('assigneeId')
  })

  it('writes "Unassigned" as the unassigned question, not an empty assignee', async () => {
    const person = userEvent.setup()
    renderQueue()
    await screen.findByText('TKT-0001')

    await person.click(screen.getByRole('button', { name: 'Unassigned' }))

    await waitFor(() => {
      expect(address()).toContain('unassigned=true')
    })
  })

  it('writes "Overdue" as the breached resolution SLA', async () => {
    const person = userEvent.setup()
    renderQueue()
    await screen.findByText('TKT-0001')

    await person.click(screen.getByRole('button', { name: 'Overdue' }))

    await waitFor(() => {
      expect(address()).toContain('slaState=Breached')
    })
  })

  it('reads a view as selected from the address alone', async () => {
    renderQueue('/tickets?slaState=Breached&sort=Priority&direction=Ascending&pageSize=25')

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Overdue' })).toHaveAttribute(
        'aria-pressed',
        'true',
      )
    })
    expect(screen.getByRole('button', { name: 'Unassigned' })).toHaveAttribute(
      'aria-pressed',
      'false',
    )
  })
})

describe('TicketsPage — what each role is offered', () => {
  it('gives a technician the assignee filter', async () => {
    renderQueue()

    expect(await screen.findByLabelText('Assignee')).toBeInTheDocument()
    await waitFor(() => {
      expect(fetchAssignableUsers).toHaveBeenCalled()
    })
  })

  it('does not offer an end user a picker they cannot read', async () => {
    // The endpoint behind it is Technician-guarded; hiding it is not the enforcement,
    // it is just not asking a question this person can act on.
    fetchCurrentUser.mockResolvedValue(endUser)
    renderQueue()

    await screen.findByText('TKT-0001')
    expect(screen.queryByLabelText('Assignee')).not.toBeInTheDocument()
    expect(fetchAssignableUsers).not.toHaveBeenCalled()
  })
})

describe('TicketsPage — the table treatment', () => {
  it('leads a row with the number over the subject and how long ago it was raised', async () => {
    renderQueue()

    const row = (await screen.findByText('TKT-0001')).closest('tr')
    const cells = within(row as HTMLElement)

    expect(cells.getByRole('button', { name: 'TKT-0001' })).toBeInTheDocument()
    expect(cells.getByText('Laptop will not connect to Wi-Fi')).toBeInTheDocument()
    expect(cells.getByText(/^Created /)).toBeInTheDocument()
  })

  it('states the priority in words as well as in a hue', async () => {
    // DESIGN.md §6: a queue that says "critical" only in red says nothing to somebody
    // who cannot see red. The arrow is aria-hidden; the name is the accessible content.
    renderQueue()

    const row = (await screen.findByText('TKT-0001')).closest('tr')
    expect(within(row as HTMLElement).getByText('High')).toBeInTheDocument()
  })

  it('renders the SLA as a meter that reports where it stands', async () => {
    renderQueue()

    const meter = await screen.findByRole('progressbar', { name: /resolution sla/i })
    expect(meter).toHaveAttribute('aria-valuenow')
    expect(meter).toHaveAccessibleName(/on track/i)
  })

  it('says how many tickets the query matched', async () => {
    fetchTickets.mockResolvedValue(page([ticket()], 32))
    renderQueue()

    expect(await screen.findByText('32 tickets')).toBeInTheDocument()
  })

  it('counts one ticket in the singular', async () => {
    fetchTickets.mockResolvedValue(page([ticket()], 1))
    renderQueue()

    expect(await screen.findByText('1 ticket')).toBeInTheDocument()
  })
})

describe('TicketsPage — the reader’s own layout', () => {
  it('hides a column the reader turns off, and remembers it', async () => {
    const person = userEvent.setup()
    renderQueue()

    expect(await screen.findByRole('columnheader', { name: /department/i })).toBeInTheDocument()

    await person.click(screen.getByRole('button', { name: /columns/i }))
    await person.click(await screen.findByRole('checkbox', { name: 'Department' }))

    await waitFor(() => {
      expect(screen.queryByRole('columnheader', { name: /department/i })).not.toBeInTheDocument()
    })

    // Column choices are a per-browser preference, not URL state — they describe how one
    // person reads rather than which rows are shown, so they must not travel in a link.
    expect(address()).not.toContain('department')
    expect(window.localStorage.getItem('itms.tickets.table')).toContain('department')
  })

  it('starts from what the browser already remembers', async () => {
    window.localStorage.setItem(
      'itms.tickets.table',
      JSON.stringify({ hidden: ['assignee'], density: 'comfortable' }),
    )
    renderQueue()

    expect(await screen.findByRole('columnheader', { name: /requester/i })).toBeInTheDocument()
    expect(screen.queryByRole('columnheader', { name: /assignee/i })).not.toBeInTheDocument()
  })

  it('packs the rows down and drops the created caption when asked', async () => {
    const person = userEvent.setup()
    renderQueue()

    const density = await screen.findByRole('switch', { name: /compact rows/i })
    expect(density).toHaveAttribute('aria-checked', 'false')

    await person.click(density)

    expect(density).toHaveAttribute('aria-checked', 'true')
    await waitFor(() => {
      expect(screen.queryByText(/^Created /)).not.toBeInTheDocument()
    })
  })
})

describe('TicketsPage — the toolbar’s ordering', () => {
  it('reports the ordering the address is carrying', async () => {
    renderQueue()

    expect(await screen.findByLabelText('Sort')).toHaveTextContent('Priority')
  })

  it('writes both the column and the direction when an ordering is chosen', async () => {
    const person = userEvent.setup()
    renderQueue()

    await person.click(await screen.findByLabelText('Sort'))
    await person.click(await screen.findByRole('option', { name: 'Due soonest' }))

    await waitFor(() => {
      expect(lastQuery().sort).toBe('DueAt')
    })
    expect(lastQuery().direction).toBe('Ascending')
    expect(address()).toContain('sort=DueAt')
  })

  it('says the ordering is custom when a column header produced one it cannot name', async () => {
    // Priority descending is reachable from the header and is not on the select's list.
    renderQueue('/tickets?sort=Priority&direction=Descending&pageSize=25')

    expect(await screen.findByLabelText('Sort')).toHaveTextContent('Custom')
  })
})

describe('TicketsPage — the filter popover', () => {
  it('keeps the three common filters on the bar', async () => {
    renderQueue()

    expect(await screen.findByLabelText('Status')).toBeInTheDocument()
    expect(screen.getByLabelText('Priority')).toBeInTheDocument()
    expect(await screen.findByLabelText('Assignee')).toBeInTheDocument()
  })

  it('counts the filters it is hiding, so nothing is out of sight and unaccounted for', async () => {
    renderQueue(
      '/tickets?categoryId=cat-network&departmentId=dep-it&slaState=Breached&sort=Priority&direction=Ascending&pageSize=25',
    )

    const filters = await screen.findByRole('button', { name: /filters/i })
    expect(filters).toHaveTextContent('3')
  })

  it('carries no badge when only the inline filters are set', async () => {
    renderQueue('/tickets?priorityId=pri-high&sort=Priority&direction=Ascending&pageSize=25')

    const filters = await screen.findByRole('button', { name: /filters/i })
    expect(filters).not.toHaveTextContent('1')
  })

  it('writes a filter chosen inside the popover straight to the URL', async () => {
    const person = userEvent.setup()
    renderQueue()

    await person.click(await screen.findByRole('button', { name: /filters/i }))
    await person.click(await screen.findByLabelText('Category'))
    await person.click(await screen.findByRole('option', { name: 'Network' }))

    await waitFor(() => {
      expect(address()).toContain('categoryId=cat-network')
    })
  })
})
