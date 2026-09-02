import { useCallback } from 'react'
import { Navigate, useParams } from 'react-router'
import { History, HardDrive } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader } from '@/components/layout/page-header'
import { Panel } from '@/components/common/panel'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { ApiError } from '@/lib/api/client'
import { useNow } from '@/lib/use-now'
import { formatDateTime } from '@/lib/datetime'
import { AssetDetailHeader } from '../components/asset-detail-header'
import { AssetDetailSkeleton } from '../components/asset-detail-skeleton'
import { AssetLifecycleActions } from '../components/asset-lifecycle-actions'
import { AssetProperties } from '../components/asset-properties'
import { AssetTicketsPanel } from '../components/asset-tickets-panel'
import { AssetTimeline } from '../components/asset-timeline'
import { assetTitle } from '../lib/asset-display'
import type { AssetAction } from '../lib/asset-lifecycle'
import { useAssignAsset, useMoveAsset } from '../hooks/use-asset-write'
import {
  useAsset,
  useAssetHistory,
  useAssetHolders,
  useAssetTickets,
} from '../hooks/use-assets'

/**
 * One asset, in full (WP-2.6a).
 *
 * ## Three reads, not one
 *
 * The asset, its timeline, and the tickets raised about it are three requests, following
 * the call WP-2.5 made for the user 360: a package's criterion is a single round trip
 * *per panel*, and a screen refreshing one panel should not re-read the others. It also
 * means the timeline failing does not take the asset down with it — each panel says what
 * it could not load and the rest of the screen still reads.
 *
 * ## Concurrency
 *
 * Every write here sends the `ETag` the detail was read at as `If-Match` (WP-2.6b).
 * ARCHITECTURE.md §6 asks for optimistic concurrency on assets as well as tickets, and
 * WP-2.1 and WP-2.2 built the header surface for this screen specifically. With the
 * precondition, a stale copy is refused with **412 before the move is attempted**; without
 * it the second technician would find out from a 409 after losing a race. Both carry
 * `assets.asset_conflict`, and both are handled the same way — say the asset moved, and
 * reload it.
 *
 * ## Why every write refetches
 *
 * A lifecycle response is the asset as it now stands, but the timeline beside it has also
 * changed — issuing equipment out of stock writes two history lines — and the response
 * carries no new `ETag` for the *read*, which the next write needs. One invalidation
 * answers both. `use-asset-write.ts` holds it.
 *
 * ## What decides which actions appear
 *
 * The server does. `allowedNextStatusCodes` and `canBeAssigned` come off the asset, and
 * `asset-lifecycle.ts` turns them into buttons — so an illegal action is absent rather than
 * disabled, and `AssetLifecycle`'s table is never written a second time in TypeScript.
 */
export function AssetDetailPage(): React.JSX.Element {
  const { id } = useParams<{ id: string }>()
  const now = useNow()

  const assetId = id ?? ''
  const detail = useAsset(assetId)
  const history = useAssetHistory(assetId)
  const tickets = useAssetTickets(assetId)
  const holders = useAssetHolders()

  const assign = useAssignAsset(assetId)
  const move = useMoveAsset(assetId)
  const busy = assign.isPending || move.isPending

  const etag = detail.data?.etag ?? null

  /**
   * A stale write and an ordinary failure read differently to the person in front of them:
   * one means "somebody else got there first, look again", the other means "that did not
   * happen". The screen has already been told to refetch by the mutation's `onSettled`, so
   * the message is all that is left to get right.
   */
  const reportFailure = useCallback((error: unknown, whatFailed: string) => {
    if (error instanceof ApiError && (error.status === 412 || error.status === 409)) {
      toast.error('This asset changed while you were reading it.', {
        description: 'It has been reloaded. Check what happened and try again.',
      })
      return
    }

    toast.error(whatFailed, {
      description: error instanceof Error ? error.message : undefined,
    })
  }, [])

  const onAct = useCallback(
    (action: AssetAction, holderId: string | null, note: string | null) => {
      const done = {
        onSuccess: (result: { assetTag: string }) => {
          toast.success(`${result.assetTag} ${action.outcome}.`)
        },
        onError: (error: unknown) => {
          reportFailure(error, `The asset could not be ${action.outcome}.`)
        },
      }

      // The three assignment acts share a route and are told apart by what they send: a
      // person for an issue or a transfer, and null for a return (WP-2.2).
      if (action.route === null) {
        assign.mutate({ assignedToUserId: holderId, note, etag }, done)
        return
      }

      move.mutate({ route: action.route, note, etag }, done)
    },
    [assign, etag, move, reportFailure],
  )

  if (id === undefined) {
    return <Navigate to="/assets" replace />
  }

  if (detail.isPending) {
    return (
      <>
        <PageHeader title="Asset" subtitle="Loading…" back={backToAssets} />
        <AssetDetailSkeleton />
      </>
    )
  }

  if (detail.isError) {
    const missing = detail.error instanceof ApiError && detail.error.status === 404

    return (
      <>
        <PageHeader title="Asset" subtitle="" back={backToAssets} />
        {missing ? (
          // A soft-deleted asset answers 404 like one that never existed — the list never
          // returns deleted rows either, so the screen says what the server says.
          <EmptyState
            icon={HardDrive}
            title="No such asset"
            description="It may have been removed from the register."
          />
        ) : (
          <ErrorState
            title="The asset could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void detail.refetch()
            }}
          />
        )}
      </>
    )
  }

  const asset = detail.data.asset

  return (
    <>
      <PageHeader
        title={assetTitle(asset)}
        subtitle={`${asset.assetTag} · ${asset.assetTypeName} · Recorded ${formatDateTime(asset.createdAt)}`}
        back={backToAssets}
        actions={
          <AssetLifecycleActions
            asset={asset}
            holders={holders.data ?? []}
            busy={busy}
            onAct={onAct}
          />
        }
      />

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
        <div className="flex flex-col gap-5 lg:col-span-8">
          <AssetDetailHeader asset={asset} now={now} />

          <Panel icon={History} title="History">
            {history.isPending ? (
              <p className="text-copy text-muted-foreground" aria-busy="true">
                Loading the history…
              </p>
            ) : history.isError ? (
              <p role="alert" className="text-copy text-body">
                The history could not be loaded.
              </p>
            ) : (
              <AssetTimeline
                asset={asset}
                entries={history.data.items}
                total={history.data.total}
                now={now}
              />
            )}
          </Panel>
        </div>

        <div className="flex flex-col gap-5 lg:col-span-4">
          <AssetProperties asset={asset} />

          <AssetTicketsPanel
            tickets={tickets.data?.items ?? []}
            total={tickets.data?.total ?? 0}
            loading={tickets.isPending}
            failed={tickets.isError}
            now={now}
          />
        </div>
      </div>
    </>
  )
}

/** One wording for leaving an asset, shared by every screen that returns to the register. */
const backToAssets = { to: '/assets', label: 'Back to assets' }
