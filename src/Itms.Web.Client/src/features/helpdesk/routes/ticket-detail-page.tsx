import { useCallback } from 'react'
import { Link, Navigate, useParams } from 'react-router'
import { ArrowLeft, MessagesSquare, Ticket } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader } from '@/components/layout/page-header'
import { Panel } from '@/components/common/panel'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { Button } from '@/components/ui/button'
import { ApiError } from '@/lib/api/client'
import { useNow } from '@/lib/use-now'
import { hasAnyRole, Roles } from '@/lib/roles'
import { formatDateTime } from '@/lib/datetime'
import type { TicketStatus } from '@/lib/api/types'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { TicketAttachments } from '../components/ticket-attachments'
import { TicketCommentComposer } from '../components/ticket-comment-composer'
import { TicketDetailHeader } from '../components/ticket-detail-header'
import { TicketDetailSkeleton } from '../components/ticket-detail-skeleton'
import { TicketProperties } from '../components/ticket-properties'
import { TicketTimeline } from '../components/ticket-timeline'
import { TicketTransitionButtons } from '../components/ticket-transition-buttons'
import {
  useAddTicketComment,
  useAssignTicket,
  useChangeTicketStatus,
  useTicket,
  useUploadTicketAttachment,
} from '../hooks/use-ticket'
import { useAssignableUsers } from '../hooks/use-tickets'

/**
 * One ticket, in full (WP-1.10).
 *
 * ## Concurrency
 *
 * Every write here sends the `ETag` the detail was read at as `If-Match`. ARCHITECTURE.md
 * §6 asks for optimistic concurrency on tickets and WP-1.5 and WP-1.6 built the header
 * surface for this screen specifically. The difference it buys is real: with the
 * precondition, a stale copy is refused with **412 before the transition is attempted**,
 * so the person is told to reload before they have typed a resolution. Without it they
 * would find out from a 409 after the write lost a race. Both carry
 * `helpdesk.ticket_conflict`, and both are handled the same way — say the ticket moved,
 * and reload it.
 *
 * ## Why every write refetches
 *
 * The transition response deliberately carries no SLA (WP-1.8), and moving a ticket to
 * Waiting parks the resolution clock and shifts `dueAt` — so a screen that patched itself
 * from the response would show a deadline that had already changed. The refetch also
 * brings back the new `ETag`, which the next write needs.
 */
