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

/** One ticket in full — `GET /api/v1/tickets/{id}`, and what a creation answers with. */
export type TicketDetail = Schemas['TicketDetailResponse']

/** What a ticket looks like immediately after a status change. */
export type TicketStatusChange = Schemas['TicketStatusChangeResponse']

/** What a ticket looks like immediately after an assignment. */
export type TicketAssignment = Schemas['TicketAssignmentResponse']

/** One line of a ticket's conversation. */
export type TicketComment = Schemas['TicketCommentResponse']

/** A file attached to a ticket. Metadata only; the bytes come from the download route. */
export type TicketAttachment = Schemas['TicketAttachmentResponse']

/** One line of a ticket's timeline. */
export type TicketHistoryEntry = Schemas['TicketHistoryEntryResponse']

/** Which dimension of a ticket a history entry records having moved. */
export type TicketChangeKind = Schemas['TicketChangeKind']

/** The body of `POST /api/v1/tickets`. */
export type CreateTicketRequest = Schemas['CreateTicketRequest']

/** The queue's headline figures — `GET /api/v1/tickets/counters`. */
export type TicketCounters = Schemas['TicketCountersResponse']

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

/** One row of the asset register — `GET /api/v1/assets`. */
export type AssetListItem = Schemas['AssetListItemResponse']

/** The register's page envelope. */
export type PagedAssets = Schemas['PagedResultOfAssetListItemResponse']

/** One asset in full — `GET /api/v1/assets/{id}`. */
export type Asset = Schemas['AssetResponse']

/** The body of `POST /api/v1/assets`. */
export type CreateAssetRequest = Schemas['CreateAssetRequest']

/**
 * The body of `PUT /api/v1/assets/{id}` (WP-2.6b).
 *
 * A full replacement of the descriptive half of an asset, which is why it carries no tag,
 * no status, and no holder: the tag is immutable and the other two move through the
 * lifecycle routes.
 */
export type UpdateAssetRequest = Schemas['UpdateAssetRequest']

/** One line of an asset's timeline — `GET /api/v1/assets/{id}/history`. */
export type AssetHistoryEntry = Schemas['AssetHistoryEntryResponse']

/** A page of an asset's timeline. */
export type PagedAssetHistory = Schemas['PagedResultOfAssetHistoryEntryResponse']

/** Which dimension of an asset a history entry records having moved. */
export type AssetChangeKind = Schemas['AssetChangeKind']

/** A ticket as another module's screen sees it — `GET /api/v1/assets/{id}/tickets`. */
export type TicketSummary = Schemas['TicketSummary']

/** A page of an asset's support history. */
export type PagedTicketSummaries = Schemas['PagedResultOfTicketSummary']

/** What kind of thing an asset is, for the register's type filter. */
export type AssetType = Schemas['AssetTypeResponse']

/** Where an asset is in its life, for the register's status filter. */
export type AssetStatus = Schemas['AssetStatusResponse']

/** The columns the register may be ordered by. */
export type AssetSort = NonNullable<Schemas['AssetSort']>

/** A location in the directory tree, for the register's location filter. */
export type Location = Schemas['LocationResponse']

/** A page of the user directory — `GET /api/v1/users` (WP-2.7). */
export type PagedUsers = Schemas['PagedResultOfUserSummary']

/** The columns the user directory may be ordered by. */
export type UserSort = NonNullable<Schemas['UserSort']>

/** A page of departments — `GET /api/v1/departments`. */
export type PagedDepartments = Schemas['PagedResultOfDepartmentResponse']

/** The body of `POST /api/v1/departments`. */
export type CreateDepartmentRequest = Schemas['CreateDepartmentRequest']

/** The body of `PUT /api/v1/departments/{id}`. */
export type UpdateDepartmentRequest = Schemas['UpdateDepartmentRequest']

/** What a department still holds, before it is retired — `GET /api/v1/departments/{id}/usage`. */
export type DepartmentUsage = Schemas['DepartmentUsageResponse']

/** What a location still holds, before it is deleted — `GET /api/v1/locations/{id}/usage`. */
export type LocationUsage = Schemas['LocationUsageResponse']

/** One module's count within a usage breakdown. */
export type UsageCount = Schemas['UsageCountResponse']

/** A page of locations. */
export type PagedLocations = Schemas['PagedResultOfLocationResponse']

/** Which level of the hierarchy a location is. */
export type LocationKind = NonNullable<Schemas['LocationKind']>

/** The body of `POST /api/v1/locations`. */
export type CreateLocationRequest = Schemas['CreateLocationRequest']

/** The body of `PUT /api/v1/locations/{id}`. */
export type UpdateLocationRequest = Schemas['UpdateLocationRequest']

/** The body of `POST /api/v1/locations/{id}/move`. */
export type MoveLocationRequest = Schemas['MoveLocationRequest']

/** A ticket the user 360 lists — the same summary an asset's support history carries. */
export type UserTicketPage = Schemas['PagedResultOfTicketSummary']

/**
 * Whether a user's tickets are the open ones or the finished ones.
 *
 * Spelled here rather than taken from the contract, uniquely on this file. `TicketActivity`
 * carries no `JsonStringEnumConverter`, so the document types it as a bare integer and the
 * generated type is `number` — which would let any number through and say nothing about
 * which three the server accepts. The wire value is still the name: ASP.NET Core binds an
 * enum from a query string by name regardless of how it would be serialised in a body, and
 * `?state=Open` is the call the endpoint documents.
 */
export type TicketActivity = 'All' | 'Open' | 'Past'

/** Equipment a person holds — `GET /api/v1/users/{id}/assets`. */
export type AssetSummary = Schemas['AssetSummary']
