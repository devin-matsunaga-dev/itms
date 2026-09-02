import { describe, expect, it } from 'vitest'
import { assetActions, type AssetActionId } from '@/features/assets/lib/asset-lifecycle'
import { asset } from '@/features/assets/test/asset-fixtures'
import type { Asset } from '@/lib/api/types'

/**
 * The derivation WP-2.6b's done-criterion rests on: an illegal action is absent, and what
 * is legal comes from the server rather than from a table restated here.
 *
 * The fixtures below spell out the destination lists the server actually sends for each
 * seeded status — they are `AssetLifecycle`'s edges — so a change to that table which is
 * not mirrored on the wire shows up as a failure here rather than as a button that answers
 * 409.
 */

function ids(subject: Asset): AssetActionId[] {
  return assetActions(subject).map((action) => action.id)
}

/** In stock, nobody holding it: `in-stock → deployed | repair | retired`. */
function inStock(overrides: Partial<Asset> = {}): Asset {
  return asset({
    assetStatusCode: 'in-stock',
    assetStatusName: 'In Stock',
    assignedToUserId: null,
    assignedToUserName: null,
    allowedNextStatusCodes: ['deployed', 'repair', 'retired'],
    canBeAssigned: true,
    ...overrides,
  })
}

/** Deployed and held: `deployed → in-stock | repair | retired`. */
function deployed(overrides: Partial<Asset> = {}): Asset {
  return asset({
    assetStatusCode: 'deployed',
    assetStatusName: 'Deployed',
    allowedNextStatusCodes: ['in-stock', 'repair', 'retired'],
    canBeAssigned: true,
    ...overrides,
  })
}

/** Away being fixed, holder kept: `repair → deployed | in-stock | retired`. */
function inRepair(overrides: Partial<Asset> = {}): Asset {
  return asset({
    assetStatusCode: 'repair',
    assetStatusName: 'Repair',
    allowedNextStatusCodes: ['deployed', 'in-stock', 'retired'],
    canBeAssigned: true,
    ...overrides,
  })
}

