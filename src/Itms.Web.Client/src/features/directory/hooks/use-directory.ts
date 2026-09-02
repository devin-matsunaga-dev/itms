import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import type { Department, Location } from '@/lib/api/types'
import { fetchDepartments, fetchLocations } from '../api/directory-api'

/**
 * Directory reference data for the filters and pickers on other modules' screens.
 *
 * Departments and the location tree change about once a quarter, so they are held for the
 * session rather than refetched per interaction — the same budget the helpdesk queue's
 * reference data runs on.
 */
const referenceDataStaleTime = 10 * 60_000

/** Active departments. */
export function useDepartments(): UseQueryResult<Department[]> {
  return useQuery({
    queryKey: ['directory', 'departments'],
    queryFn: ({ signal }) => fetchDepartments(signal),
    staleTime: referenceDataStaleTime,
  })
}

/** The location tree as a flat, path-ordered list. */
export function useLocations(): UseQueryResult<Location[]> {
  return useQuery({
    queryKey: ['directory', 'locations'],
    queryFn: ({ signal }) => fetchLocations(signal),
    staleTime: referenceDataStaleTime,
  })
}
