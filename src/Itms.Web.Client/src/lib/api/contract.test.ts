import { describe, expect, it } from 'vitest'
// The committed document itself, imported the way the generator reads it. Vite resolves
// it at transform time, so the test needs no filesystem access and no Node type globals.
import contract from '../../../../Itms.Web.Host/openapi/v1.json'
import type { AuthenticatedUser, CsrfTokenResponse, LoginRequest, ProblemDetails } from './types'

/**
 * The contract, checked from the client's side.
 *
 * `tsc` already fails the build when a server type loses a field the shell reads — that
 * is what generating the types buys. What it cannot catch is the other half: `apiFetch`
 * takes a path as a plain string, so a client calling a route the server does not serve
 * type-checks perfectly and fails at runtime. These assertions read the same committed
 * document the types were generated from and check the routes as well as the shapes.
 */

interface OpenApiDocument {
  paths: Record<string, Record<string, { operationId?: string }>>
  components: { schemas: Record<string, { properties?: Record<string, unknown> }> }
}

const document = contract as unknown as OpenApiDocument

describe('the API contract', () => {
  // Every call auth-api.ts makes. A route renamed server-side fails here rather than in
  // the browser, which is the whole reason the document is committed next to the client.
  it.each([
    ['get', '/api/v1/auth/csrf', 'CsrfToken'],
    ['post', '/api/v1/auth/login', 'Login'],
    ['post', '/api/v1/auth/logout', 'Logout'],
    ['get', '/api/v1/auth/me', 'CurrentUser'],
  ])('serves %s %s as %s', (method, route, operationId) => {
    expect(document.paths[route]?.[method]?.operationId).toBe(operationId)
  })

  it('gives every operation a name, so the generated types stay stable', () => {
    const anonymous = Object.entries(document.paths).flatMap(([route, methods]) =>
      Object.entries(methods)
        .filter(([, operation]) => !operation.operationId)
        .map(([method]) => `${method.toUpperCase()} ${route}`),
    )

    expect(anonymous).toEqual([])
  })

  it('declares the problem-details extensions the client reads', () => {
    // `code` is what ApiError matches on and `errors` is what forms map onto fields.
    // Both are RFC 7807 extensions, so nothing but the document says they are there.
    const problem = document.components.schemas['ProblemDetails']?.properties ?? {}

    expect(Object.keys(problem)).toEqual(expect.arrayContaining(['code', 'errors']))
  })
})

/**
 * Compile-time only: each of these fails `tsc -b` if the generated type stops carrying a
 * member the shell depends on. They assert nothing at runtime, which is why the body is
 * empty — the assertion is that this file compiles at all.
 */
describe('the generated shapes', () => {
  it('still carry what the shell reads', () => {
    const user: Pick<AuthenticatedUser, 'id' | 'userName' | 'displayName' | 'roles'> =
      {} as AuthenticatedUser
    const credentials: Pick<LoginRequest, 'userName' | 'password'> = {} as LoginRequest
    const csrf: Pick<CsrfTokenResponse, 'token' | 'headerName'> = {} as CsrfTokenResponse
    const problem: Pick<ProblemDetails, 'code' | 'errors' | 'detail' | 'title'> =
      {} as ProblemDetails

    expect([user, credentials, csrf, problem]).toHaveLength(4)
  })
})
