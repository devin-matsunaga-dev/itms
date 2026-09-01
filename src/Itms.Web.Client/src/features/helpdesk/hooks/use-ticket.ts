import {
  useMutation,
  useQueryClient,
  useQuery,
  type UseMutationResult,
  type UseQueryResult,
} from '@tanstack/react-query'
import type {
  CreateTicketRequest,
  TicketAssignment,
  TicketAttachment,
  TicketComment,
  TicketDetail,
  TicketStatus,
  TicketStatusChange,
} from '@/lib/api/types'
import {
  addTicketComment,
  assignTicket,
  changeTicketStatus,
  createTicket,
  fetchTicket,
  uploadTicketAttachment,
  type TicketRead,
} from '../api/tickets-api'
import { ticketKeys } from './use-tickets'

/** The detail's cache key, beside the queue's in `ticketKeys`. */
export const ticketDetailKey = (id: string): readonly unknown[] => ['helpdesk', 'ticket', id]

/**
 * One ticket, with the version tag every write on this screen sends back as `If-Match`.
 *
 * `staleTime` is zero, unlike the queue's: a technician who has just moved a ticket is
 * looking at the record they changed, and a cached copy of the state before the move is
 * the one thing this screen must never show.
 */
export function useTicket(id: string): UseQueryResult<TicketRead> {
  return useQuery({
    queryKey: ticketDetailKey(id),
    queryFn: ({ signal }) => fetchTicket(id, signal),
  })
}

/**
 * Everything a write on this screen has to do afterwards.
 *
 * The detail is refetched rather than patched from the response, for two reasons. The
 * transition response deliberately carries no SLA (WP-1.8), and moving a ticket to
 * Waiting parks the resolution clock and moves `dueAt` — so a screen that trusted the
 * response would show a deadline that had already changed. And the response carries no
 * new `ETag` for the *read*, which the next write needs. One refetch answers both.
 *
 * The queue is invalidated too: the row that was just moved is on it.
 */
function useTicketWrite<TArgs, TResult>(
  id: string,
  write: (args: TArgs) => Promise<TResult>,
): UseMutationResult<TResult, Error, TArgs> {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: write,
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketDetailKey(id) }),
        queryClient.invalidateQueries({ queryKey: ticketKeys.all }),
      ])
    },
  })
}

/** Moves a ticket to another status, conditional on the version it was read at. */
export function useChangeTicketStatus(
  id: string,
): UseMutationResult<
  TicketStatusChange,
  Error,
  { status: TicketStatus; resolutionNotes: string | null; etag: string | null }
> {
  return useTicketWrite(id, ({ status, resolutionNotes, etag }) =>
    changeTicketStatus(id, status, resolutionNotes, etag),
  )
}

/** Assigns, reassigns, or unassigns — one route for all three (WP-1.6). */
export function useAssignTicket(
  id: string,
): UseMutationResult<TicketAssignment, Error, { assigneeId: string | null; etag: string | null }> {
  return useTicketWrite(id, ({ assigneeId, etag }) => assignTicket(id, assigneeId, etag))
}

/** Posts a comment or an internal note. */
export function useAddTicketComment(
  id: string,
): UseMutationResult<TicketComment, Error, { body: string; isInternal: boolean }> {
  return useTicketWrite(id, ({ body, isInternal }) => addTicketComment(id, body, isInternal))
}

/** Attaches a file. */
export function useUploadTicketAttachment(
  id: string,
): UseMutationResult<TicketAttachment, Error, { file: File; isInternal: boolean }> {
  return useTicketWrite(id, ({ file, isInternal }) => uploadTicketAttachment(id, file, isInternal))
}

/**
 * Raises a ticket.
 *
 * The queue is invalidated but no detail is seeded: the reply is the detail projection,
 * and the screen navigates to it, where the query fetches it with its `ETag`. Seeding the
 * cache from a response that carried no tag would leave the first write on the new ticket
 * unable to state a precondition.
 */
export function useCreateTicket(): UseMutationResult<TicketDetail, Error, CreateTicketRequest> {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createTicket,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ticketKeys.all })
    },
  })
}
