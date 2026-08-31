import { Navigate, useLocation, useNavigate } from 'react-router'
import { BrandMark } from '@/components/common/brand-mark'
import { organisationName, productDescriptor } from '@/lib/branding'
import { FullPageLoading } from '@/components/common/full-page-loading'
import { LoginForm } from '@/features/auth/components/login-form'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'

interface LoginLocationState {
  /** Where the visitor was headed before the gate sent them here. */
  from?: string
}

export function LoginPage(): React.JSX.Element {
  const navigate = useNavigate()
  const location = useLocation()
  const { data: user, isPending } = useCurrentUser()

  const state = location.state as LoginLocationState | null
  // A returned path is only ever one this application produced, and only ever a path:
  // an absolute URL here would make the login page an open redirect.
  const target = state?.from?.startsWith('/') === true && !state.from.startsWith('//')
    ? state.from
    : '/'

  if (isPending) {
    return <FullPageLoading label="Checking your session" />
  }

  if (user) {
    return <Navigate to={target} replace />
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-canvas px-5 py-10">
      <div className="w-full max-w-[420px]">
        <div className="mb-6 flex flex-col items-center gap-2 text-center">
          <BrandMark className="size-10" />
          <p className="text-brand font-bold text-balance text-heading">
            {organisationName}
          </p>
          <p className="text-label font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            {productDescriptor}
          </p>
        </div>

        <div className="rounded-card border border-border bg-surface p-6 shadow-card">
          <h1 className="text-section-title font-semibold text-heading">Sign in</h1>
          <p className="mt-1 mb-5 text-copy text-body">
            Use your ITMS account to continue.
          </p>
          <LoginForm onSignedIn={() => void navigate(target, { replace: true })} />
        </div>

        <p className="mt-4 text-center text-caption text-muted-foreground">
          Trouble signing in? Contact your IT administrator.
        </p>
      </div>
    </main>
  )
}
