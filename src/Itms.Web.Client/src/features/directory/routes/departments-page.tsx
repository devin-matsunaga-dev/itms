import { useCallback, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { Building2, Plus } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { Pagination } from '@/components/common/pagination'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'
import type { Department } from '@/lib/api/types'
import { DepartmentDialog } from '../components/department-dialog'
import { DepartmentRetireDialog } from '../components/department-retire-dialog'
import { useDepartmentPage } from '../hooks/use-directory'
import type { DepartmentListQuery } from '../api/directory-api'

/** The page sizes this screen offers. The API clamps at 200 regardless. */
const pageSizeOptions: readonly number[] = [25, 50, 100]

const defaultPageSize = 25

/**
 * Department management (WP-2.7).
 *
 * It lives under Administration because every write here is Admin-only server-side, and
 * because SPEC.md §13 puts configuration there. The reads are open to any signed-in
 * account — a picker needs them — so the policy split is the server's, not this screen's.
 *
 * ## Retired, never deleted
 *
 * WP-0.6 made departments retire-only and WP-2.4 left that standing: a department is named
 * by tickets, assets, and people that outlive it, and §3 rule 6 leaves no foreign key the
 * database could protect. So the destructive action here is *retire*, it is reversible, and
 * the usage breakdown says what still points at the department before anybody takes it.
 *
 * The search term and the retired toggle are in the URL, as CONVENTIONS.md asks of a list
 * screen — this one is short enough that the ordering is the server's own and is not
 * offered as a control.
 */
export function DepartmentsPage(): React.JSX.Element {
  const [searchParams, setSearchParams] = useSearchParams()
  const [editing, setEditing] = useState<Department | null>(null)
  const [creating, setCreating] = useState(false)
  const [retiring, setRetiring] = useState<Department | null>(null)

  const query = useMemo<DepartmentListQuery>(
    () => ({
      search: searchParams.get('search') ?? '',
      includeInactive: searchParams.get('includeInactive') === 'true',
      page: positiveInteger(searchParams.get('page')) ?? 1,
      pageSize: positiveInteger(searchParams.get('pageSize')) ?? defaultPageSize,
    }),
    [searchParams],
  )

  const departments = useDepartmentPage(query)

  const navigate = useCallback(
    (next: DepartmentListQuery) => {
      const params = new URLSearchParams()
      if (next.search.trim().length > 0) {
        params.append('search', next.search.trim())
      }
      if (next.includeInactive) {
        params.append('includeInactive', 'true')
      }
      if (next.page > 1) {
        params.append('page', String(next.page))
      }
      if (next.pageSize !== defaultPageSize) {
        params.append('pageSize', String(next.pageSize))
      }
      setSearchParams(params)
    },
    [setSearchParams],
  )

  return (
    <>
      <PageHeader
        title="Departments"
        subtitle="The organisational units tickets, assets, and people are recorded against."
        back={backToAdministration}
        actions={
          <Button
            onClick={() => {
              setCreating(true)
            }}
          >
            <Plus className="size-4" aria-hidden="true" />
            New department
          </Button>
        }
      />

      <div className="flex flex-col gap-5">
        <div className="flex flex-wrap items-center gap-4 rounded-card border border-border bg-surface p-4 shadow-card">
          <div className="min-w-64 flex-1">
            <Label htmlFor="department-search" className="sr-only">
              Search departments
            </Label>
            <Input
              id="department-search"
              type="search"
              placeholder="Search by name or code…"
              defaultValue={query.search}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  navigate({ ...query, search: event.currentTarget.value, page: 1 })
                }
              }}
              onBlur={(event) => {
                if (event.currentTarget.value !== query.search) {
                  navigate({ ...query, search: event.currentTarget.value, page: 1 })
                }
              }}
            />
          </div>

          <div className="flex items-center gap-2">
            <Checkbox
              id="department-include-retired"
              checked={query.includeInactive}
              onCheckedChange={(checked: boolean) => {
                navigate({ ...query, includeInactive: checked, page: 1 })
              }}
            />
            <Label htmlFor="department-include-retired" className="text-copy font-normal text-body">
              Show retired
            </Label>
          </div>
        </div>

        {departments.isPending ? (
          <TableSkeleton />
        ) : departments.isError ? (
          <ErrorState
            title="The departments could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void departments.refetch()
            }}
          />
        ) : departments.data.items.length === 0 ? (
          <EmptyState
            icon={Building2}
            title={query.search.length > 0 ? 'No departments match that' : 'No departments yet'}
            description={
              query.search.length > 0
                ? 'Clear the search to see the whole list.'
                : 'Departments are what tickets, assets, and people are recorded against.'
            }
            action={
              query.search.length > 0
                ? undefined
                : {
                    label: 'Create the first department',
                    onClick: () => {
                      setCreating(true)
                    },
                  }
            }
          />
        ) : (
          <>
            <div className="overflow-hidden rounded-card border border-border bg-surface shadow-card">
              <Table className="text-cell">
                <TableHeader>
                  <TableRow className="border-border hover:bg-transparent">
                    <Head>Department</Head>
                    <Head>Code</Head>
                    <Head>Status</Head>
                    <Head className="text-right">Actions</Head>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {departments.data.items.map((entry) => (
                    <TableRow key={entry.id} className="border-border hover:bg-canvas">
                      <TableCell className="max-w-[28rem] px-4 py-2.5">
                        <p className="font-semibold text-heading">{entry.name}</p>
                        {entry.description === null ? null : (
                          <p className="truncate text-caption text-muted-foreground">
                            {entry.description}
                          </p>
                        )}
                      </TableCell>
                      <TableCell className="px-4 py-2.5 text-body">{entry.code ?? '—'}</TableCell>
                      <TableCell className="px-4 py-2.5">
                        <StatusPill active={entry.isActive} />
                      </TableCell>
                      <TableCell className="px-4 py-2.5 text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setEditing(entry)
                            }}
                          >
                            Edit
                          </Button>
                          <Button
                            variant={entry.isActive ? 'ghost' : 'outline'}
                            size="sm"
                            onClick={() => {
                              setRetiring(entry)
                            }}
                          >
                            {entry.isActive ? 'Retire' : 'Bring back'}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            <Pagination
              page={departments.data.page}
              pageSize={departments.data.pageSize}
              total={departments.data.total}
              pageSizeOptions={pageSizeOptions}
              noun="departments"
              idPrefix="department"
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

      <DepartmentDialog
        department={editing}
        open={creating || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false)
            setEditing(null)
          }
        }}
      />

      <DepartmentRetireDialog
        department={retiring}
        onOpenChange={(open) => {
          if (!open) {
            setRetiring(null)
          }
        }}
      />
    </>
  )
}

