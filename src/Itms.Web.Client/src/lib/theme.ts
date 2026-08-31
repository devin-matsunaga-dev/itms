/**
 * The colour-scheme store.
 *
 * A theme is application-wide client state that lives outside React's tree — the class
 * on `<html>` is applied before the first paint, so a reload does not flash the wrong
 * palette. That is why this is a small module-level store read through
 * `useSyncExternalStore` rather than component state or a context provider.
 *
 * The palette itself is entirely in `src/index.css`: both modes are token values, and
 * nothing here knows a colour.
 */

export type Theme = 'light' | 'dark'

/** Where the choice is remembered. Read by the inline script in `index.html` too. */
export const themeStorageKey = 'itms.theme'

const listeners = new Set<() => void>()

/** The viewer's operating-system preference, and the default before they choose. */
export function systemTheme(): Theme {
  return globalThis.matchMedia?.('(prefers-color-scheme: dark)').matches === true
    ? 'dark'
    : 'light'
}

function readStored(): Theme | null {
  try {
    const value = globalThis.localStorage?.getItem(themeStorageKey)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    // A browser with site data blocked still gets a working toggle, just not a
    // remembered one.
    return null
  }
}

function writeStored(theme: Theme): void {
  try {
    globalThis.localStorage?.setItem(themeStorageKey, theme)
  } catch {
    // Nothing to do: the theme applies for this tab either way.
  }
}

let current: Theme = readStored() ?? systemTheme()

/** Puts the theme on the document. The `.dark` class is what the token layer keys on. */
function apply(theme: Theme): void {
  const root = globalThis.document?.documentElement
  if (!root) {
    return
  }

  root.classList.toggle('dark', theme === 'dark')
  // So native controls, scrollbars, and form widgets follow the app.
  root.style.colorScheme = theme
}

apply(current)

// Follow the operating system until the viewer expresses a preference of their own.
globalThis
  .matchMedia?.('(prefers-color-scheme: dark)')
  .addEventListener('change', () => {
    if (readStored() === null) {
      setTheme(systemTheme(), { remember: false })
    }
  })

/** The theme in force. */
export function getTheme(): Theme {
  return current
}

export function setTheme(theme: Theme, options: { remember?: boolean } = {}): void {
  if (options.remember !== false) {
    writeStored(theme)
  }

  if (theme === current) {
    return
  }

  current = theme
  apply(theme)
  for (const listener of listeners) {
    listener()
  }
}

/** Subscribes to theme changes. Returns the unsubscribe. */
export function subscribeToTheme(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** Test seam: forgets the remembered choice and returns to the system preference. */
export function resetTheme(): void {
  try {
    globalThis.localStorage?.removeItem(themeStorageKey)
  } catch {
    // Ignored, as above.
  }
  setTheme(systemTheme(), { remember: false })
}
