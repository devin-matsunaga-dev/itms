import { ShieldAlert } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

/**
 * Shown when a signed-in account reaches a screen its role is not offered. The server
 * refuses the data independently; this is only what the person sees.
 */
export function ForbiddenPage(): React.JSX.Element {
  return (
    <>
      <PageHeader
        title="Not available to your role"
        subtitle="This screen belongs to a role your account does not hold."
      />
      <EmptyState
        icon={ShieldAlert}
        title="You do not have access to this screen"
        description="If you need it, ask an administrator to review your role."
      />
    </>
  )
}
