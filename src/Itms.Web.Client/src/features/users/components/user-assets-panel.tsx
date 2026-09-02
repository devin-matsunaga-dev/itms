import { Link } from 'react-router'
import { HardDrive } from 'lucide-react'
import { Panel } from '@/components/common/panel'
import { Skeleton } from '@/components/ui/skeleton'
import type { AssetStatus, AssetSummary } from '@/lib/api/types'
import { AssetStatusPill } from '@/features/assets/components/asset-status-pill'

interface UserAssetsPanelProps {
  assets: readonly AssetSummary[]
  /**
   * The configured statuses, for turning the code a summary carries into the word an
   * administrator gave it.
   *
   * `AssetSummary.status` is the status's immutable *code* — the enum stays inside Assets
   * (WP-2.5) — so rendering it raw would print `in-stock` at somebody. This is the same
   * reference data the register already holds under the same cache key, so it costs
   * nothing after the first asset screen of the session.
   */
  statuses: readonly AssetStatus[]
  loading: boolean
  failed: boolean
  /** Where "View all" goes: the register, filtered to this person. */
  registerHref: string
}

/**
 * The equipment somebody is holding right now (WP-2.5, WP-2.7).
 *
 * Unpaged, because that is what the endpoint answers: what one person is holding is a
 * handful of things rather than a queue. Everything here is a link into the register, which
 * is where an asset is actually worked on.
 *
 * **The status pill is imported from the assets feature rather than reimplemented**, the
 * call WP-2.6a made in the other direction for the ticket pill: a lifecycle status is
 * Assets' fact and DESIGN.md §2 fixes one hue per status product-wide, so a second copy of
 * that map living here is how two screens end up disagreeing about what "In Stock" looks
 * like. `AssetSummary` carries the status as its immutable *code* — the enum stays
 * inside Assets — so the hue is keyed on the code and the word comes from the configured
 * status list, and a code the design system does not name takes the muted unmapped
 * treatment instead of somebody else's colour.
 */
export function UserAssetsPanel({
  assets,
  statuses,
  loading,
  failed,
  registerHref,
}: UserAssetsPanelProps): React.JSX.Element {
  const statusNames = new Map(statuses.map((status) => [status.code, status.name]))

  return (
    <Panel
      icon={HardDrive}
      title="Equipment held"
      action={
        assets.length === 0 ? undefined : (
          <Link
            to={registerHref}
            className="rounded-sm text-cell font-medium text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
          >
            View all
          </Link>
        )
      }
    >
      {loading ? (
        <div className="flex flex-col gap-4" aria-busy="true">
          <span className="sr-only">Loading the equipment…</span>
          {[0, 1].map((row) => (
            <div key={row} className="flex flex-col gap-1.5">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="h-4 w-full" />
            </div>
          ))}
        </div>
      ) : failed ? (
        <p role="alert" className="text-copy text-body">
          The equipment list could not be loaded.
        </p>
      ) : assets.length === 0 ? (
        <p className="text-copy text-body">No equipment is issued to this person.</p>
      ) : (
        <ul aria-label="Equipment held" className="flex flex-col">
          {assets.map((asset, index) => (
            <li
              key={asset.id}
              className={index === 0 ? 'py-3 first:pt-0' : 'border-t border-border py-3'}
            >
              <div className="flex items-start justify-between gap-3">
                <Link
                  to={`/assets/${asset.id}`}
                  className="rounded-sm text-cell font-semibold text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
                >
                  {asset.assetTag}
                </Link>
                <AssetStatusPill
                  code={asset.status}
                  name={statusNames.get(asset.status) ?? asset.status}
                />
              </div>
              <p className="mt-1 text-cell text-heading">{asset.name}</p>
              <p className="text-caption text-muted-foreground">{asset.assetType}</p>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  )
}
