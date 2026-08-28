# DESIGN.md — ITMS Visual System

> Binding for every frontend work package. The reference screenshot at `docs/design/reference-dashboard.png` is the source of truth — when this document and the screenshot disagree, the screenshot wins. Do not invent a new visual direction, do not "modernize" it, do not swap the palette.

## 1. Stack

- **React 19** + **Vite** (latest template), TypeScript strict.
- **Tailwind CSS** for all styling. No CSS modules, no styled-components, no inline style objects except for computed values (chart geometry, dynamic widths).
- **shadcn/ui** for primitives (Button, Input, Select, Dialog, DropdownMenu, Table, Tabs, Toast, Badge, Skeleton, Popover, Tooltip). Restyle its tokens to match this document; never ship the shadcn default gray/black theme.
- **lucide-react** for all icons. One icon family, no mixing.
- **Recharts** for donuts and line charts.
- **TanStack Query** for server state, **TanStack Table** for data tables, **react-hook-form + zod** for forms.

## 2. Tokens

Define these once in `tailwind.config.ts` + a CSS variable layer. Every component reads tokens; no raw hex in component files.

### Color

| Token | Hex | Use |
|---|---|---|
| `primary` | `#2563EB` | Primary buttons, active nav, links, ticket IDs, "View All", focus ring |
| `primary-hover` | `#1D4FD7` | Button/link hover |
| `primary-soft` | `#E8EFFD` | Soft icon tiles, selected rows, badges |
| `sidebar` | `#0B2B63` | Sidebar background (top of gradient) |
| `sidebar-deep` | `#082250` | Sidebar background (bottom of gradient) |
| `sidebar-fg` | `#DCE6F7` | Inactive nav label |
| `sidebar-fg-muted` | `#93A9CE` | Sidebar subtitle, collapse label |
| `canvas` | `#F4F7FB` | App background behind cards |
| `surface` | `#FFFFFF` | Cards, topbar, table surface |
| `border` | `#E6EBF2` | Card borders, table rules, dividers |
| `heading` | `#0F1B33` | Headings, table primary cells, big numbers |
| `body` | `#5A6B85` | Body copy, secondary cells |
| `muted` | `#8B9BB4` | Column headers, timestamps, captions |
| `success` | `#22C55E` | Online, recovery, positive delta |
| `warning` | `#F5B22D` | Medium priority, maintenance, latency warnings |
| `danger` | `#EF4444` | Offline, overdue, high priority, negative delta |
| `info` | `#3B82F6` | Informational alerts, In Stock |
| `teal` | `#14B8A6` | Chart series (Waiting) |
| `violet` | `#8B5CF6` | Chart series (Resolved) |
| `neutral-chart` | `#CBD5E1` | Chart series (Closed / Retired) |

Semantic mapping is fixed across the whole app — the same status is the same color everywhere:

- Ticket status: New `primary` · In Progress `warning` · Waiting `teal` · Resolved `violet` · Closed `neutral-chart` · Cancelled `muted`
- Priority: Critical `#B91C1C` · High `danger` · Medium `warning` · Low `success`
- Asset status: Deployed/Online `success` · Offline `danger` · In Stock `info` · Repair/Maintenance `warning` · Retired `neutral-chart` · Lost/Disposed `muted`
- Alert severity: Critical `danger` · Warning `warning` · Info `info` · Recovery `success`

### Type

- Family: **Inter** (variable), `-apple-system` fallback. Tabular numerals on all numeric cells and KPI figures (`font-variant-numeric: tabular-nums`).
- Scale: page title 28/600 · card title 15/600 · KPI number 30/700 · KPI label 11/600 uppercase `tracking-[0.06em]` `muted` · body 14/400 · table cell 13.5/400 · table header 11/600 uppercase tracked `muted` · caption 12/400 `muted`.
- Sentence case everywhere except the uppercase KPI/table labels. Never title-case UI copy.

### Shape, elevation, spacing

- Radius: cards `12px` · buttons/inputs `8px` · soft icon tiles `10px` · pills `9999px`.
- Elevation: cards get `border border-border` plus `shadow-[0_1px_2px_rgba(15,27,51,0.04)]`. Hover on interactive cards lifts to `shadow-[0_4px_12px_rgba(15,27,51,0.08)]`. No heavy drop shadows anywhere.
- Spacing: 4px base. Card padding `20px`; grid gutter `20px`; page padding `32px`; section stack `20px`.
- Grid: 12 columns. KPI row = 4 × 3col. Dashboard middle row = 7col + 5col. Bottom row = 4col + 4col + 4col.

## 3. Layout shell

Persistent three-part frame — every page renders inside it.

```
┌──────────┬────────────────────────────────────────────────┐
│ SIDEBAR  │ TOPBAR: search pill · alerts · messages · user  │
│ 244px    ├────────────────────────────────────────────────┤
│ fixed    │ PAGE: title + context line ····· right meta     │
│ dark     │ content on `canvas`                            │
└──────────┴────────────────────────────────────────────────┘
```

