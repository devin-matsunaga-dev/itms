import { useSyncExternalStore } from 'react'
import { getTheme, setTheme, subscribeToTheme, type Theme } from '@/lib/theme'

export interface ThemeControl {
  theme: Theme
  setTheme: (theme: Theme) => void
  toggleTheme: () => void
}

/** Reads and changes the colour scheme. */
export function useTheme(): ThemeControl {
  const theme = useSyncExternalStore(
    subscribeToTheme,
    getTheme,
    // Server/prerender has no document; light is what the token layer defaults to.
    () => 'light' as Theme,
  )

  return {
    theme,
    setTheme: (next: Theme) => {
      setTheme(next)
    },
    toggleTheme: () => {
      setTheme(theme === 'dark' ? 'light' : 'dark')
    },
  }
}
