import { Plus, Ticket } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { Button } from '@/components/ui/button'

export function TicketsPage(): React.JSX.Element {
  // The create form is WP-1.10. Until it exists the button says so rather than
  // navigating to a screen that is not there.
  const newTicket = () => {
    toast.info('Ticket creation arrives with the helpdesk module.')
  }

  return (
    <>
      <PageHeader
        title="Tickets"
        subtitle="Every request raised across the organisation."
        actions={
          <Button onClick={newTicket}>
            <Plus className="size-4" aria-hidden="true" />
            New Ticket
          </Button>
        }
      />
      <EmptyState
        icon={Ticket}
        title="No ticket queue yet"
        description="The queue, filters, and ticket detail are built in Phase 1 (WP-1.9 and WP-1.10)."
        action={{ label: 'New Ticket', onClick: newTicket }}
      />
    </>
  )
}
