import { ChevronLeft, ChevronRight, Moon, Sun } from 'lucide-react'
import { NavLink } from 'react-router'
import { BrandMark } from '@/components/common/brand-mark'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { organisationName, productDescriptor } from '@/lib/branding'
import { navItems } from '@/routes/navigation'
import { hasAnyRole } from '@/lib/roles'
import { useTheme } from '@/lib/use-theme'
import { cn } from '@/lib/utils'

interface SidebarProps {
  /** The signed-in account's roles; the nav is filtered to what they are offered. */
  roles: readonly string[]
  collapsed: boolean
  onToggleCollapsed: () => void
}

/**
 * The persistent left frame (DESIGN.md §3): 244px, collapsing to 72px, on a vertical
 * `sidebar` → `sidebar-deep` gradient.
 *
 * The nav is permission-filtered rather than disabled in place — an item a role cannot
 * use is absent, not greyed out.
 *
 * Pinned to the bottom: the colour-scheme switch, then the collapse row. Ticket
 * creation is not here — it belongs to the Tickets screen, where the thing being
 * created lives.
 */
export function Sidebar({ roles, collapsed, onToggleCollapsed }: SidebarProps): React.JSX.Element {
  const visible = navItems.filter((item) => hasAnyRole(roles, item.roles))
  const { theme, toggleTheme } = useTheme()
  const nextTheme = theme === 'dark' ? 'Light mode' : 'Dark mode'

  return (
    <div
      className={cn(
        'sticky top-0 flex h-screen shrink-0 flex-col bg-gradient-to-b from-sidebar to-sidebar-deep transition-[width] duration-150',
        collapsed ? 'w-sidebar-collapsed' : 'w-sidebar',
      )}
    >
      <div
        className={cn(
          'flex h-topbar shrink-0 items-center gap-3 border-b border-white/8',
          collapsed ? 'justify-center px-0' : 'px-5',
        )}
      >
        <BrandMark />
        {collapsed ? null : (
          <span className="flex min-w-0 flex-col gap-0.5">
            {/* The name wraps rather than truncates: an organisation's name is not a
                field to elide, and the rail has room for two lines at this size. */}
            <span className="text-org font-bold text-balance text-white">
              {organisationName}
            </span>
            <span className="text-brand-sub font-semibold text-sidebar-fg-muted uppercase">
              {productDescriptor}
            </span>
          </span>
        )}
      </div>

      <nav aria-label="Main" className="flex-1 overflow-y-auto px-3 py-4">
        <ul className="flex flex-col gap-1">
          {visible.map((item) => (
            <li key={item.path}>
              <Tooltip>
                <TooltipTrigger
                  render={
                    <NavLink
                      to={item.path}
                      end={item.path === '/'}
                      className={({ isActive }) =>
                        cn(
                          'flex h-11 items-center gap-3 rounded-tile text-copy font-medium transition-colors duration-150',
                          'outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-sidebar',
                          collapsed ? 'justify-center px-0' : 'px-3',
                          isActive
                            ? 'bg-primary text-white'
                            : 'text-sidebar-fg hover:bg-white/8 hover:text-white',
                        )
                      }
                    >
                      <item.icon className="size-5 shrink-0" aria-hidden="true" />
                      <span className={cn(collapsed && 'sr-only')}>{item.label}</span>
                    </NavLink>
                  }
                />
                {collapsed ? <TooltipContent side="right">{item.label}</TooltipContent> : null}
              </Tooltip>
            </li>
          ))}
        </ul>
      </nav>

      <div className="flex flex-col gap-1 px-3 pb-4">
        <Tooltip>
          <TooltipTrigger
            render={
              <button
                type="button"
                onClick={toggleTheme}
                // The control is a switch over one setting rather than two buttons, so
                // its state is what it reports; the label names the mode it moves to.
                role="switch"
                aria-checked={theme === 'dark'}
                aria-label={nextTheme}
                className={cn(
                  'flex h-11 items-center gap-3 rounded-tile text-copy font-medium text-sidebar-fg transition-colors duration-150 hover:bg-white/8 hover:text-white',
                  'outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-sidebar',
                  collapsed ? 'justify-center px-0' : 'px-3',
                )}
              >
                {theme === 'dark' ? (
                  <Sun className="size-5 shrink-0" aria-hidden="true" />
                ) : (
                  <Moon className="size-5 shrink-0" aria-hidden="true" />
                )}
                <span className={cn(collapsed && 'sr-only')}>{nextTheme}</span>
              </button>
            }
          />
          {collapsed ? <TooltipContent side="right">{nextTheme}</TooltipContent> : null}
        </Tooltip>

        <button
          type="button"
          onClick={onToggleCollapsed}
          aria-expanded={!collapsed}
          className={cn(
            'flex h-11 items-center gap-3 rounded-tile text-copy font-medium text-sidebar-fg-muted transition-colors duration-150 hover:bg-white/8',
            'outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-sidebar',
            collapsed ? 'justify-center px-0' : 'px-3',
          )}
        >
          {collapsed ? (
            <ChevronRight className="size-5 shrink-0" aria-hidden="true" />
          ) : (
            <ChevronLeft className="size-5 shrink-0" aria-hidden="true" />
          )}
          <span className={cn(collapsed && 'sr-only')}>Collapse</span>
        </button>
      </div>
    </div>
  )
}
