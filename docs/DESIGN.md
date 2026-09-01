# DESIGN.md — ITMS Visual System

> Binding for every frontend work package. The reference screenshot at `docs/design/reference-dashboard.png` is the source of truth — when this document and the screenshot disagree, the screenshot wins. Do not invent a new visual direction, do not "modernize" it, do not swap the palette.
>
> **Two deliberate departures from the screenshot**, decided at WP-0.8 and not to be "corrected" back:
>
> 1. The sidebar's bottom slot holds the **colour-scheme switch**, not a New Ticket button. Ticket creation lives in the Tickets screen header (§4, *Page actions*).
> 2. The topbar's bell and message icons carry **no count badge** until the Notifications module exists (Phase 4). The screenshot's 6 and 2 are illustrative.
> 3. The **date and time sit in the topbar**, not in each page header, and the brand block leads with the operating organisation's name rather than the "ITMS" wordmark.

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

**Sidebar (244px, collapsible to 72px).** Vertical gradient `sidebar` → `sidebar-deep`. Brand block at top: the mark + **"Commonwealth Utilities Corporation"** 13/700 white, wrapping to two lines rather than truncating, over "UNIFIED IT MANAGEMENT" 9/600 uppercase tracked `sidebar-fg-muted`. The organisation's name sets at 13px here and 20px on the login page, where the column is wide enough — an organisation's name is not a field to elide. Both names are spelled once, in `src/lib/branding.ts`.

The **mark** is `src/Itms.Web.Client/public/brand-mark.png` — a rounded tile quartering the three services the system exists to keep running: water supply, power distribution, and the reservoir. Its rounded corners are in the alpha channel, so it needs no plate behind it on either the dark sidebar or the light login canvas. The same file is the browser-tab icon, alongside a 32px favicon and a 180px Apple touch icon; all three are generated from one source image and live at stable, unhashed paths. It is decorative in markup — the word "ITMS" always sits beside it. Nav items 44px tall, 20px icon + 14/500 label, 10px radius, 12px side inset. Active = solid `primary` fill, white label, no left bar. Inactive hover = `white/8` fill. Order: Dashboard, Tickets, Assets, Users, Monitoring, Alerts, Knowledge Base, Reports, Administration. Pinned to the bottom: a **colour-scheme switch** — moon icon + "Dark mode" in light, sun icon + "Light mode" in dark, styled as a nav row and announcing its state as a `switch` — then a `Collapse` row with chevron in `sidebar-fg-muted`. Both collapse to icon-plus-tooltip at 72px. Administration is hidden for non-admins; the nav is permission-filtered, never disabled-in-place.

The sidebar holds navigation and app-level settings only. A create action belongs to the screen that owns the thing being created, not to the frame.

**Topbar (72px, white, bottom `border`).** Left: search pill — full-round, `canvas` fill, 1px `border`, magnifier icon, placeholder "Search anything…", ~470px wide, opens the global-search palette. Right, in order: bell with count badge, message icon with count badge, vertical divider, the date block, then the 40px round avatar + name 14/600 + role 12/400 `muted` + chevron menu. Badges are `primary` circles with white 11/600 text, top-right of the icon.

The **date block** is deliberately quiet: two right-aligned caption lines — date 12/600 `heading` over weekday and time 12/400 `muted`, both tabular — with no icon and no tile, sitting immediately left of the account it is signed in as so the two read as one corner. It is context, not a control. The clock is stated once for the whole application, here, and re-reads the time on an interval: the topbar mounts once and is never remounted by navigation, so a value computed at render would freeze at the moment of sign-in.

**Page header.** Title 28/600 `heading` ("Welcome back, John" on the dashboard, plain page name elsewhere) with a one-line `body` subtitle under it. Right side: the screen's own actions, and nothing else — the date lives in the topbar.

## 4. Component patterns

**KPI card.** White card, left `48px` soft-tinted rounded tile holding a 22px icon, then uppercase label and the figure. Delta line below: `▲` in `success` or `▼` in `danger`, the number, then the comparison phrase in `muted`. Sentiment is semantic, not directional — fewer overdue tickets is green even though the arrow points down. Tint per card: open `primary-soft`, unassigned `#EEF2FF`, overdue `#FEE9E9`, SLA `primary-soft`.

**Panel card.** Header row: 18px icon in `primary` + title 15/600, right-aligned control — either a "View All" `primary` 13/500 link, or a period `Select` ("This Week") plus a kebab `DropdownMenu`. Body has no internal header rule; separation comes from spacing.

**Donut.** Thick ring (~26px stroke), center holds the total 26/700 `heading` with a 12/400 `muted` word under it. Legend sits to the right as a three-column grid: colored 8px dot + label (left), count (right-aligned), percent (right-aligned `muted`). Never render percentages inside the ring. Segment colors come from the semantic map.

**Data table.** Header 11/600 uppercase tracked `muted`, single `border` rule beneath. No zebra striping, `canvas` tint on hover, 1px `border` between rows. Numeric and age columns right-aligned and muted. Row click opens the detail page.

The **identifying column is two lines**: the identifier (ticket number, asset tag) as a `primary` link, the title beneath it in `heading`, and a 12/400 `muted` caption under that saying how long ago the record was raised. That is what buys the row its width — the title stops competing with eight other columns, and age stops needing a column at the far end where nobody reads it. A row is 44px when the caption is suppressed and grows to fit when it is not.

