import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query'
import type {
  CreateDepartmentRequest,
  CreateLocationRequest,
  Department,
  Location,
  MoveLocationRequest,
  UpdateDepartmentRequest,
  UpdateLocationRequest,
} from '@/lib/api/types'
import {
  createDepartment,
  createLocation,
  deleteLocation,
  moveLocation,
  setDepartmentActive,
  updateDepartment,
  updateLocation,
} from '../api/directory-api'

/**
 * The writes the directory management screens make (WP-2.7).
 *
 * ## Everything directory is invalidated after every write
 *
 * Not the one row that moved. A rename or a move rewrites the materialised path of every
 * node beneath it in one server-side transaction (WP-2.4), so "which cached entries did
 * this change" has no cheap answer on the client: renaming a site changes the label of
 * four hundred rooms, in every level the picker has open, in the flat list the asset
 * register filters on, and in the path rendered beside a user. Invalidating the whole
 * `directory` namespace is one line and is always right; the alternative is a list of
 * keys that would be wrong the first time somebody moves a building.
 *
 * The reads are cheap and staleness here is visible — a room under the wrong parent is the
 * kind of wrong somebody screenshots — so this is the trade to make.
 */
function useDirectoryWrite<TArgs, TResult>(
  write: (args: TArgs) => Promise<TResult>,
): UseMutationResult<TResult, Error, TArgs> {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: write,
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: ['directory'] })
    },
  })
}

export function useCreateDepartment(): UseMutationResult<
  Department,
  Error,
  CreateDepartmentRequest
> {
  return useDirectoryWrite(createDepartment)
}

export function useUpdateDepartment(): UseMutationResult<
  Department,
  Error,
  { id: string; request: UpdateDepartmentRequest }
> {
  return useDirectoryWrite(({ id, request }) => updateDepartment(id, request))
}

/** Retires a department, or brings one back. There is no delete (WP-0.6, WP-2.4). */
export function useSetDepartmentActive(): UseMutationResult<
  Department,
  Error,
  { id: string; active: boolean }
> {
  return useDirectoryWrite(({ id, active }) => setDepartmentActive(id, active))
}

export function useCreateLocation(): UseMutationResult<Location, Error, CreateLocationRequest> {
  return useDirectoryWrite(createLocation)
}

export function useUpdateLocation(): UseMutationResult<
  Location,
  Error,
  { id: string; request: UpdateLocationRequest }
> {
  return useDirectoryWrite(({ id, request }) => updateLocation(id, request))
}

export function useMoveLocation(): UseMutationResult<
  Location,
  Error,
  { id: string; request: MoveLocationRequest }
> {
  return useDirectoryWrite(({ id, request }) => moveLocation(id, request))
}

/**
 * Deletes a location.
 *
 * Refused with 409 when the node still has children (`directory.location_has_children`) or
 * is still referenced by another module (`directory.location_in_use`). The two are separate
 * codes because they send an administrator to different places — empty the subtree, or move
 * the equipment and the people — and the dialog says which.
 */
export function useDeleteLocation(): UseMutationResult<void, Error, string> {
  return useDirectoryWrite(deleteLocation)
}
