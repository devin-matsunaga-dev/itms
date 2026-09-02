import { SlidersHorizontal } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from '@/components/ui/popover'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { Department } from '@/lib/api/types'
import { Roles, type Role } from '@/lib/roles'
import { LocationPicker } from '@/features/directory/components/location-picker'
import { hasActiveFilters, type UserQuery } from '../lib/user-query'

/** The value a "no filter on this" option carries. Empty string is a legitimate id. */
const any = '__any__'

interface UserFiltersProps {
  query: UserQuery
  departments: readonly Department[]
  onChange: (changes: Partial<UserQuery>) => void
  onClear: () => void
  /** The search box, rendered inside this card — they are one job, not two. */
  search: React.ReactNode
}

/**
 * The directory's filter bar (DESIGN.md §4, *Filter bar*).
 *
 * **Role** stays inline, because "who are the technicians" is the question a directory is
 * opened with most often. Department, location, and whether deactivated accounts are shown
 * sit behind the Filters button, whose badge counts how many of them are set — so nothing
 * is hidden without being counted.
 *
 * Every control writes straight through to the URL. There is no draft state and no "apply"
 * button, inside the popover or outside it, so what the address says and what the table
 * shows cannot come apart.
 *
 * The location filter is the cascading picker, which asks the server for one level at a
 * time — so filtering the directory by a room works on an estate of any size, which is the
 * thing the flat two-hundred-row list could not do.
 */
export function UserFilters({
  query,
  departments,
  onChange,
  onClear,
  search,
}: UserFiltersProps): React.JSX.Element {
  const advanced =
    (query.departmentId === null ? 0 : 1) +
    (query.locationId === null ? 0 : 1) +
    (query.includeInactive ? 1 : 0)

  return (
    <div className="flex flex-col gap-3 rounded-card border border-border bg-surface p-4 shadow-card">
      {search}

      <div className="flex flex-wrap items-center gap-3">
        <Select
          items={[
            { value: any, label: 'Any role' },
            { value: Roles.admin, label: 'Administrators' },
            { value: Roles.technician, label: 'Technicians' },
            { value: Roles.user, label: 'End users' },
          ]}
          value={query.role ?? any}
          onValueChange={(value: string | null) => {
            onChange({ role: value === null || value === any ? null : (value as Role) })
          }}
        >
          <SelectTrigger id="filter-user-role" size="default" className="w-44" aria-label="Role">
            <SelectValue>
              {(current: string) =>
                current === any
                  ? 'Role'
                  : current === Roles.admin
                    ? 'Administrators'
                    : current === Roles.technician
                      ? 'Technicians'
                      : 'End users'
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={any}>Any role</SelectItem>
            <SelectItem value={Roles.admin}>Administrators</SelectItem>
            <SelectItem value={Roles.technician}>Technicians</SelectItem>
            <SelectItem value={Roles.user}>End users</SelectItem>
          </SelectContent>
        </Select>

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

            <FilterField label="Department" htmlFor="filter-user-department">
              <Select
                items={[
                  { value: any, label: 'Any department' },
                  ...departments.map((department) => ({
                    value: department.id,
                    label: department.name,
                  })),
                ]}
                value={query.departmentId ?? any}
                onValueChange={(value: string | null) => {
                  onChange({ departmentId: value === null || value === any ? null : value })
                }}
              >
                <SelectTrigger id="filter-user-department" size="default" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={any}>Any department</SelectItem>
                  {departments.map((department) => (
                    <SelectItem key={department.id} value={department.id}>
                      {department.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FilterField>

            <FilterField label="Location" htmlFor="filter-user-location">
              <LocationPicker
                id="filter-user-location"
                value={query.locationId}
                placeholder="Any location"
                onValueChange={(locationId) => {
                  onChange({ locationId })
                }}
              />
            </FilterField>

            <div className="flex items-start gap-2">
              <Checkbox
                id="filter-user-inactive"
                checked={query.includeInactive}
                onCheckedChange={(checked: boolean) => {
                  onChange({ includeInactive: checked })
                }}
              />
              <div>
                <Label
                  htmlFor="filter-user-inactive"
                  className="text-copy font-normal text-body"
                >
                  Include deactivated accounts
                </Label>
                {/*
                  Said rather than assumed: a deactivated person keeps every ticket,
                  comment, and asset history row they own (invariant 9), so they are
                  exactly who somebody is looking for when they are working out where a
                  laptop went.
                */}
                <p className="text-caption text-muted-foreground">
                  People who have left still hold history, and sometimes equipment.
                </p>
              </div>
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

function FilterField({
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