**Row density and column choice belong to the reader**, not to the URL. Filters, sorting, and paging are linkable state (§6) because they describe *which rows*; which columns are drawn and how tightly they pack describe how one person reads, and are remembered per browser. A "Columns" popover lists every optional column; the identifying column is never optional.

**Status pill.** Soft background at ~12% of the semantic hue, a 6px dot in the full hue, and the label in `heading`. 6px radius, 11/600, no border.

> This is a deliberate departure from an earlier reading of this section, decided at WP-1.9 and not to be "corrected" back. §6 makes AA contrast on status pills non-negotiable in **both** colour schemes, and the label cannot carry the semantic hue and clear it: `warning` (#F5B22D) reaches about 1.8:1 against a 12% wash of itself, `neutral-chart` is worse, and `danger` as text reaches about 3.8:1. The hue still identifies the status — it is carried by the fill and the dot rather than by the letterforms. The same rule governs the priority pill and the SLA meter's caption.

**Priority pill.** The same soft fill, with a **direction arrow** in the full hue and the name in `heading`: two chevrons up for Critical, one arrow up for High, a dash for Medium, one arrow down for Low. Three encodings of one fact — fill, hue, and direction — because §6 forbids relying on colour alone and the arrow is what survives greyscale and a red-green deficiency both. Keyed on the priority's immutable code, so a rename moves the word and not the hue; an unmapped code takes `muted` and a flat arrow.

**SLA meter.** A 6px full-round track in `border` with a fill at the state's hue, over a 12/400 caption reading `42m left` or `Overdue 1h`. The caption sets in `heading`, never in the hue, for the contrast reason above. A parked clock is measured at the instant it was parked and says `· paused`; a finished clock shows a full bar and the state's word instead of a countdown.

**Person cell.** A 24px round tile of initials in `primary-soft` over `primary`, then the name in `body`. The tile is decorative and hidden from assistive technology — the name is always beside it and is never abbreviated. There are no uploaded avatars in this system.

**Filter bar.** The three filters a person reaches for constantly stay inline; the rest sit behind a `Filters` button whose badge counts how many of them are set, so nothing is hidden without being counted. Every control writes straight through to the URL — no draft state, no "apply" button, inside the popover or outside it. `Clear all` sits at the end of the bar and is the same words the empty state uses.

**Alert / activity list row.** 36px circular soft-tinted icon (down-arrow `danger`, triangle `warning`, up-arrow `success`, info `info`) + title 14/500 `heading` + subtext 12/400 `muted` (device location or hostname), right column with absolute time 12/400 `muted` above relative time 12/500 in the severity hue. Rows separated by `border`, no card-in-card.

**Expiration list row.** Soft `primary-soft` icon tile + item title + owning entity in `muted`; right column shows "in N days" (hue shifts to `warning` under 30 days, `danger` under 7) above the absolute date in `muted`.

**Buttons.** Primary solid `primary`, white, 40px, 8px radius, 14/500. Secondary white with `border`, `heading` text. Destructive solid `danger`. Ghost/icon for toolbars. Disabled = 50% opacity, no color change.

**Page actions.** The primary action for a screen sits in that screen's page header, left of the date block, as a primary button with a leading icon — **New Ticket** on Tickets, and the same pattern for the create action on Assets, Users, and Knowledge Base. An empty state offers the same action a second time; those are the only two places it appears.

**Forms.** Labels above inputs, 13/500 `heading`. Inputs 40px, 8px radius, `border`, `primary` focus ring at 2px with 2px offset. Errors 12/400 `danger` below the field. Required marked with an asterisk in `danger`. Long forms use section cards, not accordions.

**Empty, loading, error.** Skeleton shimmer inside the card's own shape — never a centered spinner in a card. Empty states get an outlined icon, a plain sentence saying what would appear here, and the primary action button ("Create the first ticket"). Errors state what failed and offer a retry; they don't apologize.

## 5. Dark mode

**Both modes ship.** The switch is the sidebar's bottom slot (§3). It defaults to the viewer's operating-system preference, remembers a choice per browser, and follows the system until they make one. The class goes on the document element before the first paint, so a reload never flashes the wrong palette.

Dark values: `canvas #0E1626`, `surface #172032`, `border #263247`, `heading #EDF1F7`, `body #A5B3C9`, `muted #8496B0`. Sidebar is already dark — deepen the gradient's bottom stop to `#04122F`. Semantic hues hold: a status is the same colour in both modes. Soft fills become 15% alpha of the hue; chart gridlines and `neutral-chart` dim to `#475569`.

Card elevation is not redefined in dark — a 4% black shadow is invisible on a dark ground, and the `border` carries the edge instead.

Every screen is checked in both modes. A colour that only works in one is a bug, not a trade-off.

## 6. Quality floor

Non-negotiable on every screen, without being announced in the UI:

- Responsive to 1280px minimum; tables scroll horizontally below that rather than reflowing into cards.
- Visible `primary` focus ring on every interactive element; full keyboard path through nav, tables, and dialogs.
- WCAG AA contrast on text and on status pills, in **both** colour schemes.
- `prefers-reduced-motion` respected; transitions capped at 150ms.
- Dates, times, and durations formatted through one shared utility, rendered in the viewer's local timezone with the absolute value available on hover.
- Every list view keeps filter/sort/page state in the URL so views are linkable.
