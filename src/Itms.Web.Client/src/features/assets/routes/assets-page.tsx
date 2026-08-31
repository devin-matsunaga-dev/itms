import { HardDrive } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function AssetsPage(): React.JSX.Element {
  return (
    <>
      <PageHeader title="Assets" subtitle="The hardware, software, and licences on record." />
      <EmptyState
        icon={HardDrive}
        title="No asset register yet"
        description="The asset list and detail are built in Phase 2 (WP-2.4)."
      />
    </>
  )
}
