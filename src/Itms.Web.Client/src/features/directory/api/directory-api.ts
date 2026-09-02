import { apiFetch } from '@/lib/api/client'
import type { Department, Location } from '@/lib/api/types'

/**
 * The directory reads a screen outside Directory needs in order to name a department or a
 * room (WP-0.6, WP-2.4).
 *
 * They live here rather than beside the screen that happens to want them first, because
 * departments and locations belong to Directory and the next screen to need them —
 * `WP-2.7`'s directory management, the user 360 — should find one copy. The helpdesk queue
 * has had its own `fetchDepartments` since WP-1.9; folding that one into this file is
 * WP-2.7's to do, and is recorded in STATUS.md rather than done from an asset package.
 */

/** Active departments, for a picker or a filter. */
export async function fetchDepartments(signal?: AbortSignal): Promise<Department[]> {
  const page = await apiFetch<{ items: Department[] }>(
    '/departments?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * Locations as a flat list, ordered by the tree's own materialised path.
 *
 * **Flat rather than cascaded, deliberately.** WP-2.4 built the roots, ancestor, and
 * `adoptableFor` reads a cascading picker walks, and recorded at the human's direction
 * that the picker itself is `WP-2.7`'s. A filter has to name one room, and `path` already
 * reads as "Site → Building → Floor → Room", so a searchable flat list answers that
 * question today without pre-empting the shape that package has to design.
 *
 * One page of two hundred, filtered in the browser — the same bound and the same trade the
 * department picker has run on since WP-1.9. An estate with more rooms than that is
 * exactly the case the cascading picker exists for, and is the reason not to grow this one.
 */
export async function fetchLocations(signal?: AbortSignal): Promise<Location[]> {
  const page = await apiFetch<{ items: Location[] }>(
    '/locations?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}
