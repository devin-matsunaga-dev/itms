import { FileQuestion } from 'lucide-react'
import { useNavigate } from 'react-router'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'

export function NotFoundPage(): React.JSX.Element {
  const navigate = useNavigate()

  return (
    <>
      <PageHeader
        title="Page not found"
        subtitle="The address you asked for does not exist in ITMS."
      />
      <EmptyState
        icon={FileQuestion}
        title="Nothing lives at this address"
        description="Check the link, or go back to the dashboard."
        action={{ label: 'Go to dashboard', onClick: () => void navigate('/') }}
      />
    </>
  )
}
