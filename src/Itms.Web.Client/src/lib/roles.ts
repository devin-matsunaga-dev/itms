/**
 * The three roles the system has (ARCHITECTURE.md §7, SPEC.md §14), mirrored from
 * `Itms.Platform.Identity.ItmsRoles` so the client never spells one as a loose string.
 *
 * These decide what the interface *offers*. They decide nothing about access: every
 * endpoint evaluates its own policy server-side, and a role hidden here is still
 * enforced there.
 */
export const Roles = {
  admin: 'Admin',
  technician: 'Technician',
  user: 'User',
} as const

export type Role = (typeof Roles)[keyof typeof Roles]

/** True when the account holds at least one of `allowed`. An empty list allows anyone. */
export function hasAnyRole(roles: readonly string[], allowed: readonly Role[]): boolean {
  if (allowed.length === 0) {
    return true
  }
  return allowed.some((role) => roles.includes(role))
}

/** The role shown under a person's name in the topbar: the most privileged they hold. */
export function primaryRole(roles: readonly string[]): string {
  if (roles.includes(Roles.admin)) {
    return 'Administrator'
  }
  if (roles.includes(Roles.technician)) {
    return 'Technician'
  }
  if (roles.includes(Roles.user)) {
    return 'User'
  }
  return 'No role assigned'
}

/** `John Santos` → `JS`, for the avatar fallback. */
export function initials(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) {
    return '?'
  }

  const first = parts[0] ?? ''
  const last = parts.length > 1 ? (parts[parts.length - 1] ?? '') : ''
  return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase()
}
