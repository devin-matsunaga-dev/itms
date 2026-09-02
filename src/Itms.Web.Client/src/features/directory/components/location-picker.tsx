import { useState } from 'react'
import { ChevronRight, Home, MapPin, Search, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import type { Location, LocationKind } from '@/lib/api/types'
import {
  useLocationAncestors,
  useLocationChildren,
  useLocationRoots,
  useLocationSearch,
} from '../hooks/use-directory'

interface LocationPickerProps {
  id: string
  /** The chosen location's id, or null for none. */
  value: string | null
  placeholder: string
  invalid?: boolean
  /**
   * Narrows every level to the nodes that could legally be the parent of a location of
   * this kind. Omitted on a plain "where is this?" question, which any node answers.
   */
  adoptableFor?: LocationKind
  /** Ids this picker must not offer — a node's own subtree, on a move. */
  excludedIds?: readonly string[]
  onValueChange: (locationId: string | null) => void
}

/**
 * The cascading location picker (WP-2.7).
 *
 * WP-2.4 built the three reads this walks — the roots, one level's children, and the
 * root-to-node chain — and recorded at the human's direction that the picker itself was
 * WP-2.7's. Until now every screen used a flat searchable list of one page of two hundred
 * rooms, and STATUS.md has carried the same caveat since WP-2.6a: an estate with more
 * locations than that could not filter by the ones past the first page. **That is the case
 * this control exists for.** It never asks for the whole tree — it asks for one level at a
 * time, so the size of the estate stops mattering.
 *
 * ## Two ways in, because people know two different things
 *
 * Somebody placing equipment knows the building and browses down to the room; somebody who
 * knows the room's name should not have to walk four levels to reach it. So the popover
 * opens on the tree and turns into a flat result list the moment anything is typed — the
 * server matches the node's name *and* its full path, so "pump" finds the pump station and
 * everything inside it. Both halves select the same way and write the same value.
 *
 * ## The hierarchy rule is never restated here
 *
 * `adoptableFor` is passed straight to the server, which resolves it through
 * `LocationHierarchy.KindsThatCanContain`. WP-2.4 put that rule on the server precisely so
 * a picker filtering client-side would not become a second copy of it, and this control
 * holds no notion of which kind may contain which. Filtering each level is safe because the
 * rule is by rank: if a node may adopt something, so may every one of its ancestors, so the
 * legal parents are always a prefix-closed part of the tree and drilling never has to pass
 * through a node the filter has hidden.
 *
 * A level *below* the last legal one can still come back empty — a room may not hold a
 * floor — and that is said rather than hidden, because the alternative is a chevron that
 * silently does nothing.
 */
export function LocationPicker({
  id,
  value,
  placeholder,
  invalid,
  adoptableFor,
  excludedIds,
  onValueChange,
}: LocationPickerProps): React.JSX.Element {
  const [open, setOpen] = useState(false)
  const [term, setTerm] = useState('')
  // Which node's children are on screen. Null is the root level.
  const [parentId, setParentId] = useState<string | null>(null)
  const [trail, setTrail] = useState<readonly Location[]>([])

  // The chain down to whatever is already chosen, so opening the picker on a value shows
  // where that value sits rather than dropping the reader at the top of the tree.
  const chosenChain = useLocationAncestors(value)
  const chosen = chosenChain.data?.at(-1) ?? null

  const roots = useLocationRoots(adoptableFor)
  const children = useLocationChildren(parentId, adoptableFor)
  const matches = useLocationSearch(term, adoptableFor)

  const searching = term.trim().length > 0

  // Opening on a chosen value lands on its parent's level, with the value itself in view.
  //
  // Adjusted during render rather than in an effect, the shape the search boxes use: React
  // re-runs this component before touching the DOM, so there is no flash of the wrong level
  // and no second commit. The guard is what keeps it from fighting somebody who is
  // browsing — while the popover is open the chain is ignored, and the level is resynced on
  // the render after it closes.
  const chain = chosenChain.data ?? []
  const chainKey = chain.map((node) => node.id).join('/')
  const [lastChainKey, setLastChainKey] = useState(chainKey)

  if (!open && chainKey !== lastChainKey) {
    setLastChainKey(chainKey)
    setTrail(chain.slice(0, -1))
    setParentId(chain.at(-2)?.id ?? null)
  }

  const level = parentId === null ? roots : children
  const excluded = new Set(excludedIds ?? [])
  const rows = (searching ? (matches.data ?? []) : (level.data ?? [])).filter(
    (location) => !excluded.has(location.id),
  )
  const pending = searching ? matches.isPending : level.isPending

  const select = (location: Location): void => {
    onValueChange(location.id)
    setOpen(false)
    setTerm('')
  }

  const drill = (location: Location): void => {
    setTrail((current) => [...current, location])
    setParentId(location.id)
    setTerm('')
  }

  const climb = (depth: number): void => {
    setTrail((current) => current.slice(0, depth))
    setParentId(depth === 0 ? null : (trail[depth - 1]?.id ?? null))
    setTerm('')
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <div className="relative">
        <PopoverTrigger
          render={
            <button
              id={id}
              type="button"
              aria-invalid={invalid}
              className={cn(
                'flex h-10 w-full items-center gap-2 rounded-input border border-border bg-surface px-3 text-left text-copy',
                'focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none',
                'aria-invalid:border-danger',
                value === null && 'text-muted-foreground',
                value !== null && 'pr-9',
              )}
            />
          }
        >
          <MapPin className="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
          <span className="truncate">
            {value === null ? placeholder : (chosen?.path ?? 'Loading…')}
          </span>
        </PopoverTrigger>

        {value === null ? null : (
          <button
            type="button"
            aria-label="Clear the location"
            className="absolute top-1/2 right-2 -translate-y-1/2 rounded-sm p-1 text-muted-foreground transition-colors hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
            onClick={() => {
              onValueChange(null)
            }}
          >
            <X className="size-4" aria-hidden="true" />
          </button>
        )}
      </div>

      <PopoverContent align="start" className="w-(--anchor-width) min-w-80 gap-3 p-3">
        <div className="relative">
          <Search
            className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden="true"
          />
          <Input
            type="search"
            aria-label="Search locations"
            placeholder="Search every location…"
            className="pl-9"
            value={term}
            onChange={(event) => {
              setTerm(event.target.value)
            }}
          />
        </div>

        {searching ? null : (
          <nav aria-label="Location breadcrumb" className="flex flex-wrap items-center gap-1">
            <button
              type="button"
              className="flex items-center gap-1 rounded-sm px-1 text-caption text-primary hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
              onClick={() => {
                climb(0)
              }}
            >
              <Home className="size-3" aria-hidden="true" />
              All locations
            </button>
            {trail.map((node, depth) => (
              <span key={node.id} className="flex items-center gap-1">
                <ChevronRight className="size-3 text-muted-foreground" aria-hidden="true" />
                <button
                  type="button"
                  className="rounded-sm px-1 text-caption text-primary hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                  onClick={() => {
                    climb(depth + 1)
                  }}
                >
                  {node.name}
                </button>
              </span>
            ))}
          </nav>
        )}

        <ul className="max-h-72 overflow-y-auto">
          {pending ? (
            <li className="flex flex-col gap-2 py-1">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-4/5" />
            </li>
          ) : rows.length === 0 ? (
            <li className="px-2 py-6 text-center text-caption text-muted-foreground">
              {searching
                ? 'No location matches that.'
                : adoptableFor === undefined
                  ? 'Nothing is recorded here.'
                  : `Nothing here can hold a ${adoptableFor.toLowerCase()}.`}
            </li>
          ) : (
            rows.map((location) => (
              <li key={location.id} className="flex items-center gap-1">
                <button
                  type="button"
                  aria-current={location.id === value ? 'true' : undefined}
                  className={cn(
                    'flex min-w-0 flex-1 flex-col rounded-input px-2 py-1.5 text-left hover:bg-canvas focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
                    location.id === value && 'bg-primary-soft',
                  )}
                  onClick={() => {
                    select(location)
                  }}
                >
                  <span className="truncate text-copy text-heading">{location.name}</span>
                  <span className="truncate text-caption text-muted-foreground">
                    {searching ? location.path : location.kind}
                  </span>
                </button>

                {!searching && location.childCount > 0 ? (
                  <button
                    type="button"
                    aria-label={`Open ${location.name}`}
                    className="rounded-input p-2 text-muted-foreground hover:bg-canvas hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                    onClick={() => {
                      drill(location)
                    }}
                  >
                    <ChevronRight className="size-4" aria-hidden="true" />
                  </button>
                ) : null}
              </li>
            ))
          )}
        </ul>

        {value === null ? null : (
          <Button
            variant="ghost"
            size="sm"
            className="self-start"
            onClick={() => {
              onValueChange(null)
              setOpen(false)
            }}
          >
            Clear the location
          </Button>
        )}
      </PopoverContent>
    </Popover>
  )
}
