import { Bell, ChevronDown, LogOut, MessageSquare, Search } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { CurrentDateTime } from '@/components/layout/current-date-time'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { initials, primaryRole } from '@/lib/roles'
import type { AuthenticatedUser } from '@/lib/api/generated-pending'

interface TopbarProps {
  user: AuthenticatedUser
  onSearch: () => void
  onSignOut: () => void
  signingOut: boolean
}

/**
 * The 72px topbar (DESIGN.md §3): the search pill on the left, then notifications and
 * messages, then the date and the account it is signed in as, on the right.
 *
 * The bell and the message icon carry no count. The reference screenshot shows badges,
 * but the Notifications module is Phase 4 and there is nothing to count yet — a
 * hardcoded number would be a claim about the system that is not true. Each icon keeps
 * its relative wrapper so the badge drops in without touching this layout.
 */
export function Topbar({
  user,
  onSearch,
  onSignOut,
  signingOut,
}: TopbarProps): React.JSX.Element {
  return (
    <header className="sticky top-0 z-30 flex h-topbar shrink-0 items-center justify-between gap-5 border-b border-border bg-surface px-8">
      <button
        type="button"
        onClick={onSearch}
        className="flex h-10 w-full max-w-[470px] items-center gap-2.5 rounded-full border border-border bg-canvas px-4 text-copy text-muted-foreground transition-colors duration-150 outline-none hover:border-primary/40 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      >
        <Search className="size-4 shrink-0" aria-hidden="true" />
        <span>Search anything…</span>
      </button>

      <div className="flex items-center gap-1">
        <IconButton label="Notifications" icon={Bell} />
        <IconButton label="Messages" icon={MessageSquare} />

        <span className="mx-3 h-8 w-px bg-border" aria-hidden="true" />

        <CurrentDateTime />

        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <button
                type="button"
                className="ml-4 flex items-center gap-3 rounded-lg py-1.5 pr-2 pl-1.5 transition-colors duration-150 outline-none hover:bg-canvas focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              >
                <Avatar size="lg">
                  <AvatarFallback className="bg-primary-soft text-copy font-semibold text-primary">
                    {initials(user.displayName)}
                  </AvatarFallback>
                </Avatar>
                <span className="flex flex-col items-start leading-tight">
                  <span className="text-copy font-semibold text-heading">{user.displayName}</span>
                  <span className="text-caption text-muted-foreground">
                    {primaryRole(user.roles)}
                  </span>
                </span>
                <ChevronDown className="size-4 text-muted-foreground" aria-hidden="true" />
              </button>
            }
          />
          <DropdownMenuContent align="end" className="w-56">
            <div className="px-2 py-1.5">
              <p className="text-copy font-semibold text-heading">{user.displayName}</p>
              <p className="text-caption text-muted-foreground">{user.email}</p>
            </div>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={onSignOut} disabled={signingOut}>
              <LogOut className="size-4" aria-hidden="true" />
              {signingOut ? 'Signing out…' : 'Sign out'}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}

interface IconButtonProps {
  label: string
  icon: typeof Bell
}

function IconButton({ label, icon: Icon }: IconButtonProps): React.JSX.Element {
  return (
    <span className="relative inline-flex">
      <button
        type="button"
        aria-label={label}
        className="flex size-10 items-center justify-center rounded-lg text-body transition-colors duration-150 outline-none hover:bg-canvas hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      >
        <Icon className="size-5" aria-hidden="true" />
      </button>
      {/* The count badge lands here when the Notifications module exists (Phase 4). */}
    </span>
  )
}
