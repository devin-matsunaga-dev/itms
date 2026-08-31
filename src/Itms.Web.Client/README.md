# Itms.Web.Client

The ITMS web interface: React 19 + Vite + TypeScript (strict), styled with Tailwind CSS
v4 and shadcn/ui, themed to `docs/DESIGN.md` and the reference screenshot at
`docs/design/reference-dashboard.png`.

## Running it

Start the whole system from the repository root — the AppHost brings up PostgreSQL,
Redis, MailHog, the API, and this client, and runs `npm install` for you:

```bash
aspire run
```

To run only the client (the API must already be listening on `https://localhost:7014`):

```bash
npm install
npm run dev
```

`/api` is proxied to the ASP.NET host, so the browser sees one origin and the session
cookie behaves the way it does in production. Under `aspire run` the proxy target comes
from Aspire's service discovery; on a bare `npm run dev` it falls back to the
`launchSettings.json` HTTPS address.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Vite dev server on `http://localhost:5173` |
| `npm run build` | Type-check (`tsc -b`) then build to `dist/` |
| `npm test` | Vitest + Testing Library, once |
| `npm run test:watch` | The same suite in watch mode |
| `npm run lint` | oxlint |

## Layout

```
src/
├─ components/
│  ├─ ui/          shadcn/ui primitives, restyled to the DESIGN.md tokens
│  ├─ layout/      the shell: sidebar, topbar, page header
│  └─ common/      empty, error, and loading states; the brand mark
├─ features/<area>/{api,hooks,components,routes}/
├─ lib/            the API client, the query client, dates, roles
├─ routes/         the route table and the navigation model
└─ index.css       the token layer — every colour, type step, radius, and shadow
```

## Things to know

- **Tokens only.** Colours, type steps, radii, and shadows come from `src/index.css`.
  A raw hex in a component is a review failure.
- **API types are temporary.** `src/lib/api/generated-pending.ts` holds four hand-written
  auth shapes and is deleted by WP-0.9, which generates them from OpenAPI. Do not add
  to it.
- **Server state is TanStack Query.** No `useEffect` fetching, no `fetch` in a component.
- **Role filtering hides; it never protects.** `src/routes/navigation.ts` decides what the
  nav offers and what the router guards. Every endpoint enforces its own policy anyway.
