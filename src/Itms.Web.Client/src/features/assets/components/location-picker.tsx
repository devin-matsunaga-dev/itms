import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from '@/components/ui/combobox'
import type { Location } from '@/lib/api/types'

interface LocationPickerProps {
  id: string
  locations: readonly Location[]
  /** The chosen location's id, or null for none. */
  value: string | null
  placeholder: string
  invalid?: boolean
  onValueChange: (locationId: string | null) => void
}

/**
 * The location field: type to narrow, or open it and pick.
 *
 * The label is the room's full `path`, not its name — three buildings can each have a
 * "Server Room", and a picker offering that word three times is one nobody can use. The
 * path is also what the register's Location column and the detail screen render, so the
 * control and the rows read the same way.
 *
 * Clearing it is a real answer: the clear button writes null, which is "no location" on a
 * form and "no filter" on the register.
 *
 * **Flat rather than cascaded, still.** WP-2.4 built the roots, ancestor, and
 * `adoptableFor` reads a cascading picker walks and recorded that the picker itself is
 * `WP-2.7`'s; this is one page of two hundred rooms filtered in the browser, which is the
 * same bound and the same trade the department picker has run on since WP-1.9. An estate
 * with more rooms than that is exactly the case the cascading picker exists for, and is the
 * reason not to grow this one.
 *
 * WP-2.6a's register held a private copy of this; WP-2.6b needed the same control on two
 * forms, so it moved here rather than becoming a second copy. `WP-2.7` replaces one file.
 */
export function LocationPicker({
  id,
  locations,
  value,
  placeholder,
  invalid,
  onValueChange,
}: LocationPickerProps): React.JSX.Element {
  const items = locations.map((location) => ({ value: location.id, label: location.path }))

  return (
    <Combobox
      items={items}
      value={items.find((item) => item.value === value) ?? null}
      onValueChange={(next: { value: string; label: string } | null) => {
        onValueChange(next?.value ?? null)
      }}
    >
      <ComboboxInput id={id} aria-invalid={invalid} showClear placeholder={placeholder} />
      <ComboboxContent>
        <ComboboxEmpty>No location matches that.</ComboboxEmpty>
        <ComboboxList>
          {(item: { value: string; label: string }) => (
            <ComboboxItem key={item.value} value={item}>
              {item.label}
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}
