import { Settings } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function AdministrationPage(): React.JSX.Element {
  return (
    <>
      <PageHeader title="Administration" subtitle="System configuration, reference data, and the audit trail." />
      <EmptyState
        icon={Settings}
        title="Nothing to administer yet"
        description="Administration screens and the audit viewer are built in Phase 5 (WP-5.8 and WP-5.9)."
      />
    </>
  )
}
