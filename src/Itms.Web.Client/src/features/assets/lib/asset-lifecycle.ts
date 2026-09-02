/**
 * Which lifecycle actions an asset offers, derived from what the server said.
 *
 * WP-2.6b's criterion is that an illegal action is *absent* rather than disabled, and that
 * the actions read the server's legal destinations rather than restating
 * `AssetLifecycle`'s table in TypeScript. `AssetLifecycle.DestinationsFrom`'s own doc
 * comment asks for exactly that. So nothing here decides whether a move is legal — it
 * decides only which operation a legal destination corresponds to, and how it is worded.
 *
 * ## The two fields this reads, and why there are two
 *
 * `allowedNextStatusCodes` answers for the lifecycle moves. It cannot answer for
 * assignment, and that is not an oversight in the server: the list is empty both from a
 * terminal status **and** from a custom status an administrator added, and those two cases
 * differ — `Asset.AssignTo` refuses only the terminal three, deliberately, so that adding a
 * status does not quietly make the equipment in it unissuable. `canBeAssigned` is the
 * server's own `!AssetLifecycle.IsTerminal(code)`, computed on every read. Inferring one
 * from the other here would get the custom-status case exactly backwards.
 *
 * ## Neither field is the enforcement
 *
 * The server refuses an illegal move with 409 and a terminal assignment with
 * `assets.asset_not_assignable` whatever a client sends. What this buys is a screen that
 * does not offer a button which always fails.
 */

import type { Asset } from '@/lib/api/types'
import type { AssetLifecycleRoute } from '../api/assets-api'

/** `AssetHistoryEntry.NoteMaxLength`. */
export const noteMaxLength = 1000

/** The six operations SPEC.md §3 names, as a screen offers them. */
export type AssetActionId =
  | 'assign'
  | 'transfer'
  | 'return'
  | 'repair'
  | 'return-to-service'
  | 'retire'

/** One lifecycle action, as a button and the dialog behind it. */
export interface AssetAction {
  readonly id: AssetActionId
  /** The verb on the button. Sentence case, per DESIGN.md §2. */
  readonly label: string
  /** What the dialog says will happen. */
  readonly description: string
  /**
   * Where the write goes: one of the three single-party routes, or `null` for the
   * assignment route, which the three assignment acts share (WP-2.2).
   */
  readonly route: AssetLifecycleRoute | null
  /**
   * What happened, past tense, for the sentence a toast reports it with — "LAP-0042
   * issued", "the asset could not be issued". Written here beside the label so the two
   * cannot describe the same action differently.
   */
  readonly outcome: string
  /** True when the dialog has to collect a person before it can be confirmed. */
  readonly needsHolder: boolean
  /** True for the one action DESIGN.md §4 paints in `danger`. */
  readonly destructive: boolean
}

/**
 * The order the actions are offered in: the move that carries the asset forward from where
 * it stands, then the ways sideways, then the way out. The first is rendered as the
 * screen's primary button.
 */
const offered: readonly AssetActionId[] = [
  'return-to-service',
  'assign',
  'transfer',
  'return',
  'repair',
  'retire',
]

/** The status codes the lifecycle table names, spelled once. */
const inStock = 'in-stock'
const deployed = 'deployed'
const repair = 'repair'
const retired = 'retired'

/**
 * The lifecycle actions this asset offers.
 *
 * @param asset The asset as the server last described it, including its
 * `allowedNextStatusCodes` and `canBeAssigned`.
 * @returns The actions to render, in order. Empty from a terminal status.
 */
export function assetActions(asset: Asset): AssetAction[] {
  const destinations = new Set(asset.allowedNextStatusCodes ?? [])
  const held = asset.assignedToUserId !== null && asset.assignedToUserId !== undefined
  const assignable = asset.canBeAssigned === true

  // Where `POST /returns-to-service` would put the asset: back to whoever holds it, or
  // into stock if nobody does. The server decides this the same way, in
  // `Asset.ReturnToService` — so asking whether *that* destination is legal is asking
  // whether the operation would succeed.
  const backTo = held ? deployed : inStock

  return offered
    .filter((id) => {
      switch (id) {
        case 'assign':
          return assignable && !held
        case 'transfer':
          return assignable && held
        // Taking equipment back clears the holder, and moves the status only when the
        // asset was deployed — which is the one case that needs the destination to be
        // legal.
        case 'return':
          return assignable && held && (asset.assetStatusCode !== deployed || destinations.has(inStock))
        case 'repair':
          return destinations.has(repair)
        case 'return-to-service':
          return destinations.has(backTo)
        case 'retire':
          return destinations.has(retired)
      }
    })
    .map((id) => describe(id, asset, backTo))
}

function describe(id: AssetActionId, asset: Asset, backTo: string): AssetAction {
  switch (id) {
    case 'assign':
      return {
        id,
        label: 'Assign',
        outcome: 'issued',
        description: 'Issue this asset to somebody. Equipment issued out of stock is deployed at the same time.',
        route: null,
        needsHolder: true,
        destructive: false,
      }
    case 'transfer':
      return {
        id,
        label: 'Transfer',
        outcome: 'transferred',
        description:
          'Hand this asset to somebody else. A transfer moves who holds it and nothing else — the equipment does not restart its life.',
        route: null,
        needsHolder: true,
        destructive: false,
      }
    case 'return':
      return {
        id,
        label: 'Return',
        outcome: 'taken back',
        description:
          asset.assetStatusCode === deployed
            ? 'Take this asset back off whoever holds it. It goes into stock.'
            : 'Take this asset back off whoever holds it. Its status does not change — it is not back yet.',
        route: null,
        needsHolder: false,
        destructive: false,
      }
    case 'repair':
      return {
        id,
        label: 'Send for repair',
        outcome: 'sent for repair',
        description:
          'Send this asset away to be fixed. Whoever holds it keeps it on the record, which is what tells a later return where to put it back.',
        route: 'repairs',
        needsHolder: false,
        destructive: false,
      }
    case 'return-to-service':
      // Status-aware wording. "Return to service" is the repair-shop phrase and is what
      // this action almost always is; from anywhere else it is plainly a move to where the
      // destination says, and saying "return to service" there would be misleading.
      return {
        id,
        label:
          asset.assetStatusCode === repair
            ? 'Return to service'
            : backTo === inStock
              ? 'Return to stock'
              : 'Put into service',
        outcome: asset.assetStatusCode === repair ? 'returned to service' : 'brought back',
        description:
          backTo === inStock
            ? 'Bring this asset back into stock. Nobody holds it, so it goes back on the shelf.'
            : 'Bring this asset back into service. It goes back to whoever still holds it.',
        route: 'returns-to-service',
        needsHolder: false,
        destructive: false,
      }
    case 'retire':
      return {
        id,
        label: 'Retire',
        outcome: 'retired',
        description:
          'Take this asset out of service and keep it on the books. Retiring also releases whoever holds it, and it cannot be undone — a retired asset has no way back.',
        route: 'retirements',
        needsHolder: false,
        destructive: true,
      }
  }
}
