import { apiFetch, apiRequest } from '@/lib/api/client'
import type {
  CreateTicketRequest,
  Department,
  PagedTickets,
  TicketAssignment,
  TicketAttachment,
  TicketCategory,
  TicketComment,
  TicketDetail,
  TicketPriority,
  TicketStatus,
  TicketStatusChange,
  UserSummary,
} from '@/lib/api/types'
import { serializeTicketQuery, type TicketQuery } from '../lib/ticket-query'

/** The helpdesk endpoints the queue and detail screens read (WP-1.5 through WP-1.8). */

/**
 * A page of the ticket queue.
 *
 * The query is serialized by the same function that writes the address bar, so what the
 * screen asks the server for and what the URL claims it is showing cannot drift.
 */
export function fetchTickets(query: TicketQuery, signal?: AbortSignal): Promise<PagedTickets> {
  const params = serializeTicketQuery(query)
  // The URL omits page 1 to keep a first-page link tidy; the API is told explicitly.
  params.set('page', String(query.page))

  return apiFetch<PagedTickets>(`/tickets?${params.toString()}`, signal ? { signal } : {})
}

/** Active ticket categories, for the category filter. */
export async function fetchTicketCategories(signal?: AbortSignal): Promise<TicketCategory[]> {
  const page = await apiFetch<{ items: TicketCategory[] }>(
    '/ticket-categories?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/** Active ticket priorities, rank first, for the priority filter. */
export async function fetchTicketPriorities(signal?: AbortSignal): Promise<TicketPriority[]> {
  const page = await apiFetch<{ items: TicketPriority[] }>(
    '/ticket-priorities?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/** Active departments, for the department filter. */
export async function fetchDepartments(signal?: AbortSignal): Promise<Department[]> {
  const page = await apiFetch<{ items: Department[] }>(
    '/departments?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * People who can hold a ticket, for the assignee filter.
 *
 * The endpoint is Technician-guarded, so this is never called for an end user — their
 * queue is their own tickets and an assignee filter would answer nothing they can ask.
 */
export function fetchAssignableUsers(signal?: AbortSignal): Promise<UserSummary[]> {
  return apiFetch<UserSummary[]>('/users?limit=200', signal ? { signal } : {})
}

/** One ticket, with the version tag a later write sends back as its precondition. */
export interface TicketRead {
  readonly ticket: TicketDetail
  /** The response's `ETag`, or null if it carried none. Opaque — never parsed. */
  readonly etag: string | null
}

/**
 * One ticket in full.
 *
 * The `ETag` is kept because every write on this screen sends it back as `If-Match`:
 * ARCHITECTURE.md §6 asks for optimistic concurrency on tickets, and WP-1.5 and WP-1.6
 * built the header surface for exactly this screen. A caller holding a stale copy is
 * refused with 412 before the transition is attempted, instead of losing a race and
 * finding out through a 409.
 */
export async function fetchTicket(id: string, signal?: AbortSignal): Promise<TicketRead> {
  const result = await apiRequest<TicketDetail>(`/tickets/${id}`, signal ? { signal } : {})
  return { ticket: result.data, etag: result.etag }
}

/** Raises a ticket. The reply is the detail projection, so the screen can go straight to it. */
export function createTicket(request: CreateTicketRequest): Promise<TicketDetail> {
  return apiFetch<TicketDetail>('/tickets', { method: 'POST', body: request })
}

/**
 * Moves a ticket to another status.
 *
 * `resolutionNotes` is required and non-blank when the destination is `Resolved`, and
 * rejected for every other destination (WP-1.3) — so it is sent as null unless resolving.
 */
export function changeTicketStatus(
  id: string,
  status: TicketStatus,
  resolutionNotes: string | null,
  etag: string | null,
): Promise<TicketStatusChange> {
  return apiFetch<TicketStatusChange>(`/tickets/${id}/status-changes`, {
    method: 'POST',
    body: { status, resolutionNotes },
    ...ifMatch(etag),
  })
}

/**
 * Assigns, reassigns, or unassigns a ticket.
 *
 * One route for all three (WP-1.6): a null `assigneeId` is a deliberate instruction to
 * unassign, not an omitted field.
 */
export function assignTicket(
  id: string,
  assigneeId: string | null,
  etag: string | null,
): Promise<TicketAssignment> {
  return apiFetch<TicketAssignment>(`/tickets/${id}/assignments`, {
    method: 'POST',
    body: { assigneeId },
    ...ifMatch(etag),
  })
}

/** Posts a comment, or an internal note, on a ticket. */
export function addTicketComment(
  ticketId: string,
  body: string,
  isInternal: boolean,
): Promise<TicketComment> {
  return apiFetch<TicketComment>(`/tickets/${ticketId}/comments`, {
    method: 'POST',
    body: { body, isInternal },
  })
}

/**
 * Attaches a file to a ticket.
 *
 * Multipart, so the body is a `FormData` the browser writes the boundary for. The
 * extension allowlist and the size cap are the server's (WP-1.7) and are deliberately
 * not restated here — a refusal comes back as a problem document naming what is
 * accepted, which is one policy in one place rather than two that can disagree.
 */
export function uploadTicketAttachment(
  ticketId: string,
  file: File,
  isInternal: boolean,
): Promise<TicketAttachment> {
  const form = new FormData()
  form.append('file', file)
  form.append('isInternal', String(isInternal))

  return apiFetch<TicketAttachment>(`/tickets/${ticketId}/attachments`, {
    method: 'POST',
    body: form,
  })
}

/**
 * Where an attachment's bytes live.
 *
 * A plain same-origin URL: the session is a cookie, so an ordinary link carries it, and
 * the endpoint answers with `Content-Disposition: attachment` and `nosniff`. It
 * re-checks the ticket and the audience on every request, so a link that leaks tells the
 * holder nothing.
 */
export function attachmentDownloadUrl(ticketId: string, attachmentId: string): string {
  return `/api/v1/tickets/${ticketId}/attachments/${attachmentId}`
}

/** The precondition header, when a version is in hand. */
function ifMatch(etag: string | null): { headers?: Record<string, string> } {
  return etag === null ? {} : { headers: { 'If-Match': etag } }
}
