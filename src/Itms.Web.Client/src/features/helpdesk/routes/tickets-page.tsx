import { useCallback, useMemo } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router'
import { Plus, Ticket } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { Pagination } from '@/components/common/pagination'
import { Button } from '@/components/ui/button'
import { useNow } from '@/lib/use-now'
import { hasAnyRole, Roles } from '@/lib/roles'
import type { SortDirection, TicketListItem, TicketSort } from '@/lib/api/types'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { useDepartments } from '@/features/directory/hooks/use-directory'
import { TicketFilters } from '../components/ticket-filters'
import { TicketKpiRow } from '../components/ticket-kpi-row'
import { TicketSearch } from '../components/ticket-search'
import { TicketTable } from '../components/ticket-table'
import { TicketTableSkeleton } from '../components/ticket-table-skeleton'
import { TicketToolbar } from '../components/ticket-toolbar'
import { useTablePreferences } from '../hooks/use-table-preferences'
import {
  useAssignableUsers,
  useTicketCategories,
  useTicketCounters,
  useTicketPriorities,
  useTickets,
} from '../hooks/use-tickets'
import { endOfLocalDay } from '../lib/ticket-kpis'
import {
  clearedFilters,
  defaultTicketQuery,
  pageSizeOptions,
  parseTicketQuery,
  sameTicketQuery,
  serializeTicketQuery,
  withFilters,
  type TicketQuery,
} from '../lib/ticket-query'
import { applyMyTickets, clearMyTickets, isMyTickets } from '../lib/ticket-views'

/**
 * The ticket queue (WP-1.9).
 *
 * The URL is the state. Every filter, the ordering, the page, and the page size are read
 * out of the address and written back into it, so a queue survives a reload, works in a
 * new tab, and can be sent to somebody else — which is what CONVENTIONS.md and
 * DESIGN.md §6 both ask of a list screen.
 */
