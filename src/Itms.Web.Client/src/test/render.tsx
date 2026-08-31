import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, type RenderResult } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { TooltipProvider } from '@/components/ui/tooltip'

/**
 * Renders a component inside the providers the application supplies, with a query
 * client that retries nothing — a test asserting an error state should see it on the
 * first attempt, not after the production retry policy has run.
 */
export function renderWithProviders(
  ui: React.ReactNode,
  options: { route?: string; queryClient?: QueryClient } = {},
): RenderResult & { queryClient: QueryClient } {
  const queryClient =
    options.queryClient ??
    new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: 0, gcTime: 0 },
        mutations: { retry: false },
      },
    })

  const result = render(
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <MemoryRouter initialEntries={[options.route ?? '/']}>{ui}</MemoryRouter>
      </TooltipProvider>
    </QueryClientProvider>,
  )

  return { ...result, queryClient }
}
