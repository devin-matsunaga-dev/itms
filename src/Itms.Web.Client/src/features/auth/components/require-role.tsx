import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { hasAnyRole, type Role } from '@/lib/roles'
import { ForbiddenPage } from '@/routes/forbidden-page'

interface RequireRoleProps {
  /** Roles allowed here. Empty means any signed-in account. */
  allowed: readonly Role[]
  children: React.ReactNode
}

/**
 * Renders `children` only for a role the screen is meant for, and an explanation
 * otherwise. It is the same rule the sidebar filters on, applied to a typed address —
 * a hidden nav item is not protection, and neither is this one: the server refuses the
 * data regardless.
 */
export function RequireRole({ allowed, children }: RequireRoleProps): React.JSX.Element {
  const { data: user } = useCurrentUser()

  if (!user || !hasAnyRole(user.roles, allowed)) {
    return <ForbiddenPage />
  }

  return <>{children}</>
}
