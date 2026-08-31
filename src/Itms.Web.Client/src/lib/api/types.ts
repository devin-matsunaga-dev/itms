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
