/**
 * How an asset's lifecycle status is rendered — the one place DESIGN.md §2's asset
 * colour map is spelled out.
 *
 * ## Keyed on the code, never on the name
 *
 * WP-2.1 gave every asset status an immutable `code` precisely so something stable could
 * be reasoned about while the *name* stays an administrator's to edit. The server's own
 * lifecycle table is keyed the same way. So a rename moves the word on the pill and not
 * the hue, which is the call WP-1.9 made for ticket priorities and the reason
 * `AssetListItemResponse` carries the code beside the name at all.
 *
 * A status an administrator adds beyond the seeded six has no colour in the design system
 * and takes `muted` — a hue nothing else claims — so it reads as unmapped rather than as
 * somebody else's state. That is the same treatment an unmapped priority gets, and it is
 * why nothing here builds a class name by concatenation: Tailwind only emits classes it
 * can see written down.
 *
 * ## Why the label is not in the hue
 *
 * DESIGN.md §4 describes a status pill as a soft fill with the text at full hue; §6 makes
 * WCAG AA contrast on status pills non-negotiable in both colour schemes. Several of these
 * hues cannot do both — `warning` reaches about 1.8:1 against a 12% wash of itself and
 * `neutral-chart` is worse — so the hue is carried by the fill and the dot and the label
 * sets in `heading`. That was settled at WP-1.9 and DESIGN.md §4 now records it; the
 * asset pill follows it so one status is not two treatments on two screens.
 */

/** The six codes SPEC.md §3 names, in the order the lifecycle runs. */
export const statusCodeOrder: readonly string[] = [
  'in-stock',
  'deployed',
  'repair',
  'retired',
  'lost',
  'disposed',
]

export interface StatusTone {
  /** The soft pill fill: 12% of the hue in light, 15% in dark (DESIGN.md §5). */
  readonly fill: string
  /** The full hue, worn by the dot. */
  readonly dot: string
}

/**
 * DESIGN.md §2's asset map: Deployed `success`, In Stock `info`, Repair `warning`,
 * Retired `neutral-chart`, Lost and Disposed `muted`.
 *
 * "Offline `danger`" in the same line of §2 is a *monitoring* state rather than a
 * lifecycle status — no asset status carries that code, and `WP-3.1` is where a device's
 * reachability arrives. It is deliberately absent here rather than guessed at.
 */
const tones: Record<string, StatusTone> = {
  'in-stock': { fill: 'bg-info/12 dark:bg-info/15', dot: 'bg-info' },
  deployed: { fill: 'bg-success/12 dark:bg-success/15', dot: 'bg-success' },
  repair: { fill: 'bg-warning/12 dark:bg-warning/15', dot: 'bg-warning' },
  retired: { fill: 'bg-neutral-chart/25 dark:bg-neutral-chart/30', dot: 'bg-neutral-chart' },
  lost: { fill: 'bg-muted-foreground/12 dark:bg-muted-foreground/15', dot: 'bg-muted-foreground' },
  disposed: {
    fill: 'bg-muted-foreground/12 dark:bg-muted-foreground/15',
    dot: 'bg-muted-foreground',
  },
}

const unmapped: StatusTone = {
  fill: 'bg-muted-foreground/12 dark:bg-muted-foreground/15',
  dot: 'bg-muted-foreground',
}

/** The pill treatment for a status code. */
export function statusTone(code: string): StatusTone {
  return tones[code.toLowerCase()] ?? unmapped
}

/** True when the design system has a colour for this code. */
export function isMappedStatus(code: string): boolean {
  return code.toLowerCase() in tones
}

/**
 * The three statuses an asset can no longer move out of (WP-2.2, at the human's
 * direction), for a screen that wants to say so.
 *
 * A second copy of the server's terminal set, and deliberately a *narrow* one: it is used
 * only to word a caption, never to decide whether an action is legal. The legality
 * question belongs to `AssetLifecycle` server-side, and `WP-2.6b` reads it over the wire
 * rather than restating the table here — which is what the doc comment on
 * `AssetLifecycle.DestinationsFrom` asks for.
 */
export function isTerminalStatus(code: string): boolean {
  const normalized = code.toLowerCase()
  return normalized === 'retired' || normalized === 'lost' || normalized === 'disposed'
}

/**
 * What an asset is called when the register has to name it in one line.
 *
 * `name` is optional on an asset, so the fallback walks make and model and then the tag
 * — which is never null and is what is printed on the label somebody is holding. It never
 * answers an empty string, because a row whose title is blank is a row that cannot be
 * clicked with any confidence.
 */
export function assetTitle(asset: {
  name?: string | null
  manufacturer?: string | null
  model?: string | null
  assetTag: string
}): string {
  const name = asset.name?.trim()
  if (name !== undefined && name.length > 0) {
    return name
  }

  const makeAndModel = [asset.manufacturer?.trim(), asset.model?.trim()]
    .filter((part): part is string => part !== undefined && part.length > 0)
    .join(' ')

  return makeAndModel.length > 0 ? makeAndModel : asset.assetTag
}
