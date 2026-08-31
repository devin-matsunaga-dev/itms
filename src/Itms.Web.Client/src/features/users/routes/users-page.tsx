import { Users } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function UsersPage(): React.JSX.Element {
  return (
    <>
      <PageHeader title="Users" subtitle="The people the helpdesk and the asset register refer to." />
      <EmptyState
        icon={Users}
        title="No user directory yet"
        description="The directory screen is built in Phase 2 (WP-2.6); user administration is WP-5.8."
      />
    </>
  )
}
