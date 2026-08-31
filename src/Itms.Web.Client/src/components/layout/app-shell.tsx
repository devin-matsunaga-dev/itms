import { useState } from 'react'
import { Outlet, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { Sidebar } from '@/components/layout/sidebar'
import { Topbar } from '@/components/layout/topbar'
import { FullPageLoading } from '@/components/common/full-page-loading'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { useLogout } from '@/features/auth/hooks/use-logout'

/**
 * The persistent three-part frame every signed-in page renders inside (DESIGN.md §3).
 * It is a layout route, so the sidebar and topbar are not remounted on navigation and
 * the collapsed state survives moving between screens.
 */
export function AppShell(): React.JSX.Element {
  const navigate = useNavigate()
  const [collapsed, setCollapsed] = useState(false)
  const { data: user } = useCurrentUser()
  const signOut = useLogout()

  // RequireAuth has already established there is a user; this is the render between
  // its decision and the cache settling.
  if (!user) {
    return <FullPageLoading label="Loading your workspace" />
  }

  return (
    <div className="flex min-h-screen bg-canvas">
      <Sidebar
        roles={user.roles}
        collapsed={collapsed}
        onToggleCollapsed={() => setCollapsed((value) => !value)}
      />

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar
          user={user}
          signingOut={signOut.isPending}
          onSearch={() => {
            toast.info('Global search arrives with the search module.')
          }}
          onSignOut={() => {
            signOut.mutate(undefined, {
              onSettled: () => void navigate('/login', { replace: true }),
            })
          }}
        />

        {/* Page padding 32px (DESIGN.md §2). Content narrower than 1280px scrolls
            horizontally rather than reflowing (§6). */}
        <main className="min-w-0 flex-1 overflow-x-auto p-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
