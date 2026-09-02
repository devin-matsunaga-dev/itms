import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import type { Department } from '@/lib/api/types'
import { useDepartmentUsage } from '../hooks/use-directory'
import { useSetDepartmentActive } from '../hooks/use-directory-write'
import { UsageBreakdown } from './usage-breakdown'

interface DepartmentRetireDialogProps {
  /** The department being retired or brought back, or null when the dialog is closed. */
  department: Department | null
  onOpenChange: (open: boolean) => void
}

/**
 * Retiring a department, and bringing one back (WP-0.6, WP-2.4).
 *
 * **There is no delete, and this is where that shows.** A department is named by tickets,
 * assets, and people that all outlive it, and none of those references is a foreign key the
 * database could protect — so WP-0.6 made departments retire-only and WP-2.4 left that
 * standing. Retiring takes the department out of every picker and leaves every record that
 * names it untouched.
 *
 * The usage read is what makes that decision an informed one: it says how many tickets,
 * assets, and people still point at this department before anybody clicks. Unlike a
 * location's, it **reports and never refuses** — retiring is reversible, and the button
 * beside this text is the reversal.
 */
export function DepartmentRetireDialog({
  department,
  onOpenChange,
}: DepartmentRetireDialogProps): React.JSX.Element {
  const retiring = department?.isActive ?? false
  const usage = useDepartmentUsage(retiring ? (department?.id ?? null) : null)
  const setActive = useSetDepartmentActive()

  return (
    <Dialog open={department !== null} onOpenChange={onOpenChange}>
      <DialogContent>
        {department === null ? null : (
          <>
            <DialogHeader>
              <DialogTitle>
                {retiring ? `Retire ${department.name}?` : `Bring back ${department.name}?`}
              </DialogTitle>
              <DialogDescription>
                {retiring
                  ? 'It stops being offered on new tickets, assets, and people. Everything already recorded against it is kept.'
                  : 'It is offered again wherever a department can be chosen.'}
              </DialogDescription>
            </DialogHeader>

            {retiring ? (
              <div className="flex flex-col gap-2">
                <p className="text-label font-semibold tracking-[0.06em] text-primary uppercase">
                  Still referenced by
                </p>
                {usage.isPending ? (
                  <Skeleton className="h-12 w-full" aria-label="Loading the usage" />
                ) : usage.isError ? (
                  <p role="alert" className="text-copy text-body">
                    The reference counts could not be read. Retiring is still safe — nothing
                    that names this department is changed.
                  </p>
                ) : (
                  <UsageBreakdown
                    references={usage.data.references}
                    emptyMessage="Nothing references this department."
                  />
                )}
              </div>
            ) : null}

            <DialogFooter>
              <Button
                variant="outline"
                disabled={setActive.isPending}
                onClick={() => {
                  onOpenChange(false)
                }}
              >
                Cancel
              </Button>
              <Button
                variant={retiring ? 'destructive' : 'default'}
                disabled={setActive.isPending}
                onClick={() => {
                  setActive.mutate(
                    { id: department.id, active: !retiring },
                    {
                      onSuccess: () => {
                        toast.success(
                          retiring ? `${department.name} retired.` : `${department.name} is active again.`,
                        )
                        onOpenChange(false)
                      },
                      onError: (error: unknown) => {
                        toast.error('That could not be done.', {
                          description: error instanceof Error ? error.message : undefined,
                        })
                      },
                    },
                  )
                }}
              >
                {retiring ? 'Retire' : 'Bring back'}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
