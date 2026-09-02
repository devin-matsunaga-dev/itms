import { apiFetch } from '@/lib/api/client'
import type {
  CreateDepartmentRequest,
  CreateLocationRequest,
  Department,
  DepartmentUsage,
  Location,
  LocationKind,
  LocationUsage,
  MoveLocationRequest,
  PagedDepartments,
  UpdateDepartmentRequest,
  UpdateLocationRequest,
} from '@/lib/api/types'

/**
 * Directory's reads and writes: departments, and the location tree (WP-0.6, WP-2.4, WP-2.7).
 *
 * Everything the product knows about a department or a room comes through this file. It
 * started at WP-2.6a as the two reference reads other modules' screens need, and WP-2.7
 * folded the helpdesk's own `fetchDepartments` into it — the copy WP-1.9 wrote before this
 * folder existed — so there is now exactly one call per question.
 *
 * The writes are Admin-only server-side and carry the antiforgery token through
 * `apiFetch`. Nothing here is the enforcement.
 */

/** How many rows the flat reads ask for. The API clamps at 200 regardless. */
const referencePageSize = 200

/** Active departments, for a picker or a filter. */
export async function fetchDepartments(signal?: AbortSignal): Promise<Department[]> {
  const page = await apiFetch<PagedDepartments>(
    `/departments?pageSize=${String(referencePageSize)}`,
    signal ? { signal } : {},
  )
  return page.items
}

/** What the department management screen is looking at. */
export interface DepartmentListQuery {
  readonly search: string
  /** Retired departments are hidden until asked for, exactly as the server defaults. */
  readonly includeInactive: boolean
  readonly page: number
  readonly pageSize: number
}

/** Serializes the department screen's state into the query the server reads. */
export function serializeDepartmentQuery(query: DepartmentListQuery): URLSearchParams {
  const params = new URLSearchParams()

  if (query.search.trim().length > 0) {
    params.append('search', query.search.trim())
  }
  if (query.includeInactive) {
    params.append('includeInactive', 'true')
  }
  if (query.page > 1) {
    params.append('page', String(query.page))
  }
  params.append('pageSize', String(query.pageSize))

  return params
}

/** A page of departments, for the management screen. */
export function fetchDepartmentPage(
  query: DepartmentListQuery,
  signal?: AbortSignal,
): Promise<PagedDepartments> {
  return apiFetch<PagedDepartments>(
    `/departments?${serializeDepartmentQuery(query).toString()}`,
    signal ? { signal } : {},
  )
}

/**
 * What a department still holds, per module.
 *
 * Admin-only, and read before retiring one: a department that still names people and
 * equipment is one somebody should see the size of first. It reports and never refuses —
 * WP-2.4's call, since retiring a department is reversible where deleting a room is not.
 */
export function fetchDepartmentUsage(id: string, signal?: AbortSignal): Promise<DepartmentUsage> {
  return apiFetch<DepartmentUsage>(`/departments/${id}/usage`, signal ? { signal } : {})
}

export function createDepartment(request: CreateDepartmentRequest): Promise<Department> {
  return apiFetch<Department>('/departments', { method: 'POST', body: request })
}

export function updateDepartment(id: string, request: UpdateDepartmentRequest): Promise<Department> {
  return apiFetch<Department>(`/departments/${id}`, { method: 'PUT', body: request })
}

/**
 * Retires a department, or brings one back.
 *
 * There is no delete, deliberately: WP-0.6 made departments retire-only and WP-2.4 left
 * that standing, because a department is named by tickets, assets, and people that all
 * outlive it and none of those references is a foreign key the database could protect.
 */
export function setDepartmentActive(id: string, active: boolean): Promise<Department> {
  return apiFetch<Department>(`/departments/${id}/${active ? 'reactivate' : 'deactivate'}`, {
    method: 'POST',
  })
}

/**
 * Locations as a flat list, ordered by the tree's own materialised path.
 *
 * One page of two hundred, filtered in the browser. It is what the *asset register's*
 * location filter still reads, because a filter needs every room in one array to render a
 * chosen one's path without a second call. The cascading picker (WP-2.7) reads the tree a
 * level at a time instead, and is what an estate larger than this bound is served by.
 */
