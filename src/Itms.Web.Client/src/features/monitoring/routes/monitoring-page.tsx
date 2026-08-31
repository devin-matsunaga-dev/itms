import { MonitorDot } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function MonitoringPage(): React.JSX.Element {
  return (
    <>
      <PageHeader title="Monitoring" subtitle="Reachability and health of the devices under watch." />
      <EmptyState
        icon={MonitorDot}
        title="Nothing is being monitored yet"
        description="Device monitoring is built in Phase 3 (WP-3.5)."
      />
    </>
  )
}
