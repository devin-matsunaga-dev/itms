import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query'
import type { Asset, CreateAssetRequest, UpdateAssetRequest } from '@/lib/api/types'
import {
  assignAsset,
  createAsset,
  moveAsset,
  updateAsset,
  type AssetLifecycleRoute,
} from '../api/assets-api'
import { assetKeys } from './use-assets'

/**
 * The writes the asset screens make (WP-2.6b).
 *
 * ## Every one of them refetches
 *
 * The response to a lifecycle call is the asset as it now stands, and it would be tempting
 * to patch the cache from it. Two things make the refetch the right answer instead. The
 * response carries no new `ETag` for the *read*, which the next write needs — the header is
 * on the response, but a cache seeded from the body would not have it. And the timeline and
 * the support-history panel beside the asset have both just changed: issuing equipment
 * writes two history lines, and nothing in the asset's own payload says so. One
 * invalidation answers both, and it is what `useTicketWrite` settled at WP-1.10.
 *
 * The register is invalidated too, because the row that just moved is on it.
 */
function useAssetWrite<TArgs>(
  id: string,
  write: (args: TArgs) => Promise<Asset>,
): UseMutationResult<Asset, Error, TArgs> {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: write,
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: assetKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: assetKeys.history(id) }),
        queryClient.invalidateQueries({ queryKey: assetKeys.all }),
      ])
    },
  })
}

/** What every write on the detail screen carries: the tag it was read at. */
export interface AssetWriteArgs {
  /** The operator's note, recorded against the move. Null when they wrote none. */
  note: string | null
  /** The detail's `ETag`, echoed back as `If-Match`. */
  etag: string | null
}

/**
 * Issues an asset, transfers it, or takes it back — one route, three acts (WP-2.2).
 */
export function useAssignAsset(
  id: string,
): UseMutationResult<Asset, Error, AssetWriteArgs & { assignedToUserId: string | null }> {
  return useAssetWrite(id, ({ assignedToUserId, note, etag }) =>
    assignAsset(id, assignedToUserId, note, etag),
  )
}

/** Sends an asset for repair, brings it back, or retires it. */
export function useMoveAsset(
  id: string,
): UseMutationResult<Asset, Error, AssetWriteArgs & { route: AssetLifecycleRoute }> {
  return useAssetWrite(id, ({ route, note, etag }) => moveAsset(id, route, note, etag))
}

/** Corrects an asset's descriptive facts. */
export function useUpdateAsset(
  id: string,
): UseMutationResult<Asset, Error, { request: UpdateAssetRequest; etag: string | null }> {
  return useAssetWrite(id, ({ request, etag }) => updateAsset(id, request, etag))
}

/**
 * Records a new asset.
 *
 * The register is invalidated but no detail is seeded: the reply carried no `ETag` for a
 * later read, and the screen navigates to the detail route, where the query fetches the
 * asset with its tag. Seeding the cache from the response would leave the first lifecycle
 * call on the new asset unable to state a precondition — the call `useCreateTicket` made
 * for the same reason.
 */
export function useCreateAsset(): UseMutationResult<Asset, Error, CreateAssetRequest> {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createAsset,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: assetKeys.all })
    },
  })
}