export async function fetchLocations(signal?: AbortSignal): Promise<Location[]> {
  const page = await apiFetch<{ items: Location[] }>(
    `/locations?pageSize=${String(referencePageSize)}`,
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * The top level of the tree — the first level of a cascading picker.
 *
 * `adoptableFor` narrows every level to the nodes that could legally be the parent of a
 * location of that kind. The rule is the server's: WP-2.4 resolved it there precisely so a
 * picker filtering client-side would not become a second copy of the hierarchy.
 */
export async function fetchLocationRoots(
  adoptableFor?: LocationKind,
  signal?: AbortSignal,
): Promise<Location[]> {
  const params = new URLSearchParams({ pageSize: String(referencePageSize) })
  if (adoptableFor !== undefined) {
    params.append('adoptableFor', adoptableFor)
  }

  const page = await apiFetch<{ items: Location[] }>(
    `/locations/roots?${params.toString()}`,
    signal ? { signal } : {},
  )
  return page.items
}

/** One level of the tree: the direct children of a node. */
export async function fetchLocationChildren(
  parentId: string,
  adoptableFor?: LocationKind,
  signal?: AbortSignal,
): Promise<Location[]> {
  const params = new URLSearchParams({
    parentId,
    pageSize: String(referencePageSize),
  })
  if (adoptableFor !== undefined) {
    params.append('adoptableFor', adoptableFor)
  }

  const page = await apiFetch<{ items: Location[] }>(
    `/locations?${params.toString()}`,
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * Locations matching a term, anywhere in the tree.
 *
 * The escape hatch beside the cascade: somebody who knows the room's name should not have
 * to walk four levels to reach it. The server matches the node's own name and its full
 * path, so "pump" finds the pump station and everything inside it.
 */
export async function searchLocations(
  term: string,
  adoptableFor?: LocationKind,
  signal?: AbortSignal,
): Promise<Location[]> {
  const params = new URLSearchParams({ search: term, pageSize: '50' })
  if (adoptableFor !== undefined) {
    params.append('adoptableFor', adoptableFor)
  }

  const page = await apiFetch<{ items: Location[] }>(
    `/locations?${params.toString()}`,
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * The root-to-node chain, so a picker opened on an existing value can show where that
 * value sits without walking the tree itself.
 *
 * Ordered root first and including the node, so the chain of a root is that node alone
 * and is never empty (WP-2.4).
 */
export function fetchLocationAncestors(id: string, signal?: AbortSignal): Promise<Location[]> {
  return apiFetch<Location[]>(`/locations/${id}/ancestors`, signal ? { signal } : {})
}

/**
 * A node and everything beneath it.
 *
 * The move dialog's second read: a node cannot be moved beneath itself or one of its own
 * descendants, and offering a parent the server would refuse with
 * `directory.location_cycle` is offering a button that always fails.
 */
export async function fetchLocationSubtree(id: string, signal?: AbortSignal): Promise<Location[]> {
  const page = await apiFetch<{ items: Location[] }>(
    `/locations?rootId=${id}&pageSize=${String(referencePageSize)}`,
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * What a location still holds: how many nodes sit under it, and what the other modules
 * still point at it with.
 *
 * Admin-only, and read before the delete is offered. `canDelete` is advisory — the delete
 * re-checks both counts inside its own transaction, because either can change between the
 * two calls (WP-2.4).
 */
export function fetchLocationUsage(id: string, signal?: AbortSignal): Promise<LocationUsage> {
  return apiFetch<LocationUsage>(`/locations/${id}/usage`, signal ? { signal } : {})
}

export function createLocation(request: CreateLocationRequest): Promise<Location> {
  return apiFetch<Location>('/locations', { method: 'POST', body: request })
}

export function updateLocation(id: string, request: UpdateLocationRequest): Promise<Location> {
  return apiFetch<Location>(`/locations/${id}`, { method: 'PUT', body: request })
}

/** Moves a node — and, in one transaction server-side, every path beneath it. */
export function moveLocation(id: string, request: MoveLocationRequest): Promise<Location> {
  return apiFetch<Location>(`/locations/${id}/move`, { method: 'POST', body: request })
}

export function deleteLocation(id: string): Promise<void> {
  return apiFetch<void>(`/locations/${id}`, { method: 'DELETE' })
}
