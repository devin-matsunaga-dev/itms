import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { cn } from '@/lib/utils'
import { initials } from '@/lib/roles'

interface PersonCellProps {
  /** The display name, or null when nobody holds the ticket. */
  name: string | null | undefined
  /** What to say when there is no one — "Unassigned" on the assignee column. */
  absent?: string
  className?: string
}

/**
 * A person in a table cell: initials in a round tile, then their name.
 *
 * There are no uploaded avatars in this system and none are planned, so the tile is
 * always the fallback — which is why it takes a name and not a user. It exists to give
 * the eye something to track down a column of forty rows, not to identify anybody on its
 * own; the name is always beside it and is never abbreviated.
 *
 * The tile is decorative and hidden from assistive technology: a screen reader reading
 * "J S, J. Santos" would announce the same person twice.
 */
export function PersonCell({ name, absent = '—', className }: PersonCellProps): React.JSX.Element {
  if (name === null || name === undefined || name.length === 0) {
    return <span className={cn('text-muted-foreground', className)}>{absent}</span>
  }

  return (
    <span className={cn('flex items-center gap-2', className)}>
      <Avatar size="sm" aria-hidden="true" className="after:border-transparent">
        <AvatarFallback className="bg-primary-soft text-label font-semibold text-primary">
          {initials(name)}
        </AvatarFallback>
      </Avatar>
      <span className="truncate text-body">{name}</span>
    </span>
  )
}
