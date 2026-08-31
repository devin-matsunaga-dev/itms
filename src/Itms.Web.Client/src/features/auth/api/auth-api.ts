import { apiFetch, ApiError } from '@/lib/api/client'
import type { AuthenticatedUser, LoginRequest } from '@/lib/api/types'

/** The auth endpoints the shell talks to (WP-0.5's `/api/v1/auth` group). */

/**
 * Who the caller is, or null when nobody is signed in.
 *
 * A 401 here is the answer to the question, not a failure: it is what the server says
 * when the cookie is missing, expired, or its session was revoked.
 */
export async function fetchCurrentUser(signal?: AbortSignal): Promise<AuthenticatedUser | null> {
  try {
    return await apiFetch<AuthenticatedUser>('/auth/me', signal ? { signal } : {})
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null
    }
    throw error
  }
}

/** Signs in. The session cookie is set by the response; nothing is stored by the client. */
export function login(request: LoginRequest): Promise<AuthenticatedUser> {
  return apiFetch<AuthenticatedUser>('/auth/login', { method: 'POST', body: request })
}

/** Revokes the current session server-side and clears the cookie. */
export function logout(): Promise<void> {
  return apiFetch<void>('/auth/logout', { method: 'POST' })
}