describe('assetActions', () => {
  it('offers an in-stock asset the issue, the repair, and the retirement', () => {
    expect(ids(inStock())).toEqual(['assign', 'repair', 'retire'])
  })

  it('offers a deployed asset the transfer, the return, the repair, and the retirement', () => {
    expect(ids(deployed())).toEqual(['transfer', 'return', 'repair', 'retire'])
  })

  it('leads a repaired asset with the way back into service', () => {
    expect(ids(inRepair())).toEqual([
      'return-to-service',
      'transfer',
      'return',
      'retire',
    ])
  })

  /**
   * Sending an asset that is already in repair back for repair is not a move the server
   * allows — a status is never a destination from itself — so the button is not rendered.
   */
  it('does not offer a repair to an asset already in repair', () => {
    expect(ids(inRepair())).not.toContain('repair')
  })

  /**
   * The done-criterion, stated directly. Retired, lost, and disposed have no way out
   * (WP-2.2), so a terminal asset offers nothing at all rather than six greyed buttons.
   */
  it.each(['retired', 'lost', 'disposed'])('offers a %s asset nothing', (code) => {
    const terminal = asset({
      assetStatusCode: code,
      assignedToUserId: null,
      assignedToUserName: null,
      allowedNextStatusCodes: [],
      canBeAssigned: false,
    })

    expect(assetActions(terminal)).toEqual([])
  })

  /**
   * The reason `canBeAssigned` is a second field. A status an administrator added is not
   * in the lifecycle table, so it offers no destinations — but it is not terminal, and the
   * equipment in it is still issuable. Inferring assignability from the empty list would
   * get this backwards and hide a button the server would accept.
   */
  it('still offers the issue from a custom status that has no destinations', () => {
    const onLoan = inStock({
      assetStatusCode: 'on-loan',
      assetStatusName: 'On Loan',
      allowedNextStatusCodes: [],
      canBeAssigned: true,
    })

    expect(ids(onLoan)).toEqual(['assign'])
  })

  /** Assign and transfer are the same route and never both apply: one holder, or none. */
  it('offers the issue or the transfer, never both', () => {
    expect(ids(inStock())).toContain('assign')
    expect(ids(inStock())).not.toContain('transfer')
    expect(ids(deployed())).toContain('transfer')
    expect(ids(deployed())).not.toContain('assign')
  })

  it('offers no return when nobody holds the asset', () => {
    expect(ids(inStock())).not.toContain('return')
  })

  /**
   * Taking a deployed asset back moves its status into stock, so the action depends on
   * that destination being legal — which is the one case where a return is more than
   * clearing a column.
   */
  it('withholds the return from a deployed asset when stock is not a legal destination', () => {
    const stuck = deployed({ allowedNextStatusCodes: ['repair', 'retired'] })

    expect(ids(stuck)).not.toContain('return')
    expect(ids(stuck)).toContain('transfer')
  })

  /**
   * Return-to-service asks whether the destination the *server* would choose is legal:
   * where the asset goes back to depends on whether anybody still holds it.
   */
  it('asks about the destination the server would choose', () => {
    // Held: the server would move it to deployed, and that edge exists from repair.
    expect(ids(inRepair())).toContain('return-to-service')

    // Held, but deployed is not offered: the operation would be refused, so neither is it.
    expect(ids(inRepair({ allowedNextStatusCodes: ['in-stock', 'retired'] }))).not.toContain(
      'return-to-service',
    )

    // Unheld: the server would move it into stock, and that edge does exist.
    const unheld = inRepair({ assignedToUserId: null, assignedToUserName: null })
    expect(ids(unheld)).toContain('return-to-service')
  })

  /** An asset in stock is already where a return-to-service would put it. */
  it('offers no return to service from stock', () => {
    expect(ids(inStock())).not.toContain('return-to-service')
  })

  describe('wording', () => {
    it('calls it a return to service when the asset is coming back from repair', () => {
      expect(label(inRepair(), 'return-to-service')).toBe('Return to service')
    })

    /**
     * A deployed asset nobody holds — booked in as deployed, say — can legally move into
     * stock through this route, and calling that "return to service" would be misleading:
     * it is not coming back from anywhere. The action is still offered, because the
     * transition is valid; only the wording follows the status.
     */
    it('names the destination when the asset is not coming back from repair', () => {
      const deployedUnheld = deployed({ assignedToUserId: null, assignedToUserName: null })

      expect(ids(deployedUnheld)).toContain('return-to-service')
      expect(label(deployedUnheld, 'return-to-service')).toBe('Return to stock')
    })

    it('paints only the retirement as destructive', () => {
      const destructive = assetActions(deployed())
        .filter((action) => action.destructive)
        .map((action) => action.id)

      expect(destructive).toEqual(['retire'])
    })

    it('sends the three single-party actions to their own routes and the rest to assignments', () => {
      const routes = new Map(assetActions(inRepair()).map((action) => [action.id, action.route]))

      expect(routes.get('return-to-service')).toBe('returns-to-service')
      expect(routes.get('transfer')).toBeNull()
      expect(routes.get('return')).toBeNull()
      expect(routes.get('retire')).toBe('retirements')
      expect(assetActions(inStock()).find((a) => a.id === 'repair')?.route).toBe('repairs')
    })

    it('asks for a person on the two actions that name one', () => {
      const needsHolder = (subject: Asset): AssetActionId[] =>
        assetActions(subject)
          .filter((action) => action.needsHolder)
          .map((action) => action.id)

      expect(needsHolder(inStock())).toEqual(['assign'])
      expect(needsHolder(deployed())).toEqual(['transfer'])
    })
  })
})

function label(subject: Asset, id: AssetActionId): string | undefined {
  return assetActions(subject).find((action) => action.id === id)?.label
}
