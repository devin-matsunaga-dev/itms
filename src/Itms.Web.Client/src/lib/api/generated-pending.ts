/**
 * TEMPORARY — DELETE AT WP-0.9.
 *
 * CONVENTIONS.md is explicit that API types are generated from OpenAPI and that a
 * hand-written one is a review failure. WP-0.9 is the package that produces the
 * OpenAPI document and the generator; until it lands, the shell cannot talk to the
 * auth endpoints at all without *some* declared shape.
 *
 * So this file exists under three constraints, and they are the whole reason it is
 * allowed to exist:
 *
 *   1. It holds exactly the four shapes the auth flow needs and nothing else. No
 *      ticket, asset, user-directory, or department type may be added here — a
 *      package that needs one waits for the generator or brings it forward.
 *   2. Every type is transcribed from a named server type, cited below, so the
 *      generator's output can be diffed against it.
 *   3. WP-0.9 deletes this file and re-points the imports at the generated module.
 *      It is listed in STATUS.md as owed work.
 */

/**
 * `Itms.Modules.Identity.Security.CsrfTokenResponse` — `GET /api/v1/auth/csrf`.
 * The header name travels with the token so the client never hard-codes it.
 */
export interface CsrfTokenResponse {
  token: string
  headerName: string
}

/** `Itms.Modules.Identity.Features.Auth.Login.LoginRequest` — `POST /api/v1/auth/login`. */
export interface LoginRequest {
  userName: string
  password: string
}

/**
 * `Itms.Modules.Identity.Features.Auth.AuthenticatedUserResponse` — returned by both
 * `/login` and `/me`, so "who am I" has one shape however the client got there.
 */
export interface AuthenticatedUser {
  id: string
  userName: string
  email: string
  displayName: string
  /** Used to hide what a role cannot use. Hiding is never the enforcement (ARCHITECTURE.md §7). */
  roles: string[]
  departmentId: string | null
  locationId: string | null
}

/**
 * RFC 7807, as `Itms.Platform.Http.ProblemDetailsMapper` writes it: every error in this
 * system is one of these. `code` is the machine-readable extension the mapper adds;
 * `errors` is present only on a validation failure, keyed by camel-cased field name.
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  code?: string
  errors?: Record<string, string[]>
}
