import type {
  TicketAttachment,
  TicketComment,
  TicketDetail,
  TicketHistoryEntry,
  TicketSla,
} from '@/lib/api/types'

/**
 * One shape of a ticket detail, shared by the tests that read it.
 *
 * A factory rather than a literal per test file: the payload has forty fields and two
 * copies of it would drift, which is exactly how a test ends up asserting against a shape
 * the server stopped sending.
 */

export const ticketId = 'ticket-1'
export const requesterId = 'requester-1'
export const technicianId = '11111111-1111-1111-1111-111111111111'

export function sla(overrides: Partial<TicketSla> = {}): TicketSla {
  return {
    responseTargetMinutes: 30,
    responseDueAt: '2026-09-01T09:30:00Z',
    responseWarnAt: '2026-09-01T09:24:00Z',
    respondedAt: null,
    resolutionTargetMinutes: 480,
    resolutionDueAt: '2026-09-01T17:00:00Z',
    resolutionWarnAt: '2026-09-01T15:24:00Z',
    resolvedAt: null,
    pausedAt: null,
    responseState: 'Pending',
    resolutionState: 'Pending',
    isPaused: false,
    pausedSeconds: 0,
    ...overrides,
  }
}

export function historyEntry(overrides: Partial<TicketHistoryEntry> = {}): TicketHistoryEntry {
  return {
    id: 'history-1',
    kind: 'Status',
    fromValue: 'New',
    toValue: 'Assigned',
    occurredAt: '2026-09-01T10:00:00Z',
    sequence: 0,
    actorId: technicianId,
    actorName: 'Mark Reyes',
    ...overrides,
  }
}

export function comment(overrides: Partial<TicketComment> = {}): TicketComment {
  return {
    id: 'comment-1',
    ticketId,
    body: 'Have you tried the other network?',
    isInternal: false,
    authorId: technicianId,
    authorName: 'Mark Reyes',
    createdAt: '2026-09-01T11:00:00Z',
    ...overrides,
  }
}

export function attachment(overrides: Partial<TicketAttachment> = {}): TicketAttachment {
  return {
    id: 'attachment-1',
    ticketId,
    fileName: 'screenshot.png',
    contentType: 'image/png',
    byteLength: 20_480,
    isInternal: false,
    uploadedById: requesterId,
    uploadedByName: 'Jane Doe',
    createdAt: '2026-09-01T09:05:00Z',
    ...overrides,
  }
}

export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: ticketId,
    number: 'TKT-0001',
    subject: 'Laptop will not connect to Wi-Fi',
    description: 'It drops the connection every few minutes in the east wing.',
    status: 'New',
    categoryId: 'cat-network',
    categoryName: 'Network',
    priorityId: 'pri-high',
    priorityName: 'High',
    priorityCode: 'high',
    priorityRank: 2,
    requesterId,
    requesterName: 'Jane Doe',
    departmentId: 'dep-it',
    departmentName: 'Information Technology',
    assigneeId: null,
    assigneeName: null,
    resolutionNotes: null,
    holdReason: null,
    resolvedAt: null,
    closedAt: null,
    relatedAssetId: null,
    relatedAlertId: null,
    createdAt: '2026-09-01T09:00:00Z',
    updatedAt: '2026-09-01T09:00:00Z',
    dueAt: '2026-09-01T17:00:00Z',
    sla: sla(),
    allowedNextStatuses: ['Assigned', 'Cancelled'],
    history: [],
    hasMoreHistory: false,
    comments: [],
    hasMoreComments: false,
    attachments: [],
    hasMoreAttachments: false,
    ...overrides,
  }
}
