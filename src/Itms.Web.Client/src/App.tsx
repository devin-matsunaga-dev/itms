import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router'
import { TooltipProvider } from '@/components/ui/tooltip'
import { Toaster } from '@/components/ui/sonner'
import { SessionExpiryWatcher } from '@/features/auth/components/session-expiry-watcher'
import { AppRoutes } from '@/routes/app-routes'
import { createQueryClient } from '@/lib/query-client'

const queryClient = createQueryClient()

export function App(): React.JSX.Element {
  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <BrowserRouter>
          <SessionExpiryWatcher />
          <AppRoutes />
        </BrowserRouter>
        <Toaster />
      </TooltipProvider>
    </QueryClientProvider>
  )
}
