import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { ChevronRight, Home, MapPin, Plus, Search } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import type { Location } from '@/lib/api/types'
import {
  LocationDeleteDialog,
  LocationFormDialog,
  LocationMoveDialog,
} from '../components/location-dialogs'
import {
  useLocationAncestors,
  useLocationChildren,
  useLocationRoots,
  useLocationSearch,
} from '../hooks/use-directory'

/**
 * Location management (WP-0.6, WP-2.4, WP-2.7).
 *
 * ## One level at a time, not one page of the whole tree
 *
 * The tree is walked with the same three reads the cascading picker uses — the roots, one
 * node's children, and the root-to-node chain — rather than listed flat. A flat list has to
 * be paged, and a paged tree is one whose indentation lies at every page boundary; more to
 * the point, an estate of any size is browsed by walking into the building somebody means,
 * not by scrolling past four hundred rooms to reach it. Typing anything switches to a flat
 * search across the whole tree, because somebody who knows the room's name should not have
 * to walk to it.
 *
 * **Which node is open is in the URL** (`?parent=`), so a level is linkable and the browser's
 * back button walks back up the tree, which is what CONVENTIONS.md asks of a list screen.
 *
 * ## What the four actions do
 *
 * Add a child, rename, move, delete. A move rewrites every descendant's path in one
 * server-side transaction (WP-2.4's own done-criterion); a delete is refused two different
 * ways, for children and for references, and the dialog shows both before anybody clicks.
 * `location-dialogs.tsx` holds all four and says why the create offers illegal levels while
 * the move offers no illegal parents.
 */
