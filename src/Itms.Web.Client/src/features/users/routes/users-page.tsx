import { useCallback, useMemo } from 'react'
import { Navigate, useNavigate, useSearchParams } from 'react-router'
import { Users } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { Pagination } from '@/components/common/pagination'
import type { SortDirection, UserSort, UserSummary } from '@/lib/api/types'
import { useDepartments, useLocations } from '@/features/directory/hooks/use-directory'
import { UserFilters } from '../components/user-filters'
import { UserSearch } from '../components/user-search'
import { UserTable } from '../components/user-table'
import { UserTableSkeleton } from '../components/user-table-skeleton'
import { UserToolbar } from '../components/user-toolbar'
import { useUsers } from '../hooks/use-users'
import {
  clearedFilters,
  defaultUserQuery,
  pageSizeOptions,
  parseUserQuery,
  sameUserQuery,
  serializeUserQuery,
  withFilters,
  type UserQuery,
} from '../lib/user-query'

/**
 * The staff directory (WP-2.7).
 *
 * SPEC.md §4's acceptance shape is "a technician searches a user and immediately sees their
 * equipment and support history" — this is the first half of it, and every row leads to the
 * second.
 *
 * The URL is the state. Every filter, the ordering, the page, and the page size are read
 * out of the address and written back into it, so a directory survives a reload, works in a
 * new tab, and can be sent to somebody else — which is what CONVENTIONS.md and DESIGN.md §6
 * both ask of a list screen. **That is what the server change in this package bought:**
 * `GET /api/v1/users` was a picker search returning a capped array with no total, and a
 * page nobody can count is a page nobody can link to.
 *
 * ## What is not here
 *
 * **No create action, and no edit.** DESIGN.md §4 puts a screen's primary action in its page
 * header, and this screen has none: user administration — creating an account, changing
 * somebody's role, deactivating them — is `WP-5.8`, and no write endpoint exists. WP-1.11
 * settled that a control which silently does nothing is worse than one that is absent, so
 * the header carries no button and the empty state offers no first-run action it could not
 * complete.
 *
 * Every route behind this screen is Technician-or-Admin (SPEC.md §14), which the nav and the
 * router already enforce through one rule in `navigation.ts`. Nothing here is the
 * enforcement; the server's policy is.
 */
export function UsersPage(): React.JSX.Element {
  const [searchParams, setSearchParams] = useSearchParams()
  const routerNavigate = useNavigate()
  const query = useMemo(() => parseUserQuery(searchParams), [searchParams])

  const users = useUsers(query)
  const departments = useDepartments()
  const locations = useLocations()

  const navigate = useCallback(
    (next: UserQuery) => {
      setSearchParams(serializeUserQuery(next))
    },
    [setSearchParams],
  )

  const onFilterChange = useCallback(
    (changes: Partial<UserQuery>) => {
      navigate(withFilters(query, changes))
    },
    [navigate, query],
  )

  const onSort = useCallback(
    (column: UserSort) => {
      // Clicking the column already sorted on reverses it; a new column starts in the
      // direction that column reads best in — a directory runs forwards by name and by
      // address, and "when was this account added" means most recent.
      const direction: SortDirection =
        query.sort === column
          ? query.direction === 'Ascending'
            ? 'Descending'
            : 'Ascending'
          : column === 'CreatedAt'
            ? 'Descending'
            : 'Ascending'

      navigate({ ...query, sort: column, direction, page: 1 })
    },
    [navigate, query],
  )

  const onSortChange = useCallback(
    (sort: UserSort, direction: SortDirection) => {
      navigate({ ...query, sort, direction, page: 1 })
    },
    [navigate, query],
  )

  const openUser = useCallback(
    (user: UserSummary) => {
      void routerNavigate(`/users/${user.id}`)
    },
    [routerNavigate],
  )

  // The ordering is written into the address rather than left implicit, so what somebody is
  // looking at is what they can send on. A bare /users is normalised once, on arrival, and
  // replaces rather than stacks history.
  const canonical = useMemo(() => serializeUserQuery(query).toString(), [query])
  if (canonical !== searchParams.toString()) {
    return <Navigate to={{ search: `?${canonical}` }} replace />
  }

  const unfiltered = sameUserQuery(query, defaultUserQuery)

  return (
    <>
      <PageHeader
        title="Users"
        subtitle="The people the helpdesk and the asset register refer to."
      />

      <div className="flex flex-col gap-5">
        <UserFilters
          query={query}
          departments={departments.data ?? []}
          onChange={onFilterChange}
          onClear={() => {
            navigate(clearedFilters(query))
          }}
          search={
            <UserSearch
              value={query.search}
              onChange={(search) => {
                onFilterChange({ search })
              }}
            />
          }
        />

        {users.isPending ? (
          <UserTableSkeleton />
        ) : users.isError ? (
          <ErrorState
            title="The user directory could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void users.refetch()
            }}
          />
        ) : users.data.items.length === 0 ? (
          <EmptyState
            icon={Users}
            title={unfiltered ? 'No people yet' : 'Nobody matches these filters'}
            description={
              unfiltered
                ? 'Accounts appear here once they exist. Creating them is an administrative task.'
                : 'Widen or clear the filters to see more of the directory.'
            }
            action={
              unfiltered
                ? undefined
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
            <UserTable
              users={users.data.items}
              query={query}
              departments={departments.data ?? []}
              locations={locations.data ?? []}
              onSort={onSort}
              onOpen={openUser}
              toolbar={
                <UserToolbar
                  total={users.data.total}
                  sort={query.sort}
                  direction={query.direction}
                  refreshing={users.isFetching}
                  onSortChange={onSortChange}
                  onRefresh={() => {
                    void users.refetch()
                  }}
                />
              }
            />
            <Pagination
              page={query.page}
              pageSize={query.pageSize}
              total={users.data.total}
              pageSizeOptions={pageSizeOptions}
              noun="people"
              idPrefix="user"
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