export function TicketDetailPage(): React.JSX.Element {
  const { id } = useParams<{ id: string }>()
  const now = useNow()
  const { data: currentUser } = useCurrentUser()
  const roles = currentUser?.roles ?? []
  const worksTheQueue = hasAnyRole(roles, [Roles.admin, Roles.technician])

  const ticketId = id ?? ''
  const detail = useTicket(ticketId)
  const assignees = useAssignableUsers(worksTheQueue)

  const changeStatus = useChangeTicketStatus(ticketId)
  const assign = useAssignTicket(ticketId)
  const comment = useAddTicketComment(ticketId)
  const upload = useUploadTicketAttachment(ticketId)

  const busy =
    changeStatus.isPending || assign.isPending || comment.isPending || upload.isPending

  /**
   * A stale write and an ordinary failure read differently to the person in front of
   * them: one means "somebody else got there first, look again", the other means "that
   * did not happen". The screen has already been told to refetch by the mutation's
   * `onSettled`, so the message is all that is left to get right.
   */
  const reportFailure = useCallback((error: unknown, whatFailed: string) => {
    if (error instanceof ApiError && (error.status === 412 || error.status === 409)) {
      toast.error('This ticket changed while you were reading it.', {
        description: 'It has been reloaded. Check what happened and try again.',
      })
      return
    }

    toast.error(whatFailed, {
      description: error instanceof Error ? error.message : undefined,
    })
  }, [])

  const etag = detail.data?.etag ?? null

  const onMove = useCallback(
    (status: TicketStatus, resolutionNotes: string | null) => {
      changeStatus.mutate(
        { status, resolutionNotes, etag },
        {
          onSuccess: (result) => {
            toast.success(`${result.number} moved to ${result.status}.`)
          },
          onError: (error) => {
            reportFailure(error, 'The ticket could not be moved.')
          },
        },
      )
    },
    [changeStatus, etag, reportFailure],
  )

  const onAssign = useCallback(
    (assigneeId: string | null) => {
      assign.mutate(
        { assigneeId, etag },
        {
          onSuccess: (result) => {
            toast.success(
              result.assigneeName === null || result.assigneeName === undefined
                ? `${result.number} is back on the queue.`
                : `${result.number} assigned to ${result.assigneeName}.`,
            )
          },
          onError: (error) => {
            reportFailure(error, 'The ticket could not be assigned.')
          },
        },
      )
    },
    [assign, etag, reportFailure],
  )

  const onComment = useCallback(
    (body: string, isInternal: boolean) => {
      comment.mutate(
        { body, isInternal },
        {
          onSuccess: () => {
            toast.success(isInternal ? 'Internal note added.' : 'Comment posted.')
          },
          onError: (error) => {
            reportFailure(error, 'The comment could not be posted.')
          },
        },
      )
    },
    [comment, reportFailure],
  )

  const onUpload = useCallback(
    (file: File, isInternal: boolean) => {
      upload.mutate(
        { file, isInternal },
        {
          onSuccess: (result) => {
            toast.success(`${result.fileName} attached.`)
          },
          onError: (error) => {
            reportFailure(error, 'The file could not be attached.')
          },
        },
      )
    },
    [reportFailure, upload],
  )

  if (id === undefined) {
    return <Navigate to="/tickets" replace />
  }

  if (detail.isPending) {
    return (
      <>
        <PageHeader title="Ticket" subtitle="Loading…" actions={<BackToQueue />} />
        <TicketDetailSkeleton />
      </>
    )
  }

  if (detail.isError) {
    const missing = detail.error instanceof ApiError && detail.error.status === 404

    return (
      <>
        <PageHeader title="Ticket" subtitle="" actions={<BackToQueue />} />
        {missing ? (
          // 404 covers three cases on purpose (WP-1.5): no such ticket, a deleted one,
          // and somebody else's. Telling them apart would let any account walk the id
          // space and count what it cannot see, so the screen says what the server says.
          <EmptyState
            icon={Ticket}
            title="No such ticket"
            description="It may have been removed, or it may belong to somebody else."
          />
        ) : (
          <ErrorState
            title="The ticket could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void detail.refetch()
            }}
          />
        )}
      </>
    )
  }

  const ticket = detail.data.ticket

  return (
    <>
      <PageHeader
        title={ticket.subject}
        subtitle={`${ticket.number} · Raised by ${ticket.requesterName} · ${formatDateTime(ticket.createdAt)}`}
        actions={
          <>
            <BackToQueue />
            <TicketTransitionButtons ticket={ticket} busy={busy} onMove={onMove} />
          </>
        }
      />

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
        <div className="flex flex-col gap-5 lg:col-span-8">
          <TicketDetailHeader ticket={ticket} now={now} />

          <Panel icon={MessagesSquare} title="Activity">
            <TicketCommentComposer
              canWriteInternal={worksTheQueue}
              busy={busy}
              onSubmit={onComment}
            />
            <div className="mt-2">
              <TicketTimeline ticket={ticket} now={now} />
            </div>
          </Panel>
        </div>

        <div className="flex flex-col gap-5 lg:col-span-4">
          <TicketProperties
            ticket={ticket}
            assignees={assignees.data ?? []}
            canAssign={worksTheQueue}
            busy={busy}
            onAssign={onAssign}
          />

          <TicketAttachments
            ticket={ticket}
            canAttachInternal={worksTheQueue}
            busy={busy}
            onUpload={onUpload}
          />
        </div>
      </div>
    </>
  )
}

function BackToQueue(): React.JSX.Element {
  return (
    <Button variant="outline" render={<Link to="/tickets" />}>
      <ArrowLeft className="size-4" aria-hidden="true" />
      Queue
    </Button>
  )
}
