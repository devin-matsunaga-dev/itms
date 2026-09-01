import { SlidersHorizontal } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from '@/components/ui/popover'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { Department, TicketCategory, TicketPriority, UserSummary } from '@/lib/api/types'
import { canHoldTickets } from '../lib/ticket-assignment'
import { slaLabels, slaStateOrder, statusLabels, statusOrder } from '../lib/ticket-display'
import {
  advancedFilterCount,
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
  /** The search box, rendered inside this card — they are one job, not two. */
  search: React.ReactNode
  /** True when the queue is already narrowed to the viewer's own tickets. */
  mine: boolean
  /** How many those are, scope-wide, or undefined while the counters load. */
  mineCount: number | undefined
  onToggleMine: () => void
}

/**
 * The queue's filter bar: the three a technician reaches for constantly, and the rest
 * behind a popover.
 *
 * WP-1.9 laid all eight out inline and recorded that they wrap into two rows at the
 * 1280px floor DESIGN.md §6 sets, and that a ninth would want a popover rather than a
 * third row. This is that popover, pulled forward — the row is now three controls and a
 * button whose badge says how many of the others are set, so nothing is hidden without
 * being counted.
 *
 * Every control still writes straight through to the URL. There is no draft state and no
 * "apply" button, inside the popover or outside it, so what the address says and what the
 * table shows cannot come apart.
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
  search,
  mine,
  mineCount,
  onToggleMine,
}: TicketFiltersProps): React.JSX.Element {
  const assigneeValue = query.unassigned ? nobody : (query.assigneeId ?? any)
  const advanced = advancedFilterCount(query)

  return (
    <div className="flex flex-col gap-3 rounded-card border border-border bg-surface p-4 shadow-card">
      {/*
        The search and the filters are one card because they are one job — narrowing the
        queue. Two cards with a gap and two sets of padding between them cost about
        seventy pixels of a screen whose whole point is the list underneath.
      */}
      {search}

      <div className="flex flex-wrap items-center gap-3">
      <Button
        variant={mine ? 'default' : 'outline'}
        aria-pressed={mine}
        onClick={onToggleMine}
      >
        My tickets
        {mineCount === undefined ? null : (
          <span
            className={cn(
              'ml-1 inline-flex min-w-5 justify-center rounded-full px-1.5 tabular',
              mine ? 'bg-white/20 text-white' : 'bg-canvas text-muted-foreground',
            )}
          >
            {mineCount}
          </span>
        )}
      </Button>

      <Select
        multiple
        items={statusOrder.map((status) => ({ label: statusLabels[status], value: status }))}
        value={[...query.status]}
        onValueChange={(value: string[]) => {
          onChange({ status: value.filter(isKnownStatus) })
        }}
      >
        <SelectTrigger id="filter-status" size="default" className="w-40" aria-label="Status">
          <SelectValue>
            {(value: string[]) =>
              value.length === 0
                ? 'Status'
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

      <InlineSelect
        id="filter-priority"
        label="Priority"
        value={query.priorityId ?? any}
        onValueChange={(value) => {
          onChange({ priorityId: value === any ? null : value })
        }}
        options={priorities.map((priority) => ({ value: priority.id, label: priority.name }))}
      />

      {showAssignee ? (
        <InlineSelect
          id="filter-assignee"
          label="Assignee"
          value={assigneeValue}
          onValueChange={(value) => {
            // "No filter on the assignee" and "only the ones nobody holds" are different
            // questions, and one null cannot mean both (WP-1.5).
            if (value === nobody) {
              onChange({ assigneeId: null, unassigned: true })
              return
            }
            onChange({ assigneeId: value === any ? null : value, unassigned: false })
          }}
          options={[
            { value: nobody, label: 'Unassigned' },
            // Only people who can hold a ticket, for the same reason the detail screen's
            // picker filters: filtering the queue by somebody who can never be an assignee
            // returns nothing, every time.
            ...assignees
              .filter(canHoldTickets)
              .map((user) => ({ value: user.id, label: user.displayName })),
          ]}
        />
      ) : null}

      <Popover>
        <PopoverTrigger
          render={<Button variant="outline" className={cn(advanced > 0 && 'border-primary')} />}
        >
          <SlidersHorizontal className="size-4" aria-hidden="true" />
          Filters
          {advanced > 0 ? (
            <span className="ml-1 inline-flex size-5 items-center justify-center rounded-full bg-primary text-label font-semibold text-white tabular">
              {advanced}
            </span>
          ) : null}
        </PopoverTrigger>

        <PopoverContent align="start" className="w-80">
          <PopoverTitle>More filters</PopoverTitle>

          <Field label="Category" htmlFor="filter-category">
            <PanelSelect
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

          <Field label="Department" htmlFor="filter-department">
            <PanelSelect
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
            <PanelSelect
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

          <div className="grid grid-cols-2 gap-3">
            <Field label="Raised from" htmlFor="filter-created-from">
              <Input
                id="filter-created-from"
                type="date"
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
                value={toDateInput(query.createdTo)}
                onChange={(event) => {
                  onChange({ createdTo: dayEnd(event.target.value) })
                }}
              />
            </Field>
          </div>
        </PopoverContent>
      </Popover>

      {hasActiveFilters(query) ? (
        <Button variant="link" className="ml-auto px-0" onClick={onClear}>
          Clear all
        </Button>
      ) : null}
      </div>
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

interface SelectProps {
  id: string
  value: string
  onValueChange: (value: string) => void
  options: readonly { value: string; label: string }[]
}

/**
 * One of the three filters that stay on the bar.
 *
 * The trigger reads as the filter's own name until something is chosen, so an untouched
 * bar says "Status · Priority · Assignee" rather than three variations on "Any".
 */
function InlineSelect({
  id,
  label,
  value,
  onValueChange,
  options,
}: SelectProps & { label: string }): React.JSX.Element {
  const all = [{ value: any, label: `Any ${label.toLowerCase()}` }, ...options]

  return (
    <Select
      items={all}
      value={value}
      onValueChange={(next: string | null) => {
        onValueChange(next ?? any)
      }}
    >
      <SelectTrigger id={id} size="default" className="w-40" aria-label={label}>
        <SelectValue>
          {(current: string) =>
            current === any ? label : (all.find((option) => option.value === current)?.label ?? label)
          }
        </SelectValue>
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

/** One filter inside the popover, where a label sits above it and the width is fixed. */
function PanelSelect({
  id,
  placeholder,
  value,
  onValueChange,
  options,
  anyLabel,
}: SelectProps & { placeholder: string; anyLabel: string }): React.JSX.Element {
  const all = [{ value: any, label: anyLabel }, ...options]

  return (
    <Select
      items={all}
      value={value}
      onValueChange={(next: string | null) => {
        onValueChange(next ?? any)
      }}
    >
      <SelectTrigger id={id} size="default" className="w-full">
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
