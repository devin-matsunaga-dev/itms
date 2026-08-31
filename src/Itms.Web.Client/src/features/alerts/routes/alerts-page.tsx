import { Bell } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function AlertsPage(): React.JSX.Element {
  return (
    <>
      <PageHeader title="Alerts" subtitle="What the monitoring poller has raised, newest first." />
      <EmptyState
        icon={Bell}
        title="No alerts yet"
        description="The alert feed and alert-to-ticket flow are built in Phase 3 (WP-3.7)."
      />
    </>
  )
}