**Sidebar (244px, collapsible to 72px).** Vertical gradient `sidebar` → `sidebar-deep`. Brand block at top: hexagon mark + "ITMS" 20/700 white, "UNIFIED IT MANAGEMENT" 9/600 uppercase tracked `sidebar-fg-muted`. Nav items 44px tall, 20px icon + 14/500 label, 10px radius, 12px side inset. Active = solid `primary` fill, white label, no left bar. Inactive hover = `white/8` fill. Order: Dashboard, Tickets, Assets, Users, Monitoring, Alerts, Knowledge Base, Reports, Administration. Pinned to the bottom: full-width **New Ticket** primary button with `+` icon, then a `Collapse` row with chevron in `sidebar-fg-muted`. Administration is hidden for non-admins; the nav is permission-filtered, never disabled-in-place.

**Topbar (72px, white, bottom `border`).** Left: search pill — full-round, `canvas` fill, 1px `border`, magnifier icon, placeholder "Search anything…", ~470px wide, opens the global-search palette. Right: bell with count badge, message icon with count badge, vertical divider, 40px round avatar + name 14/600 + role 12/400 `muted` + chevron menu. Badges are `primary` circles with white 11/600 text, top-right of the icon.

**Page header.** Title 28/600 `heading` ("Welcome back, John" on the dashboard, plain page name elsewhere) with a one-line `body` subtitle under it. Right side: calendar icon in `primary-soft` tile + date 15/600 and weekday/time 12/400 `muted`.

## 4. Component patterns

**KPI card.** White card, left `48px` soft-tinted rounded tile holding a 22px icon, then uppercase label and the figure. Delta line below: `▲` in `success` or `▼` in `danger`, the number, then the comparison phrase in `muted`. Sentiment is semantic, not directional — fewer overdue tickets is green even though the arrow points down. Tint per card: open `primary-soft`, unassigned `#EEF2FF`, overdue `#FEE9E9`, SLA `primary-soft`.

**Panel card.** Header row: 18px icon in `primary` + title 15/600, right-aligned control — either a "View All" `primary` 13/500 link, or a period `Select` ("This Week") plus a kebab `DropdownMenu`. Body has no internal header rule; separation comes from spacing.

**Donut.** Thick ring (~26px stroke), center holds the total 26/700 `heading` with a 12/400 `muted` word under it. Legend sits to the right as a three-column grid: colored 8px dot + label (left), count (right-aligned), percent (right-aligned `muted`). Never render percentages inside the ring. Segment colors come from the semantic map.

**Data table.** Header 11/600 uppercase tracked `muted`, single `border` rule beneath. Rows 44px, no zebra striping, `canvas` tint on hover, 1px `border` between rows. First column is the identifier (ticket number, asset tag) rendered as a `primary` link. Priority and status render as dot+label or pill, never as bare text. Numeric and age columns right-aligned and muted. Row click opens the detail page; row actions live in a trailing kebab.

**Status pill.** Soft background at ~12% of the semantic hue, text at full hue, 6px radius, 11/600, no border. Use pills in detail headers and dense list cells; use dot+label in legends and priority columns.

**Alert / activity list row.** 36px circular soft-tinted icon (down-arrow `danger`, triangle `warning`, up-arrow `success`, info `info`) + title 14/500 `heading` + subtext 12/400 `muted` (device location or hostname), right column with absolute time 12/400 `muted` above relative time 12/500 in the severity hue. Rows separated by `border`, no card-in-card.

**Expiration list row.** Soft `primary-soft` icon tile + item title + owning entity in `muted`; right column shows "in N days" (hue shifts to `warning` under 30 days, `danger` under 7) above the absolute date in `muted`.

**Buttons.** Primary solid `primary`, white, 40px, 8px radius, 14/500. Secondary white with `border`, `heading` text. Destructive solid `danger`. Ghost/icon for toolbars. Disabled = 50% opacity, no color change.

**Forms.** Labels above inputs, 13/500 `heading`. Inputs 40px, 8px radius, `border`, `primary` focus ring at 2px with 2px offset. Errors 12/400 `danger` below the field. Required marked with an asterisk in `danger`. Long forms use section cards, not accordions.

**Empty, loading, error.** Skeleton shimmer inside the card's own shape — never a centered spinner in a card. Empty states get an outlined icon, a plain sentence saying what would appear here, and the primary action button ("Create the first ticket"). Errors state what failed and offer a retry; they don't apologize.

## 5. Dark mode

Ship light mode in V1; keep the token layer dark-ready. When it lands: `canvas #0E1626`, `surface #172032`, `border #263247`, `heading #EDF1F7`, `body #A5B3C9`. Sidebar is already dark — deepen slightly. Semantic hues hold; soft fills become 15% alpha of the hue; chart gridlines dim.

## 6. Quality floor

Non-negotiable on every screen, without being announced in the UI:

- Responsive to 1280px minimum; tables scroll horizontally below that rather than reflowing into cards.
- Visible `primary` focus ring on every interactive element; full keyboard path through nav, tables, and dialogs.
- WCAG AA contrast on text and on status pills.
- `prefers-reduced-motion` respected; transitions capped at 150ms.
- Dates, times, and durations formatted through one shared utility, rendered in the viewer's local timezone with the absolute value available on hover.
- Every list view keeps filter/sort/page state in the URL so views are linkable.
