import {
  ArrowRightLeft,
  MoreHorizontal,
  PackagePlus,
  UserRound,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Asset, AssetChangeKind, AssetHistoryEntry } from '@/lib/api/types'
import { formatDateTime, formatRelative } from '@/lib/datetime'
import {
  buildAssetActivity,
  changeKindLabels,
  hasOlderHistory,
  type AssetActivityItem,
} from '../lib/asset-activity'

interface AssetTimelineProps {
  asset: Asset
  entries: readonly AssetHistoryEntry[]
  /** How many entries the server says there are, so the gap can be marked. */
  total: number
  /** The instant relative times are measured against, threaded from the page. */
  now: Date
}

/**
 * The asset's timeline — assignment, transfer, repair, return to service, retirement.
 *
 * Newest first, with the lines one operation wrote grouped into one row:
 * `asset-activity.ts` sets out the three rules this renders, and the endpoint's own
 * description asks for exactly this ("entries sharing an `occurredAt` came from one
 * operation and are meant to be read together, in sequence order"). Issuing a machine out
 * of stock is therefore one event with two lines under it, and a transfer between two
 * people is one event with one — which is WP-2.2's own done-criterion, rendered.
 *
 * Rows follow DESIGN.md §4's activity-list treatment: a soft-tinted circular icon, the
 * sentence, and a right-hand column with the absolute time over the relative one.
 *
 * **The values are the text they were at the time, not ids.** `AssetHistoryEntry` caches
 * the display string deliberately, so a timeline still reads correctly after the person
 * who held the machine has been deactivated or the status has been renamed. Nothing here
 * tries to resolve one back to a row.
 */
export function AssetTimeline({
  asset,
  entries,
  total,
  now,
}: AssetTimelineProps): React.JSX.Element {
  const items = buildAssetActivity(asset, entries)
  const older = hasOlderHistory(entries.length, total)

  return (
    <ol aria-label="Asset history" className="flex flex-col">
      {items.map((item, index) => (
        <li key={item.id}>
          {/* The gap marker sits between the page on screen and the synthesised recorded
              line, so nothing on either side of it looks adjacent to anything it is not. */}
          {older && item.kind === 'recorded' ? <OlderMarker /> : null}
          <Row item={item} now={now} first={index === 0} />
        </li>
      ))}
    </ol>
  )
}

function Row({
  item,
  now,
  first,
}: {
  item: AssetActivityItem
  now: Date
  first: boolean
}): React.JSX.Element {
  const { icon: Icon, tint, tone } = decoration(item)

  return (
    <article className={cn('flex items-start gap-3 py-4', !first && 'border-t border-border')}>
      <span
        className={cn('mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-full', tint)}
      >
        <Icon className={cn('size-[18px]', tone)} aria-hidden="true" />
      </span>

      <div className="min-w-0 flex-1">
        {item.kind === 'recorded' ? (
          <p className="text-copy font-medium text-heading">
            Recorded as <span className="font-semibold">{item.assetTag}</span>
          </p>
        ) : (
          <>
            <p className="text-copy font-medium text-heading">
              {item.actorName === null ? 'The system' : item.actorName} updated this asset
            </p>
            <ul className="mt-1 flex flex-col gap-0.5">
              {item.entries.map((entry) => (
                <li key={entry.id} className="text-copy text-body">
                  <span className="text-muted-foreground">{changeKindLabels[entry.kind]}: </span>
                  <span>{describeValue(entry, entry.fromValue)}</span>
                  <span className="px-1 text-muted-foreground" aria-hidden="true">
                    →
                  </span>
                  <span className="font-medium text-heading">
                    {describeValue(entry, entry.toValue)}
                  </span>
                  {entry.note === null || entry.note === undefined || entry.note.length === 0 ? null : (
                    <span className="block text-caption whitespace-pre-wrap text-muted-foreground">
                      {entry.note}
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </>
        )}
      </div>

      <div className="flex shrink-0 flex-col items-end">
        <span className="text-caption text-muted-foreground">{formatDateTime(item.at)}</span>
        <span className="text-caption font-medium text-body">{formatRelative(item.at, now)}</span>
      </div>
    </article>
  )
}

/** The marker standing in for the entries the page on screen did not reach back to. */
function OlderMarker(): React.JSX.Element {
  return (
    <div className="flex items-center gap-3 border-t border-border py-4">
      <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-muted">
        <MoreHorizontal className="size-[18px] text-muted-foreground" aria-hidden="true" />
      </span>
      <p className="text-copy text-muted-foreground">Older history for this asset is not shown.</p>
    </div>
  )
}

/**
 * One history value as a person reads it.
 *
 * An empty value means the dimension had nothing in it: nobody held the machine, which is
 * a fact worth wording rather than showing as a dash. A status never arrives empty — an
 * asset always has one.
 */
function describeValue(entry: AssetHistoryEntry, value: string | null | undefined): string {
  if (value === null || value === undefined || value.length === 0) {
    return entry.kind === 'Assignment' ? 'Nobody' : '—'
  }

  return value
}

interface Decoration {
  readonly icon: LucideIcon
  readonly tint: string
  readonly tone: string
}

/** DESIGN.md §4: a 36px circular soft-tinted icon, in the hue the row is about. */
function decoration(item: AssetActivityItem): Decoration {
  if (item.kind === 'recorded') {
    return { icon: PackagePlus, tint: 'bg-primary-soft', tone: 'text-primary' }
  }

  return changeDecoration(item.entries[0]?.kind)
}

/**
 * The icon for an event, taken from the first line the operation wrote.
 *
 * The first line is the right one to key on: `AssetChanges.Between` writes the assignment
 * before the status, so issuing a machine out of stock reads as a hand-over — which is
 * what the person did — rather than as a lifecycle move, which is what it caused.
 */
function changeDecoration(kind: AssetChangeKind | undefined): Decoration {
  switch (kind) {
    case 'Assignment':
      return { icon: UserRound, tint: 'bg-info/12 dark:bg-info/15', tone: 'text-info' }
    default:
      return { icon: ArrowRightLeft, tint: 'bg-primary-soft', tone: 'text-primary' }
  }
}
