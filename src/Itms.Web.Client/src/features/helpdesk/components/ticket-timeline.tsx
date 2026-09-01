import {
  ArrowRightLeft,
  CircleCheck,
  Flag,
  Lock,
  MessageSquare,
  MoreHorizontal,
  PauseCircle,
  TicketPlus,
  UserRound,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TicketChangeKind, TicketDetail, TicketHistoryEntry } from '@/lib/api/types'
import { formatDateTime, formatRelative } from '@/lib/datetime'
import {
  buildActivity,
  changeKindLabels,
  hasOlderActivity,
  type ActivityItem,
} from '../lib/ticket-activity'
import { statusLabels } from '../lib/ticket-display'

interface TicketTimelineProps {
  ticket: TicketDetail
  /** The instant relative times are measured against, threaded from the page. */
  now: Date
}

/**
 * The ticket's conversation and its timeline, as one list.
 *
 * Newest first, because the detail carries only the head of each list and newest-first is
 * the only order in which what is on screen is contiguous — `ticket-activity.ts` sets out
 * the three rules this renders. Rows follow DESIGN.md §4's activity-list treatment: a
 * soft-tinted circular icon, the sentence, and a right-hand column with the absolute time
 * over the relative one.
 */
export function TicketTimeline({ ticket, now }: TicketTimelineProps): React.JSX.Element {
  const items = buildActivity(ticket)
  const older = hasOlderActivity(ticket)

  return (
    <ol aria-label="Ticket activity" className="flex flex-col">
      {items.map((item, index) => (
        <li key={item.id}>
          {/* The gap marker sits between the head and the synthesised raised line, so
              nothing on either side of it looks adjacent to anything it is not. */}
          {older && item.kind === 'raised' ? <OlderMarker /> : null}
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
  item: ActivityItem
  now: Date
  first: boolean
}): React.JSX.Element {
  const { icon: Icon, tint, tone } = decoration(item)

  return (
    <article
      className={cn(
        'flex items-start gap-3 py-4',
        !first && 'border-t border-border',
        item.kind === 'comment' &&
          item.comment.isInternal &&
          '-mx-2 rounded-tile border-t-0 bg-warning/12 px-2 dark:bg-warning/15',
      )}
    >
      <span
        className={cn('mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-full', tint)}
      >
        <Icon className={cn('size-[18px]', tone)} aria-hidden="true" />
      </span>

      <div className="min-w-0 flex-1">
        <Headline item={item} />
        <Body item={item} />
      </div>

      <div className="flex shrink-0 flex-col items-end">
        <span className="text-caption text-muted-foreground">{formatDateTime(item.at)}</span>
        <span className="text-caption font-medium text-body">{formatRelative(item.at, now)}</span>
      </div>
    </article>
  )
}

function Headline({ item }: { item: ActivityItem }): React.JSX.Element {
  if (item.kind === 'raised') {
    return (
      <p className="text-copy font-medium text-heading">
        Raised by <span className="font-semibold">{item.actorName}</span>
      </p>
    )
  }

  if (item.kind === 'comment') {
    return (
      <p className="flex flex-wrap items-center gap-2 text-copy font-medium text-heading">
        <span className="font-semibold">{item.comment.authorName}</span>
        {item.comment.isInternal ? (
          <span className="inline-flex items-center gap-1 rounded-md bg-warning/20 px-1.5 py-0.5 text-label font-semibold text-heading">
            <Lock className="size-3" aria-hidden="true" />
            Internal note
          </span>
        ) : null}
      </p>
    )
  }

  return (
    <p className="text-copy font-medium text-heading">
      {item.actorName === null ? 'The system' : item.actorName} updated this ticket
    </p>
  )
}

function Body({ item }: { item: ActivityItem }): React.JSX.Element | null {
  if (item.kind === 'raised') {
    return null
  }

  if (item.kind === 'comment') {
    // Plain text. React escapes it, and nothing on this screen renders comment bodies as
    // markup — the only sanitized rich text in this system is a KB article (WP-4.1).
    return (
      <p className="mt-1 text-copy whitespace-pre-wrap text-body">{item.comment.body}</p>
    )
  }

  return (
    <ul className="mt-1 flex flex-col gap-0.5">
      {item.entries.map((entry) => (
        <li key={entry.id} className="text-copy text-body">
          <span className="text-muted-foreground">{changeKindLabels[entry.kind]}: </span>
          {entry.kind === 'Resolution' || entry.kind === 'Hold' ? (
            // A value, not a move between two: "on hold — waiting on the vendor" reads as
            // a sentence, where "— → waiting on the vendor" reads as a bug.
            <span className="whitespace-pre-wrap">
              {entry.toValue === null || entry.toValue.length === 0
                ? 'lifted'
                : describeValue(entry, entry.toValue)}
            </span>
          ) : (
            <>
              <span>{describeValue(entry, entry.fromValue)}</span>
              <span className="px-1 text-muted-foreground" aria-hidden="true">
                →
              </span>
              <span className="font-medium text-heading">{describeValue(entry, entry.toValue)}</span>
            </>
          )}
        </li>
      ))}
    </ul>
  )
}

/** The marker standing in for the entries the embedded head did not reach back to. */
function OlderMarker(): React.JSX.Element {
  return (
    <div className="flex items-center gap-3 border-t border-border py-4">
      <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-muted">
        <MoreHorizontal className="size-[18px] text-muted-foreground" aria-hidden="true" />
      </span>
      <p className="text-copy text-muted-foreground">
        Older activity on this ticket is not shown.
      </p>
    </div>
  )
}

/**
 * One history value as a person reads it.
 *
 * A status arrives as the enum name the wire carries, and `InProgress` in a sentence
 * reads as a typo — the same call `HelpdeskErrors.Describe` makes server-side. An empty
 * assignment value means nobody held it.
 */
function describeValue(entry: TicketHistoryEntry, value: string | null): string {
  if (value === null || value === undefined || value.length === 0) {
    return entry.kind === 'Assignment' ? 'Unassigned' : '—'
  }

  if (entry.kind === 'Status' && value in statusLabels) {
    return statusLabels[value as keyof typeof statusLabels]
  }

  return value
}

interface Decoration {
  readonly icon: LucideIcon
  readonly tint: string
  readonly tone: string
}

/** DESIGN.md §4: a 36px circular soft-tinted icon, in the hue the row is about. */
function decoration(item: ActivityItem): Decoration {
  if (item.kind === 'raised') {
    return { icon: TicketPlus, tint: 'bg-primary-soft', tone: 'text-primary' }
  }

  if (item.kind === 'comment') {
    return item.comment.isInternal
      ? { icon: Lock, tint: 'bg-warning/20', tone: 'text-warning' }
      : { icon: MessageSquare, tint: 'bg-info/12 dark:bg-info/15', tone: 'text-info' }
  }

  return changeDecoration(item.entries[0]?.kind)
}

function changeDecoration(kind: TicketChangeKind | undefined): Decoration {
  switch (kind) {
    case 'Assignment':
      return { icon: UserRound, tint: 'bg-info/12 dark:bg-info/15', tone: 'text-info' }
    case 'Priority':
      return { icon: Flag, tint: 'bg-warning/12 dark:bg-warning/15', tone: 'text-warning' }
    case 'Resolution':
      return { icon: CircleCheck, tint: 'bg-violet/12 dark:bg-violet/15', tone: 'text-violet' }
    case 'Hold':
      // `teal` is Waiting's hue in DESIGN.md §2's semantic map, so a hold in the timeline
      // and the Waiting status pill above it are the same colour.
      return { icon: PauseCircle, tint: 'bg-teal/12 dark:bg-teal/15', tone: 'text-teal' }
    default:
      return { icon: ArrowRightLeft, tint: 'bg-primary-soft', tone: 'text-primary' }
  }
}
