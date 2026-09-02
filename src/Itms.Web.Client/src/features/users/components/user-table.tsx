import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Department, Location, UserSort, UserSummary } from '@/lib/api/types'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { initials } from '@/lib/roles'
import { departmentName, locationPath, roleLabel } from '../lib/user-display'
import type { UserQuery } from '../lib/user-query'

interface UserTableProps {
  users: readonly UserSummary[]
  query: UserQuery
  /** For resolving the department a row names. */
  departments: readonly Department[]
  /** For resolving the room a row names. One page of two hundred — see `locationPath`. */
  locations: readonly Location[]
  onSort: (column: UserSort) => void
  onOpen: (user: UserSummary) => void
  /** The toolbar, rendered as this card's header — it describes the list it sits on. */
  toolbar?: React.ReactNode
}

/**
 * The staff directory (DESIGN.md §4, *Data table*).
 *
 * The identifying column is two lines like the queue's and the register's, but built from
 * the person cell rather than from an identifier: a name over an address, with the initials
 * tile beside them. There is no third caption line — a person has no "raised 3 days ago",
 * and when the account was created is an *ordering* the toolbar offers rather than a column
 * anybody scans.
 *
 * **There is no column or density control here, unlike the two lists before it.** DESIGN.md
 * §4 gives those to the reader for a table wide enough that column choice is a way of
 * reading it; this one has five columns and every one of them answers a question somebody
 * came to the directory with. A "Columns" popover over five columns is a control that costs
 * more attention than it saves, and a third `localStorage` key nobody asked for.
 */
export function UserTable({
  users,
  query,
  departments,
  locations,
  onSort,
  onOpen,
  toolbar,
}: UserTableProps): React.JSX.Element {
  return (
    <div className="overflow-hidden rounded-card border border-border bg-surface shadow-card">
      {toolbar}
      <Table className="text-cell">
        <TableHeader>
          <TableRow className="border-border hover:bg-transparent">
            <SortableHead column="DisplayName" query={query} onSort={onSort}>
              Person
            </SortableHead>
            <PlainHead>Role</PlainHead>
            <PlainHead>Department</PlainHead>
            <PlainHead>Location</PlainHead>
            <PlainHead>Account</PlainHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          {users.map((user) => {
            const department = departmentName(user.departmentId, departments)
            const location = locationPath(user.locationId, locations)

            return (
              <TableRow
                key={user.id}
                // DESIGN.md §4: a row opens the detail page. The keyboard path to the same
                // action is the person's name, which is a real control.
                onClick={() => {
                  onOpen(user)
                }}
                className="cursor-pointer border-border hover:bg-canvas"
              >
                <TableCell className="max-w-[26rem] px-4 py-2.5">
                  <div className="flex items-center gap-2.5">
                    <Avatar size="sm" aria-hidden="true" className="after:border-transparent">
                      <AvatarFallback className="bg-primary-soft text-label font-semibold text-primary">
                        {initials(user.displayName)}
                      </AvatarFallback>
                    </Avatar>
                    <div className="min-w-0">
                      <button
                        type="button"
                        onClick={(event) => {
                          event.stopPropagation()
                          onOpen(user)
                        }}
                        className="truncate rounded-sm font-semibold text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
                      >
                        {user.displayName}
                      </button>
                      <p className="truncate text-caption text-muted-foreground">{user.email}</p>
                    </div>
                  </div>
                </TableCell>

                <TableCell className="px-4 py-2.5 text-body">{roleLabel(user.roles)}</TableCell>

                <TableCell
                  className={cn('px-4 py-2.5', department.known ? 'text-body' : 'text-muted-foreground')}
                >
                  {department.text}
                </TableCell>

                <TableCell
                  className={cn(
                    'max-w-[20rem] truncate px-4 py-2.5',
                    location.known ? 'text-body' : 'text-muted-foreground',
                  )}
                  title={location.known ? location.text : undefined}
                >
                  {location.text}
                </TableCell>

                <TableCell className="px-4 py-2.5">
                  <AccountPill active={user.isActive} />
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}

/**
 * Whether the account can sign in.
 *
 * Built as DESIGN.md §4's status pill — a soft fill, a dot in the full hue, the label in
 * `heading` — rather than as a bare word, because it is a status like any other and §6
 * forbids the colour carrying it alone. Deactivated takes `muted` rather than `danger`: a
 * deactivated account is a settled fact about somebody who has left, not a fault.
 */
function AccountPill({ active }: { active: boolean }): React.JSX.Element {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-label font-semibold text-heading',
        active
          ? 'bg-success/12 dark:bg-success/15'
          : 'bg-muted-foreground/12 dark:bg-muted-foreground/15',
      )}
    >
      <span
        aria-hidden="true"
        className={cn('size-1.5 rounded-full', active ? 'bg-success' : 'bg-muted-foreground')}
      />
      {active ? 'Active' : 'Deactivated'}
    </span>
  )
}

const headClass =
  'h-10 bg-surface px-4 text-label font-semibold tracking-[0.06em] text-muted-foreground uppercase'

function PlainHead({ children }: { children: React.ReactNode }): React.JSX.Element {
  return <TableHead className={headClass}>{children}</TableHead>
}

interface SortableHeadProps {
  column: UserSort
  query: UserQuery
  onSort: (column: UserSort) => void
  children: React.ReactNode
}

/**
 * A column header that orders the directory.
 *
 * The sort is server-side and lives in the URL, so the header reports what the address says
 * rather than any state of its own — and `aria-sort` says the same thing to a screen reader
 * that the arrow says to everyone else.
 */
function SortableHead({ column, query, onSort, children }: SortableHeadProps): React.JSX.Element {
  const active = query.sort === column
  const ascending = query.direction === 'Ascending'
  const Icon = active ? (ascending ? ArrowUp : ArrowDown) : ChevronsUpDown

  return (
    <TableHead
      className={headClass}
      aria-sort={active ? (ascending ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        onClick={() => {
          onSort(column)
        }}
        className={cn(
          'inline-flex items-center gap-1.5 rounded-sm tracking-[0.06em] uppercase transition-colors hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none',
          active && 'text-heading',
        )}
      >
        {children}
        <Icon className={cn('size-3', !active && 'opacity-50')} aria-hidden="true" />
      </button>
    </TableHead>
  )
}
