/**
 * An asset's timeline, grouped the way it was written.
 *
 * The server records *dimensions* rather than operations (WP-2.2): `AssetChangeKind` is
 * `Assignment` and `Status` and nothing else, because the five things SPEC.md §3 names —
 * assignment, transfer, repair, return to service, retirement — are five operations over
 * those two facts. One operation therefore writes one entry per dimension it actually
 * moved, and the endpoint's own description says entries sharing an `occurredAt` "came
 * from one operation and are meant to be read together, in sequence order".
 *
 * So this module does three things, each of them somebody else's lesson:
 *
 * 1. **Entries sharing an `occurredAt` are one event.** Issuing a machine out of stock
 *    moves the holder *and* the status and writes two lines at one instant; a transfer
 *    between two people moves one and writes one. WP-2.2 added the `sequence` ordinal for
 *    exactly this, because version 7 ids are not monotonic within a millisecond.
 * 2. **Newest first**, following the ticket timeline: the screen holds the head of a
 *    paged list, and newest-first is the only order in which what is on screen is
 *    contiguous.
 * 3. **An asset's timeline has a beginning.** Recording an asset writes no history entry
 *    — `CreateAssetHandler` audits and raises nothing, because a creation has no "before"
 *    for `AssetChanges.Between` to diff — so the first line is synthesised here from
 *    `createdAt`, exactly as the ticket timeline synthesises "Raised by" rather than
 *    inventing a third `AssetChangeKind` server-side. It is always last, being always the
 *    earliest instant.
 */

import { parseTimestamp } from '@/lib/datetime'
import type { Asset, AssetChangeKind, AssetHistoryEntry } from '@/lib/api/types'

/** The asset was recorded. Synthesised from `createdAt` — see rule 3 above. */
export interface RecordedActivity {
  readonly kind: 'recorded'
  readonly id: string
  readonly at: string
  readonly assetTag: string
}

/** One operation on the asset, carrying every line it wrote. */
export interface ChangeActivity {
  readonly kind: 'change'
  readonly id: string
  readonly at: string
  readonly actorName: string | null
  /** The lines this operation wrote, in the order it wrote them (`sequence` ascending). */
  readonly entries: readonly AssetHistoryEntry[]
}

export type AssetActivityItem = RecordedActivity | ChangeActivity

/** What each dimension of a change is called on screen. */
export const changeKindLabels: Record<AssetChangeKind, string> = {
  Assignment: 'Assigned to',
  Status: 'Status',
}

/**
 * Groups an asset's history into newest-first events, with the recorded line last.
 *
 * @param asset The asset the timeline belongs to, for the synthesised first line.
 * @param entries The page of history the API returned, in any order.
 * @returns The timeline. Never empty: an asset always has the line saying it was recorded.
 */
export function buildAssetActivity(
  asset: Pick<Asset, 'id' | 'assetTag' | 'createdAt'>,
  entries: readonly AssetHistoryEntry[],
): AssetActivityItem[] {
  const items: AssetActivityItem[] = [
    ...groupChanges(entries),
    {
      kind: 'recorded',
      id: `recorded-${asset.id}`,
      at: asset.createdAt,
      assetTag: asset.assetTag,
    },
  ]

  // Every tie is broken. A list whose order changes between two reads of the same data
  // is the lesson WP-1.4's `sequence` and WP-1.5's id tie-break both came from.
  return items.sort((left, right) => {
    const byInstant = instant(right.at) - instant(left.at)
    if (byInstant !== 0) {
      return byInstant
    }

    // A change always reads above the recorded line it shares an instant with — an asset
    // issued in the same millisecond it was recorded still happened after being recorded.
    const byKind = tieRank(left) - tieRank(right)
    if (byKind !== 0) {
      return byKind
    }

    return left.id < right.id ? 1 : left.id > right.id ? -1 : 0
  })
}

/**
 * True when the page on screen does not reach back to the asset being recorded, so the
 * timeline has to mark the gap rather than let two events look adjacent.
 *
 * @param shown How many entries the screen is holding.
 * @param total How many the server says there are.
 */
export function hasOlderHistory(shown: number, total: number): boolean {
  return total > shown
}

/** Groups history entries that share an instant, oldest line of each operation first. */
function groupChanges(entries: readonly AssetHistoryEntry[]): ChangeActivity[] {
  const byInstant = new Map<number, AssetHistoryEntry[]>()

  for (const entry of entries) {
    const key = instant(entry.occurredAt)
    const group = byInstant.get(key)
    if (group) {
      group.push(entry)
    } else {
      byInstant.set(key, [entry])
    }
  }

  return [...byInstant.values()].map((group) => {
    // `sequence` is the ordinal within one operation, which is what orders two lines
    // written in the same millisecond — and the id breaks even that tie. `Array.sort` is
    // stable, so without the second comparison two entries that somehow share an instant
    // *and* a sequence would keep whatever order they arrived in, and the group's key
    // below would change between two reads of the same data. That is the failure WP-1.4's
    // `sequence` and WP-1.5's id tie-break were both added to prevent.
    const ordered = [...group].sort(
      (left, right) =>
        left.sequence - right.sequence || (left.id < right.id ? -1 : left.id > right.id ? 1 : 0),
    )
    const first = ordered[0]

    return {
      kind: 'change' as const,
      // Every line's id names the group, so the key is stable across reads.
      id: ordered.map((entry) => entry.id).join('+'),
      at: first?.occurredAt ?? '',
      actorName: first?.actorName ?? null,
      entries: ordered,
    }
  })
}

function tieRank(item: AssetActivityItem): number {
  return item.kind === 'change' ? 0 : 1
}

function instant(value: string): number {
  return parseTimestamp(value)?.getTime() ?? 0
}
