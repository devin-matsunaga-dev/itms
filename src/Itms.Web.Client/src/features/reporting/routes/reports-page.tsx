import { FileText } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function ReportsPage(): React.JSX.Element {
  return (
    <>
      <PageHeader title="Reports" subtitle="Operational reports across tickets, assets, and uptime." />
      <EmptyState
        icon={FileText}
        title="No reports yet"
        description="Operational reports and CSV export are built in Phase 5 (WP-5.5 and WP-5.6)."
      />
    </>
  )
}
