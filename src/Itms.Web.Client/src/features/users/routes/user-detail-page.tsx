import { Navigate, useParams } from 'react-router'
import { History, LifeBuoy, Users } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { ApiError } from '@/lib/api/client'
import { useNow } from '@/lib/use-now'
import { useAssetStatuses } from '@/features/assets/hooks/use-assets'
import { useDepartments, useLocationAncestors } from '@/features/directory/hooks/use-directory'
import { UserAssetsPanel } from '../components/user-assets-panel'
import { UserDetailSkeleton } from '../components/user-detail-skeleton'
import { UserProfileCard } from '../components/user-profile-card'
import { UserTicketsPanel } from '../components/user-tickets-panel'
import {
  useUser,
  useUserAssets,
  useUserOpenTickets,
  useUserPastTickets,
} from '../hooks/use-users'

/**
 * One person, in full — the user 360 (SPEC.md §4, WP-2.5, WP-2.7).
 *
 * SPEC.md §4's acceptance shape ends here: a technician searches a user and immediately
 * sees their equipment and support history. The three things it names are the three panels
 * — what they hold, what is still being worked, and what is finished with.
 *
 * ## Four reads, not one
 *
 * The profile, the equipment, the open tickets and the past tickets are four requests.
 * WP-2.5's own criterion is a single round trip *per panel*, and it built two panel
 * endpoints beside the profile read rather than one aggregate for exactly this reason: a
 * screen refreshing one panel should not re-read the others, and a panel that fails says so
 * without taking the rest of the screen down with it.
 *
 * The location is a fifth, and it is the one that is always right: `GET
 * /locations/{id}/ancestors` names the room whatever the size of the estate, where the flat
 * two-hundred-row read every list resolves a room from can honestly not contain it.
 *
 * ## No actions
 *
 * Nothing on this screen writes. Changing somebody's department, their role, or their
 * account status is user administration and belongs to `WP-5.8`, which has the endpoints to
 * build it with; there are none today. WP-1.11 settled that a control which silently does
 * nothing is worse than one that is absent.
 */
export function UserDetailPage(): React.JSX.Element {
  const { id } = useParams<{ id: string }>()
  const now = useNow()

  const userId = id ?? ''
  const profile = useUser(userId)
  const assets = useUserAssets(userId)
  const open = useUserOpenTickets(userId)
  const past = useUserPastTickets(userId)

  const departments = useDepartments()
  const statuses = useAssetStatuses()
  const locationChain = useLocationAncestors(profile.data?.locationId ?? null)

  if (id === undefined) {
    return <Navigate to="/users" replace />
  }

  if (profile.isPending) {
    return (
      <>
        <PageHeader title="Person" subtitle="Loading…" back={backToUsers} />
        <UserDetailSkeleton />
      </>
    )
  }

  if (profile.isError) {
    const missing = profile.error instanceof ApiError && profile.error.status === 404

    return (
      <>
        <PageHeader title="Person" subtitle="" back={backToUsers} />
        {missing ? (
          <EmptyState
            icon={Users}
            title="No such person"
            description="The account may have been removed, or the link may be out of date."
          />
        ) : (
          <ErrorState
            title="This person could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void profile.refetch()
            }}
          />
        )}
      </>
    )
  }

  const user = profile.data

  return (
    <>
      <PageHeader
        title={user.displayName}
        subtitle={user.email}
        back={backToUsers}
      />

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
        <div className="flex flex-col gap-5 lg:col-span-4">
          <UserProfileCard
            user={user}
            departments={departments.data ?? []}
            locationChain={locationChain.data ?? null}
          />

          <UserAssetsPanel
            assets={assets.data ?? []}
            statuses={statuses.data ?? []}
            loading={assets.isPending}
            failed={assets.isError}
            // The register already filters by holder (WP-2.6a), so "View all" is a real
            // link rather than a promise: it is the same rows, in the screen built to
            // work on them.
            registerHref={`/assets?assignedToUserId=${user.id}&sort=AssetTag&direction=Ascending&pageSize=25`}
          />
        </div>

        <div className="flex flex-col gap-5 lg:col-span-8">
          <UserTicketsPanel
            icon={LifeBuoy}
            title="Open tickets"
            tickets={open.data?.items ?? []}
            total={open.data?.total ?? 0}
            loading={open.isPending}
            failed={open.isError}
            emptyMessage="This person has nothing open with the helpdesk."
            queueHref={`/tickets?requesterId=${user.id}&sort=CreatedAt&direction=Descending&pageSize=25`}
            now={now}
          />

          <UserTicketsPanel
            icon={History}
            title="Previous tickets"
            tickets={past.data?.items ?? []}
            total={past.data?.total ?? 0}
            loading={past.isPending}
            failed={past.isError}
            emptyMessage="This person has no finished tickets."
            queueHref={`/tickets?requesterId=${user.id}&sort=CreatedAt&direction=Descending&pageSize=25`}
            now={now}
          />
        </div>
      </div>
    </>
  )
}

/** One wording for leaving a person, shared by every screen that returns to the directory. */
const backToUsers = { to: '/users', label: 'Back to users' }
