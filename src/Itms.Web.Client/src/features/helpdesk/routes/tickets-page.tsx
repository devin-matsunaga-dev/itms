import { useCallback, useMemo } from 'react'
import { Navigate, useSearchParams } from 'react-router'
import { Plus, Ticket } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { Button } from '@/components/ui/button'
import { useNow } from '@/lib/use-now'
import { hasAnyRole, Roles } from '@/lib/roles'
import type { TicketListItem, TicketSort } from '@/lib/api/types'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { TicketFilters } from '../components/ticket-filters'
import { TicketPagination } from '../components/ticket-pagination'
import { TicketTable } from '../components/ticket-table'
import { TicketTableSkeleton } from '../components/ticket-table-skeleton'
import { TicketViewChips } from '../components/ticket-view-chips'
import {
  useAssignableUsers,
  useDepartments,
  useTicketCategories,
  useTicketPriorities,
  useTickets,
} from '../hooks/use-tickets'
import {
  clearedFilters,
  defaultTicketQuery,
  parseTicketQuery,
  sameTicketQuery,
  serializeTicketQuery,
  withFilters,
  type TicketQuery,
} from '../lib/ticket-query'
import { applyView, isViewActive, type TicketViewId } from '../lib/ticket-views'

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
  const query = useMemo(() => parseTicketQuery(searchParams), [searchParams])

  const now = useNow()
  const { data: currentUser } = useCurrentUser()
  const roles = currentUser?.roles ?? []

  // A Technician or an Admin works the whole queue; an end user sees only their own
  // tickets, which is enforced by the server's row filter and merely reflected here.
  const worksTheQueue = hasAnyRole(roles, [Roles.admin, Roles.technician])

  const tickets = useTickets(query)
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

  const onSelectView = useCallback(
    (view: TicketViewId) => {
      if (currentUser === null || currentUser === undefined) {
        return
      }
      navigate(applyView(query, view, { currentUserId: currentUser.id, worksTheQueue }))
    },
    [currentUser, navigate, query, worksTheQueue],
  )

  const activeViews = useMemo<TicketViewId[]>(() => {
    if (currentUser === null || currentUser === undefined) {
      return []
    }
    const options = { currentUserId: currentUser.id, worksTheQueue }
    return (['mine', 'unassigned', 'overdue'] as const).filter((view) =>
      isViewActive(query, view, options),
    )
  }, [currentUser, query, worksTheQueue])

  // The create form is WP-1.10. Until it exists the button says so rather than
  // navigating to a screen that is not there — the same call WP-0.8 made for the
  // search pill.
  const newTicket = useCallback(() => {
    toast.info('The ticket create form arrives in WP-1.10.')
  }, [])

  const openTicket = useCallback((ticket: TicketListItem) => {
    toast.info(`Ticket detail for ${ticket.number} arrives in WP-1.10.`)
  }, [])

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
          <Button onClick={newTicket}>
            <Plus className="size-4" aria-hidden="true" />
            New Ticket
          </Button>
        }
      />

      <div className="flex flex-col gap-5">
        <TicketViewChips activeViews={activeViews} onSelect={onSelectView} />

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
                    label: 'Clear filters',
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
              onSort={onSort}
              onOpen={openTicket}
            />
            <TicketPagination
              page={query.page}
              pageSize={query.pageSize}
              total={tickets.data.total}
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