/** One wording for leaving a directory screen, shared by both of them. */
const backToAdministration = { to: '/administration', label: 'Back to administration' }

function Head({
  children,
  className,
}: {
  children: React.ReactNode
  className?: string
}): React.JSX.Element {
  return (
    <TableHead
      className={cn(
        'h-10 bg-surface px-4 text-label font-semibold tracking-[0.06em] text-muted-foreground uppercase',
        className,
      )}
    >
      {children}
    </TableHead>
  )
}

/** Active or retired, as DESIGN.md §4's status pill — a soft fill, a dot, the label in `heading`. */
function StatusPill({ active }: { active: boolean }): React.JSX.Element {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-label font-semibold text-heading',
        active
          ? 'bg-success/12 dark:bg-success/15'
          : 'bg-neutral-chart/25 dark:bg-neutral-chart/30',
      )}
    >
      <span
        aria-hidden="true"
        className={cn('size-1.5 rounded-full', active ? 'bg-success' : 'bg-neutral-chart')}
      />
      {active ? 'Active' : 'Retired'}
    </span>
  )
}

function TableSkeleton(): React.JSX.Element {
  return (
    <div
      className="overflow-hidden rounded-card border border-border bg-surface shadow-card"
      aria-busy="true"
      aria-label="Loading departments"
    >
      <div className="h-10 border-b border-border" />
      {Array.from({ length: 6 }, (_, index) => (
        <div
          key={index}
          className="flex items-center gap-4 border-b border-border px-4 py-3 last:border-b-0"
        >
          <Skeleton className="h-3 w-64" />
          <Skeleton className="h-3 w-16" />
          <Skeleton className="h-5 w-20 rounded-md" />
        </div>
      ))}
    </div>
  )
}

function positiveInteger(value: string | null): number | null {
  if (value === null || value.trim().length === 0) {
    return null
  }

  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null
}
