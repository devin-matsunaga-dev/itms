import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import type {
  Department,
  DepartmentUsage,
  Location,
  LocationKind,
  LocationUsage,
  PagedDepartments,
} from '@/lib/api/types'
import {
  fetchDepartmentPage,
  fetchDepartmentUsage,
  fetchDepartments,
  fetchLocationAncestors,
  fetchLocationChildren,
  fetchLocationRoots,
  fetchLocationSubtree,
  fetchLocationUsage,
  fetchLocations,
  searchLocations,
  type DepartmentListQuery,
} from '../api/directory-api'

/**
 * Directory reference data for the filters and pickers on other modules' screens, and the
 * reads its own management screens run on.
 *
 * Departments and the location tree change about once a quarter, so they are held for the
 * session rather than refetched per interaction — the same budget the helpdesk queue's
 * reference data runs on. The management screens invalidate what they change, which is
 * what keeps a ten-minute cache from showing somebody the room they just renamed.
 */
const referenceDataStaleTime = 10 * 60_000

/** Every directory query key, so a write can invalidate exactly what it moved. */
export const directoryKeys = {
  departments: ['directory', 'departments'] as const,
  departmentPage: (query: DepartmentListQuery) =>
    ['directory', 'department-page', query] as const,
  departmentUsage: (id: string) => ['directory', 'department-usage', id] as const,
  locations: ['directory', 'locations'] as const,
  locationRoots: (adoptableFor: LocationKind | undefined) =>
    ['directory', 'location-roots', adoptableFor ?? null] as const,
  locationChildren: (parentId: string, adoptableFor: LocationKind | undefined) =>
    ['directory', 'location-children', parentId, adoptableFor ?? null] as const,
  locationSearch: (term: string, adoptableFor: LocationKind | undefined) =>
    ['directory', 'location-search', term, adoptableFor ?? null] as const,
  locationAncestors: (id: string) => ['directory', 'location-ancestors', id] as const,
  locationSubtree: (id: string) => ['directory', 'location-subtree', id] as const,
  locationUsage: (id: string) => ['directory', 'location-usage', id] as const,
}

/** Active departments. */
export function useDepartments(): UseQueryResult<Department[]> {
  return useQuery({
    queryKey: directoryKeys.departments,
    queryFn: ({ signal }) => fetchDepartments(signal),
    staleTime: referenceDataStaleTime,
  })
}

/** A page of departments, for the management screen. */
export function useDepartmentPage(query: DepartmentListQuery): UseQueryResult<PagedDepartments> {
  return useQuery({
    queryKey: directoryKeys.departmentPage(query),
    queryFn: ({ signal }) => fetchDepartmentPage(query, signal),
  })
}

/**
 * What a department still holds, read only while the retire dialog is open.
 *
 * `enabled` is why it takes a nullable id: the usage read is Admin-only and answers a
 * question nobody has asked until the dialog opens.
 */
export function useDepartmentUsage(id: string | null): UseQueryResult<DepartmentUsage> {
  return useQuery({
    queryKey: directoryKeys.departmentUsage(id ?? ''),
    queryFn: ({ signal }) => fetchDepartmentUsage(id ?? '', signal),
    enabled: id !== null,
  })
}

/** The location tree as a flat, path-ordered list. */
export function useLocations(): UseQueryResult<Location[]> {
  return useQuery({
    queryKey: directoryKeys.locations,
    queryFn: ({ signal }) => fetchLocations(signal),
    staleTime: referenceDataStaleTime,
  })
}

/** The tree's top level. */
export function useLocationRoots(adoptableFor?: LocationKind): UseQueryResult<Location[]> {
  return useQuery({
    queryKey: directoryKeys.locationRoots(adoptableFor),
    queryFn: ({ signal }) => fetchLocationRoots(adoptableFor, signal),
    staleTime: referenceDataStaleTime,
  })
}

/** One level of the tree. Idle until a node is opened. */
export function useLocationChildren(
  parentId: string | null,
  adoptableFor?: LocationKind,
): UseQueryResult<Location[]> {
  return useQuery({
    queryKey: directoryKeys.locationChildren(parentId ?? '', adoptableFor),
    queryFn: ({ signal }) => fetchLocationChildren(parentId ?? '', adoptableFor, signal),
    enabled: parentId !== null,
    staleTime: referenceDataStaleTime,
  })
}

/** Locations matching a term, anywhere in the tree. Idle until somebody types. */
export function useLocationSearch(
  term: string,
  adoptableFor?: LocationKind,
): UseQueryResult<Location[]> {
  const trimmed = term.trim()

  return useQuery({
    queryKey: directoryKeys.locationSearch(trimmed, adoptableFor),
    queryFn: ({ signal }) => searchLocations(trimmed, adoptableFor, signal),
    enabled: trimmed.length > 0,
    staleTime: referenceDataStaleTime,
  })
}

/** The root-to-node chain for a node the picker was opened on. */
export function useLocationAncestors(id: string | null): UseQueryResult<Location[]> {
  return useQuery({
    queryKey: directoryKeys.locationAncestors(id ?? ''),
    queryFn: ({ signal }) => fetchLocationAncestors(id ?? '', signal),
    enabled: id !== null,
    staleTime: referenceDataStaleTime,
  })
}

/** A node and everything under it — the set a move may not target. */
export function useLocationSubtree(id: string | null): UseQueryResult<Location[]> {
  return useQuery({
    queryKey: directoryKeys.locationSubtree(id ?? ''),
    queryFn: ({ signal }) => fetchLocationSubtree(id ?? '', signal),
    enabled: id !== null,
  })
}

/** What a location still holds, read only while the delete dialog is open. */
export function useLocationUsage(id: string | null): UseQueryResult<LocationUsage> {
  return useQuery({
    queryKey: directoryKeys.locationUsage(id ?? ''),
    queryFn: ({ signal }) => fetchLocationUsage(id ?? '', signal),
    enabled: id !== null,
  })
}
