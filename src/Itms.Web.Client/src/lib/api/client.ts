import type { CsrfTokenResponse, ProblemDetails } from './generated-pending'

/**
 * The one place a request leaves this application. Everything else calls through here,
 * so cookie credentials, the antiforgery token, and RFC 7807 error handling are decided
 * once rather than per feature.
 */

/** Everything hangs off the versioned prefix; the dev server proxies it to the host. */
const apiRoot = '/api/v1'

/** Methods that change state, and so need an antiforgery token. */
const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

/**
 * A failed response, carrying the problem document the server sent. Handlers read
 * `status` and `code` rather than matching on message text, which is human copy and
 * will change.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
    this.code = problem?.code
  }

  /** Per-field messages from a validation failure, ready to map onto form fields. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {}
  }
}

/**
 * The antiforgery token, held in memory for the life of the tab. It is deliberately not
 * in `localStorage`: it is a per-session secret, and the companion cookie it is checked
 * against does not outlive the browser either.
 */
let csrfToken: CsrfTokenResponse | null = null
let csrfRequest: Promise<CsrfTokenResponse> | null = null

/** Drops the cached token. Called after an antiforgery rejection, and by tests. */
export function resetCsrfToken(): void {
  csrfToken = null
  csrfRequest = null
}

async function getCsrfToken(): Promise<CsrfTokenResponse> {
  if (csrfToken) {
    return csrfToken
  }

  // Concurrent mutations must not each fetch a token; the second waits on the first.
  csrfRequest ??= (async () => {
    const response = await fetch(`${apiRoot}/auth/csrf`, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
      csrfRequest = null
      throw await toApiError(response)
    }

    csrfToken = (await response.json()) as CsrfTokenResponse
    return csrfToken
  })()

  return csrfRequest
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | null = null

  // ARCHITECTURE.md §6 promises a problem document on every error, but a proxy or a
  // dropped connection can still produce a body that is not one.
  if (response.headers.get('content-type')?.includes('json')) {
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      problem = null
    }
  }

  const message =
    problem?.detail ??
    problem?.title ??
    `The request failed with status ${String(response.status)}.`

  return new ApiError(response.status, problem, message)
}

/** Listeners notified when the server says the caller is no longer signed in. */
const unauthorizedListeners = new Set<() => void>()

/**
 * Registers a listener for a 401 on any call. A session can end between requests — it
 * was revoked, it expired, the password changed elsewhere — and every screen has to
 * react to that, not just the one that happened to make the call.
 */
export function onUnauthorized(listener: () => void): () => void {
  unauthorizedListeners.add(listener)
  return () => unauthorizedListeners.delete(listener)
}

export interface ApiRequest {
  method?: string
  /** Serialized as JSON. Omit for a body-less request. */
  body?: unknown
  signal?: AbortSignal
}

/**
 * Issues a request against the API and returns the parsed body.
 *
 * @typeParam T The response shape, from the generated (today: pending) contract types.
 * @param path Path below `/api/v1`, starting with a slash.
 * @throws ApiError when the response is not a success status.
 */
export async function apiFetch<T>(path: string, request: ApiRequest = {}): Promise<T> {
  const method = (request.method ?? 'GET').toUpperCase()
  const headers: Record<string, string> = { Accept: 'application/json' }

  if (request.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (unsafeMethods.has(method)) {
    const token = await getCsrfToken()
    headers[token.headerName] = token.token
  }

  const send = (): Promise<Response> =>
    fetch(`${apiRoot}${path}`, {
      method,
      headers,
      // The session is a cookie. Nothing is read from or written to browser storage.
      credentials: 'same-origin',
      ...(request.body === undefined ? {} : { body: JSON.stringify(request.body) }),
      ...(request.signal ? { signal: request.signal } : {}),
    })

  let response = await send()

  // A token outlives its cookie when the browser drops the companion cookie, and the
  // server rejects it with a known code. One silent retry with a fresh token is right;
  // a second would be a loop.
  if (response.status === 400 && unsafeMethods.has(method)) {
    const error = await toApiError(response)
    if (error.code !== 'auth.antiforgery_failed') {
      throw error
    }

    resetCsrfToken()
    const token = await getCsrfToken()
    headers[token.headerName] = token.token
    response = await send()
  }

  if (response.status === 401) {
    for (const listener of unauthorizedListeners) {
      listener()
    }
  }

  if (!response.ok) {
    throw await toApiError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