export function TicketsPage(): React.JSX.Element {
  const [searchParams, setSearchParams] = useSearchParams()
  const routerNavigate = useNavigate()
  const query = useMemo(() => parseTicketQuery(searchParams), [searchParams])

  const now = useNow()
  const { data: currentUser } = useCurrentUser()
  const roles = currentUser?.roles ?? []

  // A Technician or an Admin works the whole queue; an end user sees only their own
  // tickets, which is enforced by the server's row filter and merely reflected here.
  const worksTheQueue = hasAnyRole(roles, [Roles.admin, Roles.technician])

  const tickets = useTickets(query)
  // Keyed on the calendar day rather than the instant, so the counters' cache key is
  // stable through a session instead of moving with every 30-second `useNow` tick — and
  // still turns over at midnight, which is when "due today" means something else.
  const dayKey = now.toDateString()
  const dayEnd = useMemo(() => endOfLocalDay(new Date(dayKey)), [dayKey])
  const counters = useTicketCounters(dayEnd)
  const { preferences, toggle, setDensity } = useTablePreferences()
  const categories = useTicketCategories()
  const priorities = useTicketPriorities()
  const departments = useDepartments()
  const assignees = useAssignableUsers(worksTheQueue)

  const navigate = useCallback(
    (next: TicketQuery) => {
      setSearchParams(serializeTicketQuery(next))
    },
    [setSearchParams],
  )

  const onFilterChange = useCallback(
    (changes: Partial<TicketQuery>) => {
      navigate(withFilters(query, changes))
    },
    [navigate, query],
  )

  const onSort = useCallback(
    (column: TicketSort) => {
      // Clicking the column already sorted on reverses it; a new column starts in the
      // direction that column reads best in — most urgent, soonest due, newest first.
      const direction =
        query.sort === column
          ? query.direction === 'Ascending'
            ? 'Descending'
            : 'Ascending'
          : column === 'Priority' || column === 'DueAt'
            ? 'Ascending'
            : 'Descending'

      navigate({ ...query, sort: column, direction, page: 1 })
    },
    [navigate, query],
  )

  const onSortChange = useCallback(
    (sort: TicketSort, direction: SortDirection) => {
      navigate({ ...query, sort, direction, page: 1 })
    },
    [navigate, query],
  )

  const viewer = useMemo(
    () =>
      currentUser === null || currentUser === undefined
        ? null
        : { currentUserId: currentUser.id, worksTheQueue },
    [currentUser, worksTheQueue],
  )

  const mine = viewer !== null && isMyTickets(query, viewer)

  const onToggleMine = useCallback(() => {
    if (viewer === null) {
      return
    }
    navigate(mine ? clearMyTickets(query, viewer) : applyMyTickets(query, viewer))
  }, [mine, navigate, query, viewer])

  // WP-1.9 fixed both of these in the frame and had them raise a toast naming this
  // package. They navigate now; nothing else about the table changed.
  const newTicket = useCallback(() => {
    void routerNavigate('/tickets/new')
  }, [routerNavigate])

  const openTicket = useCallback(
    (ticket: TicketListItem) => {
      void routerNavigate(`/tickets/${ticket.id}`)
    },
    [routerNavigate],
  )

  // The queue's own ordering is written into the address rather than inherited from the
  // API's default, so what somebody is looking at is what they can send on. A bare
  // /tickets is normalised once, on arrival, and replaces rather than stacks history.
  const canonical = useMemo(() => serializeTicketQuery(query).toString(), [query])
  if (canonical !== searchParams.toString()) {
    return <Navigate to={{ search: `?${canonical}` }} replace />
  }

  return (
    <>
      <PageHeader
        title="Tickets"
        subtitle="Every request raised across the organisation."
        actions={
          <Button render={<Link to="/tickets/new" />}>
            <Plus className="size-4" aria-hidden="true" />
            New Ticket
          </Button>
        }
      />

      <div className="flex flex-col gap-5">
        <TicketKpiRow counters={counters.data ?? null} dayEnd={dayEnd} />

        <TicketFilters
          query={query}
          categories={categories.data ?? []}
          priorities={priorities.data ?? []}
          departments={departments.data ?? []}
          assignees={assignees.data ?? []}
          showAssignee={worksTheQueue}
          onChange={onFilterChange}
          onClear={() => {
            navigate(clearedFilters(query))
          }}
          mine={mine}
          mineCount={counters.data?.mine}
          onToggleMine={onToggleMine}
          search={
            <TicketSearch
              value={query.search}
              onChange={(search) => {
                onFilterChange({ search })
              }}
            />
          }
        />

        {tickets.isPending ? (
          <TicketTableSkeleton />
        ) : tickets.isError ? (
          <ErrorState
            title="The ticket queue could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void tickets.refetch()
            }}
          />
        ) : tickets.data.items.length === 0 ? (
          <EmptyState
            icon={Ticket}
            title={
              sameTicketQuery(query, defaultTicketQuery)
                ? 'No tickets yet'
                : 'No tickets match these filters'
            }
            description={
              sameTicketQuery(query, defaultTicketQuery)
                ? 'Every request raised across the organisation will appear here.'
                : 'Widen or clear the filters to see more of the queue.'
            }
            action={
              sameTicketQuery(query, defaultTicketQuery)
                ? { label: 'Create the first ticket', onClick: newTicket }
                : {
                    label: 'Clear all',
                    onClick: () => {
                      navigate(clearedFilters(query))
                    },
                  }
            }
          />
        ) : (
          <>
            <TicketTable
              tickets={tickets.data.items}
              query={query}
              now={now}
              preferences={preferences}
              onSort={onSort}
              onOpen={openTicket}
              toolbar={
                <TicketToolbar
                  total={tickets.data.total}
                  sort={query.sort}
                  direction={query.direction}
                  refreshing={tickets.isFetching}
                  preferences={preferences}
                  onSortChange={onSortChange}
                  onRefresh={() => {
                    void tickets.refetch()
                  }}
                  onToggleColumn={toggle}
                  onDensityChange={setDensity}
                />
              }
            />
            <Pagination
              page={query.page}
              pageSize={query.pageSize}
              total={tickets.data.total}
              pageSizeOptions={pageSizeOptions}
              noun="tickets"
              idPrefix="ticket"
              onPageChange={(page) => {
                navigate({ ...query, page })
              }}
              onPageSizeChange={(pageSize) => {
                navigate({ ...query, pageSize, page: 1 })
              }}
            />
          </>
        )}
      </div>
    </>
  )
}
