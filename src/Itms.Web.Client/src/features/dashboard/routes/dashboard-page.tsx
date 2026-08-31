import { LayoutDashboard } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'

export function DashboardPage(): React.JSX.Element {
  const { data: user } = useCurrentUser()
  const firstName = user?.displayName.split(' ')[0] ?? 'there'

  return (
    <>
      <PageHeader
        title={`Welcome back, ${firstName}`}
        subtitle="Here's what's happening with your IT environment today."
      />
      <EmptyState
        icon={LayoutDashboard}
        title="The dashboard fills in as the modules arrive"
        description="KPI cards, the ticket and asset donuts, recent alerts, and upcoming expirations are built in Phase 5 (WP-5.1 to WP-5.3)."
      />
    </>
  )
}
