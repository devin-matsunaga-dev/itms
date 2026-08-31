import { cn } from '@/lib/utils'

/**
 * The ITMS mark: the three services this system exists to keep running — water supply,
 * power distribution, and the reservoir — quartered in one tile.
 *
 * It is a raster asset rather than inline SVG because the artwork is illustrative
 * rather than geometric. The file in `public/` is deliberately at a stable path: the
 * same image is the browser-tab icon (see `index.html`), so it cannot carry a build
 * hash. Its rounded corners are baked into the alpha channel, which is what lets it sit
 * on the dark sidebar and on the light login canvas without a plate behind it.
 *
 * `alt` is empty on purpose: every place this renders puts the organisation's name
 * beside it, and a screen reader announcing the logo as well would say it twice.
 */
export function BrandMark({ className }: { className?: string }): React.JSX.Element {
  return (
    <img
      src="/brand-mark.png"
      alt=""
      width={192}
      height={192}
      className={cn('size-8 shrink-0 select-none', className)}
    />
  )
}