export function LocationsPage(): React.JSX.Element {
  const [searchParams, setSearchParams] = useSearchParams()
  const [term, setTerm] = useState('')

  const [creatingIn, setCreatingIn] = useState<{ parent: Location | null } | null>(null)
  const [editing, setEditing] = useState<Location | null>(null)
  const [moving, setMoving] = useState<Location | null>(null)
  const [deleting, setDeleting] = useState<Location | null>(null)

  const parentId = searchParams.get('parent')

  const roots = useLocationRoots()
  const children = useLocationChildren(parentId)
  const chain = useLocationAncestors(parentId)
  const matches = useLocationSearch(term)

  const searching = term.trim().length > 0
  const level = parentId === null ? roots : children
  const rows = useMemo(
    () => (searching ? (matches.data ?? []) : (level.data ?? [])),
    [level.data, matches.data, searching],
  )
  const pending = searching ? matches.isPending : level.isPending
  const failed = searching ? matches.isError : level.isError

  const open = (location: Location | null): void => {
    const params = new URLSearchParams()
    if (location !== null) {
      params.append('parent', location.id)
    }
    setSearchParams(params)
    setTerm('')
  }

  const here = chain.data?.at(-1) ?? null

  return (
    <>
      <PageHeader
        title="Locations"
        subtitle="Organisation, site, building, floor or area, room — the physical context every ticket, asset, and person is placed in."
        back={backToAdministration}
        actions={
          <Button
            onClick={() => {
              setCreatingIn({ parent: here })
            }}
          >
            <Plus className="size-4" aria-hidden="true" />
            {here === null ? 'New organisation' : `New location in ${here.name}`}
          </Button>
        }
      />

      <div className="flex flex-col gap-5">
        <div className="rounded-card border border-border bg-surface p-4 shadow-card">
          <div className="relative">
            <Label htmlFor="location-search" className="sr-only">
              Search locations
            </Label>
            <Search
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden="true"
            />
            <Input
              id="location-search"
              type="search"
              placeholder="Search every location…"
              className="pl-9"
              value={term}
              onChange={(event) => {
                setTerm(event.target.value)
              }}
            />
          </div>

          {searching ? null : (
            <nav aria-label="Location breadcrumb" className="mt-3 flex flex-wrap items-center gap-1">
              <button
                type="button"
                className="flex items-center gap-1 rounded-sm px-1 text-copy text-primary hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                onClick={() => {
                  open(null)
                }}
              >
                <Home className="size-3.5" aria-hidden="true" />
                All locations
              </button>

              {chain.isPending && parentId !== null ? (
                <Skeleton className="ml-2 h-4 w-40" />
              ) : (
                (chain.data ?? []).map((node) => (
                  <span key={node.id} className="flex items-center gap-1">
                    <ChevronRight className="size-3.5 text-muted-foreground" aria-hidden="true" />
                    <button
                      type="button"
                      className="rounded-sm px-1 text-copy text-primary hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                      onClick={() => {
                        open(node)
                      }}
                    >
                      {node.name}
                    </button>
                  </span>
                ))
              )}
            </nav>
          )}
        </div>

        {pending ? (
          <ListSkeleton />
        ) : failed ? (
          <ErrorState
            title="The locations could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void (searching ? matches.refetch() : level.refetch())
            }}
          />
        ) : rows.length === 0 ? (
          <EmptyState
            icon={MapPin}
            title={
              searching
                ? 'No location matches that'
                : here === null
                  ? 'No locations yet'
                  : `Nothing is recorded in ${here.name}`
            }
            description={
              searching
                ? 'The search matches a location’s own name and its full path.'
                : 'The tree starts at an organisation and runs down to rooms.'
            }
            action={
              searching
                ? undefined
                : {
                    label: here === null ? 'Create the first organisation' : 'Add a location here',
                    onClick: () => {
                      setCreatingIn({ parent: here })
                    },
                  }
            }
          />
        ) : (
          <ul className="overflow-hidden rounded-card border border-border bg-surface shadow-card">
            {rows.map((location) => (
              <li
                key={location.id}
                className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-3 last:border-b-0 hover:bg-canvas"
              >
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="truncate text-cell font-semibold text-heading">
                      {location.name}
                    </span>
                    <KindPill kind={location.kind} />
                  </div>
                  <p className="truncate text-caption text-muted-foreground">
                    {searching
                      ? location.path
                      : location.childCount === 0
                        ? 'No locations inside'
                        : `${String(location.childCount)} ${location.childCount === 1 ? 'location' : 'locations'} inside`}
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  {location.childCount > 0 || !searching ? (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        open(location)
                      }}
                    >
                      Open
                      <ChevronRight className="size-4" aria-hidden="true" />
                    </Button>
                  ) : null}
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setEditing(location)
                    }}
                  >
                    Rename
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setMoving(location)
                    }}
                  >
                    Move
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      setDeleting(location)
                    }}
                  >
                    Delete
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <LocationFormDialog
        location={editing}
        parent={creatingIn?.parent ?? null}
        open={creatingIn !== null || editing !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) {
            setCreatingIn(null)
            setEditing(null)
          }
        }}
      />

      <LocationMoveDialog
        location={moving}
        onOpenChange={(isOpen) => {
          if (!isOpen) {
            setMoving(null)
          }
        }}
      />

      <LocationDeleteDialog
        location={deleting}
        onOpenChange={(isOpen) => {
          if (!isOpen) {
            setDeleting(null)
          }
        }}
      />
    </>
  )
}

/** One wording for leaving a directory screen, shared by both of them. */
const backToAdministration = { to: '/administration', label: 'Back to administration' }

/**
 * Which level of the hierarchy a node is.
 *
 * One neutral treatment for all six rather than a hue each: DESIGN.md §2 fixes its
 * semantic colours to states — a status, a priority, a severity — and a level is not one of
 * those. Inventing six more hues would put colour in the reader's way on a screen where
 * nothing is urgent.
 */
function KindPill({ kind }: { kind: string }): React.JSX.Element {
  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center rounded-md bg-primary-soft px-2 py-0.5 text-label font-semibold text-primary uppercase',
      )}
    >
      {kind}
    </span>
  )
}

function ListSkeleton(): React.JSX.Element {
  return (
    <div
      className="overflow-hidden rounded-card border border-border bg-surface shadow-card"
      aria-busy="true"
      aria-label="Loading locations"
    >
      {Array.from({ length: 6 }, (_, index) => (
        <div
          key={index}
          className="flex items-center justify-between gap-4 border-b border-border px-4 py-3 last:border-b-0"
        >
          <div className="flex flex-col gap-1.5">
            <Skeleton className="h-3.5 w-48" />
            <Skeleton className="h-2.5 w-32" />
          </div>
          <Skeleton className="h-8 w-56" />
        </div>
      ))}
    </div>
  )
}
