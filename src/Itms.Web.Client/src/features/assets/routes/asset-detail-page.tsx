import { Navigate, useParams } from 'react-router'
import { History, HardDrive } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { Panel } from '@/components/common/panel'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { ApiError } from '@/lib/api/client'
import { useNow } from '@/lib/use-now'
import { formatDateTime } from '@/lib/datetime'
import { AssetDetailHeader } from '../components/asset-detail-header'
import { AssetDetailSkeleton } from '../components/asset-detail-skeleton'
import { AssetProperties } from '../components/asset-properties'
import { AssetTicketsPanel } from '../components/asset-tickets-panel'
import { AssetTimeline } from '../components/asset-timeline'
import { assetTitle } from '../lib/asset-display'
import { useAsset, useAssetHistory, useAssetTickets } from '../hooks/use-assets'

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
 * ## What is not here yet
 *
 * **No lifecycle actions and no edit.** `WP-2.6b` owns assign, transfer, repair, return to
 * service, and retire, along with the create and edit forms — and, with them, the server
 * additions those need: the `PUT /assets/{id}` write path, which does not exist yet, and
 * the legal-destination list that lets an illegal action be *absent* rather than disabled
 * in place. That list is `AssetLifecycle.DestinationsFrom` server-side, and its own doc
 * comment asks the UI to read it rather than restate the table in TypeScript. Until it is
 * over the wire there is nothing here that could render those buttons honestly, so this
 * screen renders none.
 */
export function AssetDetailPage(): React.JSX.Element {
  const { id } = useParams<{ id: string }>()
  const now = useNow()

  const assetId = id ?? ''
  const detail = useAsset(assetId)
  const history = useAssetHistory(assetId)
  const tickets = useAssetTickets(assetId)

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

  const asset = detail.data

  return (
    <>
      <PageHeader
        title={assetTitle(asset)}
        subtitle={`${asset.assetTag} · ${asset.assetTypeName} · Recorded ${formatDateTime(asset.createdAt)}`}
        back={backToAssets}
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
