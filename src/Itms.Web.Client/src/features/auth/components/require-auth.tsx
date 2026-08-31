import { Navigate, Outlet, useLocation } from 'react-router'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { FullPageLoading } from '@/components/common/full-page-loading'
import { ErrorState } from '@/components/common/error-state'

/**
 * The gate every application route sits behind. An unauthenticated visitor is sent to
 * the login page with the address they asked for, so signing in resumes where they
 * were rather than dumping them on the dashboard.
 *
 * This hides screens. It does not protect data: every endpoint evaluates its own
 * policy server-side (ARCHITECTURE.md §7).
 */
export function RequireAuth(): React.JSX.Element {
  const location = useLocation()
  const { data: user, isPending, isError, refetch } = useCurrentUser()

  if (isPending) {
    return <FullPageLoading label="Checking your session" />
  }

  // A failure here is not a signed-out visitor: `fetchCurrentUser` answers a 401 with
  // null, so anything that reaches this branch is the server being unreachable or
  // broken. Sending them to the login page would blame them for it.
  if (isError) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-canvas px-5">
        <div className="w-full max-w-[520px]">
          <ErrorState
            title="ITMS could not be reached"
            description="Your session could not be checked. The server may be restarting."
            onRetry={() => void refetch()}
          />
        </div>
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return <Outlet />
}
