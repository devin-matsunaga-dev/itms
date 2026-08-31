import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { Department, TicketCategory, TicketPriority, UserSummary } from '@/lib/api/types'
import { slaLabels, slaStateOrder, statusLabels, statusOrder } from '../lib/ticket-display'
import {
  dayEnd,
  dayStart,
  hasActiveFilters,
  toDateInput,
  type TicketQuery,
} from '../lib/ticket-query'

/** The value a "no filter on this" option carries. Empty string is a legitimate id. */
const any = '__any__'

/** The assignee select's "nobody holds it" option, which is a filter rather than an absence. */
const nobody = '__unassigned__'

interface TicketFiltersProps {
  query: TicketQuery
  categories: readonly TicketCategory[]
  priorities: readonly TicketPriority[]
  departments: readonly Department[]
  assignees: readonly UserSummary[]
  /** False for an end user, whose queue is their own tickets and who cannot read the picker. */
  showAssignee: boolean
  onChange: (changes: Partial<TicketQuery>) => void
  onClear: () => void
}

/**
 * The queue's filter bar.
 *
 * Every control writes straight through to the URL — there is no draft state and no
 * "apply" button, so what the address says and what the table shows cannot come apart.
 * The filters offered are exactly the ones WP-1.5 shipped on the endpoint; there is no
 * free-text search, deliberately, because the API has none (global search is WP-4.2).
 */
export function TicketFilters({
  query,
  categories,
  priorities,
  departments,
  assignees,
  showAssignee,
  onChange,
  onClear,
}: TicketFiltersProps): React.JSX.Element {
  const assigneeValue = query.unassigned ? nobody : (query.assigneeId ?? any)

  return (
    <div className="flex flex-wrap items-end gap-4 rounded-card border border-border bg-surface p-5 shadow-card">
      <Field label="Status" htmlFor="filter-status">
        <Select
          multiple
          items={statusOrder.map((status) => ({ label: statusLabels[status], value: status }))}
          value={[...query.status]}
          onValueChange={(value: string[]) => {
            onChange({ status: value.filter(isKnownStatus) })
          }}
        >
          <SelectTrigger id="filter-status" size="default" className="w-44">
            <SelectValue>
              {(value: string[]) =>
                value.length === 0
                  ? 'Any status'
                  : value.length === 1
                    ? statusLabels[value[0] as keyof typeof statusLabels]
                    : `${String(value.length)} statuses`
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {statusOrder.map((status) => (
              <SelectItem key={status} value={status}>
                {statusLabels[status]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field label="Priority" htmlFor="filter-priority">
        <SingleSelect
          id="filter-priority"
          placeholder="Any priority"
          value={query.priorityId ?? any}
          onValueChange={(value) => {
            onChange({ priorityId: value === any ? null : value })
          }}
          options={priorities.map((priority) => ({ value: priority.id, label: priority.name }))}
          anyLabel="Any priority"
        />
      </Field>

      <Field label="Category" htmlFor="filter-category">
        <SingleSelect
          id="filter-category"
          placeholder="Any category"
          value={query.categoryId ?? any}
          onValueChange={(value) => {
            onChange({ categoryId: value === any ? null : value })
          }}
          options={categories.map((category) => ({ value: category.id, label: category.name }))}
          anyLabel="Any category"
        />
      </Field>

      {showAssignee ? (
        <Field label="Assignee" htmlFor="filter-assignee">
          <SingleSelect
            id="filter-assignee"
            placeholder="Anyone"
            value={assigneeValue}
            onValueChange={(value) => {
              // "No filter on the assignee" and "only the ones nobody holds" are
              // different questions, and one null cannot mean both (WP-1.5).
              if (value === nobody) {
                onChange({ assigneeId: null, unassigned: true })
                return
              }
              onChange({ assigneeId: value === any ? null : value, unassigned: false })
            }}
            options={[
              { value: nobody, label: 'Unassigned' },
              ...assignees.map((user) => ({ value: user.id, label: user.displayName })),
            ]}
            anyLabel="Anyone"
          />
        </Field>
      ) : null}

      <Field label="Department" htmlFor="filter-department">
        <SingleSelect
          id="filter-department"
          placeholder="Any department"
          value={query.departmentId ?? any}
          onValueChange={(value) => {
            onChange({ departmentId: value === any ? null : value })
          }}
          options={departments.map((department) => ({
            value: department.id,
            label: department.name,
          }))}
          anyLabel="Any department"
        />
      </Field>

      <Field label="Resolution SLA" htmlFor="filter-sla">
        <SingleSelect
          id="filter-sla"
          placeholder="Any state"
          value={query.slaState ?? any}
          onValueChange={(value) => {
            onChange({ slaState: value === any ? null : (value as TicketQuery['slaState']) })
          }}
          options={slaStateOrder.map((state) => ({ value: state, label: slaLabels[state] }))}
          anyLabel="Any state"
        />
      </Field>

      <Field label="Raised from" htmlFor="filter-created-from">
        <Input
          id="filter-created-from"
          type="date"
          className="w-40"
          value={toDateInput(query.createdFrom)}
          onChange={(event) => {
            onChange({ createdFrom: dayStart(event.target.value) })
          }}
        />
      </Field>

      <Field label="Raised to" htmlFor="filter-created-to">
        <Input
          id="filter-created-to"
          type="date"
          className="w-40"
          value={toDateInput(query.createdTo)}
          onChange={(event) => {
            onChange({ createdTo: dayEnd(event.target.value) })
          }}
        />
      </Field>

      {hasActiveFilters(query) ? (
        <Button variant="ghost" onClick={onClear} className="text-body">
          <X className="size-4" aria-hidden="true" />
          Clear filters
        </Button>
      ) : null}
    </div>
  )
}

function Field({
  label,
  htmlFor,
  children,
}: {
  label: string
  htmlFor: string
  children: React.ReactNode
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={htmlFor} className="text-field-label font-medium text-heading">
        {label}
      </Label>
      {children}
    </div>
  )
}

interface SingleSelectProps {
  id: string
  placeholder: string
  value: string
  onValueChange: (value: string) => void
  options: readonly { value: string; label: string }[]
  /** The wording of the "no filter" option, which is always first. */
  anyLabel: string
}

/** One filter select, with an explicit "no filter" option rather than a clearable empty. */
function SingleSelect({
  id,
  placeholder,
  value,
  onValueChange,
  options,
  anyLabel,
}: SingleSelectProps): React.JSX.Element {
  const all = [{ value: any, label: anyLabel }, ...options]

  return (
    <Select
      items={all}
      value={value}
      onValueChange={(next: string | null) => {
        onValueChange(next ?? any)
      }}
    >
      <SelectTrigger id={id} size="default" className="w-44">
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {all.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function isKnownStatus(value: string): value is (typeof statusOrder)[number] {
  return (statusOrder as readonly string[]).includes(value)
}
