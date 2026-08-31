/**
 * Names for the shapes in `generated.ts`.
 *
 * Nothing here declares a type: every alias resolves into the generated module, which is
 * produced from `src/Itms.Web.Host/openapi/v1.json` by `npm run generate:api`. A server
 * change therefore reaches the call sites through these names — which is the point, and
 * why aliasing is not the hand-written API type CONVENTIONS.md forbids.
 *
 * They exist because `components['schemas']['AuthenticatedUserResponse']` at forty call
 * sites reads badly and would have to be edited at all forty if the server type were ever
 * renamed. Add an alias here when a feature starts using a shape; do not add a shape.
 */

import type { components, operations } from './generated'

/** Every schema the API document declares, by its server-side name. */
export type Schemas = components['schemas']

/** Every operation the API document declares, by its `operationId`. */
export type Operations = operations

/** The account the caller is signed in as — `GET /api/v1/auth/me`, and `/login`'s reply. */
export type AuthenticatedUser = Schemas['AuthenticatedUserResponse']

/** Credentials for `POST /api/v1/auth/login`. */
export type LoginRequest = Schemas['LoginRequest']

/** The antiforgery token and the header it belongs in — `GET /api/v1/auth/csrf`. */
export type CsrfTokenResponse = Schemas['CsrfTokenResponse']

/**
 * RFC 7807. Every error in this system is one of these (ARCHITECTURE.md §6).
 *
 * `code` is the machine-readable extension `ProblemDetailsMapper` adds, and is what
 * handlers match on — never the message text, which is human copy and will change.
 * `errors` is present only on a validation failure, keyed by camel-cased field name.
 */
export type ProblemDetails = Schemas['ProblemDetails']

/** One row of the ticket queue — `GET /api/v1/tickets`. */
export type TicketListItem = Schemas['TicketListItemResponse']

/** The queue's page envelope. */
export type PagedTickets = Schemas['PagedResultOfTicketListItemResponse']

/** A ticket's two SLA clocks, and where each stands right now. */
export type TicketSla = Schemas['TicketSlaResponse']

/** Where a ticket sits in the workflow (SPEC.md §2). */
export type TicketStatus = Schemas['TicketStatus']

/** Where one of a ticket's SLA clocks stands. */
export type SlaState = Schemas['SlaState']

/** The columns the queue may be ordered by. */
export type TicketSort = NonNullable<Schemas['TicketSort']>

/** Which way an ordering runs. */
export type SortDirection = NonNullable<Schemas['SortDirection']>

/** A ticket category, for the queue's category filter. */
export type TicketCategory = Schemas['TicketCategoryResponse']

/** A ticket priority, for the queue's priority filter. */
export type TicketPriority = Schemas['TicketPriorityResponse']

/** A department, for the queue's department filter. */
export type Department = Schemas['DepartmentResponse']

/** A person, as every picker in the system sees them. */
export type UserSummary = Schemas['UserSummary']
