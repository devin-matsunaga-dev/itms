import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiFetch, onUnauthorized, resetCsrfToken } from '@/lib/api/client'

/** A JSON response, as the host would send it. */
function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

/** An RFC 7807 document, as `ProblemDetailsMapper` writes it. */
function problemResponse(status: number, code: string, detail: string, errors?: Record<string, string[]>): Response {
  return new Response(
    JSON.stringify({ status, code, detail, title: 'Bad Request', ...(errors ? { errors } : {}) }),
    { status, headers: { 'content-type': 'application/problem+json' } },
  )
}

const csrf = { token: 'token-value', headerName: 'X-CSRF-TOKEN' }

let fetchMock: ReturnType<typeof vi.fn>

beforeEach(() => {
  resetCsrfToken()
  fetchMock = vi.fn()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('apiFetch', () => {
  it('sends a read without fetching an antiforgery token', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: '1' }))

    await apiFetch('/auth/me')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/v1/auth/me')
    expect(init.credentials).toBe('same-origin')
    expect(init.headers).not.toHaveProperty('X-CSRF-TOKEN')
  })

  it('fetches a token and echoes it in the header the server named', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(csrf)).mockResolvedValueOnce(jsonResponse({ ok: true }))

    await apiFetch('/auth/login', { method: 'POST', body: { userName: 'admin' } })

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/auth/csrf')
    const [, init] = fetchMock.mock.calls[1] as [string, RequestInit]
    const headers = init.headers as Record<string, string>
    expect(headers['X-CSRF-TOKEN']).toBe('token-value')
    expect(init.body).toBe(JSON.stringify({ userName: 'admin' }))
  })

  it('reuses the token across mutations rather than fetching one each time', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(csrf))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))

    await apiFetch('/auth/login', { method: 'POST', body: {} })
    await apiFetch('/auth/logout', { method: 'POST' })

    const csrfCalls = fetchMock.mock.calls.filter((call) => call[0] === '/api/v1/auth/csrf')
    expect(csrfCalls).toHaveLength(1)
  })

  it('refreshes the token once when the server rejects it, then succeeds', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(csrf))
      .mockResolvedValueOnce(
        problemResponse(400, 'auth.antiforgery_failed', 'The request did not carry a valid antiforgery token.'),
      )
      .mockResolvedValueOnce(jsonResponse({ token: 'fresh', headerName: 'X-CSRF-TOKEN' }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))

    await apiFetch('/auth/login', { method: 'POST', body: {} })

    const headers = (fetchMock.mock.calls[3] as [string, RequestInit])[1].headers as Record<string, string>
    expect(headers['X-CSRF-TOKEN']).toBe('fresh')
  })

  it('does not retry a validation failure that is not about the token', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(csrf))
      .mockResolvedValueOnce(
        problemResponse(400, 'validation.failed', 'One or more fields are invalid.', {
          userName: ['Enter your user name or email address.'],
        }),
      )

    const error = await apiFetch('/auth/login', { method: 'POST', body: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).fieldErrors).toEqual({
      userName: ['Enter your user name or email address.'],
    })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('carries the problem document code onto the error', async () => {
    fetchMock.mockResolvedValueOnce(
      problemResponse(401, 'auth.locked_out', 'This account is temporarily locked.'),
    )

    const error = (await apiFetch('/auth/me').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(401)
    expect(error.code).toBe('auth.locked_out')
    expect(error.message).toBe('This account is temporarily locked.')
  })

  it('still produces an ApiError when the body is not a problem document', async () => {
    fetchMock.mockResolvedValueOnce(new Response('<html>gateway</html>', { status: 502 }))

    const error = (await apiFetch('/auth/me').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(502)
    expect(error.problem).toBeNull()
  })

  it('notifies listeners on a 401 so the whole application can react', async () => {
    const listener = vi.fn()
    const unsubscribe = onUnauthorized(listener)
    fetchMock.mockResolvedValueOnce(problemResponse(401, 'auth.not_signed_in', 'You are not signed in.'))

    await apiFetch('/auth/me').catch(() => undefined)

    expect(listener).toHaveBeenCalledOnce()
    unsubscribe()
  })

  it('returns undefined for a 204 rather than trying to parse a body', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(csrf)).mockResolvedValueOnce(new Response(null, { status: 204 }))

    await expect(apiFetch('/auth/logout', { method: 'POST' })).resolves.toBeUndefined()
  })
})
