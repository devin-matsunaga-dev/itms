import { BookOpen } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function KnowledgeBasePage(): React.JSX.Element {
  return (
    <>
      <PageHeader title="Knowledge Base" subtitle="Procedures and fixes worth writing down once." />
      <EmptyState
        icon={BookOpen}
        title="No articles yet"
        description="Article browsing and authoring are built in Phase 4 (WP-4.2)."
      />
    </>
  )
}
