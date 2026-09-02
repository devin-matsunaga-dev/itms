import { IdCard } from 'lucide-react'
import { Panel } from '@/components/common/panel'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { cn } from '@/lib/utils'
import { initials } from '@/lib/roles'
import type { Department, Location, UserSummary } from '@/lib/api/types'
import { departmentName, roleLabel } from '../lib/user-display'

interface UserProfileCardProps {
  user: UserSummary
  departments: readonly Department[]
  /**
   * The root-to-node chain for this person's location, or null while it loads and when
   * they have none.
   *
   * The **chain** rather than the flat list every other screen resolves a room from: this
   * screen is about one person, so it can afford the one request that is always right,
   * where the flat read is one page of two hundred and can honestly not contain the room.
   * The last entry is the room itself and carries the full path.
   */
  locationChain: readonly Location[] | null
}

/**
 * Who somebody is (SPEC.md §4).
 *
 * ## What is missing, and why
 *
 * SPEC.md §4 lists name, username, email, department, job title, phone, location, manager,
 * and account status. `ItmsUser` has four of those and `UserSummary` carries the four that
 * may leave Identity — the sign-in name is not among them, deliberately, because it is
 * half of a credential. **Job title, phone, and manager are columns that do not exist**;
 * adding them is a migration and belongs to the package that builds user administration
 * (`WP-5.8`). Rendering an empty "Job title —" against a field the database has no room
 * for would promise something nothing can fill in.
 */
export function UserProfileCard({
  user,
  departments,
  locationChain,
}: UserProfileCardProps): React.JSX.Element {
  const department = departmentName(user.departmentId, departments)
  const room = locationChain?.at(-1) ?? null

  return (
    <Panel icon={IdCard} title="Profile">
      <div className="flex items-center gap-3">
        <Avatar size="lg" aria-hidden="true" className="after:border-transparent">
          <AvatarFallback className="bg-primary-soft font-semibold text-primary">
            {initials(user.displayName)}
          </AvatarFallback>
        </Avatar>
        <div className="min-w-0">
          <p className="truncate text-card-title font-semibold text-heading">
            {user.displayName}
          </p>
          <a
            href={`mailto:${user.email}`}
            className="truncate rounded-sm text-copy text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
          >
            {user.email}
          </a>
        </div>
      </div>

      <dl className="mt-5 flex flex-col gap-4">
        <Row term="Roles">{roleLabel(user.roles)}</Row>
        <Row term="Department" muted={!department.known}>
          {department.text}
        </Row>
        <Row term="Location" muted={user.locationId !== null && room === null}>
          {user.locationId === null ? '—' : (room?.path ?? 'Loading…')}
        </Row>
        <Row term="Account">
          {user.isActive ? (
            'Active'
          ) : (
            <>
              Deactivated
              {/*
                Said in full, because it is the fact somebody is on this screen to
                understand: invariant 9 keeps every ticket, comment, and asset history row
                a deactivated person owns, and the panels below are exactly those rows.
              */}
              <span className="mt-1 block text-caption font-normal text-muted-foreground">
                They can no longer sign in. Their tickets, comments, and equipment history
                are kept.
              </span>
            </>
          )}
        </Row>
      </dl>
    </Panel>
  )
}

function Row({
  term,
  children,
  muted = false,
}: {
  term: string
  children: React.ReactNode
  muted?: boolean
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-label font-semibold text-muted-foreground uppercase">{term}</dt>
      <dd className={cn('text-cell break-words', muted ? 'text-muted-foreground' : 'text-heading')}>
        {children}
      </dd>
    </div>
  )
}
