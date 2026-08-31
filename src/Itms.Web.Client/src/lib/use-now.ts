import { useEffect, useState } from 'react'

/**
 * The current instant, re-read on an interval.
 *
 * The clock lives in the topbar, which mounts once and is never remounted by
 * navigation — so a value computed at render would show the time the person signed in
 * for the rest of the session. Half a minute keeps the displayed minute honest without
 * waking the tab more than it has to.
 */
export function useNow(intervalMs = 30_000): Date {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    const timer = setInterval(() => {
      setNow(new Date())
    }, intervalMs)

    return () => {
      clearInterval(timer)
    }
  }, [intervalMs])

  return now
}
