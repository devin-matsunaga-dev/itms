import { CircleSlash } from 'lucide-react'
import type { Asset } from '@/lib/api/types'
import { PersonCell } from '@/features/helpdesk/components/person-cell'
import { AssetStatusPill } from './asset-status-pill'
import { WarrantyCell } from './warranty-cell'
import { isTerminalStatus } from '../lib/asset-display'

interface AssetDetailHeaderProps {
  asset: Asset
  /** The instant the warranty countdown is measured against, threaded from the page. */
  now: Date
}

/**
 * The asset's state at a glance: where it is in its life, who holds it, where it is, and
 * how long its warranty has left — over the identification a person reads off the label.
 *
 * The pill follows the treatment `asset-display.ts` sets out and the register already
 * uses, so one status is not two treatments on two screens.
 *
 * **A terminal status says so in words.** Retired, Lost, and Disposed have no way out
 * (WP-2.2, at the human's direction), and an asset sitting in one is not a machine
 * somebody should go looking for. The caption states it rather than leaving the reader to
 * infer it from an absence of actions — which is the shape `WP-2.6b` will put in the page
 * header, where the actions that remain legal are the ones rendered at all.
 */
export function AssetDetailHeader({ asset, now }: AssetDetailHeaderProps): React.JSX.Element {
  const terminal = isTerminalStatus(asset.assetStatusCode)

  return (
    <section className="rounded-card border border-border bg-surface p-5 shadow-card">
      <div className="flex flex-wrap items-center gap-x-5 gap-y-3">
        <AssetStatusPill code={asset.assetStatusCode} name={asset.assetStatusName} />
        <span className="text-copy text-body">{asset.assetTypeName}</span>

        {terminal ? (
          <span className="inline-flex items-center gap-1.5 text-caption text-muted-foreground">
            <CircleSlash className="size-3.5" aria-hidden="true" />
            This asset has reached the end of its lifecycle.
          </span>
        ) : null}
      </div>

      <dl className="mt-5 grid grid-cols-1 gap-5 border-t border-border pt-5 sm:grid-cols-3">
        <Field term="Assigned to">
          <PersonCell name={asset.assignedToUserName} absent="Nobody" />
        </Field>

        <Field term="Location">
          {/* The cached path (§3 rule 6). STATUS.md carries the gap: nothing refreshes it
              when a room is renamed or moved, so `GET /locations/{id}/usage` and this
              string can disagree, and the usage figure is the correct one. */}
          <span className="text-cell text-heading">{asset.locationPath ?? '—'}</span>
        </Field>

        <Field term="Warranty">
          <WarrantyCell expiresAt={asset.warrantyExpiresAt} now={now} showDate />
        </Field>
      </dl>

      {asset.notes === null || asset.notes === undefined || asset.notes.length === 0 ? null : (
        <div className="mt-5 border-t border-border pt-5">
          <h2 className="text-label font-semibold text-muted-foreground uppercase">Notes</h2>
          {/* Plain text, typed by a technician. React escapes it; nothing on this screen
              renders asset text as markup. */}
          <p className="mt-2 text-copy whitespace-pre-wrap text-body">{asset.notes}</p>
        </div>
      )}
    </section>
  )
}

function Field({
  term,
  children,
}: {
  term: string
  children: React.ReactNode
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1.5">
      <dt className="text-label font-semibold text-muted-foreground uppercase">{term}</dt>
      <dd>{children}</dd>
    </div>
  )
}
