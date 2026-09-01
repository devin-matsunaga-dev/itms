/**
 * A ticket's activity: its timeline and its conversation, merged into one list.
 *
 * The API keeps them apart — `history` is the ticket's own narrative (WP-1.4) and
 * `comments` is the thread (WP-1.7) — and says so: WP-1.7 recorded that "the comment
 * thread and the history timeline are two lists ordered on the same axis, which the
 * detail page has to interleave itself". This is that interleave, kept pure so it can be
 * asserted without a router, a query client, or a rendered tree.
 *
 * Three rules, each of them somebody else's lesson:
 *
 * 1. **Entries sharing an `occurredAt` are one event.** Resolving writes two lines at one
 *    instant — the status move and the resolution — and WP-1.4 added the `sequence`
 *    ordinal precisely because version 7 ids are not monotonic within a millisecond. Its
 *    note asks a UI to group them and render them together rather than as two rows
 *    wearing the same timestamp.
 * 2. **Newest first.** The detail embeds only the *head* of each list with
 *    `hasMoreHistory` / `hasMoreComments` beside it, so newest-first is the only order in
 *    which what is shown is contiguous. Reading it oldest-first would put the oldest row
 *    of the head at the top and quietly imply it was the beginning.
 * 3. **A ticket's timeline has a beginning.** Creation writes no history entry — WP-1.5
 *    chose that deliberately, because `TicketChanges.Between` compares two snapshots and
 *    a creation has no "before" — so the first line is synthesised here from `createdAt`
 *    and the requester's name rather than by inventing a fifth `TicketChangeKind`
 *    server-side. It is always last, being always the earliest instant; when the head is
 *    incomplete the screen marks the gap above it, so nothing claims to be adjacent to
 *    anything it is not.
 */

import { parseTimestamp } from '@/lib/datetime'
import type {
  TicketChangeKind,
  TicketComment,
  TicketDetail,
  TicketHistoryEntry,
} from '@/lib/api/types'

/** The ticket was raised. Synthesised from `createdAt` — see rule 3 above. */
export interface RaisedActivity {
  readonly kind: 'raised'
  readonly id: string
  readonly at: string
  readonly actorName: string
}

/** One change to the ticket, carrying every line that change wrote. */
export interface ChangeActivity {
  readonly kind: 'change'
  readonly id: string
  readonly at: string
  readonly actorName: string | null
  /** The lines this change wrote, in the order it wrote them (`sequence` ascending). */
  readonly entries: readonly TicketHistoryEntry[]
}

/** One line of the conversation — a public comment, or a note only the queue can read. */
export interface CommentActivity {
  readonly kind: 'comment'
  readonly id: string
  readonly at: string
  readonly comment: TicketComment
}

export type ActivityItem = RaisedActivity | ChangeActivity | CommentActivity

/** What each dimension of a change is called on screen. */
export const changeKindLabels: Record<TicketChangeKind, string> = {
  Status: 'Status',
  Priority: 'Priority',
  Assignment: 'Assignee',
  Resolution: 'Resolution',
}

/**
 * Merges a ticket's timeline and conversation into one newest-first list.
 *
 * @param ticket The detail payload, as the API returned it.
 * @returns The merged activity. Never empty: a ticket always has the line saying it was
 * raised.
 */
export function buildActivity(ticket: TicketDetail): ActivityItem[] {
  const items: ActivityItem[] = [
    ...groupChanges(ticket.history ?? []),
    ...(ticket.comments ?? []).map(
      (comment): CommentActivity => ({
        kind: 'comment',
        id: comment.id,
        at: comment.createdAt,
        comment,
      }),
    ),
    {
      kind: 'raised',
      id: `raised-${ticket.id}`,
      at: ticket.createdAt,
      actorName: ticket.requesterName,
    },
  ]

  // A paged list whose order changes between two reads silently drops and duplicates
  // rows — WP-1.4 and WP-1.5 both learned that the hard way — so every tie is broken.
  return items.sort((left, right) => {
    const byInstant = instant(right.at) - instant(left.at)
    if (byInstant !== 0) {
      return byInstant
    }

    const byKind = tieRank(left) - tieRank(right)
    if (byKind !== 0) {
      return byKind
    }

    return left.id < right.id ? 1 : left.id > right.id ? -1 : 0
  })
}

/**
 * True when the embedded head does not reach back to the ticket's creation, so the
 * screen has to say the middle is missing rather than let two rows look adjacent.
 */
export function hasOlderActivity(ticket: TicketDetail): boolean {
  return ticket.hasMoreHistory === true || ticket.hasMoreComments === true
}

/** Groups history entries that share an instant, oldest line of each change first. */
function groupChanges(entries: readonly TicketHistoryEntry[]): ChangeActivity[] {
  const byInstant = new Map<number, TicketHistoryEntry[]>()

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
    // `sequence` is the ordinal within one change, which is the only thing that orders
    // two lines written in the same millisecond.
    const ordered = [...group].sort((left, right) => left.sequence - right.sequence)
    const first = ordered[0]

    return {
      kind: 'change' as const,
      // The earliest line's id names the group, so the key is stable across reads.
      id: ordered.map((entry) => entry.id).join('+'),
      at: first?.occurredAt ?? '',
      actorName: first?.actorName ?? null,
      entries: ordered,
    }
  })
}

/** Changes read before the comments that share their instant; the raised line is last. */
function tieRank(item: ActivityItem): number {
  return item.kind === 'change' ? 0 : item.kind === 'comment' ? 1 : 2
}

function instant(value: string): number {
  return parseTimestamp(value)?.getTime() ?? 0
}
