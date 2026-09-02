import { Link } from 'react-router'
import { Building2, MapPin, type LucideIcon } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'

/**
 * Administration (WP-2.7 for the two directory screens; the rest is Phase 5).
 *
 * **Why the directory screens live here.** Every department and location *write* is
 * Admin-only server-side, and SPEC.md §13 puts configuration under administration. The
 * reads stay open to any signed-in account — a picker needs them — so the split is the
 * server's policy rather than this screen's routing. Putting them here also adds no nav
 * entry: DESIGN.md §3 fixes the nine destinations in the sidebar, and Administration is
 * already one of them, hidden for non-admins by the one rule in `navigation.ts`.
 *
 * The rest of what this page will hold — ticket categories and priorities, asset types and
 * statuses, user administration, the audit viewer — is WP-5.8 and WP-5.9. It is named
 * rather than mocked up, because a tile that goes nowhere is worse than one that is absent.
 */
export function AdministrationPage(): React.JSX.Element {
  return (
    <>
      <PageHeader
        title="Administration"
        subtitle="System configuration, reference data, and the audit trail."
      />

      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <Destination
          to="/administration/departments"
          icon={Building2}
          title="Departments"
          description="The organisational units tickets, assets, and people are recorded against."
        />
        <Destination
          to="/administration/locations"
          icon={MapPin}
          title="Locations"
          description="The site, building, floor, and room tree everything is placed in."
        />
      </div>

      <p className="mt-5 text-copy text-muted-foreground">
        Reference data for tickets and assets, user administration, and the audit viewer are
        built in Phase 5 (WP-5.8 and WP-5.9).
      </p>
    </>
  )
}

interface DestinationProps {
  to: string
  icon: LucideIcon
  title: string
  description: string
}

/**
 * One administration destination.
 *
 * Built as DESIGN.md §4's interactive card: the soft icon tile, the title, and one line
 * saying what is behind it — and the whole card is the link, because a card whose only
 * clickable part is its heading is one people click and nothing happens.
 */
function Destination({ to, icon: Icon, title, description }: DestinationProps): React.JSX.Element {
  return (
    <Link
      to={to}
      className="flex gap-4 rounded-card border border-border bg-surface p-5 shadow-card transition-shadow hover:shadow-card-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
    >
      <span className="flex size-12 shrink-0 items-center justify-center rounded-tile bg-primary-soft">
        <Icon className="size-[22px] text-primary" aria-hidden="true" />
      </span>
      <span className="min-w-0">
        <span className="block text-card-title font-semibold text-heading">{title}</span>
        <span className="mt-1 block text-copy text-body">{description}</span>
      </span>
    </Link>
  )
}
