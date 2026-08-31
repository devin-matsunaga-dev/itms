import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import {
  getTheme,
  resetTheme,
  setTheme,
  subscribeToTheme,
  systemTheme,
  themeStorageKey,
} from '@/lib/theme'

beforeEach(() => {
  localStorage.clear()
  resetTheme()
})

afterEach(() => {
  localStorage.clear()
  resetTheme()
})

describe('the colour-scheme store', () => {
  it('follows the operating system before anyone chooses', () => {
    // The test environment reports no dark preference, so light is the default.
    expect(systemTheme()).toBe('light')
    expect(getTheme()).toBe('light')
    expect(localStorage.getItem(themeStorageKey)).toBeNull()
  })

  it('puts the class the token layer keys on onto the document', () => {
    setTheme('dark')

    expect(document.documentElement).toHaveClass('dark')
    // So native controls and scrollbars follow the app, not the other way round.
    expect(document.documentElement.style.colorScheme).toBe('dark')

    setTheme('light')

    expect(document.documentElement).not.toHaveClass('dark')
    expect(document.documentElement.style.colorScheme).toBe('light')
  })

  it('remembers a choice under a key the pre-paint script reads', () => {
    setTheme('dark')

    expect(localStorage.getItem(themeStorageKey)).toBe('dark')
  })

  it('notifies subscribers when the theme changes, and not when it does not', () => {
    let notifications = 0
    const unsubscribe = subscribeToTheme(() => {
      notifications += 1
    })

    setTheme('dark')
    setTheme('dark')

    expect(notifications).toBe(1)
    unsubscribe()

    setTheme('light')
    expect(notifications).toBe(1)
  })

  it('can follow the system without remembering, which is what a system change does', () => {
    setTheme('dark', { remember: false })

    expect(getTheme()).toBe('dark')
    expect(localStorage.getItem(themeStorageKey)).toBeNull()
  })
})
